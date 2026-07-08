namespace AudioManagerAPI.Cache
{
    using LabApi.Features.Console;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Log = AudioManagerAPI.Logger.ApiLogger;

    /// <summary>
    /// Thread-safe audio sample cache implementing predictive background pre-decoding, 
    /// lock-free multi-format parsing, and least-recently-used (LRU) memory eviction with ArrayPool optimization.
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
                    Logger.Error($" Predictive background audio warmup failed for '{key}': {ex.Message}");
                }
            });
        }

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

            Stream stream = null;
            try
            {
                stream = providerToExecute();
            }
            catch (Exception ex)
            {
                Logger.Error($" Failed to invoke stream provider for key '{key}': {ex.Message}");
                return null;
            }

            float[] newSamples = stream != null ? LoadAudio(stream, key) : null;
            if (newSamples == null) return null;

            lock (_lockObject)
            {
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

                // Explicitly remove the provider factory to release background closures,
                // localized anonymous methods, and plugin references from enduring heap chains.
                _streamProviders.Remove(lruKey);

                Log.Debug($" Evicted cached audio '{lruKey}' and released its stream provider from memory registry.");
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
                Logger.Error($" Audio cache exception while loading '{key}': {ex.Message}");
                return null;
            }
        }

        private float[] LoadWavAsFloat(BinaryReader reader, string key)
        {
            if (new string(reader.ReadChars(4)) != "RIFF") return null;
            reader.ReadInt32();
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
                    reader.ReadInt32();
                    reader.ReadInt16();
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

            int currentLength = 0;
            float[] currentSamples = ConvertPcmToFloatRented(dataBytes, audioFormat, bitsPerSample, channels, key, out currentLength);
            if (currentSamples == null) return null;

            if (channels > 1)
            {
                float[] monoSamples = DownmixToMonoRented(currentSamples, currentLength, channels, out int monoLength);
                ArrayPool<float>.Shared.Return(currentSamples);
                currentSamples = monoSamples;
                currentLength = monoLength;
            }

            if (sampleRate != TargetSampleRate && sampleRate > 0)
            {
                float[] resampledSamples = ResampleLinearRented(currentSamples, currentLength, sampleRate, TargetSampleRate, out int resampledLength);
                ArrayPool<float>.Shared.Return(currentSamples);
                currentSamples = resampledSamples;
                currentLength = resampledLength;
            }

            float[] finalExactArray = new float[currentLength];
            Array.Copy(currentSamples, 0, finalExactArray, 0, currentLength);
            ArrayPool<float>.Shared.Return(currentSamples);

            return finalExactArray;
        }

        private float[] ConvertPcmToFloatRented(byte[] rawData, short audioFormat, short bitsPerSample, int channels, string key, out int outLength)
        {
            int frameCount;
            float[] samples;

            const float int16Inverse = 1f / 32768f;
            const float int24Inverse = 1f / 8388608f;
            const float int32Inverse = 1f / 2147483648f;

            switch (bitsPerSample)
            {
                case 16:
                    frameCount = rawData.Length / 2 / channels;
                    outLength = frameCount * channels;
                    samples = ArrayPool<float>.Shared.Rent(outLength);
                    for (int i = 0; i < outLength; i++)
                    {
                        short pcm = (short)(rawData[i * 2] | (rawData[i * 2 + 1] << 8));
                        samples[i] = pcm * int16Inverse;
                    }
                    return samples;

                case 24:
                    frameCount = rawData.Length / 3 / channels;
                    outLength = frameCount * channels;
                    samples = ArrayPool<float>.Shared.Rent(outLength);
                    for (int i = 0; i < outLength; i++)
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
                    outLength = frameCount * channels;
                    samples = ArrayPool<float>.Shared.Rent(outLength);

                    if (audioFormat == 3)
                    {
                        for (int i = 0; i < outLength; i++)
                            samples[i] = BitConverter.ToSingle(rawData, i * 4);
                    }
                    else
                    {
                        for (int i = 0; i < outLength; i++)
                        {
                            int value = BitConverter.ToInt32(rawData, i * 4);
                            samples[i] = value * int32Inverse;
                        }
                    }
                    return samples;

                default:
                    Logger.Warn($" Unsupported WAV bit depth {bitsPerSample} for '{key}'.");
                    outLength = 0;
                    return null;
            }
        }

        private float[] DownmixToMonoRented(float[] samples, int samplesLength, int channels, out int outLength)
        {
            int frames = samplesLength / channels;
            outLength = frames;
            var mono = ArrayPool<float>.Shared.Rent(outLength);
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

        private float[] ResampleLinearRented(float[] input, int inputLength, int srcRate, int dstRate, out int outLength)
        {
            if (srcRate == dstRate || inputLength == 0)
            {
                outLength = inputLength;
                var identity = ArrayPool<float>.Shared.Rent(outLength);
                Array.Copy(input, 0, identity, 0, inputLength);
                return identity;
            }

            double ratio = (double)dstRate / srcRate;
            outLength = (int)Math.Max(1, Math.Round(inputLength * ratio));
            var output = ArrayPool<float>.Shared.Rent(outLength);

            double pos = 0.0;
            double step = 1.0 / ratio;

            for (int i = 0; i < outLength; i++)
            {
                int idx = (int)pos;
                double frac = pos - idx;

                if (idx >= inputLength - 1)
                {
                    output[i] = input[inputLength - 1];
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

        /// <summary>
        /// Decodes MP3 streams utilizing the unified cross-platform pipeline with zero allocation overhead.
        /// </summary>
        private float[] LoadMp3AsFloat(BinaryReader reader, string key)
        {
            // DRY Unification: Hand off stream decoding responsibility entirely to the new cross-platform Mp3Decoder
            float[] currentSamples = Decoders.Mp3Decoder.DecodeMp3ToFloatRented(reader.BaseStream, out int currentLength, out int sampleRate, out int channels);
            if (currentSamples == null || currentLength == 0)
                return null;

            if (channels > 1)
            {
                float[] monoSamples = DownmixToMonoRented(currentSamples, currentLength, channels, out int monoLength);
                ArrayPool<float>.Shared.Return(currentSamples);
                currentSamples = monoSamples;
                currentLength = monoLength;
            }

            if (sampleRate != TargetSampleRate && sampleRate > 0)
            {
                float[] resampledSamples = ResampleLinearRented(currentSamples, currentLength, sampleRate, TargetSampleRate, out int resampledLength);
                ArrayPool<float>.Shared.Return(currentSamples);
                currentSamples = resampledSamples;
                currentLength = resampledLength;
            }

            // Copy out precisely sized long-term payload for the cache architecture before returning transient buffer
            float[] finalExactArray = new float[currentLength];
            Array.Copy(currentSamples, 0, finalExactArray, 0, currentLength);
            ArrayPool<float>.Shared.Return(currentSamples);

            return finalExactArray;
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