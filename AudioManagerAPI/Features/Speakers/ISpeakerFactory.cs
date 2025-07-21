namespace AudioManagerAPI.Features.Speakers
{
    using UnityEngine;

    /// <summary>
    /// Defines a factory for creating speaker instances.
    /// </summary>
    public interface ISpeakerFactory
    {
        /// <summary>
        /// Creates a new speaker at the specified position with a unique controller ID.
        /// </summary>
        /// <param name="position">The 3D position where the speaker should be created.</param>
        /// <param name="controllerId">The unique identifier for the speaker.</param>
        /// <returns>A new <see cref="ISpeaker"/> instance, or <c>null</c> if creation fails.</returns>
        ISpeaker CreateSpeaker(Vector3 position, byte controllerId);
    }
}