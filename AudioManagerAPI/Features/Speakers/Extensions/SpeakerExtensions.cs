namespace AudioManagerAPI.Speakers.Extensions
{
    using System;
    using System.Collections.Generic;
    using MEC;
    using AudioManagerAPI.Features.Speakers;
    /// <summary>
    /// Provides extension methods for configuring and managing the lifecycle of <see cref="ISpeaker"/> instances.
    /// </summary>
    public static class SpeakerExtensions
    {
        /// <summary>
        /// Configures the specified speaker with volume, distance, spatialization, and optional custom settings.
        /// </summary>
        /// <param name="speaker">The speaker instance to configure.</param>
        /// <param name="volume">The desired playback volume (0.0 to 1.0).</param>
        /// <param name="minDistance">Minimum distance at which the audio begins to fall off.</param>
        /// <param name="maxDistance">Maximum distance beyond which the audio is no longer heard.</param>
        /// <param name="isSpatial">Determines whether 3D spatialization should be applied.</param>
        /// <param name="configureSpeaker">
        /// Optional delegate for applying additional speaker-specific configuration beyond standard audio parameters.
        /// </param>
        public static void Configure(this ISpeaker speaker, float volume, float minDistance, float maxDistance, bool isSpatial, Action<ISpeaker> configureSpeaker = null)
        {
            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.SetVolume(volume);
                filterSpeaker.SetMinDistance(minDistance);
                filterSpeaker.SetMaxDistance(maxDistance);
                filterSpeaker.SetSpatialization(isSpatial);
            }
            configureSpeaker?.Invoke(speaker);
        }

        /// <summary>
        /// Initiates a coroutine that automatically stops and fades out the speaker after a set lifespan, if enabled.
        /// </summary>
        /// <param name="speaker">The speaker instance to manage.</param>
        /// <param name="controllerId">The controller ID associated with the speaker.</param>
        /// <param name="lifespan">How long the speaker should remain active before being auto-stopped.</param>
        /// <param name="autoCleanup">Indicates whether auto-fade logic should be performed after lifespan expires.</param>
        /// <param name="fadeOutAction">Delegate to invoke the fade-out logic using the controller ID.</param>
        public static void StartAutoStop(this ISpeaker speaker, byte controllerId, float lifespan, bool autoCleanup, Action<byte> fadeOutAction)
        {
            if (autoCleanup && lifespan > 0)
            {
                Timing.RunCoroutine(AutoStopCoroutine(controllerId, lifespan, fadeOutAction));
            }
        }

        /// <summary>
        /// Internal coroutine responsible for timing speaker lifespan and executing fade-out logic on completion.
        /// </summary>
        /// <param name="controllerId">The controller ID to apply fade-out to.</param>
        /// <param name="lifespan">Delay duration before fade-out is triggered.</param>
        /// <param name="fadeOutAction">Action that performs fade-out for the target speaker.</param>
        /// <returns>A coroutine yield instruction for MEC scheduling.</returns>
        private static IEnumerator<float> AutoStopCoroutine(byte controllerId, float lifespan, Action<byte> fadeOutAction)
        {
            yield return Timing.WaitForSeconds(lifespan);
            fadeOutAction?.Invoke(controllerId);
        }
    }
}