using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Level
{
    [CreateAssetMenu(fileName = "LevelRouteTable", menuName = "NewFPG/Level/Route Table")]
    public sealed class LevelRouteTable : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Settings/Level/LevelRouteTable.asset";

        [SerializeField, Tooltip("路线标识。LevelFlowDirector 会用它检查当前绑定的路线表是否匹配。")]
        private LevelRouteId routeId = LevelRouteId.UndergroundFirstFloor;

        [SerializeField, Tooltip("进入路线时首先进入的房间 id，必须能在下方房间列表中找到。")]
        private string startRoomId = "b1_entry_combat";

        [SerializeField, TextArea, Tooltip("策划备注，只用于说明这张路线表的用途，不参与运行时逻辑。")]
        private string routeNote;

        [SerializeField, Tooltip("本路线包含的房间配置。房间里的 encounterId 会去 LevelEncounterTable 中查找刷怪配置。")]
        private List<LevelRoomDefinition> rooms = new List<LevelRoomDefinition>();

        public LevelRouteId RouteId => routeId;
        public string StartRoomId => startRoomId;
        public string RouteNote => routeNote;
        public IReadOnlyList<LevelRoomDefinition> Rooms => rooms;

        public LevelRoomDefinition FindRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || rooms == null)
            {
                return null;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                LevelRoomDefinition room = rooms[i];
                if (room != null && room.roomId == roomId)
                {
                    return room;
                }
            }

            return null;
        }

        public void Configure(LevelRouteId nextRouteId, string nextStartRoomId, IEnumerable<LevelRoomDefinition> nextRooms)
        {
            routeId = nextRouteId;
            startRoomId = nextStartRoomId;
            SetRooms(nextRooms);
        }

        public void SetRouteNote(string nextRouteNote)
        {
            routeNote = nextRouteNote ?? string.Empty;
        }

        public void SetRooms(IEnumerable<LevelRoomDefinition> nextRooms)
        {
            rooms.Clear();
            if (nextRooms != null)
            {
                rooms.AddRange(nextRooms);
            }

            Normalize();
        }

        private void OnValidate()
        {
            Normalize();
        }

        private void Normalize()
        {
            if (rooms == null)
            {
                rooms = new List<LevelRoomDefinition>();
            }

            if (string.IsNullOrWhiteSpace(startRoomId) && rooms.Count > 0 && rooms[0] != null)
            {
                startRoomId = rooms[0].roomId;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                NormalizeRoom(rooms[i]);
            }
        }

        private static void NormalizeRoom(LevelRoomDefinition room)
        {
            if (room == null)
            {
                return;
            }

            if (room.choices == null)
            {
                room.choices = new List<LevelRoomChoiceDefinition>();
            }

            if (room.exits == null)
            {
                room.exits = new List<LevelDoorDefinition>();
            }
        }
    }
}
