
namespace AudioManagerAPI.Cache
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Manages a cache of audio samples with LRU eviction and lazy loading capabilities.
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
        /// <param name="key">The unique key for the audio.</param>
        /// <param name="streamProvider">A function that provides the audio stream when needed.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="streamProvider"/> is null.</exception>
        public void Register(string key, Func<Stream> streamProvider)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (lockObject)
            {
                if (!streamProviders.ContainsKey(key))
                {
                    streamProviders[key] = streamProvider ?? throw new ArgumentNullException(nameof(streamProvider));
                }
            }
        }

        /// <summary>
        /// Retrieves audio samples, loading them if necessary.
        /// </summary>
        /// <param name="key">The key identifying the audio.</param>
        /// <returns>The audio samples, or <c>null</c> if unavailable.</returns>
        public float[] Get(string key)
        {
            lock (lockObject)
            {
                if (cache.TryGetValue(key, out var samples))
                {
                    lruOrder.Remove(key);
                    lruOrder.AddFirst(key);
                    return samples;
                }

                if (!streamProviders.TryGetValue(key, out var provider))
                {
                    return null;
                }

                samples = LoadAudio(provider());
                if (samples == null)
                {
                    return null;
                }

                if (cache.Count >= maxSize)
                {
                    var lruKey = lruOrder.Last.Value;
                    cache.Remove(lruKey);
                    lruOrder.RemoveLast();
                }

                cache[key] = samples;
                lruOrder.AddFirst(key);
                return samples;
            }
        }

        /// <summary>
        /// Supports only 48kHz, Mono, Singed 16-bit PCM .wav files
        /// </summary>
        private float[] LoadAudio(Stream stream)
        {
            try
            {
                using (var reader = new BinaryReader(stream))
                {
                    reader.BaseStream.Seek(44, SeekOrigin.Begin); // Skip WAV header
                    byte[] rawData = reader.ReadBytes((int)(stream.Length - 44));
                    float[] samples = new float[rawData.Length / 2];
                    for (int i = 0; i < samples.Length; i++)
                    {
                        samples[i] = BitConverter.ToInt16(rawData, i * 2) / 32768f;
                    }
                    return samples;
                }
            }
            catch
            {
                return null;
            }
        }
    }

}
