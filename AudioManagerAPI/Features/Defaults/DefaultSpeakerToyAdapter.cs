namespace AudioManagerAPI.Defaults
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using MEC;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;

    /// <summary>
    /// Adapts LabAPI's <see cref="SpeakerToy"/> to the <see cref="ISpeakerWithPlayerFilter"/> interface,
    /// including pause, resume, skip, and fade-in/fade-out functionality.
    /// </summary>
    public class DefaultSpeakerToyAdapter : ISpeakerWithPlayerFilter
    {
        private readonly SpeakerToy speakerToy;
        private float targetVolume;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSpeakerToyAdapter"/> class.
        /// </summary>
        /// <param name="position">The 3D world position for audio playback.</param>
        /// <param name="controllerId">The unique controller ID allocated by AudioManager.</param>
        public DefaultSpeakerToyAdapter(Vector3 position, byte controllerId)
        {
            speakerToy = SpeakerToy.Create(position, networkSpawn: true);
            if (speakerToy != null)
            {
                speakerToy.ControllerId = controllerId;
                targetVolume = 1f;
            }
        }

        /// <summary>
        /// Plays the provided audio samples with the specified looping behavior.
        /// </summary>
        /// <param name="samples">The audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        public void Play(float[] samples, bool loop)
        {
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                transmitter?.Play(samples, queue: false, loop: loop);
                SetVolume(targetVolume); // Ensure volume is applied
            }
        }

        /// <summary>
        /// Plays the provided list of audio samples in order with the specified looping behavior.
        /// </summary>
        /// <param name="samples">The audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        public void Queue(float[] samples, bool loop)
        {
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                transmitter?.Play(samples, queue: true, loop: loop);
            }
        }

        /// <summary>
        /// Stops the current audio playback.
        /// </summary>
        public void Stop()
        {
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                transmitter?.Stop();
            }
        }

        /// <summary>
        /// Destroys the speaker and releases its resources.
        /// </summary>
        public void Destroy()
        {
            speakerToy?.Destroy();
        }


        /// <summary>
        /// Pauses the current audio playback.
        /// </summary>
        public void Pause()
        {
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                transmitter?.Pause();
            }
        }

        /// <summary>
        /// Resumes the paused audio playback.
        /// </summary>
        public void Resume()
        {
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                transmitter?.Resume();
            }
        }

        /// <summary>
        /// Skips the audio playback.
        /// </summary>
        public void Skip(int count)
        {
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                transmitter?.Skip(count);
            }
        }

        /// <summary>
        /// Fades in for the specified duration.
        /// </summary>
        /// <param name="duration">The duration of the effect in seconds.</param>
        public void FadeIn(float duration)
        {
            if (speakerToy != null && duration > 0)
            {
                Timing.RunCoroutine(FadeVolume(0f, targetVolume, duration));
            }
        }

        /// <summary>
        /// Fades out for specified duration.
        /// </summary>
        /// <param name="duration">The duration of the effect in seconds.</param>
        public void FadeOut(float duration)
        {
            if (speakerToy != null && duration > 0)
            {
                Timing.RunCoroutine(FadeVolume(speakerToy.Volume, 0f, duration, stopOnComplete: true));
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
            if (speakerToy != null)
            {
                var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
                if (transmitter != null)
                {
                    transmitter.ValidPlayers = playerFilter as Func<Player, bool>;
                }
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
            if (speakerToy != null)
            {
                speakerToy.Volume = Mathf.Clamp01(volume);
            }
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
            if (speakerToy != null)
            {
                speakerToy.MinDistance = Mathf.Max(0, minDistance);
            }
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
            if (speakerToy != null)
            {
                speakerToy.MaxDistance = Mathf.Max(0, maxDistance);
            }
        }

        /// <summary>
        /// Enables or disables spatial audio playback for the speaker.
        /// Spatialization allows audio to be perceived from its 3D position in the game world.
        /// </summary>
        /// <param name="isSpatial">
        /// If <c>true</c>, enables spatial audio; if <c>false</c>, plays audio in a non-spatial (2D) context.
        /// </param>
        public void SetSpatialization(bool isSpatial = true)
        {
            if (speakerToy != null)
            {
                speakerToy.IsSpatial = isSpatial;
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
        public IEnumerator<float> FadeVolume(float startVolume, float endVolume, float duration, bool stopOnComplete = false)
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
        }
    }
}