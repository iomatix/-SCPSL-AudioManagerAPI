namespace AudioManagerAPI.Features.Management
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AudioManagerAPI.Features.Enums;
    using MEC;

    /// <summary>
    /// Manages a shared pool of controller IDs for audio speakers across all plugins to prevent ID conflicts.
    /// </summary>
    public static class ControllerIdManager
    {
        
        private static readonly HashSet<byte> availableIds = new HashSet<byte>();
        private static readonly Dictionary<byte, AudioPriority> idPriorities = new Dictionary<byte, AudioPriority>();
        private static readonly Dictionary<byte, Action> stopCallbacks = new Dictionary<byte, Action>();
        private static readonly List<(AudioPriority priority, Action<byte> onSuccess, Action onFailure)> queuedRequests = new List<(AudioPriority priority, Action<byte> onSuccess, Action onFailure)>();
        private static readonly object lockObject = new object();

        /// <summary>
        /// Initializes the controller ID manager with a default range of IDs.
        /// </summary>
        static ControllerIdManager()
        {
            for (byte i = 1; i <= 255; i++)
            {
                availableIds.Add(i);
            }
        }

        /// <summary>
        /// Allocates a unique controller ID from the shared pool, evicting a lower-priority speaker or queuing the request if necessary.
        /// </summary>
        /// <param name="priority">The priority of the requesting audio.</param>
        /// <param name="stopCallback">Callback to stop the speaker if it is evicted later.</param>
        /// <param name="onSuccess">Callback to invoke with the allocated ID.</param>
        /// <param name="onFailure">Callback to invoke if allocation fails.</param>
        /// <returns>The allocated controller ID, or 0 if allocation fails.</returns>
        public static byte AllocateId(AudioPriority priority, Action stopCallback, Action<byte> onSuccess, Action onFailure)
        {
            lock (lockObject)
            {
                if (availableIds.Count > 0)
                {
                    byte id = availableIds.First();
                    availableIds.Remove(id);
                    idPriorities[id] = priority;
                    stopCallbacks[id] = stopCallback;
                    onSuccess(id);
                    return id;
                }

                // Try to evict a lower-priority speaker
                var lowerPriorityId = idPriorities
                    .Where(kvp => kvp.Value < priority)
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .FirstOrDefault();

                if (lowerPriorityId != 0)
                {
                    stopCallbacks[lowerPriorityId]?.Invoke();
                    idPriorities.Remove(lowerPriorityId);
                    stopCallbacks.Remove(lowerPriorityId);
                    idPriorities[lowerPriorityId] = priority;
                    stopCallbacks[lowerPriorityId] = stopCallback;
                    onSuccess(lowerPriorityId);
                    return lowerPriorityId;
                }

                // Queue high-priority requests
                if (priority == AudioPriority.High)
                {
                    queuedRequests.Add((priority, onSuccess, onFailure));
                    Timing.CallDelayed(0.5f, TryProcessQueue);
                    return 0;
                }

                onFailure();
                return 0;
            }
        }

        /// <summary>
        /// Releases a controller ID back to the shared pool and processes queued requests.
        /// </summary>
        /// <param name="id">The controller ID to release.</param>
        public static void ReleaseId(byte id)
        {
            lock (lockObject)
            {
                availableIds.Add(id);
                idPriorities.Remove(id);
                stopCallbacks.Remove(id);
                TryProcessQueue();
            }
        }

        private static void TryProcessQueue()
        {
            lock (lockObject)
            {
                if (queuedRequests.Count == 0 || availableIds.Count == 0)
                    return;

                var request = queuedRequests[0];
                queuedRequests.RemoveAt(0);
                byte id = availableIds.First();
                availableIds.Remove(id);
                idPriorities[id] = request.priority;
                stopCallbacks[id] = () => { }; // Placeholder, updated by PlayAudio
                request.onSuccess(id);
            }
        }
    }
}
