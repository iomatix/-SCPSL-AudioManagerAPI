namespace AudioManagerAPI.Features.Static
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Speakers;

    /// <summary>
    /// Provides static access to a centralized <see cref="ISpeakerFactory"/> implementation.
    /// In V2.0.0, direct manipulation of physical speakers by external plugins is prohibited 
    /// to maintain session synchronization. This class solely provides the singleton instance.
    /// </summary>
    public static class StaticSpeakerFactory
    {
        /// <summary>
        /// The underlying singleton <see cref="DefaultSpeakerFactory"/> instance.
        /// </summary>
        private static readonly DefaultSpeakerFactory factory = new DefaultSpeakerFactory();

        /// <summary>
        /// Gets the shared <see cref="ISpeakerFactory"/> instance used by the API.
        /// Exposed strictly for initializing the router (<see cref="AudioManagerAPI.Features.Management.AudioManager"/>) 
        /// or dependency injection. Plugins must never cast or use this to manually create/destroy physical speakers.
        /// </summary>
        public static ISpeakerFactory Instance => factory;
    }
}