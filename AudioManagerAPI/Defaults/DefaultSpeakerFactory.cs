namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using System.Collections.Concurrent;
    using UnityEngine;

    using Log = DebugLogger;

    /// <summary>
    /// Default factory that produces and manages <see cref="DefaultSpeakerToyAdapter"/> instances.
    /// Operates strictly at the physical hardware layer of LabAPI using byte-based controller IDs.
    /// </summary>
    public class DefaultSpeakerFactory : ISpeakerFactory
    {
        private static readonly ConcurrentDictionary<byte, ISpeaker> speakerRegistry = new ConcurrentDictionary<byte, ISpeaker>();

        /// <summary>
        /// Creates a new physical speaker adapter for the specified position and hardware controller ID.
        /// If a speaker already exists for this ID, its position is updated instead.
        /// </summary>
        /// <param name="position">The 3D world position for physical audio playback.</param>
        /// <param name="controllerId">The unique hardware controller ID allocated by the system (1-254).</param>
        /// <returns>An <see cref="ISpeaker"/> instance, or null if LabAPI fails to create the underlying object.</returns>
        public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
        {
            if (speakerRegistry.TryGetValue(controllerId, out ISpeaker existingSpeaker))
            {
                if (existingSpeaker is DefaultSpeakerToyAdapter adapter)
                {
                    adapter.SetPosition(position);
                }
                return existingSpeaker;
            }

            SpeakerToy speakerToy = SpeakerToy.Create(position, Quaternion.identity, Vector3.one, null, true);
            if (speakerToy == null)
            {
                Log.Warn($"[DefaultSpeakerFactory] Failed to create SpeakerToy for hardware controller ID {controllerId}.");
                return null;
            }

            speakerToy.ControllerId = controllerId;
            if (SpeakerToy.GetTransmitter(controllerId) == null)
            {
                Log.Warn($"[DefaultSpeakerFactory] No transmitter found for hardware controller ID {controllerId} after initialization.");
                speakerToy.Destroy();
                return null;
            }

            ISpeaker newSpeaker = new DefaultSpeakerToyAdapter(speakerToy);

            // Only add to registry if creation was fully successful to prevent null poisoning
            if (speakerRegistry.TryAdd(controllerId, newSpeaker))
            {
                Log.Debug($"[DefaultSpeakerFactory] Created and registered new DefaultSpeakerToyAdapter for controller ID {controllerId} at position {position}.");
                return newSpeaker;
            }

            // Fallback in case another thread added it simultaneously
            speakerToy.Destroy();
            return speakerRegistry[controllerId];
        }

        /// <summary>
        /// Retrieves an existing physical speaker by its hardware controller ID.
        /// Attempts to find unregistered instances in the game world if missing from the internal registry.
        /// </summary>
        /// <param name="controllerId">The hardware controller ID of the speaker.</param>
        /// <returns>The <see cref="ISpeaker"/> instance, or null if not found in the registry or the game world.</returns>
        public ISpeaker GetSpeaker(byte controllerId)
        {
            if (speakerRegistry.TryGetValue(controllerId, out ISpeaker speaker))
            {
                Log.Debug($"[DefaultSpeakerFactory] Found registered speaker for controller ID {controllerId}.");
                return speaker;
            }

            // Zero-allocation search replacing LINQ FirstOrDefault
            SpeakerToy foundToy = null;
            foreach (var toy in SpeakerToy.List)
            {
                if (toy.ControllerId == controllerId)
                {
                    foundToy = toy;
                    break;
                }
            }

            if (foundToy != null)
            {
                speaker = new DefaultSpeakerToyAdapter(foundToy);
                if (speakerRegistry.TryAdd(controllerId, speaker))
                {
                    Log.Debug($"[DefaultSpeakerFactory] Found existing SpeakerToy in world and registered adapter for controller ID {controllerId}.");
                    return speaker;
                }
                return speakerRegistry[controllerId];
            }

            Log.Warn($"[DefaultSpeakerFactory] No physical speaker found for controller ID {controllerId}.");
            return null;
        }

        /// <summary>
        /// Removes a speaker from the factory's management and explicitly destroys its physical representation.
        /// </summary>
        /// <param name="controllerId">The hardware controller ID of the speaker to remove.</param>
        /// <returns>True if the speaker was successfully removed and destroyed, false otherwise.</returns>
        public bool RemoveSpeaker(byte controllerId)
        {
            if (speakerRegistry.TryRemove(controllerId, out ISpeaker speaker))
            {
                if (speaker is DefaultSpeakerToyAdapter adapter)
                {
                    adapter.Destroy();
                }
                Log.Debug($"[DefaultSpeakerFactory] Removed and destroyed speaker for controller ID {controllerId}.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all managed speakers and destroys their physical representations in the game world.
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
            Log.Debug("[DefaultSpeakerFactory] Cleared and destroyed all registered speakers.");
        }
    }
}