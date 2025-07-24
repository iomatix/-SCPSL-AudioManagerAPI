namespace AudioManagerAPI.Features.Filters
{
    using System;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using PlayerRoles;
    using UnityEngine;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Provides common SCP:SL-specific audio filters for use with <see cref="ISpeaker.SetValidPlayers(Func{Player, bool})"/> or <see cref="ISpeaker.SetValidPlayers(IEnumerable{Func{Player, bool}})"/>.
    /// </summary>
    /// <remarks>
    /// These filters allow plugins to control which players can hear audio from a speaker, supporting scenarios like role-based audio, team-based audio, proximity-based audio, room-based conditions, or event-specific logic.
    /// </remarks>
    public static class AudioFilters
    {
        /// <summary>
        /// Filters players by their role type.
        /// </summary>
        /// <param name="roleType">The role type to allow (e.g., <see cref="RoleTypeId.Scp173"/>).</param>
        /// <returns>A filter function that returns true for players with the specified role.</returns>
        public static Func<Player, bool> ByRole(RoleTypeId roleType)
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("ByRole: Player is null.");
                    return false;
                }
                return player.Role == roleType;
            };
        }

        /// <summary>
        /// Filters players by their team.
        /// </summary>
        /// <param name="team">The team to allow (e.g., <see cref="Team.SCP"/>).</param>
        /// <returns>A filter function that returns true for players on the specified team.</returns>
        public static Func<Player, bool> ByTeam(Team team)
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("ByTeam: Player is null.");
                    return false;
                }
                return player.Team == team;
            };
        }

        /// <summary>
        /// Filters players within a specified distance from a position.
        /// </summary>
        /// <param name="position">The world position to measure distance from (e.g., speaker position).</param>
        /// <param name="maxDistance">The maximum distance in Unity units.</param>
        /// <returns>A filter function that returns true for players within the specified distance.</returns>
        /// <remarks>
        /// For performance, cache <paramref name="position"/> if this filter is called frequently (e.g., per frame for many players).
        /// </remarks>
        public static Func<Player, bool> ByDistance(Vector3 position, float maxDistance)
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("ByDistance: Player is null.");
                    return false;
                }
                if (player.Position == null)
                {
                    Log.Warn("ByDistance: Player position is null.");
                    return false;
                }
                float distance = Vector3.Distance(position, player.Position);
                return distance <= maxDistance;
            };
        }

        /// <summary>
        /// Filters players who are alive.
        /// </summary>
        /// <returns>A filter function that returns true for living players.</returns>
        public static Func<Player, bool> IsAlive()
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("IsAlive: Player is null.");
                    return false;
                }
                return player.IsAlive;
            };
        }

        /// <summary>
        /// Filters players in a room where the lights are in the specified state (enabled or disabled).
        /// </summary>
        /// <param name="lightsEnabled">True to filter for rooms with lights enabled, false for lights disabled.</param>
        /// <returns>A filter function that returns true for players in rooms with the specified light state.</returns>
        public static Func<Player, bool> IsInRoomWhereLightsAre(bool lightsEnabled)
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("IsInRoomWhereLightsAre: Player is null.");
                    return false;
                }
                if (player.Room == null)
                {
                    Log.Warn("IsInRoomWhereLightsAre: Player has no associated room.");
                    return false;
                }
                if (player.Room.LightController == null)
                {
                    Log.Warn("IsInRoomWhereLightsAre: Room has no LightController.");
                    return false;
                }
                return player.Room.LightController.LightsEnabled == lightsEnabled;
            };
        }

        /// <summary>
        /// Filters players based on a boolean condition.
        /// </summary>
        /// <param name="condition">The boolean condition to check (e.g., event active, round state).</param>
        /// <returns>A filter function that returns true if the condition is met.</returns>
        public static Func<Player, bool> IsConditionTrue(bool condition)
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("IsConditionTrue: Player is null.");
                    return false;
                }
                return condition;
            };
        }

        /// <summary>
        /// Filters players in a specific room type.
        /// </summary>
        /// <param name="roomType">The room type to filter (e.g., <see cref="RoomName.EzIntercom"/>).</param>
        /// <returns>A filter function that returns true for players in the specified room type.</returns>
        public static Func<Player, bool> IsInRoom(RoomName roomType)
        {
            return player =>
            {
                if (player == null)
                {
                    Log.Warn("IsInRoom: Player is null.");
                    return false;
                }
                if (player.Room == null)
                {
                    Log.Warn("IsInRoom: Player has no associated room.");
                    return false;
                }
                return player.Room.Name == roomType;
            };
        }
    }
}