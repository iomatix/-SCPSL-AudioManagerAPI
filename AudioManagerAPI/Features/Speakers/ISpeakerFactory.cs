namespace AudioManagerAPI.Features.Speakers
{
    using UnityEngine;

    /// <summary>
    /// Defines a hardware-level factory for creating and managing physical speaker instances based on controller IDs.
    /// </summary>
    public interface ISpeakerFactory
    {
        /// <summary>
        /// Creates a physical speaker at the specified position with a unique controller ID (1-254).
        /// </summary>
        ISpeaker CreateSpeaker(Vector3 position, byte controllerId);

        /// <summary>
        /// Gets an existing physical speaker by its controller ID.
        /// </summary>
        ISpeaker GetSpeaker(byte controllerId);

        /// <summary>
        /// Removes a speaker from the factory's management and destroys it.
        /// </summary>
        bool RemoveSpeaker(byte controllerId);

        /// <summary>
        /// Clears all managed speakers and frees hardware controllers.
        /// </summary>
        void ClearSpeakers();
    }
}