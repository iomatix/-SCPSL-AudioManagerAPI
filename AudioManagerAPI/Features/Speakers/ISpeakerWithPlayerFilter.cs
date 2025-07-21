using System;

namespace AudioManagerAPI.Features.Speakers
{
    /// <summary>
    /// Extends <see cref="ISpeaker"/> to support configuring player-specific audibility, volume, range, and spatialization.
    /// </summary>
    public interface ISpeakerWithPlayerFilter : ISpeaker
    {
        /// <summary>
        /// Configures which players can hear the audio.
        /// </summary>
        /// <param name="playerFilter">A function that determines which players can hear the audio.</param>
        void SetValidPlayers(Func<object, bool> playerFilter);

        /// <summary>
        /// Sets the volume level of the audio (0.0 to 1.0).
        /// </summary>
        /// <param name="volume">The volume level.</param>
        void SetVolume(float volume);

        /// <summary>
        /// Sets the minimum distance where the audio starts to fall off.
        /// </summary>
        /// <param name="minDistance">The minimum distance in Unity units.</param>
        void SetMinDistance(float minDistance);

        /// <summary>
        /// Sets the maximum distance where the audio falls to zero.
        /// </summary>
        /// <param name="maxDistance">The maximum distance in Unity units.</param>
        void SetMaxDistance(float maxDistance);

        /// <summary>
        /// Sets whether the audio is spatialized (3D).
        /// </summary>
        /// <param name="isSpatial">Whether to use spatial audio.</param>
        void SetSpatialization(bool isSpatial);
    }
}


