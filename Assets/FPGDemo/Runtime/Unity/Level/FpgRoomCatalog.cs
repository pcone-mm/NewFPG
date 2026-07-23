using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "FpgRoomCatalog",
        menuName = "FPG Demo/Config/Level/Room Catalog")]
    public sealed class FpgRoomCatalog : ScriptableObject
    {
        [SerializeField]
        [D0PlannerField(
            "候选房间",
            "出口在房间清空时只会从此列表抽取目标。列表中的每个房间必须通过完整房间校验，并至少声明一个玩家入口和一个出口；房间 ID 不得重复。")]
        private FpgRoomDefinition[] rooms = Array.Empty<FpgRoomDefinition>();

        public IReadOnlyList<FpgRoomDefinition> Rooms =>
            rooms ?? Array.Empty<FpgRoomDefinition>();

        public int Count => rooms == null ? 0 : rooms.Length;

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            FpgRoomDefinition[] configuredRooms =
                rooms ?? Array.Empty<FpgRoomDefinition>();
            if (configuredRooms.Length == 0)
            {
                error = "Room catalog requires at least one room.";
                return false;
            }

            HashSet<string> roomIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < configuredRooms.Length; index++)
            {
                FpgRoomDefinition room = configuredRooms[index];
                if (room == null)
                {
                    error = $"Room catalog entry {index} is missing.";
                    return false;
                }

                if (!room.TryValidate(out FpgRoomValidationResult validation))
                {
                    string detail = validation.FirstError == null
                        ? "the room definition is invalid"
                        : validation.FirstError.Message;
                    error =
                        $"Room catalog entry {index} ('{room.RoomId}') is invalid: {detail}";
                    return false;
                }

                if (room.ExitSlots.Count == 0)
                {
                    error =
                        $"Room catalog entry {index} ('{room.RoomId}') requires at least one exit slot.";
                    return false;
                }

                if (!roomIds.Add(room.RoomId))
                {
                    error =
                        $"Room catalog contains duplicate room ID '{room.RoomId}'.";
                    return false;
                }
            }

            return true;
        }

        public bool TryResolve(
            string roomId,
            out FpgRoomDefinition room,
            out string error)
        {
            room = null;
            if (!TryValidate(out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(roomId))
            {
                error = "Room lookup requires a stable room ID.";
                return false;
            }

            FpgRoomDefinition[] configuredRooms = rooms;
            for (int index = 0; index < configuredRooms.Length; index++)
            {
                FpgRoomDefinition candidate = configuredRooms[index];
                if (string.Equals(candidate.RoomId, roomId, StringComparison.Ordinal))
                {
                    room = candidate;
                    error = string.Empty;
                    return true;
                }
            }

            error = $"Room ID '{roomId}' is not present in the room catalog.";
            return false;
        }

        public bool TryGetStableRoomIds(out string[] roomIds, out string error)
        {
            roomIds = Array.Empty<string>();
            if (!TryValidate(out error))
            {
                return false;
            }

            FpgRoomDefinition[] configuredRooms = rooms;
            roomIds = new string[configuredRooms.Length];
            for (int index = 0; index < configuredRooms.Length; index++)
            {
                roomIds[index] = configuredRooms[index].RoomId;
            }

            Array.Sort(roomIds, StringComparer.Ordinal);
            error = string.Empty;
            return true;
        }
    }
}
