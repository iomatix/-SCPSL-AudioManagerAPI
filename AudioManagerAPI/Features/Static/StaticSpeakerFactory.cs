namespace AudioManagerAPI.Features.Static
{
    using AudioManagerAPI.Controllers;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using UnityEngine;

    /// <summary>
    /// Provides static access to a centralized <see cref="ISpeakerFactory"/> implementation 
    /// for managing spatial audio speakers in SCP: Secret Laboratory via the AudioManagerAPI.
    /// </summary>
    /// <remarks>
    /// This factory allows server-side plugins to create, retrieve, remove, and clear dynamically 
    /// placed speakers in the game world. It wraps a shared, thread-safe <see cref="DefaultSpeakerFactory"/> 
    /// instance, ensuring consistency with the global <see cref="ControllerIdManager"/> for unique 
    /// controller IDs across plugins.
    /// </remarks>
    public static class StaticSpeakerFactory
    {
        /// <summary>
        /// The underlying singleton <see cref="DefaultSpeakerFactory"/> instance responsible for 
        /// thread-safe speaker management, maintaining a shared registry of speakers aligned with 
        /// the global <see cref="ControllerIdManager"/>.
        /// </summary>
        private static readonly DefaultSpeakerFactory factory = new DefaultSpeakerFactory();

        /// <summary>
        /// Creates and registers a new speaker at the specified world position, owned by the given controller ID.
        /// </summary>
        /// <param name="position">The world position where the speaker should be placed (e.g., a room or door location).</param>
        /// <param name="controllerId">
        /// A unique byte identifier (1-255) representing the owner or controlling plugin of the speaker, 
        /// enforced by <see cref="ControllerIdManager"/>. Used for retrieval or cleanup.
        /// </param>
        /// <returns>
        /// An <see cref="ISpeaker"/> instance (a <see cref="DefaultSpeakerToyAdapter"/> wrapping a LabAPI <see cref="SpeakerToy"/>) 
        /// for playing audio clips or broadcasting messages, or <c>null</c> if creation fails.
        /// </returns>
        public static ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
        {
            return factory.CreateSpeaker(position, controllerId);
        }

        /// <summary>
        /// Retrieves an existing speaker by its controller ID from the shared registry or LabAPI’s <see cref="SpeakerToy.List"/>.
        /// </summary>
        /// <param name="controllerId">The unique byte ID (1-255) used when the speaker was created.</param>
        /// <returns>
        /// The corresponding <see cref="ISpeaker"/> (a <see cref="DefaultSpeakerToyAdapter"/>) if found; otherwise, <c>null</c>.
        /// </returns>
        public static ISpeaker GetSpeaker(byte controllerId)
        {
            return factory.GetSpeaker(controllerId);
        }

        /// <summary>
        /// Removes the speaker associated with the specified controller ID from the shared registry and destroys its <see cref="SpeakerToy"/>.
        /// </summary>
        /// <param name="controllerId">The unique byte ID (1-255) of the speaker to remove.</param>
        /// <returns>
        /// <c>true</c> if the speaker was successfully removed; otherwise, <c>false</c>.
        /// </returns>
        public static bool RemoveSpeaker(byte controllerId)
        {
            return factory.RemoveSpeaker(controllerId);
        }

        /// <summary>
        /// Clears all registered speakers, destroying their <see cref="SpeakerToy"/> instances and emptying the shared registry.
        /// </summary>
        /// <remarks>
        /// Use this method during SCP:SL round end events (e.g., <c>OnRoundEnded</c>) or plugin unload to prevent memory leaks.
        /// </remarks>
        public static void ClearSpeakers()
        {
            factory.ClearSpeakers();
        }

        /// <summary>
        /// Gets the underlying <see cref="ISpeakerFactory"/> instance used by the static wrapper.
        /// </summary>
        /// <remarks>
        /// Exposed for initializing <see cref="AudioManager"/> or for advanced use cases requiring 
        /// direct access to the thread-safe <see cref="DefaultSpeakerFactory"/> instance.
        /// </remarks>
        public static ISpeakerFactory Instance => factory;
    }
}