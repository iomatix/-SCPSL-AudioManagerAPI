namespace AudioManagerAPI.Cache
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Manages a thread-safe cache of audio samples with LRU eviction and asynchronous lazy loading capabilities.
    /// </summary>
    public class AudioCache
    {
        private const int TargetSampleRate = 48000;
        private readonly int maxSize;
        private readonly Dictionary<string, float[]> cache;
        private readonly LinkedList<string> lruOrder;
        private readonly Dictionary<string, Func<Stream>> streamProviders;
        private readonly object lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioCache"/> class.
        /// </summary>
        /// <param name="maxSize">The maximum number of audio samples to cache before eviction.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="maxSize"/> is not positive.</exception>
        public AudioCache(int maxSize = 50)
        {
            this.maxSize = maxSize > 0 ? maxSize : throw new ArgumentException("Cache size must be positive.", nameof(maxSize));
            cache = new Dictionary<string, float[]>();
            lruOrder = new LinkedList<string>();
            streamProviders = new Dictionary<string, Func<Stream>>();
        }

        /// <summary>
        /// Registers an audio stream provider for lazy loading.
        /// </summary>
        public void Register(string key, Func<Stream> streamProvider)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (streamProvider == null) throw new ArgumentNullException(nameof(streamProvider));

            lock (lockObject)
            {
                if (!streamProviders.ContainsKey(key))
                {
                    streamProviders[key] = streamProvider;
                }
            }
        }

        /// <summary>
        /// Retrieves audio samples, loading them from the stream if necessary without blocking the cache.
        /// </summary>
        public float[] Get(string key)
        {
            Func<Stream> providerToExecute = null;

            lock (lockObject)
            {
                if (cache.TryGetValue(key, out var cachedSamples))
                {
                    UpdateLRU(key);
                    return cachedSamples;
                }

                if (!streamProviders.TryGetValue(key, out providerToExecute))
                {
                    return null;
                }
            }

            // Execute I/O and decoding OUTSIDE the lock to prevent server thread blocking
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

            float[] newSamples = null;
            if (stream != null)
            {
                newSamples = LoadAudio(stream, key);
            }

            if (newSamples == null)
            {
                return null;
            }

            // Lock again to safely integrate the newly loaded samples
            lock (lockObject)
            {
                // Double-check: another thread might have loaded it while we were doing I/O
                if (cache.TryGetValue(key, out var existingSamples))
                {
                    UpdateLRU(key);
                    return existingSamples;
                }

                if (cache.Count >= maxSize)
                {
                    var lruKey = lruOrder.Last.Value;
                    cache.Remove(lruKey);
                    lruOrder.RemoveLast();
                    Log.Debug($"[AudioManagerAPI] Evicted cached audio '{lruKey}' due to memory limits.");
                }

                cache[key] = newSamples;
                lruOrder.AddFirst(key);
                return newSamples;
            }
        }

        private void UpdateLRU(string key)
        {
            lruOrder.Remove(key);
            lruOrder.AddFirst(key);
        }

        private float[] LoadAudio(Stream stream, string key)
        {
            try
            {
                using (stream)
                using (var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false))
                {
                    // Peek first 4 bytes to detect format
                    var header = reader.ReadBytes(4);
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);

                    if (header.Length == 4 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F')
                        return LoadWavAsFloat(reader, key);

                    if (header[0] == 'I' && header[1] == 'D' && header[2] == '3')
                        return LoadMp3AsFloat(reader, key);

                    if (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                        return LoadMp3AsFloat(reader, key);

                    // Fallback: try raw 32-bit float PCM (mono, 48kHz)
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
            string riffHeader = new string(reader.ReadChars(4));
            if (riffHeader != "RIFF")
            {
                Log.Warn($"[AudioManagerAPI] Audio cache failed for '{key}': Not a valid RIFF file.");
                return null;
            }

            reader.ReadInt32(); // File size
            string waveHeader = new string(reader.ReadChars(4));
            if (waveHeader != "WAVE")
            {
                Log.Warn($"[AudioManagerAPI] Audio cache failed for '{key}': Not a valid WAVE file.");
                return null;
            }

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

            if (dataBytes == null)
            {
                Log.Warn($"[AudioManagerAPI] Audio cache failed for '{key}': No 'data' chunk found.");
                return null;
            }

            var samples = ConvertPcmToFloat(dataBytes, audioFormat, bitsPerSample, channels, key);
            if (samples == null)
                return null;

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

            switch (bitsPerSample)
            {
                case 16:
                    frameCount = rawData.Length / 2 / channels;
                    samples = new float[frameCount * channels];
                    for (int i = 0; i < frameCount * channels; i++)
                    {
                        short pcm = (short)(rawData[i * 2] | (rawData[i * 2 + 1] << 8));
                        samples[i] = pcm / 32768f;
                    }
                    return samples;

                case 24:
                    frameCount = rawData.Length / 3 / channels;
                    samples = new float[frameCount * channels];
                    for (int i = 0; i < frameCount * channels; i++)
                    {
                        int index = i * 3;
                        int value = (rawData[index] | (rawData[index + 1] << 8) | (rawData[index + 2] << 16));
                        if ((value & 0x800000) != 0)
                            value |= unchecked((int)0xFF000000);
                        samples[i] = value / 8388608f;
                    }
                    return samples;

                case 32:
                    frameCount = rawData.Length / 4 / channels;
                    samples = new float[frameCount * channels];

                    if (audioFormat == 3) // IEEE float
                    {
                        for (int i = 0; i < frameCount * channels; i++)
                            samples[i] = BitConverter.ToSingle(rawData, i * 4);
                    }
                    else // PCM 32-bit
                    {
                        for (int i = 0; i < frameCount * channels; i++)
                        {
                            int value = BitConverter.ToInt32(rawData, i * 4);
                            samples[i] = value / 2147483648f;
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
            if (channels == 1)
                return samples;

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
            if (srcRate == dstRate || input.Length == 0)
                return input;

            double ratio = (double)dstRate / srcRate;
            int outLength = (int)Math.Max(1, Math.Round(input.Length * ratio));
            var output = new float[outLength];

            double pos = 0.0;
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

                pos += 1.0 / ratio;
            }

            return output;
        }

        private float[] LoadMp3AsFloat(BinaryReader reader, string key)
        {
            try
            {
                // Read entire MP3 file
                byte[] mp3Bytes = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                if (mp3Bytes.Length < 4)
                {
                    Log.Warn($"[AudioManagerAPI] MP3 '{key}' is too small.");
                    return null;
                }

                using (var mp3Stream = new MemoryStream(mp3Bytes))
                using (var mp3Reader = new NAudio.Wave.Mp3FileReader(mp3Stream))
                {
                    var waveFormat = mp3Reader.WaveFormat;
                    int channels = waveFormat.Channels;
                    int sampleRate = waveFormat.SampleRate;

                    // Convert MP3 → PCM16
                    using (var pcmStream = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(mp3Reader))
                    using (var mem = new MemoryStream())
                    {
                        pcmStream.CopyTo(mem);
                        byte[] pcmBytes = mem.ToArray();

                        int sampleCount = pcmBytes.Length / 2;
                        short[] pcm16 = new short[sampleCount];
                        Buffer.BlockCopy(pcmBytes, 0, pcm16, 0, pcmBytes.Length);

                        // Convert PCM16 → float
                        float[] samples = new float[sampleCount];
                        const float inv = 1f / 32768f;
                        for (int i = 0; i < sampleCount; i++)
                            samples[i] = pcm16[i] * inv;

                        // Downmix if needed
                        if (channels > 1)
                            samples = DownmixToMono(samples, channels);

                        // Resample if needed
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
            if (bytes.Length % 4 != 0)
            {
                Log.Warn($"[AudioManagerAPI] Audio cache failed for '{key}': Raw float PCM size is not divisible by 4.");
                return null;
            }

            int sampleCount = bytes.Length / 4;
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = BitConverter.ToSingle(bytes, i * 4);

            return samples;
        }

    }
}