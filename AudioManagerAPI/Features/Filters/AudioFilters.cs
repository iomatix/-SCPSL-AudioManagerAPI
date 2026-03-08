namespace AudioManagerAPI.Features.Filters
{
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using PlayerRoles;
    using System;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Provides common SCP:SL-specific audio filters for use with speaker configurations.
    /// Evaluated dynamically per player during audio transmission.
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
        /// Filters players within a specified distance from a position.
        /// </summary>
        public static Func<Player, bool> ByDistance(Vector3 position, float maxDistance)
        {
            return player =>
            {
                if (player?.Position == null) return false;
                return Vector3.Distance(position, player.Position) <= maxDistance;
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
        /// Filters players in a room where the lights are in the specified state (enabled or disabled).  
        /// </summary>  
        public static Func<Player, bool> IsInRoomWhereLightsAre(bool lightsEnabled)
        {
            return player =>
            {
                if (player?.Room == null) return false;

                var lightControllers = player.Room.AllLightControllers;
                if (!lightControllers.Any()) return false;

                return lightControllers.All(lc => lc.LightsEnabled == lightsEnabled);
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
            return player => player?.Room != null && player.Room.Name == roomType;
        }
    }
}