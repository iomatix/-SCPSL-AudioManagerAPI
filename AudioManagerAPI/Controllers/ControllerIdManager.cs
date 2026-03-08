namespace AudioManagerAPI.Controllers
{
    using System;
    using System.Collections.Generic;
    using MEC;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Speakers.State;

    using Log = DebugLogger;

    /// <summary>
    /// Manages physical audio controller IDs (1-254) with priority-based allocation.
    /// Manages abstract session states independently of physical IDs.
    /// </summary>
    public static class ControllerIdManager
    {
        // Object for thread safety
        private static readonly object lockObject = new object();

        // Physical controller IDs available for immediate use
        private static readonly Stack<byte> availableIds = new Stack<byte>(254);

        // Maps physical controller ID to the currently active Session ID
        private static readonly Dictionary<byte, int> activeControllers = new Dictionary<byte, int>(254);

        // Abstract Session data
        private static readonly Dictionary<int, SessionData> activeSessions = new Dictionary<int, SessionData>();

        // Auto-incrementing session ID
        private static int nextSessionId = 1;

        // Internal struct to hold all data related to an audio session
        private struct SessionData
        {
            public AudioPriority Priority;
            public Action StopCallback;
            public SpeakerState State;
            public byte? CurrentControllerId; // Null if evicted or waiting
        }

        static ControllerIdManager()
        {
            // Initialize physical slots (1 to 254)
            // Push in reverse order so ID 1 is popped first (optional, but cleaner)
            for (byte i = 254; i >= 1; i--)
            {
                availableIds.Push(i);
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
                sessionId = nextSessionId++;
                controllerId = 0;

                // Create session record
                var session = new SessionData
                {
                    Priority = priority,
                    StopCallback = stopCallback,
                    State = state,
                    CurrentControllerId = null
                };

                // 1. Try to get a free physical ID
                if (availableIds.Count > 0)
                {
                    controllerId = availableIds.Pop();
                }
                // 2. No free IDs, try to evict a lower priority session
                else
                {
                    byte? evictedId = TryEvictLowerPriority(priority, out callbackToInvoke);
                    if (evictedId.HasValue)
                    {
                        controllerId = evictedId.Value;
                    }
                }

                // If we got a physical ID (either free or evicted)
                if (controllerId != 0)
                {
                    session.CurrentControllerId = controllerId;
                    activeControllers[controllerId] = sessionId;
                    activeSessions[sessionId] = session;

                    Log.Debug($"[AudioManagerAPI] Session {sessionId} allocated controller ID {controllerId} with priority {priority} at {Timing.LocalTime}.");
                }
                else
                {
                    // Allocation failed, but we still might want to keep the session state if it's persistent
                    if (state != null && state.Persistent)
                    {
                        activeSessions[sessionId] = session;
                        Log.Debug($"[AudioManagerAPI] Session {sessionId} failed allocation but state saved (persistent) at {Timing.LocalTime}.");
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
            byte? candidateId = null;
            int? candidateSessionId = null;
            AudioPriority lowestFound = newPriority;

            // Find the lowest priority active controller
            foreach (var kvp in activeControllers)
            {
                int sId = kvp.Value;
                if (activeSessions.TryGetValue(sId, out var session))
                {
                    // We look for strictly lower priority
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
                // We found a victim
                var victimSession = activeSessions[candidateSessionId.Value];

                // Prepare callback for external execution
                evictionCallback = victimSession.StopCallback;

                // Update victim's state
                victimSession.CurrentControllerId = null;
                activeSessions[candidateSessionId.Value] = victimSession; // Update struct

                // If victim isn't persistent, remove its session entirely
                if (victimSession.State == null || !victimSession.State.Persistent)
                {
                    activeSessions.Remove(candidateSessionId.Value);
                }

                activeControllers.Remove(candidateId.Value);
                Log.Debug($"[AudioManagerAPI] Evicted session {candidateSessionId.Value} from controller {candidateId.Value}.");
                return candidateId.Value;
            }

            return null; // Nothing lower priority found
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
                    availableIds.Push(controllerId); // Return to pool

                    if (activeSessions.TryGetValue(sessionId, out var session))
                    {
                        session.CurrentControllerId = null;
                        activeSessions[sessionId] = session;

                        // If not persistent, clean up the session
                        if (session.State == null || !session.State.Persistent)
                        {
                            activeSessions.Remove(sessionId);
                        }
                    }
                    Log.Debug($"[AudioManagerAPI] Released controller ID {controllerId} (from session {sessionId}) at {Timing.LocalTime}.");
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
                        // Force release the physical controller if it's active
                        ReleaseController(session.CurrentControllerId.Value);
                    }
                    activeSessions.Remove(sessionId);
                    Log.Debug($"[AudioManagerAPI] Destroyed session {sessionId}.");
                }
            }
        }
    }
}