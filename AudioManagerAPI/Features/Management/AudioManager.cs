namespace AudioManagerAPI.Features.Management
{
    using AudioManagerAPI.Cache;
    using AudioManagerAPI.Controllers;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Implements audio management with speaker lifecycle and caching for game audio playback.
    /// </summary>
    public class AudioManager : IAudioManager
    {
        private readonly Dictionary<byte, ISpeaker> speakers = new Dictionary<byte, ISpeaker>();
        private readonly AudioCache audioCache;
        private readonly ISpeakerFactory speakerFactory;
        private readonly object lockObject = new object();
        private const float DEFAULT_FADE_DURATION = 1f;

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
            if (cacheSize <= 0)
                throw new ArgumentException("Cache size must be positive.", nameof(cacheSize));
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
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (streamProvider == null)
                throw new ArgumentNullException(nameof(streamProvider));
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
                speakers.TryGetValue(controllerId, out ISpeaker speaker);
                return speaker;
            }
        }

        /// <summary>
        /// Determines whether a given controller ID refers to a valid, active speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID to validate.</param>
        /// <returns><c>true</c> if the controller ID is valid; otherwise, <c>false</c>.</returns>
        public bool IsValidController(byte controllerId)
        {
            lock (lockObject)
            {
                return controllerId != 0 && speakers.ContainsKey(controllerId);
            }
        }

        /// <summary>
        /// Plays audio at the specified position with optional configuration.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="position">The 3D position for playback.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance where audio starts to fall off.</param>
        /// <param name="maxDistance">The maximum distance where audio falls to zero.</param>
        /// <param name="isSpatial">
        /// If <c>true</c>, enables 3D spatial audio (distance-based attenuation).
        /// If <c>false</c>, plays the clip globally without attenuation.
        /// </param>
        /// <param name="priority">The priority of the audio.</param>
        /// <param name="configureSpeaker">An optional action to configure the speaker before playback.</param>
        /// <param name="queue">Whether to queue the audio instead of playing immediately.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        public byte PlayAudio(string key, Vector3 position, bool loop, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, Action<ISpeaker> configureSpeaker = null, bool queue = false)
        {
            lock (lockObject)
            {
                float[] samples = audioCache.Get(key);
                if (samples == null)
                {
                    Log.Warn($"[AudioManagerAPI] Audio with key {key} not found.");
                    return 0;
                }

                byte controllerId = ControllerIdManager.AllocateId(
                    priority,
                    stopCallback: null, // Temporarily null to avoid capturing unassigned controllerId
                    onSuccess: id => { },
                    onFailure: () => Log.Warn($"[AudioManagerAPI] Failed to allocate controller ID for audio {key}.")
                );

                if (controllerId == 0)
                {
                    return 0;
                }

                // Assign stopCallback after controllerId is assigned
                ControllerIdManager.UpdateStopCallback(controllerId, () => FadeOutAudio(controllerId, DEFAULT_FADE_DURATION));

                ISpeaker speaker = speakerFactory.CreateSpeaker(position, controllerId);
                if (speaker == null)
                {
                    ControllerIdManager.ReleaseId(controllerId);
                    Log.Warn($"[AudioManagerAPI] Failed to create speaker for audio {key}.");
                    return 0;
                }

                if (speaker is ISpeakerWithPlayerFilter playerFilterSpeaker)
                {
                    playerFilterSpeaker.SetVolume(volume);
                    playerFilterSpeaker.SetMinDistance(minDistance);
                    playerFilterSpeaker.SetMaxDistance(maxDistance);
                    playerFilterSpeaker.SetSpatialization(isSpatial);
                    configureSpeaker?.Invoke(speaker);
                }

                speakers[controllerId] = speaker;

                if (queue)
                {
                    speaker.Queue(samples, loop);
                }
                else
                {
                    speaker.Play(samples, loop);
                }

                return controllerId;
            }
        }

        /// <summary>
        /// Plays audio globally, audible to all ready players.
        /// </summary>
        /// <param name="key">The key identifying the audio to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        /// <param name="priority">The priority of the audio.</param>
        /// <param name="queue">Whether to queue the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        public byte PlayGlobalAudio(string key, bool loop, float volume, AudioPriority priority, bool queue = false, float fadeInDuration = 0f)
        {
            byte controllerId = PlayAudio(key, Vector3.zero, loop, volume, 0f, 999.99f, false, priority, speaker =>
            {
                if (speaker is ISpeakerWithPlayerFilter playerFilterSpeaker)
                {
                    playerFilterSpeaker.SetValidPlayers(p => Player.ReadyList.Contains(p));
                }
            }, queue);

            if (controllerId != 0 && fadeInDuration > 0)
            {
                FadeInAudio(controllerId, fadeInDuration);
            }

            return controllerId;
        }

        /// <summary>
        /// Stops the audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to stop.</param>
        public void StopAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.Stop();
                }
            }
        }

        /// <summary>
        /// Pauses the audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to pause.</param>
        public void PauseAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.Pause();
                }
            }
        }

        /// <summary>
        /// Resumes paused audio associated with the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to resume.</param>
        public void ResumeAudio(byte controllerId)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.Resume();
                }
            }
        }

        /// <summary>
        /// Skips the current or queued audio clips for the specified controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to skip audio for.</param>
        /// <param name="count">The number of clips to skip, including the current one.</param>
        public void SkipAudio(byte controllerId, int count)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.Skip(count);
                }
            }
        }

        /// <summary>
        /// Fades in audio associated with the specified controller ID over the specified duration.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to fade in.</param>
        /// <param name="duration">The duration of the fade-in effect in seconds.</param>
        public void FadeInAudio(byte controllerId, float duration)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.FadeIn(duration);
                }
            }
        }

        /// <summary>
        /// Fades out audio associated with the specified controller ID over the specified duration.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to fade out.</param>
        /// <param name="duration">The duration of the fade-out effect in seconds.</param>
        public void FadeOutAudio(byte controllerId, float duration)
        {
            lock (lockObject)
            {
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.FadeOut(duration);
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
                if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    speaker.Stop();
                    speaker.Destroy();
                    speakers.Remove(controllerId);
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
                foreach (byte controllerId in new List<byte>(speakers.Keys))
                {
                    if (speakers.TryGetValue(controllerId, out ISpeaker speaker))
                    {
                        speaker.Stop();
                        speaker.Destroy();
                        ControllerIdManager.ReleaseId(controllerId);
                    }
                }
                speakers.Clear();
            }
        }
    }
}