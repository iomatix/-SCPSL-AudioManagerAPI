namespace AudioManagerAPI.Features.Enums
{
    /// <summary>
    /// Represents priority levels used to determine which audio sources
    /// should take precedence when multiple sounds compete for playback.
    /// </summary>
    /// <remarks>
    /// Higher priority audio may interrupt or override lower priority
    /// sounds depending on the audio manager's playback rules.
    ///
    /// Priority levels:
    /// <list type="bullet">
    /// <item>
    /// <description><see cref="Lowest"/> – Background or non-essential audio that should never interrupt other sounds.</description>
    /// </item>
    /// <item>
    /// <description><see cref="Low"/> – Ambient or minor sound effects with minimal importance.</description>
    /// </item>
    /// <item>
    /// <description><see cref="Medium"/> – Default priority used for standard gameplay or interaction sounds.</description>
    /// </item>
    /// <item>
    /// <description><see cref="High"/> – Important sounds that may override lower priority audio.</description>
    /// </item>
    /// <item>
    /// <description><see cref="Max"/> – Critical audio that should always take precedence over all other sounds.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public enum AudioPriority
    {
        /// <summary>
        /// The lowest possible priority.
        /// Intended for background or non-essential audio.
        /// </summary>
        Lowest = 0,

        /// <summary>
        /// Low priority audio.
        /// Suitable for ambient sounds or minor effects.
        /// </summary>
        Low = 1,

        /// <summary>
        /// Standard priority level.
        /// Used for typical gameplay or interaction sounds.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High priority audio.
        /// Can override or interrupt lower priority sounds.
        /// </summary>
        High = 3,

        /// <summary>
        /// Maximum priority level.
        /// Reserved for critical audio that should always take precedence.
        /// </summary>
        Max = 4,
    }
}