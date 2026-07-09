namespace AudioManagerAPI.Features.Speakers
{
    using LabApi.Features.Wrappers;
    using System;

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
        /// Binds a legacy allocation-heavy player predicate loop to the hardware speaker context.
        /// </summary>
        /// <param name="filter">The evaluation predicate executed per active client track.</param>
        void SetValidPlayers(Func<Player, bool> filter);

        /// <summary>
        /// Binds an allocation-free generic state-passing filter context directly to the speaker evaluation loop execution matrix.
        /// </summary>
        /// <param name="filter">The compiled non-generic bridge delegate accepting the untyped state reference for runtime client evaluation.</param>
        /// <param name="state">The untyped structural state context reference object stored for runtime hot-path execution passes.</param>
        void SetValidPlayers(Func<Player, object, bool> filter, object state);
    }
}