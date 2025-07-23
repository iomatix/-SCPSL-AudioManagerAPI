namespace AudioManagerAPI.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MEC;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Speakers.State;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Manages audio controller IDs with priority‐based allocation and eviction,
    /// plus optional persistence of speaker state across evictions.
    /// </summary>
    public static class ControllerIdManager
    {
        private static readonly byte[] availableIds = new byte[255];
        private static readonly Dictionary<byte, (AudioPriority priority, Action stopCallback, bool persistent)> controllerPriorities = new Dictionary<byte, (AudioPriority priority, Action stopCallback, bool persistent)>();
        private static readonly Dictionary<byte, SpeakerState> persistentSpeakerStates = new Dictionary<byte, SpeakerState>();
        private static readonly object lockObject = new object();


        static ControllerIdManager()
        {
            // IDs 1…254 are valid controller slots
            for (byte i = 1; i <= 254; i++)
                availableIds[i - 1] = i;
        }

        /// <summary>
        /// Allocates a free controller ID for playback, evicting a lower‐priority speaker if necessary.
        /// Optionally persists a state object for later retrieval if the speaker is evicted.
        /// </summary>
        /// <param name="priority">
        /// The relative importance of this audio (Low, Medium, High). Higher‐priority
        /// requests may evict lower‐priority speakers when IDs run out.
        /// </param>
        /// <param name="stopCallback">
        /// The action to invoke when this speaker is stopped or evicted to free up its ID.
        /// </param>
        /// <param name="persistent">
        /// Whether to remember the provided <paramref name="state"/> if this speaker is evicted.
        /// </param>
        /// <param name="state">
        /// An arbitrary object representing speaker‐specific state (e.g., position, settings).
        /// If <paramref name="persistent"/> is true, this will be stored and can be later
        /// retrieved via <see cref="GetSpeakerState(byte)"/>.
        /// </param>
        /// <param name="onSuccess">
        /// Callback invoked with the newly allocated controller ID upon success.
        /// </param>
        /// <param name="onFailure">
        /// Callback invoked if no suitable ID could be allocated or evicted.
        /// </param>
        /// <returns>
        /// The allocated controller ID (1–254), or 0 if allocation failed.
        /// </returns>
        public static byte AllocateId(AudioPriority priority, Action stopCallback, bool persistent, SpeakerState state, Action<byte> onSuccess, Action onFailure)
        {
            lock (lockObject)
            {
                byte controllerId = availableIds.FirstOrDefault(id => !controllerPriorities.ContainsKey(id));
                if (controllerId == 0)
                {
                    var evictable = controllerPriorities
                        .Where(p => !p.Value.persistent || p.Value.priority < priority)
                        .OrderBy(p => p.Value.priority)
                        .FirstOrDefault();
                    if (evictable.Key == 0)
                    {
                        onFailure?.Invoke();
                        return 0;
                    }

                    if (evictable.Value.persistent)
                    {
                        persistentSpeakerStates[evictable.Key] = state;
                    }
                    evictable.Value.stopCallback?.Invoke();
                    controllerPriorities.Remove(evictable.Key);
                    controllerId = evictable.Key;
                }

                controllerPriorities[controllerId] = (priority, stopCallback, persistent);
                if (persistent) persistentSpeakerStates[controllerId] = state;
                onSuccess?.Invoke(controllerId);
                Log.Debug($"[AudioManagerAPI] Allocated controller ID {controllerId} with priority {priority} (persistent: {persistent}) at {Timing.LocalTime}.");
                return controllerId;
            }
        }

        /// <summary>
        /// Updates the stop callback for an already‐allocated controller ID.
        /// </summary>
        /// <param name="controllerId">
        /// The ID whose callback should be replaced.
        /// </param>
        /// <param name="stopCallback">
        /// The new action to invoke when stopping or evicting this speaker.
        /// </param>
        public static void UpdateStopCallback(byte controllerId, Action stopCallback)
        {
            lock (lockObject)
            {
                if (controllerPriorities.TryGetValue(controllerId, out var entry))
                {
                    controllerPriorities[controllerId] = (entry.priority, stopCallback, entry.persistent);
                    Log.Debug($"[AudioManagerAPI] Updated stop callback for controller ID {controllerId} at {Timing.LocalTime}.");
                }
            }
        }

        /// <summary>
        /// Releases a controller ID, making it available for reuse.
        /// </summary>
        /// <param name="controllerId">
        /// The ID to release.
        /// </param>
        /// <param name="forceRemoveState">
        /// If <c>true</c>, permanently discards any persistent state tied to this ID.
        /// If <c>false</c>, moves it to the eviction cache for potential later restoration.
        /// </param>
        public static void ReleaseId(byte controllerId, bool forceRemoveState = false)
        {
            lock (lockObject)
            {
                if (controllerPriorities.Remove(controllerId))
                {
                    if (persistentSpeakerStates.ContainsKey(controllerId))
                    {
                        if (forceRemoveState)
                        {
                            persistentSpeakerStates.Remove(controllerId);
                            Log.Warn($"[AudioManagerAPI] Force-removed state for persistent speaker ID {controllerId}. This action is irreversible. Are you sure this is intended?");
                        }
                    }
                    Log.Debug($"[AudioManagerAPI] Released controller ID {controllerId} at {Timing.LocalTime}.");
                }
            }
        }

        /// <summary>
        /// Retrieves the state object associated with a speaker, whether it is currently
        /// active or has been evicted.
        /// </summary>
        /// <param name="controllerId">
        /// The controller ID whose state you want to retrieve.
        /// </param>
        /// <returns>
        /// The stored state object if found; otherwise, <c>null</c>.
        /// </returns>
        public static object GetSpeakerState(byte controllerId)
        {
            lock (lockObject)
            {
                return persistentSpeakerStates.TryGetValue(controllerId, out var state) ? state : null;
            }
        }

        /// <summary>
        /// Clears all cached states of evicted persistent speakers.
        /// </summary>
        /// <remarks>
        /// Use this to free memory if you no longer need to restore any evicted speaker states.
        /// </remarks>
        public static void CleanupEvictedSpeakers()
        {
            lock (lockObject)
            {
                persistentSpeakerStates.Clear();
                Log.Debug($"[AudioManagerAPI] Cleared all persistent speaker states at {Timing.LocalTime}.");
            }
        }
    }
}
