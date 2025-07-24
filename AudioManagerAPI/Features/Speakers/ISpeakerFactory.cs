namespace AudioManagerAPI.Features.Speakers
{
    using UnityEngine;

    /// <summary>
    /// Defines a factory for creating and managing speaker instances.
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

        /// <summary>
        /// Gets an existing speaker by its controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The <see cref="ISpeaker"/> instance, or null if not found.</returns>
        ISpeaker GetSpeaker(byte controllerId);

        /// <summary>
        /// Removes a speaker from the factory's management, if supported.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to remove.</param>
        /// <returns>True if the speaker was removed, false otherwise.</returns>
        bool RemoveSpeaker(byte controllerId);

        /// <summary>
        /// Clears all managed speakers, if supported (e.g., on round restart).
        /// </summary>
        void ClearSpeakers();
    }
}