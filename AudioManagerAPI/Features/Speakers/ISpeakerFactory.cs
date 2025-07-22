namespace AudioManagerAPI.Features.Speakers
{
    using UnityEngine;

    /// <summary>
    /// Defines a factory for creating speaker instances.
    /// </summary>
    public interface ISpeakerFactory
    {
        /// <summary>
        /// Creates a speaker at the specified position with a unique controller ID.
        /// </summary>
        /// <param name="position">The 3D world position for audio playback.</param>
        /// <param name="controllerId">The unique controller ID.</param>
        /// <returns>An <see cref="ISpeaker"/> instance, or null if creation fails.</returns>
        ISpeaker CreateSpeaker(Vector3 position, byte controllerId);
    }
}