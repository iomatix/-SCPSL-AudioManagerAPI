namespace AudioManagerAPI.Cache
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using MEC;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Thread-safe audio sample cache implementing predictive background pre-decoding, 
    /// lock-free multi-format parsing, and least-recently-used (LRU) memory eviction.
    /// </summary>
    public class AudioCache
    {
        private const int TargetSampleRate = 48000;
        private readonly int _maxSize;
        private readonly Dictionary<string, float[]> _cache;
        private readonly LinkedList<string> _lruOrder;
        private readonly Dictionary<string, Func<Stream>> _streamProviders;
        private readonly object _lockObject = new object();

        public AudioCache(int maxSize = 50)
        {
            _maxSize = maxSize > 0 ? maxSize : throw new ArgumentException("Cache size must be positive.", nameof(maxSize));
            _cache = new Dictionary<string, float[]>();
            _lruOrder = new LinkedList<string>();
            _streamProviders = new Dictionary<string, Func<Stream>>();
        }

        /// <summary>
        /// Registers a stream provider and dispatches a background worker thread to proactively 
        /// pre-decode the asset into RAM, eliminating first-play disk I/O stutter.
        /// </summary>
        public void Register(string key, Func<Stream> streamProvider)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (streamProvider == null) throw new ArgumentNullException(nameof(streamProvider));

            lock (_lockObject)
            {
                if (_streamProviders.ContainsKey(key))
                    return;

                _streamProviders[key] = streamProvider;
            }

            // Offloading execution to the global thread pool instantly avoids blocking the server boostrap process.
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    Stream stream = streamProvider();
                    if (stream == null) return;

                    float[] samples = LoadAudio(stream, key);
                    if (samples == null) return;

                    lock (_lockObject)
                    {
                        if (_cache.ContainsKey(key)) return;
                        CommitCacheInsertion(key, samples);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AudioManagerAPI] Predictive background audio warmup failed for '{key}': {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Resolves audio sample references, executing on-demand lock-free decoding 
        /// only if the predictive background warmup has not completed yet.
        /// </summary>
        public float[] Get(string key)
        {
            Func<Stream> providerToExecute = null;

            lock (_lockObject)
            {
                if (_cache.TryGetValue(key, out var cachedSamples))
                {
                    UpdateLRU(key);
                    return cachedSamples;
                }

                if (!_streamProviders.TryGetValue(key, out providerToExecute))
                {
                    return null;
                }
            }

            // I/O block runs detached from the lock to secure server frame-rate stability if a cache-miss occurs.
            Stream stream = null;
            try
            {
                stream = providerToExecute();
            }
            catch (Exception ex)
            {
                Log.Error($"[AudioManagerAPI] Failed to invoke stream provider for key '{key}': {ex.Message}");
                return null;
            }

            float[] newSamples = stream != null ? LoadAudio(stream, key) : null;
            if (newSamples == null) return null;

            lock (_lockObject)
            {
                // Double-Check Pattern: Validates if a background thread committed the identical asset during our detached I/O loop.
                if (_cache.TryGetValue(key, out var existingSamples))
                {
                    UpdateLRU(key);
                    return existingSamples;
                }

                CommitCacheInsertion(key, newSamples);
                return newSamples;
            }
        }

        /// <summary>
        /// Inserts an item into memory maps and handles LRU tracking mechanics under lock safety.
        /// </summary>
        private void CommitCacheInsertion(string key, float[] samples)
        {
            if (_cache.Count >= _maxSize)
            {
                var lruKey = _lruOrder.Last.Value;
                _cache.Remove(lruKey);
                _lruOrder.RemoveLast();
                Log.Debug($"[AudioManagerAPI] Evicted cached audio '{lruKey}' due to memory limits.");
            }

            _cache[key] = samples;
            _lruOrder.AddFirst(key);
        }

        private void UpdateLRU(string key)
        {
            _lruOrder.Remove(key);
            _lruOrder.AddFirst(key);
        }

        private float[] LoadAudio(Stream stream, string key)
        {
            try
            {
                using (stream)
                using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false))
                {
                    if (stream.Length < 4) return null;

                    var header = reader.ReadBytes(4);
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    if (header.Length == 4 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F')
                        return LoadWavAsFloat(reader, key);

                    if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
                        return LoadMp3AsFloat(reader, key);

                    if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                        return LoadMp3AsFloat(reader, key);

                    return LoadRawFloatPcm(reader, key);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AudioManagerAPI] Audio cache exception while loading '{key}': {ex.Message}");
                return null;
            }
        }

        private float[] LoadWavAsFloat(BinaryReader reader, string key)
        {
            if (new string(reader.ReadChars(4)) != "RIFF") return null;
            reader.ReadInt32(); // File size
            if (new string(reader.ReadChars(4)) != "WAVE") return null;

            int channels = 1;
            int sampleRate = TargetSampleRate;
            short audioFormat = 1;
            short bitsPerSample = 16;
            byte[] dataBytes = null;

            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    audioFormat = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byteRate
                    reader.ReadInt16(); // blockAlign
                    bitsPerSample = reader.ReadInt16();

                    int remaining = chunkSize - 16;
                    if (remaining > 0)
                        reader.BaseStream.Seek(remaining, SeekOrigin.Current);
                }
                else if (chunkId == "data")
                {
                    dataBytes = reader.ReadBytes(chunkSize);
                }
                else
                {
                    reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (dataBytes == null) return null;

            var samples = ConvertPcmToFloat(dataBytes, audioFormat, bitsPerSample, channels, key);
            if (samples == null) return null;

            if (channels > 1)
                samples = DownmixToMono(samples, channels);

            if (sampleRate != TargetSampleRate && sampleRate > 0)
                samples = ResampleLinear(samples, sampleRate, TargetSampleRate);

            return samples;
        }

        private float[] ConvertPcmToFloat(byte[] rawData, short audioFormat, short bitsPerSample, int channels, string key)
        {
            int frameCount;
            float[] samples;

            // Multiplication is historically faster than division within hot loops.
            const float int16Inverse = 1f / 32768f;
            const float int24Inverse = 1f / 8388608f;
            const float int32Inverse = 1f / 2147483648f;

            switch (bitsPerSample)
            {
                case 16:
                    frameCount = rawData.Length / 2 / channels;
                    samples = new float[frameCount * channels];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        short pcm = (short)(rawData[i * 2] | (rawData[i * 2 + 1] << 8));
                        samples[i] = pcm * int16Inverse;
                    }
                    return samples;

                case 24:
                    frameCount = rawData.Length / 3 / channels;
                    samples = new float[frameCount * channels];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        int index = i * 3;
                        int value = (rawData[index] | (rawData[index + 1] << 8) | (rawData[index + 2] << 16));
                        if ((value & 0x800000) != 0)
                            value |= unchecked((int)0xFF000000);
                        samples[i] = value * int24Inverse;
                    }
                    return samples;

                case 32:
                    frameCount = rawData.Length / 4 / channels;
                    samples = new float[frameCount * channels];

                    if (audioFormat == 3) // IEEE float
                    {
                        for (int i = 0; i < samples.Length; i++)
                            samples[i] = BitConverter.ToSingle(rawData, i * 4);
                    }
                    else // PCM 32-bit Integer
                    {
                        for (int i = 0; i < samples.Length; i++)
                        {
                            int value = BitConverter.ToInt32(rawData, i * 4);
                            samples[i] = value * int32Inverse;
                        }
                    }
                    return samples;

                default:
                    Log.Warn($"[AudioManagerAPI] Unsupported WAV bit depth {bitsPerSample} for '{key}'.");
                    return null;
            }
        }

        private float[] DownmixToMono(float[] samples, int channels)
        {
            if (channels == 1) return samples;

            int frames = samples.Length / channels;
            var mono = new float[frames];
            float inv = 1f / channels;

            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                int baseIndex = f * channels;
                for (int c = 0; c < channels; c++)
                    sum += samples[baseIndex + c];
                mono[f] = sum * inv;
            }

            return mono;
        }

        private float[] ResampleLinear(float[] input, int srcRate, int dstRate)
        {
            if (srcRate == dstRate || input.Length == 0) return input;

            double ratio = (double)dstRate / srcRate;
            int outLength = (int)Math.Max(1, Math.Round(input.Length * ratio));
            var output = new float[outLength];

            double pos = 0.0;
            double step = 1.0 / ratio;

            for (int i = 0; i < outLength; i++)
            {
                int idx = (int)pos;
                double frac = pos - idx;

                if (idx >= input.Length - 1)
                {
                    output[i] = input[input.Length - 1];
                }
                else
                {
                    float a = input[idx];
                    float b = input[idx + 1];
                    output[i] = (float)(a + (b - a) * frac);
                }

                pos += step;
            }

            return output;
        }

        private float[] LoadMp3AsFloat(BinaryReader reader, string key)
        {
            try
            {
                byte[] mp3Bytes = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                if (mp3Bytes.Length < 4) return null;

                using (var mp3Stream = new MemoryStream(mp3Bytes))
                using (var mp3Reader = new NAudio.Wave.Mp3FileReader(mp3Stream))
                {
                    var waveFormat = mp3Reader.WaveFormat;
                    int channels = waveFormat.Channels;
                    int sampleRate = waveFormat.SampleRate;

                    using (var pcmStream = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(mp3Reader))
                    using (var mem = new MemoryStream())
                    {
                        pcmStream.CopyTo(mem);
                        byte[] pcmBytes = mem.ToArray();

                        int sampleCount = pcmBytes.Length / 2;
                        short[] pcm16 = new short[sampleCount];
                        Buffer.BlockCopy(pcmBytes, 0, pcm16, 0, pcmBytes.Length);

                        float[] samples = new float[sampleCount];
                        const float inv = 1f / 32768f;
                        for (int i = 0; i < sampleCount; i++)
                            samples[i] = pcm16[i] * inv;

                        if (channels > 1)
                            samples = DownmixToMono(samples, channels);

                        if (sampleRate != TargetSampleRate)
                            samples = ResampleLinear(samples, sampleRate, TargetSampleRate);

                        return samples;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AudioManagerAPI] MP3 decode exception for '{key}': {ex.Message}");
                return null;
            }
        }

        private float[] LoadRawFloatPcm(BinaryReader reader, string key)
        {
            var bytes = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            if (bytes.Length % 4 != 0) return null;

            int sampleCount = bytes.Length / 4;
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToSingle(bytes, i * 4);

            return samples;
        }
    }
}