namespace AudioManagerAPI.Defaults
{
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Default factory that produces <see cref="DefaultSpeakerToyAdapter"/> instances.
    /// </summary>
    public class DefaultSpeakerFactory : ISpeakerFactory
    {
        private static readonly Dictionary<byte, ISpeaker> speakerRegistry = new Dictionary<byte, ISpeaker>();
        private static readonly object registryLock = new object();

        /// <summary>
        /// Creates a new speaker adapter for the specified position and controller ID.
        /// </summary>
        /// <param name="position">The 3D world position for audio playback.</param>
        /// <param name="controllerId">The unique controller ID allocated by AudioManager.</param>
        /// <returns>An <see cref="ISpeaker"/> instance with pause/resume support, or null if creation fails.</returns>
        public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
        {
            lock (registryLock)
            {


                // Check if a speaker already exists for the controllerId
                if (speakerRegistry.TryGetValue(controllerId, out ISpeaker existingSpeaker))
                {
                    Log.Debug($"CreateSpeaker: Speaker for controller ID {controllerId} already exists, returning existing instance.");
                    return existingSpeaker;
                }

                // Create a new SpeakerToy
                SpeakerToy speakerToy = SpeakerToy.Create(position, Quaternion.identity, Vector3.one, null, true);
                if (speakerToy == null)
                {
                    Log.Warn($"CreateSpeaker: Failed to create SpeakerToy for controller ID {controllerId}.");
                    return null;
                }

                // Set the controllerId
                speakerToy.ControllerId = controllerId;

                // Verify transmitter exists
                if (SpeakerToy.GetTransmitter(controllerId) == null)
                {
                    Log.Warn($"CreateSpeaker: No transmitter found for controller ID {controllerId}.");
                    speakerToy.Destroy();
                    return null;
                }

                // Create and register the DefaultSpeakerToyAdapter
                ISpeaker speaker = new DefaultSpeakerToyAdapter(speakerToy);
                speakerRegistry[controllerId] = speaker;
                Log.Debug($"CreateSpeaker: Created and registered new DefaultSpeakerToyAdapter for controller ID {controllerId} at position {position}.");
                return speaker;
            }
        }

        /// <summary>
        /// Gets an existing speaker by its controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The <see cref="ISpeaker"/> instance, or null if not found.</returns>
        public ISpeaker GetSpeaker(byte controllerId)
        {
            lock (registryLock)
            {
                // Check registry first
                if (speakerRegistry.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    Log.Debug($"GetSpeaker: Found registered speaker for controller ID {controllerId}.");
                    return speaker;
                }

                // Search for an existing SpeakerToy with the matching controllerId
                SpeakerToy speakerToy = SpeakerToy.List.FirstOrDefault(toy => toy.ControllerId == controllerId);
                if (speakerToy != null)
                {
                    speaker = new DefaultSpeakerToyAdapter(speakerToy);
                    speakerRegistry[controllerId] = speaker;
                    Log.Debug($"GetSpeaker: Found existing SpeakerToy and created DefaultSpeakerToyAdapter for controller ID {controllerId}.");
                    return speaker;
                }

                Log.Warn($"GetSpeaker: No speaker found for controller ID {controllerId}.");
                return null;
            }
        }

        /// <summary>
        /// Removes a speaker from the factory's management.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to remove.</param>
        /// <returns>True if the speaker was removed, false otherwise.</returns>
        public bool RemoveSpeaker(byte controllerId)
        {
            lock (registryLock)
            {
                if (speakerRegistry.TryGetValue(controllerId, out ISpeaker speaker))
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter)
                    {
                        adapter.Destroy();
                    }
                    speakerRegistry.Remove(controllerId);
                    Log.Debug($"RemoveSpeaker: Removed speaker for controller ID {controllerId} from registry.");
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Clears all managed speakers.
        /// </summary>
        public void ClearSpeakers()
        {
            lock (registryLock)
            {
                foreach (var speaker in speakerRegistry.Values)
                {
                    if (speaker is DefaultSpeakerToyAdapter adapter)
                    {
                        adapter.Destroy();
                    }
                }
                speakerRegistry.Clear();
                Log.Debug("ClearSpeakers: Cleared all speakers from registry.");
            }
        }
    }
}