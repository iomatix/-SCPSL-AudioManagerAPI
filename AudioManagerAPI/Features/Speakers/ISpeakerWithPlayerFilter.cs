namespace AudioManagerAPI.Features.Speakers
{
    using System;
    using LabApi.Features.Wrappers;

    /// <summary>
    /// Extends <see cref="ISpeaker"/> to support player-specific audio filtering and configuration.
    /// </summary>
    public interface ISpeakerWithPlayerFilter : ISpeaker
    {
        /// <summary>
        /// Sets the predicate to determine which players can hear the audio.
        /// </summary>
        /// <param name="playerFilter">A function that returns true for <see cref="Player"/> objects who should hear the audio.</param>
        void SetValidPlayers(Func<Player, bool> playerFilter);

        /// <summary>
        /// Sets the volume of the audio (0.0 to 1.0).
        /// </summary>
        /// <param name="volume">The volume level.</param>
        void SetVolume(float volume);

        /// <summary>
        /// Sets the minimum distance for audio falloff.
        /// </summary>
        /// <param name="minDistance">The minimum distance in Unity units.</param>
        void SetMinDistance(float minDistance);

        /// <summary>
        /// Sets the maximum distance for audio falloff.
        /// </summary>
        /// <param name="maxDistance">The maximum distance in Unity units.</param>
        void SetMaxDistance(float maxDistance);

        /// <summary>
        /// Sets whether the audio is spatialized (3D audio).
        /// </summary>
        /// <param name="isSpatial">True for spatial audio, false for non-spatial.</param>
        void SetSpatialization(bool isSpatial);
    }
}


