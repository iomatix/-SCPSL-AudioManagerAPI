namespace AudioManagerAPI
{
    using UnityEngine;
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Implements audio management with speaker lifecycle and caching for game audio playback.
    /// </summary>
    public class AudioManager : IAudioManager
    {
        private readonly ISpeakerFactory speakerFactory;
        private readonly AudioCache audioCache;
        private readonly Dictionary<byte, ISpeaker> activeSpeakers = new Dictionary<byte, ISpeaker>();
        private readonly object lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioManager"/> class.
        /// </summary>
        /// <param name="speakerFactory">The factory used to create speaker instances.</param>
        /// <param name="cacheSize">The maximum number of audio samples to cache.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="speakerFactory"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="cacheSize"/> is not positive.</exception>
        public AudioManager(ISpeakerFactory speakerFactory, int cacheSize = 50)
        {
            this.speakerFactory = speakerFactory ?? throw new ArgumentNullException(nameof(speakerFactory));
            this.audioCache = new AudioCache(cacheSize);
        }

        /// <summary>
        /// Registers an audio stream provider for a given key.
        /// </summary>
        /// <param name="key">The unique key for the audio.</param>
        /// <param name="streamProvider">A function that provides the audio stream.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="key"/> or <paramref name="streamProvider"/> is null.</exception>
        public void RegisterAudio(string key, Func<Stream> streamProvider)
        {
            audioCache.Register(key, streamProvider);
        }

        /// <summary>
        /// Retrieves the speaker instance for the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The speaker instance, or <c>null</c> if not found.</returns>
        public ISpeaker GetSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                activeSpeakers.TryGetValue(controllerId, out var speaker);
                return speaker;
            }
        }

        /// <summary>
        /// Plays audio at the specified position with optional speaker configuration.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="position">The 3D position for playback.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="configureSpeaker">An optional action to configure the speaker before playback.</param>
        /// <returns>The controller ID of the speaker, or <c>null</c> if playback fails.</returns>
        public byte? PlayAudio(string key, Vector3 position, bool loop, Action<ISpeaker> configureSpeaker = null)
        {
            lock (lockObject)
            {
                var samples = audioCache.Get(key);
                if (samples == null)
                {
                    return null;
                }

                byte? controllerId = ControllerIdManager.AllocateId();
                if (!controllerId.HasValue)
                {
                    return null;
                }

                ISpeaker speaker = speakerFactory.CreateSpeaker(position, controllerId.Value);
                if (speaker == null)
                {
                    ControllerIdManager.ReleaseId(controllerId.Value);
                    return null;
                }

                activeSpeakers[controllerId.Value] = speaker;
                configureSpeaker?.Invoke(speaker);
                speaker.Play(samples, loop);
                return controllerId.Value;
            }
        }

        /// <summary>
        /// Plays audio globally, audible to all players, at the specified position.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="position">The 3D position for playback.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <returns>The controller ID of the speaker, or <c>null</c> if playback fails.</returns>
        public byte? PlayGlobalAudio(string key, Vector3 position, bool loop)
        {
            return PlayAudio(key, position, loop, speaker =>
            {
                if (speaker is ISpeakerWithPlayerFilter playerFilterSpeaker)
                {
                    playerFilterSpeaker.SetValidPlayers(p => true);
                }
            });
        }

        /// <summary>
        /// Stops the audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to stop.</param>
        public void StopAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (activeSpeakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.Stop();
                }
            }
        }

        /// <summary>
        /// Destroys the speaker with the specified controller ID and releases its ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to destroy.</param>
        public void DestroySpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                if (activeSpeakers.TryGetValue(controllerId, out var speaker))
                {
                    speaker.Stop();
                    speaker.Destroy();
                    activeSpeakers.Remove(controllerId);
                    ControllerIdManager.ReleaseId(controllerId);
                }
            }
        }

        /// <summary>
        /// Cleans up all active speakers and releases their controller IDs.
        /// </summary>
        public void CleanupAllSpeakers()
        {
            lock (lockObject)
            {
                foreach (var speaker in activeSpeakers)
                {
                    speaker.Value.Stop();
                    speaker.Value.Destroy();
                    ControllerIdManager.ReleaseId(speaker.Key);
                }
                activeSpeakers.Clear();
            }
        }
    }
}