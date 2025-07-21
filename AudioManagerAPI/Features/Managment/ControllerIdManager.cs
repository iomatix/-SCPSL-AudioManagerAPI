namespace AudioManagerAPI.Features.Managment
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Manages a shared pool of controller IDs for audio speakers across all plugins to prevent ID conflicts.
    /// </summary>
    public static class ControllerIdManager
    {
        private static readonly object lockObject = new Dictionary<byte, ISpeaker>();
        private static readonly HashSet<byte> availableIds = new HashSet<byte>();

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
        /// Allocates a unique controller ID from the shared pool.
        /// </summary>
        /// <returns>A unique controller ID, or <c>null</c> if no IDs are available.</returns>
        public static byte? AllocateId()
        {
            lock (lockObject)
            {
                if (availableIds.Count == 0) return null;
                byte id = availableIds.First();
                availableIds.Remove(id);
                return id;
            }
        }

        /// <summary>
        /// Releases a controller ID back to the shared pool.
        /// </summary>
        /// <param name="id">The controller ID to release.</param>
        public static void ReleaseId(byte id)
        {
            lock (lockObject)
            {
                availableIds.Add(id);
            }
        }
    }
}
