namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Features.Speakers;
    using UnityEngine;

    /// <summary>
    /// Default factory that produces <see cref="DefaultSpeakerToyAdapter"/> instances.
    /// </summary>
    public class DefaultSpeakerFactory : ISpeakerFactory
    {
        /// <summary>
        /// Creates a new speaker adapter for the specified position and controller ID.
        /// </summary>
        /// <param name="position">The 3D world position for audio playback.</param>
        /// <param name="controllerId">The unique controller ID allocated by AudioManager.</param>
        /// <returns>An <see cref="ISpeaker"/> instance with pause/resume support.</returns>
        public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
        {
            return new DefaultSpeakerToyAdapter(position, controllerId);
        }
    }
}