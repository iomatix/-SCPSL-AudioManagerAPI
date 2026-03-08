namespace AudioManagerAPI.Speakers.State
{
    using AudioManagerAPI.Features.Enums;
    using LabApi.Features.Wrappers;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Represents the abstract runtime configuration of an audio session,  
    /// used for routing, recovery, playback resumption, and lifecycle management.
    /// </summary>
    public class SpeakerState
    {
        /// <summary>
        /// The unique audio key used to identify and retrieve the associated PCM samples.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The 3D world-space position where the audio was originally placed.
        /// Used to reinstantiate spatial audio correctly upon recovery.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Indicates whether the audio should loop during playback.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// The playback volume for this session (range: 0.0 to 1.0).
        /// </summary>
        public float Volume { get; set; }

        /// <summary>
        /// The minimum audible distance from the audio source before volume begins to fall off.
        /// </summary>
        public float MinDistance { get; set; }

        /// <summary>
        /// The maximum distance beyond which the audio is no longer heard.
        /// </summary>
        public float MaxDistance { get; set; }

        /// <summary>
        /// Whether this session uses 3D spatial audio positioning.
        /// </summary>
        public bool IsSpatial { get; set; }

        /// <summary>
        /// Defines the priority level of the session when allocating physical playback resources.
        /// </summary>
        public AudioPriority Priority { get; set; }

        /// <summary>
        /// An optional filter function to determine which players are valid listeners for playback.
        /// If provided, audio will only be transmitted to players that satisfy the condition.
        /// </summary>
        public Func<Player, bool> PlayerFilter { get; set; }

        /// <summary>
        /// A list of audio clips that are pending playback, represented as tuples:
        /// (<c>key</c>, <c>loop</c>).
        /// </summary>
        public List<(string key, bool loop)> QueuedClips { get; set; } = new List<(string key, bool loop)>();

        /// <summary>
        /// Whether this session is flagged for persistence across physical evictions.
        /// </summary>
        public bool Persistent { get; set; }

        /// <summary>
        /// Optional time (in seconds) to live for this session.  
        /// If set and <see cref="AutoCleanup"/> is true, the audio auto-fades and stops after this lifespan.
        /// </summary>
        public float? Lifespan { get; set; }

        /// <summary>
        /// Indicates whether the session should automatically stop and fade out after its lifespan ends.
        /// </summary>
        public bool AutoCleanup { get; set; }

        /// <summary>
        /// The playback position (in seconds or samples, depending on implementation) where audio should resume.
        /// </summary>
        public float PlaybackPosition { get; set; }

        /// <summary>
        /// Indicates whether the session was explicitly paused.
        /// Required to restore the correct state if a physical speaker is evicted and later re-allocated.
        /// </summary>
        public bool IsPaused { get; set; }
    }
}