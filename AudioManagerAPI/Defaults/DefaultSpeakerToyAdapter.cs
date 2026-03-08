namespace AudioManagerAPI.Defaults
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using MEC;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Audio;
    using LabApi.Features.Wrappers;

    using Log = DebugLogger;

    /// <summary>
    /// Adapts LabAPI's <see cref="SpeakerToy"/> to the <see cref="ISpeakerWithPlayerFilter"/> interface,
    /// including pause, resume, skip, and fade-in/fade-out functionality.
    /// Acts as the physical layer implementation for audio playback.
    /// </summary>
    public class DefaultSpeakerToyAdapter : ISpeakerWithPlayerFilter
    {
        private readonly SpeakerToy speakerToy;
        private float targetVolume;

        /// <summary>
        /// Occurs when the speaker's audio queue becomes empty.
        /// </summary>
        public event Action QueueEmpty;

        /// <summary>
        /// Gets a value indicating whether the speaker's audio queue is currently empty.
        /// Safely evaluates to true if the underlying transmitter does not exist.
        /// </summary>
        /// <value>
        /// <c>true</c> if there are no queued audio clips or no transmitter; otherwise, <c>false</c>.
        /// </value>
        public bool IsQueueEmpty => (SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0) == 0;

        /// <summary>
        /// Gets the number of audio clips currently queued for playback.
        /// </summary>
        /// <value>
        /// An integer representing the number of queued clips. Returns 0 if no transmitter is found.
        /// </value>
        public int QueuedClipsCount => SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.AudioClipSamples.Count ?? 0;

        /// <summary>
        /// Gets or sets the player filter used to determine which players can hear audio from this speaker.
        /// </summary>
        /// <value>
        /// A delegate that returns <c>true</c> for valid players who should receive audio playback.
        /// </value>
        /// <remarks>
        /// This property is typically used to restrict playback to a subset of players.
        /// For example, only players in a certain room or team.
        /// </remarks>
        public Func<Player, bool> ValidPlayers
        {
            get => SpeakerToy.GetTransmitter(speakerToy.ControllerId)?.ValidPlayers;
            set
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                if (transmitter != null)
                {
                    transmitter.ValidPlayers = value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSpeakerToyAdapter"/> class.
        /// </summary>
        /// <param name="speakerToy">The underlying LabAPI SpeakerToy instance to adapt.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="speakerToy"/> is null.</exception>
        public DefaultSpeakerToyAdapter(SpeakerToy speakerToy)
        {
            this.speakerToy = speakerToy ?? throw new ArgumentNullException(nameof(speakerToy));
            targetVolume = 1f;
        }

        /// <summary>
        /// Plays the provided audio samples with the specified looping behavior and starting position.
        /// </summary>
        /// <param name="samples">The float array of PCM audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop continuously.</param>
        /// <param name="playbackPosition">The starting playback position in seconds.</param>
        public void Play(float[] samples, bool loop, float playbackPosition = 0f)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter == null)
            {
                Log.Warn($"[DefaultSpeakerToyAdapter] No transmitter found for SpeakerToy with ControllerId {speakerToy.ControllerId}.");
                return;
            }

            float[] adjustedSamples = samples;
            if (playbackPosition > 0f && samples != null)
            {
                int sampleIndex = Mathf.FloorToInt(playbackPosition * AudioTransmitter.SampleRate);
                if (sampleIndex >= samples.Length)
                {
                    Log.Warn($"[DefaultSpeakerToyAdapter] Playback position {playbackPosition}s exceeds sample length for ControllerId {speakerToy.ControllerId}.");
                    return;
                }
                adjustedSamples = new float[samples.Length - sampleIndex];
                Array.Copy(samples, sampleIndex, adjustedSamples, 0, adjustedSamples.Length);
            }

            transmitter.Play(adjustedSamples, queue: false, loop: loop);
            SetVolume(targetVolume);
            Log.Debug($"[DefaultSpeakerToyAdapter] Playing clip at position {playbackPosition} for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Queues the provided list of audio samples to be played after the current playback finishes.
        /// </summary>
        /// <param name="samples">The float array of PCM audio samples to queue.</param>
        /// <param name="loop">Whether the queued audio should loop when it plays.</param>
        public void Queue(float[] samples, bool loop)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.Play(samples, queue: true, loop: loop);
                Log.Debug($"[DefaultSpeakerToyAdapter] Queued clip for controller ID {speakerToy.ControllerId}.");
            }
        }

        /// <summary>
        /// Clears all currently queued audio samples for this physical speaker.
        /// </summary>
        public void ClearQueue()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.AudioClipSamples.Clear();
                Log.Debug($"[DefaultSpeakerToyAdapter] Cleared queue for controller ID {speakerToy.ControllerId}.");
                if (transmitter.AudioClipSamples.Count == 0)
                {
                    QueueEmpty?.Invoke();
                }
            }
        }

        /// <summary>
        /// Immediately stops the current audio playback.
        /// </summary>
        public void Stop()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Stop();
            Log.Debug($"[DefaultSpeakerToyAdapter] Stopped playback for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Destroys the underlying LabAPI SpeakerToy and releases its allocated game resources.
        /// </summary>
        public void Destroy()
        {
            speakerToy?.Destroy();
            Log.Debug($"[DefaultSpeakerToyAdapter] Destroyed speaker for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Pauses the current audio playback, maintaining the current position.
        /// </summary>
        public void Pause()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Pause();
            Log.Debug($"[DefaultSpeakerToyAdapter] Paused playback for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Resumes the previously paused audio playback.
        /// </summary>
        public void Resume()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Resume();
            Log.Debug($"[DefaultSpeakerToyAdapter] Resumed playback for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Skips the specified number of queued clips. If the count exceeds the queue length, playback stops.
        /// </summary>
        /// <param name="count">The number of clips to skip.</param>
        public void Skip(int count)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.Skip(count);
                Log.Debug($"[DefaultSpeakerToyAdapter] Skipped {count} clips for controller ID {speakerToy.ControllerId}.");
                if (transmitter.AudioClipSamples.Count == 0)
                {
                    QueueEmpty?.Invoke();
                }
            }
        }

        /// <summary>
        /// Fades in the audio volume from 0 to the target volume over the specified duration.
        /// </summary>
        /// <param name="duration">The duration of the fade-in effect in seconds.</param>
        public void FadeIn(float duration)
        {
            if (duration > 0)
            {
                Timing.RunCoroutine(FadeVolume(0f, targetVolume, duration));
                Log.Debug($"[DefaultSpeakerToyAdapter] Fading in over {duration}s for controller ID {speakerToy.ControllerId}.");
            }
        }

        /// <summary>
        /// Fades out the audio volume to 0 over the specified duration and optionally stops playback upon completion.
        /// </summary>
        /// <param name="duration">The duration of the fade-out effect in seconds.</param>
        /// <param name="onComplete">Optional delegate invoked when the fade-out is complete.</param>
        public void FadeOut(float duration, Action onComplete = null)
        {
            if (duration > 0)
            {
                Timing.RunCoroutine(FadeVolume(speakerToy.Volume, 0f, duration, stopOnComplete: true, onComplete));
                Log.Debug($"[DefaultSpeakerToyAdapter] Fading out over {duration}s for controller ID {speakerToy.ControllerId}.");
            }
            else
            {
                Stop();
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Sets the filter function for valid players.
        /// <example>
        /// <code>
        /// adapter.SetValidPlayers(p => Player.ReadyList.Contains(p));
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="playerFilter">A filter expression that determines which players are valid listeners.</param>
        public void SetValidPlayers(Func<Player, bool> playerFilter)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.ValidPlayers = playerFilter;
                Log.Debug($"[DefaultSpeakerToyAdapter] Set player filter for controller ID {speakerToy.ControllerId}.");
            }
        }

        /// <summary>
        /// Sets the playback volume for the speaker.
        /// Use 0.0f to mute and 1.0f for maximum volume. Intermediate values apply proportionally.
        /// </summary>
        /// <param name="volume">A float value ranging from 0.0 to 1.0.</param>
        public void SetVolume(float volume)
        {
            speakerToy.Volume = Mathf.Clamp01(volume);
            targetVolume = speakerToy.Volume;
            Log.Debug($"[DefaultSpeakerToyAdapter] Set volume to {volume} for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Sets the minimum distance at which the audio starts to attenuate.
        /// Ensures the value is non-negative.
        /// </summary>
        /// <param name="minDistance">The minimum distance in meters.</param>
        public void SetMinDistance(float minDistance)
        {
            speakerToy.MinDistance = Mathf.Max(0, minDistance);
            Log.Debug($"[DefaultSpeakerToyAdapter] Set min distance to {minDistance} for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Sets the maximum distance beyond which the audio is no longer audible.
        /// Ensures the value is non-negative.
        /// </summary>
        /// <param name="maxDistance">The maximum distance in meters.</param>
        public void SetMaxDistance(float maxDistance)
        {
            speakerToy.MaxDistance = Mathf.Max(0, maxDistance);
            Log.Debug($"[DefaultSpeakerToyAdapter] Set max distance to {maxDistance} for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Enables or disables spatial audio playback for the speaker.
        /// </summary>
        /// <param name="isSpatial">
        /// If <c>true</c>, enables 3D spatial audio; if <c>false</c>, plays audio in a non-spatial (2D) context.
        /// </param>
        public void SetSpatialization(bool isSpatial)
        {
            speakerToy.IsSpatial = isSpatial;
            Log.Debug($"[DefaultSpeakerToyAdapter] Set spatialization to {isSpatial} for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Sets the 3D world position of the speaker.
        /// </summary>
        /// <param name="position">The new position vector in the 3D context.</param>
        public void SetPosition(Vector3 position)
        {
            speakerToy.Position = position;
            Log.Debug($"[DefaultSpeakerToyAdapter] Set position to {position} for controller ID {speakerToy.ControllerId}.");
        }

        /// <summary>
        /// Gets the current playback position in seconds based on the audio transmitter's state.
        /// </summary>
        /// <returns>The current playback position in seconds, or 0f if no active transmitter exists.</returns>
        public float GetPlaybackPosition()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            return transmitter != null ? transmitter.CurrentPosition / (float)AudioTransmitter.SampleRate : 0f;
        }

        /// <summary>
        /// Smoothly interpolates the speaker volume from <paramref name="startVolume"/> to <paramref name="endVolume"/> over the specified duration.
        /// This coroutine uses MEC's IEnumerator to yield frame-by-frame for volume transitions.
        /// </summary>
        /// <param name="startVolume">Starting volume level (0.0 for mute, 1.0 for max).</param>
        /// <param name="endVolume">Target volume level.</param>
        /// <param name="duration">Duration of the fade effect in seconds.</param>
        /// <param name="stopOnComplete">
        /// Whether to stop audio playback after the fade completes.
        /// </param>
        /// <param name="onComplete">Optional callback invoked when the fade completes.</param>
        private IEnumerator<float> FadeVolume(float startVolume, float endVolume, float duration, bool stopOnComplete = false, Action onComplete = null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Timing.DeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetVolume(Mathf.Lerp(startVolume, endVolume, t));
                yield return Timing.WaitForOneFrame;
            }
            SetVolume(endVolume);
            if (stopOnComplete)
            {
                Stop();
            }
            onComplete?.Invoke();
        }
    }
}