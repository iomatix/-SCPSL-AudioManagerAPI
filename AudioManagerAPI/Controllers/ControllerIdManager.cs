namespace AudioManagerAPI.Controllers
{
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Speakers.State;
    using MEC;
    using System;
    using System.Collections.Generic;
    using Log = AudioManagerAPI.Logger.ApiLogger;

    /// <summary>
    /// Manages physical audio controller IDs (1-254) with priority-based allocation.
    /// Manages abstract session states independently of physical IDs.
    /// </summary>
    public static class ControllerIdManager
    {
        private static readonly object lockObject = new object();
        private static readonly Stack<byte> availableIds = new Stack<byte>(254);
        private static readonly Dictionary<byte, int> activeControllers = new Dictionary<byte, int>(254);
        private static readonly Dictionary<int, SessionData> activeSessions = new Dictionary<int, SessionData>();

        private static int nextSessionId = 1;

        private struct SessionData
        {
            public AudioPriority Priority;
            public Action StopCallback;
            public SpeakerState State;
            public byte? CurrentControllerId;
        }

        static ControllerIdManager()
        {
            for (byte i = 254; i >= 1; i--)
            {
                availableIds.Push(i);
            }
        }

        /// <summary>
        /// Lock-free fast check to determine if an allocation or eviction is theoretically possible.
        /// Prevents wasteful heap allocations in caller methods when the pool is saturated.
        /// </summary>
        public static bool CanAllocate(AudioPriority priority)
        {
            lock (lockObject)
            {
                if (availableIds.Count > 0)
                    return true;

                // Priority.Lowest can NEVER evict anything because no priority is strictly lower than it
                if (priority <= AudioPriority.Lowest)
                    return false;

                // Check if any active session has a strictly lower priority
                foreach (var kvp in activeControllers)
                {
                    if (activeSessions.TryGetValue(kvp.Value, out var session) && session.Priority < priority)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Registers a new audio session and attempts to allocate a physical controller ID.
        /// </summary>
        public static bool TryAllocate(AudioPriority priority, Action stopCallback, SpeakerState state, out int sessionId, out byte controllerId)
        {
            Action callbackToInvoke = null;

            lock (lockObject)
            {
                controllerId = 0;

                // 1. Try to get a free physical ID
                if (availableIds.Count > 0)
                {
                    controllerId = availableIds.Pop();
                }
                // 2. No free IDs; attempt eviction only if newPriority is higher than Lowest
                else if (priority > AudioPriority.Lowest)
                {
                    byte? evictedId = TryEvictLowerPriority(priority, out callbackToInvoke);
                    if (evictedId.HasValue)
                    {
                        controllerId = evictedId.Value;
                    }
                }

                // If allocation succeeded
                if (controllerId != 0)
                {
                    sessionId = nextSessionId++;

                    var session = new SessionData
                    {
                        Priority = priority,
                        StopCallback = stopCallback,
                        State = state,
                        CurrentControllerId = controllerId
                    };

                    activeControllers[controllerId] = sessionId;
                    activeSessions[sessionId] = session;

                    Log.Debug($"Session {sessionId} allocated controller ID {controllerId} with priority {priority} at {Timing.LocalTime}.");
                }
                else
                {
                    // Allocation failed: only track session if explicitly marked as persistent
                    if (state != null && state.Persistent)
                    {
                        sessionId = nextSessionId++;
                        var session = new SessionData
                        {
                            Priority = priority,
                            StopCallback = stopCallback,
                            State = state,
                            CurrentControllerId = null
                        };
                        activeSessions[sessionId] = session;
                        Log.Debug($"Session {sessionId} failed allocation but state saved (persistent) at {Timing.LocalTime}.");
                    }
                    else
                    {
                        sessionId = 0;
                    }
                }
            }

            // Invoke eviction callback OUTSIDE the lock
            callbackToInvoke?.Invoke();

            return controllerId != 0;
        }

        /// <summary>
        /// Internal method to find and evict a lower priority session. MUST be called inside lock.
        /// </summary>
        private static byte? TryEvictLowerPriority(AudioPriority newPriority, out Action evictionCallback)
        {
            evictionCallback = null;

            if (newPriority <= AudioPriority.Lowest || activeControllers.Count == 0)
                return null;

            byte? candidateId = null;
            int? candidateSessionId = null;
            AudioPriority lowestFound = newPriority;

            foreach (var kvp in activeControllers)
            {
                int sId = kvp.Value;
                if (activeSessions.TryGetValue(sId, out var session))
                {
                    if (session.Priority < lowestFound)
                    {
                        lowestFound = session.Priority;
                        candidateId = kvp.Key;
                        candidateSessionId = sId;
                    }
                }
            }

            if (candidateId.HasValue && candidateSessionId.HasValue)
            {
                var victimSession = activeSessions[candidateSessionId.Value];
                evictionCallback = victimSession.StopCallback;

                victimSession.CurrentControllerId = null;
                activeSessions[candidateSessionId.Value] = victimSession;

                if (victimSession.State == null || !victimSession.State.Persistent)
                {
                    activeSessions.Remove(candidateSessionId.Value);
                }

                activeControllers.Remove(candidateId.Value);
                Log.Debug($"Evicted session {candidateSessionId.Value} from controller {candidateId.Value}.");
                return candidateId.Value;
            }

            return null;
        }

        /// <summary>
        /// Releases a controller ID, making it available for reuse.
        /// </summary>
        public static void ReleaseController(byte controllerId)
        {
            lock (lockObject)
            {
                if (activeControllers.TryGetValue(controllerId, out int sessionId))
                {
                    activeControllers.Remove(controllerId);
                    availableIds.Push(controllerId);

                    if (activeSessions.TryGetValue(sessionId, out var session))
                    {
                        session.CurrentControllerId = null;
                        activeSessions[sessionId] = session;

                        if (session.State == null || !session.State.Persistent)
                        {
                            activeSessions.Remove(sessionId);
                        }
                    }
                    Log.Debug($"Released controller ID {controllerId} (from session {sessionId}) at {Timing.LocalTime}.");
                }
            }
        }

        /// <summary>
        /// Returns physical ID of the controller currently associated with a session, if any. Returns false if session doesn't exist or has no active controller.
        /// </summary>
        public static bool TryGetActiveController(int sessionId, out byte controllerId)
        {
            lock (lockObject)
            {
                if (activeSessions.TryGetValue(sessionId, out var session) && session.CurrentControllerId.HasValue)
                {
                    controllerId = session.CurrentControllerId.Value;
                    return true;
                }

                controllerId = 0;
                return false;
            }
        }

        /// <summary>
        /// Retrieves the state object associated with a session ID.
        /// </summary>
        public static SpeakerState GetSessionState(int sessionId)
        {
            lock (lockObject)
            {
                return activeSessions.TryGetValue(sessionId, out var session) ? session.State : null;
            }
        }

        /// <summary>
        /// Removes a session entirely, freeing its state memory.
        /// </summary>
        public static void DestroySession(int sessionId)
        {
            lock (lockObject)
            {
                if (activeSessions.TryGetValue(sessionId, out var session))
                {
                    if (session.CurrentControllerId.HasValue)
                    {
                        ReleaseController(session.CurrentControllerId.Value);
                    }
                    activeSessions.Remove(sessionId);
                    Log.Debug($"Destroyed session {sessionId}.");
                }
            }
        }

        /// <summary>
        /// Completely resets the controller manager state. 
        /// Must be called during round restarts to prevent ID exhaustion and memory leaks.
        /// </summary>
        public static void FullReset()
        {
            lock (lockObject)
            {
                activeControllers.Clear();
                activeSessions.Clear();
                availableIds.Clear();

                for (byte i = 254; i >= 1; i--)
                {
                    availableIds.Push(i);
                }

                nextSessionId = 1;
                Log.Debug("ControllerIdManager state has been fully reset.");
            }
        }
    }
}