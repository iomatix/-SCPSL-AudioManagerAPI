namespace AudioManagerAPI.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MEC;
    using AudioManagerAPI.Features.Enums;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Manages audio controller IDs with priority-based allocation and eviction.
    /// </summary>
    public static class ControllerIdManager
    {
        private static readonly byte[] availableIds = new byte[255];
        private static readonly Dictionary<byte, (AudioPriority priority, Action stopCallback)> controllerPriorities = new Dictionary<byte, (AudioPriority, Action)>();
        private static readonly object lockObject = new object();

        static ControllerIdManager()
        {
            for (byte i = 1; i <= 254; i++)
            {
                availableIds[i - 1] = i;
            }
        }

        /// <summary>
        /// Allocates a controller ID for audio playback based on priority.
        /// </summary>
        /// <param name="priority">The priority of the audio.</param>
        /// <param name="stopCallback">The callback to execute when the audio is stopped or evicted.</param>
        /// <param name="onSuccess">The callback to execute when allocation succeeds, passing the allocated ID.</param>
        /// <param name="onFailure">The callback to execute when allocation fails.</param>
        /// <returns>The allocated controller ID, or 0 if allocation fails.</returns>
        public static byte AllocateId(AudioPriority priority, Action stopCallback, Action<byte> onSuccess, Action onFailure)
        {
            lock (lockObject)
            {
                byte controllerId = availableIds.FirstOrDefault(id => !controllerPriorities.ContainsKey(id));
                if (controllerId == 0)
                {
                    var lowestPriority = controllerPriorities.OrderBy(p => p.Value.priority).FirstOrDefault();
                    if (lowestPriority.Key == 0 || lowestPriority.Value.priority >= priority)
                    {
                        onFailure?.Invoke();
                        return 0;
                    }

                    lowestPriority.Value.stopCallback?.Invoke();
                    controllerPriorities.Remove(lowestPriority.Key);
                    controllerId = lowestPriority.Key;
                }

                controllerPriorities[controllerId] = (priority, stopCallback);
                onSuccess?.Invoke(controllerId);
                Log.Debug($"[AudioManagerAPI] Allocated controller ID {controllerId} with priority {priority} at {Timing.LocalTime}.");
                return controllerId;
            }
        }

        /// <summary>
        /// Updates the stop callback for an existing controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID to update.</param>
        /// <param name="stopCallback">The new stop callback.</param>
        public static void UpdateStopCallback(byte controllerId, Action stopCallback)
        {
            lock (lockObject)
            {
                if (controllerPriorities.ContainsKey(controllerId))
                {
                    var (priority, _) = controllerPriorities[controllerId];
                    controllerPriorities[controllerId] = (priority, stopCallback);
                    Log.Debug($"[AudioManagerAPI] Updated stop callback for controller ID {controllerId} at {Timing.LocalTime}.");
                }
            }
        }

        /// <summary>
        /// Releases a controller ID, making it available for reuse.
        /// </summary>
        /// <param name="controllerId">The controller ID to release.</param>
        public static void ReleaseId(byte controllerId)
        {
            lock (lockObject)
            {
                if (controllerPriorities.Remove(controllerId))
                {
                    Log.Debug($"[AudioManagerAPI] Released controller ID {controllerId} at {Timing.LocalTime}.");
                }
            }
        }
    }
}