namespace AudioManagerAPI.Features.Filters
{
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using PlayerRoles;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Provides common SCP:SL-specific audio filters for use with speaker configurations.
    /// Evaluated dynamically per player during audio transmission with zero heap allocation overhead.
    /// </summary>
    public static class AudioFilters
    {
        /// <summary>
        /// Filters players by their role type.
        /// </summary>
        public static Func<Player, bool> ByRole(RoleTypeId roleType)
        {
            return player => player != null && player.Role == roleType;
        }

        /// <summary>
        /// Filters players by their team.
        /// </summary>
        public static Func<Player, bool> ByTeam(Team team)
        {
            return player => player != null && player.Team == team;
        }

        /// <summary>
        /// Filters players within a specified distance from a position using optimized squared magnitude.
        /// </summary>
        public static Func<Player, bool> ByDistance(Vector3 position, float maxDistance)
        {
            // Precompute the squared distance outside the closure execution loop to save CPU cycles
            float maxDistanceSq = maxDistance * maxDistance;

            return player =>
            {
                if (player == null) return false;

                // Optimization: Utilizing sqrMagnitude avoids the expensive Mathf.Sqrt operation inside Vector3.Distance
                return (position - player.Position).sqrMagnitude <= maxDistanceSq;
            };
        }

        /// <summary>
        /// Filters players who are currently alive.
        /// </summary>
        public static Func<Player, bool> IsAlive()
        {
            return player => player != null && player.IsAlive;
        }

        /// <summary>  
        /// Filters players in a room where the lights match the specified state without LINQ or lambda allocations.  
        /// </summary>  
        public static Func<Player, bool> IsInRoomWhereLightsAre(bool lightsEnabled)
        {
            return player =>
            {
                if (player == null || player.Room == null) return false;

                var lightControllers = player.Room.AllLightControllers;
                if (lightControllers == null) return false;

                // Optimization Pattern: Extract the concrete underlying collection type via pattern matching
                // to enforce clean, zero-allocation indexing loops and avoid generic IEnumerator boxing.
                if (lightControllers is List<LightsController> list)
                {
                    int count = list.Count;
                    if (count == 0) return false;

                    for (int i = 0; i < count; i++)
                    {
                        if (list[i].LightsEnabled != lightsEnabled)
                            return false;
                    }
                    return true;
                }

                if (lightControllers is AdminToys.LightController[] array)
                {
                    int length = array.Length;
                    if (length == 0) return false;

                    for (int i = 0; i < length; i++)
                    {
                        if (array[i].LightsEnabled != lightsEnabled)
                            return false;
                    }
                    return true;
                }

                // Fallback loop for generic collections: Alleviates the lambda allocation by using a flat structural foreach loop.
                // It unifies .Any() and .All() into a single-pass evaluation.
                bool hasAny = false;
                foreach (var lc in lightControllers)
                {
                    hasAny = true;
                    if (lc.LightsEnabled != lightsEnabled)
                        return false;
                }

                return hasAny;
            };
        }

        /// <summary>
        /// Filters players based on a dynamically evaluated condition.
        /// </summary>
        /// <param name="condition">A function returning a boolean condition (e.g., () => Round.IsStarted).</param>
        public static Func<Player, bool> IsConditionTrue(Func<bool> condition)
        {
            return player => player != null && condition != null && condition();
        }

        /// <summary>
        /// Filters players in a specific room type.
        /// </summary>
        public static Func<Player, bool> IsInRoom(RoomName roomType)
        {
            return player => player != null && player.Room != null && player.Room.Name == roomType;
        }
    }
}