namespace AudioManagerAPI.Speakers.State
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Speakers;
    using System;
    using UnityEngine;

    /// <summary>
    /// Represents the saved or runtime configuration of a speaker,  
    /// used for recovery, playback resumption, and lifecycle management across scenes and sessions.
    /// </summary>
    public class SpeakerState
    {
        /// <summary>
        /// The unique audio key used to identify and retrieve the associated PCM samples.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The 3D world-space position where the speaker was originally placed.
        /// Used to reinstantiate spatial audio correctly upon recovery.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Indicates whether the audio should loop during playback.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// The playback volume for this speaker (range: 0.0 to 1.0).
        /// </summary>
        public float Volume { get; set; }

        /// <summary>
        /// The minimum audible distance from the speaker before volume begins to fall off.
        /// </summary>
        public float MinDistance { get; set; }

        /// <summary>
        /// The maximum distance beyond which the audio is no longer heard.
        /// </summary>
        public float MaxDistance { get; set; }

        /// <summary>
        /// Whether this speaker uses 3D spatial audio positioning.
        /// </summary>
        public bool IsSpatial { get; set; }

        /// <summary>
        /// Defines the priority level of the speaker when allocating playback resources or managing fade conflicts.
        /// </summary>
        public AudioPriority Priority { get; set; }

        /// <summary>
        /// Optional delegate used to apply runtime configuration logic to the speaker on instantiation or recovery.
        /// </summary>
        public Action<ISpeaker> ConfigureSpeaker { get; set; }

        /// <summary>
        /// Whether the audio clip should be queued behind the current playback  
        /// or interrupt and replace the currently playing clip.
        /// </summary>
        public bool Queue { get; set; }

        /// <summary>
        /// Whether this speaker state is flagged for persistence across unloads or recoverable via <c>RecoverSpeaker()</c>.
        /// </summary>
        public bool Persistent { get; set; }

        /// <summary>
        /// Optional time (in seconds) to live for this speaker.  
        /// If set and <see cref="AutoCleanup"/> is true, the speaker auto-fades and stops after this lifespan.
        /// </summary>
        public float? Lifespan { get; set; }

        /// <summary>
        /// Indicates whether the speaker should automatically stop and fade out after its lifespan ends.
        /// </summary>
        public bool AutoCleanup { get; set; }

        /// <summary>
        /// The playback position (in seconds or samples, depending on implementation) where audio should resume.
        /// </summary>
        public float PlaybackPosition { get; set; }
    }
}