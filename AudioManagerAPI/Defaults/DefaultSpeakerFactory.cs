namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    using Log = DebugLogger;

    /// <summary>
    /// Default factory that produces <see cref="DefaultSpeakerToyAdapter"/> instances.
    /// </summary>
    public class DefaultSpeakerFactory : ISpeakerFactory
    {
        private static readonly ConcurrentDictionary<byte, ISpeaker> speakerRegistry = new ConcurrentDictionary<byte, ISpeaker>();
        private static readonly object registryLock = new object();

        /// <summary>
        /// Creates a new speaker adapter for the specified position and controller ID.
        /// </summary>
        /// <param name="position">The 3D world position for audio playback.</param>
        /// <param name="controllerId">The unique controller ID allocated by AudioManager.</param>
        /// <returns>An <see cref="ISpeaker"/> instance with pause/resume support, or null if creation fails.</returns>
        public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
        {
            // Try to get existing speaker or create a new one
            ISpeaker speaker = speakerRegistry.GetOrAdd(controllerId, _ =>
            {
                SpeakerToy speakerToy = SpeakerToy.Create(position, Quaternion.identity, Vector3.one, null, true);
                if (speakerToy == null)
                {
                    Log.Warn($"CreateSpeaker: Failed to create SpeakerToy for controller ID {controllerId}.");
                    return null;
                }

                speakerToy.ControllerId = controllerId;
                if (SpeakerToy.GetTransmitter(controllerId) == null)
                {
                    Log.Warn($"CreateSpeaker: No transmitter found for controller ID {controllerId}.");
                    speakerToy.Destroy();
                    return null;
                }

                ISpeaker newSpeaker = new DefaultSpeakerToyAdapter(speakerToy);
                Log.Debug($"CreateSpeaker: Created and registered new DefaultSpeakerToyAdapter for controller ID {controllerId} at position {position}.");
                return newSpeaker;
            });

            if (speaker != null && speaker is DefaultSpeakerToyAdapter adapter)
            {
                // Update position if speaker already exists
                adapter.SetPosition(position);
            }

            return speaker;
        }

        /// <summary>
        /// Gets an existing speaker by its controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The <see cref="ISpeaker"/> instance, or null if not found.</returns>
        public ISpeaker GetSpeaker(byte controllerId)
        {
            if (speakerRegistry.TryGetValue(controllerId, out ISpeaker speaker))
            {
                Log.Debug($"GetSpeaker: Found registered speaker for controller ID {controllerId}.");
                return speaker;
            }

            SpeakerToy speakerToy = SpeakerToy.List.FirstOrDefault(toy => toy.ControllerId == controllerId);
            if (speakerToy != null)
            {
                speaker = new DefaultSpeakerToyAdapter(speakerToy);
                speakerRegistry.TryAdd(controllerId, speaker);
                Log.Debug($"GetSpeaker: Found existing SpeakerToy and created DefaultSpeakerToyAdapter for controller ID {controllerId}.");
                return speaker;
            }

            Log.Warn($"GetSpeaker: No speaker found for controller ID {controllerId}.");
            return null;
        }

        /// <summary>
        /// Removes a speaker from the factory's management.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to remove.</param>
        /// <returns>True if the speaker was removed, false otherwise.</returns>
        public bool RemoveSpeaker(byte controllerId)
        {
            if (speakerRegistry.TryRemove(controllerId, out ISpeaker speaker))
            {
                if (speaker is DefaultSpeakerToyAdapter adapter)
                {
                    adapter.Destroy();
                }
                Log.Debug($"RemoveSpeaker: Removed speaker for controller ID {controllerId} from registry.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all managed speakers.
        /// </summary>
        public void ClearSpeakers()
        {
            foreach (var kvp in speakerRegistry)
            {
                if (kvp.Value is DefaultSpeakerToyAdapter adapter)
                {
                    adapter.Destroy();
                }
            }
            speakerRegistry.Clear();
            Log.Debug("ClearSpeakers: Cleared all speakers from registry.");
        }
    }
}