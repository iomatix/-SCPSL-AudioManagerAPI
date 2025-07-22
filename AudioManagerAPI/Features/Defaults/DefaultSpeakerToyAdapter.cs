namespace AudioManagerAPI.Defaults
{
    using System;
    using System.Collections.Generic;
    using MEC;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Adapts LabAPI's <see cref="SpeakerToy"/> to the <see cref="ISpeakerWithPlayerFilter"/> interface,
    /// including pause, resume, skip, and fade-in/fade-out functionality.
    /// </summary>
    public class DefaultSpeakerToyAdapter : ISpeakerWithPlayerFilter
    {
        private readonly SpeakerToy speakerToy;
        private float targetVolume;
        private readonly Queue<(float[] samples, bool loop)> audioQueue = new Queue<(float[] samples, bool loop)>();
        private bool isPlaying;
        private float playbackPosition;
        private float[] currentSamples;
        private float sampleRate;

        public event Action QueueEmpty;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSpeakerToyAdapter"/> class.
        /// </summary>
        /// <param name="position">The 3D world position for audio playback.</param>
        public DefaultSpeakerToyAdapter(SpeakerToy speakerToy)
        {
            this.speakerToy = speakerToy ?? throw new ArgumentNullException(nameof(speakerToy));
            targetVolume = 1f;
            isPlaying = false;
            playbackPosition = 0f;
            sampleRate = 48000f;
        }


        /// <summary>
        /// Plays the provided audio samples with the specified looping behavior.
        /// </summary>
        /// <param name="samples">The audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        /// <param name="playbackPosition">The starting position in seconds.</param>
        public void Play(float[] samples, bool loop, float playbackPosition = 0f)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter == null)
            {
                Log.Warn($"[AudioManagerAPI] No transmitter found for SpeakerToy with ControllerId {speakerToy.ControllerId}.");
                return;
            }

            // Store current samples and position
            this.currentSamples = samples;
            this.playbackPosition = playbackPosition;

            // Convert playbackPosition (seconds) to sample index
            int sampleIndex = Mathf.FloorToInt(playbackPosition * sampleRate);
            float[] adjustedSamples = samples;

            // If playbackPosition is non-zero, slice the samples array
            if (sampleIndex > 0 && sampleIndex < samples.Length)
            {
                adjustedSamples = new float[samples.Length - sampleIndex];
                Array.Copy(samples, sampleIndex, adjustedSamples, 0, adjustedSamples.Length);
            }
            else if (sampleIndex >= samples.Length)
            {
                Log.Warn($"[AudioManagerAPI] Playback position {playbackPosition}s exceeds sample length for ControllerId {speakerToy.ControllerId}.");
                adjustedSamples = Array.Empty<float>();
                isPlaying = false;
                CheckQueue();
                return;
            }

            // Play adjusted samples
            transmitter.Play(adjustedSamples, queue: false, loop: loop);
            SetVolume(targetVolume);
            isPlaying = true;

            // Start coroutine to update playback position
            Timing.RunCoroutine(UpdatePlaybackPosition());

            CheckQueue();
        }

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        public float GetPlaybackPosition()
        {
            return playbackPosition;
        }

        /// <summary>
        /// Updates the playback position while the audio is playing.
        /// </summary>
        private IEnumerator<float> UpdatePlaybackPosition()
        {
            while (isPlaying)
            {
                playbackPosition += Timing.DeltaTime;
                yield return Timing.WaitForOneFrame;
            }
        }

        /// <summary>
        /// Plays the provided list of audio samples in order with the specified looping behavior.
        /// </summary>
        /// <param name="samples">The audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        public void Queue(float[] samples, bool loop)
        {
            audioQueue?.Enqueue((samples, loop));
            CheckQueue();
        }


        /// <summary>
        /// Stops the current audio playback.
        /// </summary>
        public void Stop()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Stop();
            audioQueue.Clear();
            isPlaying = false;
        }

        /// <summary>
        /// Destroys the speaker and releases its resources.
        /// </summary>

        public void Destroy()
        {
            speakerToy?.Destroy();
            audioQueue.Clear();
            isPlaying = false;
        }


        /// <summary>
        /// Pauses the current audio playback.
        /// </summary>
        public void Pause()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Pause();
        }

        /// <summary>
        /// Resumes the paused audio playback.
        /// </summary>
        public void Resume()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Resume();
        }

        /// <summary>
        /// Skips the audio playback.
        /// </summary>
        public void Skip(int count)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            transmitter?.Skip(count);
            while (count > 0 && audioQueue.Count > 0)
            {
                audioQueue.Dequeue();
                count--;
            }
            CheckQueue();
        }

        /// <summary>
        /// Fades in for the specified duration.
        /// </summary>
        /// <param name="duration">The duration of the effect in seconds.</param>
        public void FadeIn(float duration)
        {
            if (duration > 0)
            {
                Timing.RunCoroutine(FadeVolume(0f, targetVolume, duration));
            }
        }

        /// <summary>
        /// Fades out for specified duration.
        /// </summary>
        /// <param name="duration">The duration of the effect in seconds.</param>
        public void FadeOut(float duration, Action onComplete = null)
        {
            if (duration > 0)
            {
                Timing.RunCoroutine(FadeVolume(speakerToy.Volume, 0f, duration, stopOnComplete: true, onComplete));
            }
            else
            {
                Stop();
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Sets the filter function for valid players.
        /// See usage in the <see cref="AudioManager.PlayGlobalAudio">example</see>.
        /// <example>
        /// <code>
        /// audioManager.SetValidPlayers(p => Player.ReadyList.Contains(p));
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="playerFilter">A filter expression that determines which players are valid.</param>
        public void SetValidPlayers(Func<Player, bool> playerFilter)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.ValidPlayers = playerFilter;
            }
        }

        /// <summary>
        /// Sets the playback volume for the speaker.
        /// Use 0.0f to mute and 1.0f for maximum volume. Intermediate values apply proportionally.
        /// </summary>
        /// <param name="volume">
        /// A float value ranging from 0.0 to 1.0 representing the desired audio volume level.
        /// Values outside this range may be clamped internally.
        /// </param>
        public void SetVolume(float volume)
        {
            speakerToy.Volume = Mathf.Clamp01(volume);
            targetVolume = speakerToy.Volume;
        }


        /// <summary>
        /// Sets the minimum distance at which the audio starts to attenuate.
        /// Ensures the value is non-negative.
        /// </summary>
        /// <param name="minDistance">
        /// The minimum distance in meters. Values less than 0 will be clamped to 0.
        /// </param>
        public void SetMinDistance(float minDistance)
        {
            speakerToy.MinDistance = Mathf.Max(0, minDistance);
        }



        /// <summary>
        /// Sets the maximum distance beyond which the audio is no longer audible.
        /// Ensures the value is non-negative.
        /// </summary>
        /// <param name="maxDistance">
        /// The maximum distance in meters. Values less than 0 will be clamped to 0.
        /// </param>
        public void SetMaxDistance(float maxDistance)
        {
            speakerToy.MaxDistance = Mathf.Max(0, maxDistance);
        }

        /// <summary>
        /// Enables or disables spatial audio playback for the speaker.
        /// Spatialization allows audio to be perceived from its 3D position in the game world.
        /// </summary>
        /// <param name="isSpatial">
        /// If <c>true</c>, enables spatial audio; if <c>false</c>, plays audio in a non-spatial (2D) context.
        /// </param>
        public void SetSpatialization(bool isSpatial)
        {
            speakerToy.IsSpatial = isSpatial;
        }

        /// <summary>
        /// Evaluates the current playback queue and initiates playback of the next clip if available.
        /// </summary>
        /// <remarks>
        /// If no audio is currently playing and queued samples are present, the next item is dequeued and played.  
        /// If the queue is empty and playback is idle, triggers the <see cref="QueueEmpty"/> event to signal queue completion.
        /// </remarks>
        private void CheckQueue()
        {
            if (!isPlaying && audioQueue.Count > 0)
            {
                var (samples, loop) = audioQueue.Dequeue();
                Play(samples, loop);
            }
            else if (!isPlaying && audioQueue.Count == 0)
            {
                QueueEmpty?.Invoke();
            }
        }

        /// <summary>
        /// Smoothly interpolates the speaker volume from <paramref name="startVolume"/> to <paramref name="endVolume"/> over the specified duration.
        /// This coroutine uses <c>MEC</c>'s <c>IEnumerator&lt;float&gt;</c> to yield frame-by-frame for volume transitions.
        /// </summary>
        /// <param name="startVolume">Starting volume level (0.0 for mute, 1.0 for max).</param>
        /// <param name="endVolume">Target volume level.</param>
        /// <param name="duration">Duration of the fade effect in seconds.</param>
        /// <param name="stopOnComplete">
        /// Whether to stop audio playback after the fade completes.
        /// If <c>true</c>, the speaker will stop after reaching <paramref name="endVolume"/>.
        /// </param>
        /// <remarks>
        /// This method is designed to be run as a coroutine using <c>MEC.Timing.RunCoroutine</c>.
        /// Recommended for dynamic volume transitions such as fade-ins, fade-outs, or scripted events.
        /// Example usage:
        /// <code>
        /// Timing.RunCoroutine(FadeVolume(1.0f, 0.0f, 3.5f, true));
        /// </code>
        /// </remarks>
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