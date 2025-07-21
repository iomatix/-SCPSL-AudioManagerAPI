using System;

namespace AudioManagerAPI.Features.Speakers
{
    /// <summary>
    /// Extends <see cref="ISpeaker"/> to support configuring which players can hear the audio.
    /// </summary>
    public interface ISpeakerWithPlayerFilter : ISpeaker
    {
        /// <summary>
        /// Configures which players can hear the audio.
        /// </summary>
        /// <param name="playerFilter">A function that determines which players can hear the audio.</param>
        void SetValidPlayers(Func<object, bool> playerFilter);
    }
}


