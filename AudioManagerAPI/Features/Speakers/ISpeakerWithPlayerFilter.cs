namespace AudioManagerAPI.Features.Speakers
{
    using System;
    using LabApi.Features.Wrappers;

    /// <summary>
    /// Extends <see cref="ISpeaker"/> to support LabAPI's player-specific audio filtering capabilities.
    /// </summary>
    public interface ISpeakerWithPlayerFilter : ISpeaker
    {
        /// <summary>
        /// Gets or sets the player filter determining which players can hear the audio.
        /// </summary>
        Func<Player, bool> ValidPlayers { get; set; }

        /// <summary>
        /// Sets the predicate to determine which players can hear the audio.
        /// </summary>
        void SetValidPlayers(Func<Player, bool> playerFilter);
    }
}