namespace AudioManagerAPI
{
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Features.Managment;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// Implements audio management with speaker lifecycle and caching for game audio playback.
    /// </summary>
    public class AudioManager : IAudioManager
    {
        private readonly ISpeakerFactory speakerFactory;
        private readonly AudioCache audioCache;
        private readonly Dictionary<byte, ISpeaker> activeSpeakers = new Dictionary<byte, ISpeaker>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioManager"/> class.
        /// </summary>
        /// <param name="factory">The factory used to create speaker instances.</param>
        /// <param name="cache">The audio cache for managing audio samples.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> or <paramref name="cache"/> is null.</exception>
        public AudioManager(ISpeakerFactory factory, AudioCache cache)
        {
            speakerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            audioCache = cache ?? throw new ArgumentNullException(nameof(cache));
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
        /// Plays audio at the specified position with optional speaker configuration.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="position">The 3D position for audio playback.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="configureSpeaker">An optional action to configure the speaker before playback.</param>
        /// <returns>The controller ID of the speaker, or <c>null</c> if playback fails.</returns>
        public byte? PlayAudio(string key, Vector3 position, bool loop, Action<ISpeaker> configureSpeaker = null)
        {
            var samples = audioCache.Get(key);
            if (samples == null) return null;

            byte? controllerId = ControllerIdManager.AllocateId();
            if (!controllerId.HasValue) return null;

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

        /// <summary>
        /// Stops the audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to stop.</param>
        public void StopAudio(byte controllerId)
        {
            if (activeSpeakers.TryGetValue(controllerId, out var speaker))
            {
                speaker.Stop();
            }
        }

        /// <summary>
        /// Destroys the speaker with the specified controller ID and releases its ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to destroy.</param>
        public void DestroySpeaker(byte controllerId)
        {
            if (activeSpeakers.TryGetValue(controllerId, out var speaker))
            {
                speaker.Stop();
                speaker.Destroy();
                activeSpeakers.Remove(controllerId);
                ControllerIdManager.ReleaseId(controllerId);
            }
        }

        /// <summary>
        /// Cleans up all active speakers and resets controller IDs.
        /// </summary>
        public void CleanupAllSpeakers()
        {
            foreach (var speaker in activeSpeakers)
            {
                speaker.Value.Stop();
                speaker.Value.Destroy();
                ControllerIdManager.ReleaseId(speaker.Key);
            }
            activeSpeakers.Clear();
        }

        /// <summary>
        /// Retrieves the speaker instance for the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The speaker instance, or <c>null</c> if not found.</returns>
        public ISpeaker GetSpeaker(byte controllerId)
        {
            activeSpeakers.TryGetValue(controllerId, out var speaker);
            return speaker;
        }
    }
}