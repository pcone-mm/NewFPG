namespace FPG.Demo.Unity
{
    /// <summary>
    /// In-memory authoring override populated only by the Editor playtest
    /// workflow. Normal runtime composition uses serialized scene/config data.
    /// </summary>
    public static class FpgRoomPlaytestOverrides
    {
        public static FpgRoomDefinition RoomDefinition { get; private set; }
        public static D0CombatScenarioDefinition ScenarioDefinition { get; private set; }
        public static bool IsActive => RoomDefinition != null || ScenarioDefinition != null;

        public static void Set(
            FpgRoomDefinition roomDefinition,
            D0CombatScenarioDefinition scenarioDefinition)
        {
            RoomDefinition = roomDefinition;
            ScenarioDefinition = scenarioDefinition;
        }

        public static bool Matches(
            FpgRoomDefinition roomDefinition,
            D0CombatScenarioDefinition scenarioDefinition)
        {
            return RoomDefinition == roomDefinition
                && ScenarioDefinition == scenarioDefinition;
        }

        public static void ClearIf(
            FpgRoomDefinition roomDefinition,
            D0CombatScenarioDefinition scenarioDefinition)
        {
            if (Matches(roomDefinition, scenarioDefinition))
            {
                Clear();
            }
        }

        public static void Clear()
        {
            RoomDefinition = null;
            ScenarioDefinition = null;
        }
    }
}