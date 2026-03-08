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

        /// <summary>
        /// Robustly parses 48kHz, Mono, Signed 16-bit PCM .wav files.
        /// Searches for the actual "data" chunk instead of blindly skipping 44 bytes.
        /// </summary>
        private float[] LoadAudio(Stream stream, string key)
        {
            try
            {
                using (stream)
                using (var reader = new BinaryReader(stream, Encoding.ASCII))
                {
                    // Verify RIFF container
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

                    // Scan chunks to locate "data"
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        string chunkId = new string(reader.ReadChars(4));
                        int chunkSize = reader.ReadInt32();

                        if (chunkId == "data")
                        {
                            byte[] rawData = reader.ReadBytes(chunkSize);
                            float[] samples = new float[rawData.Length / 2];

                            // High-performance manual bitwise conversion
                            for (int i = 0; i < samples.Length; i++)
                            {
                                short pcmValue = (short)(rawData[i * 2] | (rawData[i * 2 + 1] << 8));
                                samples[i] = pcmValue / 32768f;
                            }
                            return samples;
                        }

                        // Skip other non-audio chunks (like "fmt ", "LIST", "INFO")
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                    }

                    Log.Warn($"[AudioManagerAPI] Audio cache failed for '{key}': No 'data' chunk found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AudioManagerAPI] Audio cache exception while loading '{key}': {ex.Message}");
                return null;
            }
        }
    }
}