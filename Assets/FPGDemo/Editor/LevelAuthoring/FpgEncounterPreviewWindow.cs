using System;
using System.Collections.Generic;
using System.Linq;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.LevelAuthoring
{
    /// <summary>
    /// Editor-only formal encounter preview. It creates an in-memory request
    /// from the selected Room/Profile/Override and never marks an asset dirty.
    /// </summary>
    public sealed class FpgEncounterPreviewWindow : EditorWindow
    {
        private FpgRoomDefinition room;
        private FpgEncounterProfile profile;
        private FpgEncounterOverrideDefinition encounterOverride;
        private long runSeed = 1L;
        private string regionId = "default";
        private int depth;
        private int difficultyBasisPoints = FpgEncounterRunContext.BasisPointsOne;
        private int roomVisitOrdinal;
        private Vector2 scroll;
        private FpgEncounterPlan plan;
        private string error = string.Empty;

        [MenuItem("FPG Demo/Formal Encounter/Preview", priority = 130)]
        public static void Open()
        {
            FpgEncounterPreviewWindow window = GetWindow<FpgEncounterPreviewWindow>();
            window.titleContent = new GUIContent("Formal Encounter Preview");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        [MenuItem("CONTEXT/FpgRoomDefinition/Open Formal Encounter Preview")]
        private static void OpenForContext(MenuCommand command)
        {
            FpgEncounterPreviewWindow window = GetWindow<FpgEncounterPreviewWindow>();
            window.room = command.context as FpgRoomDefinition;
            window.titleContent = new GUIContent("Formal Encounter Preview");
            window.Show();
            window.Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Formal Encounter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview uses an in-memory FpgRoomRunRequest. It does not write Room, Profile, Override, or scene assets.",
                MessageType.Info);

            room = (FpgRoomDefinition)EditorGUILayout.ObjectField(
                "Room", room, typeof(FpgRoomDefinition), false);
            profile = (FpgEncounterProfile)EditorGUILayout.ObjectField(
                "Profile", profile, typeof(FpgEncounterProfile), false);
            encounterOverride = (FpgEncounterOverrideDefinition)EditorGUILayout.ObjectField(
                "Override", encounterOverride, typeof(FpgEncounterOverrideDefinition), false);

            runSeed = EditorGUILayout.LongField("Run Seed", runSeed);
            regionId = EditorGUILayout.TextField("Region ID", regionId);
            depth = EditorGUILayout.IntField("Depth", depth);
            difficultyBasisPoints = EditorGUILayout.IntField(
                "Difficulty (Basis Points)", difficultyBasisPoints);
            roomVisitOrdinal = EditorGUILayout.IntField("Room Visit Ordinal", roomVisitOrdinal);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection"))
                {
                    FpgRoomDefinition selected = Selection.activeObject as FpgRoomDefinition;
                    if (selected != null)
                    {
                        room = selected;
                    }
                }

                if (GUILayout.Button("Generate Preview"))
                {
                    GeneratePreview();
                }
            }

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            if (plan == null)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                $"Plan {plan.Digest:X16}  |  Layout {plan.WaveLayoutId}  |  Budget {plan.TotalBudget}  |  Waves {plan.WaveCount}  |  Entries {plan.EntryCount}",
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int waveIndex = 0; waveIndex < plan.Waves.Count; waveIndex++)
            {
                FpgEncounterWavePlan wave = plan.Waves[waveIndex];
                Dictionary<FpgEnemyRole, int> roleCounts = new Dictionary<FpgEnemyRole, int>();
                int capWeight = 0;
                for (int index = 0; index < wave.Entries.Count; index++)
                {
                    FpgSpawnEntry entry = wave.Entries[index];
                    if (!roleCounts.ContainsKey(entry.Role))
                    {
                        roleCounts.Add(entry.Role, 0);
                    }

                    roleCounts[entry.Role]++;
                    capWeight += entry.CapWeight;
                }

                string composition = string.Join(", ", roleCounts
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "=" + pair.Value));
                EditorGUILayout.LabelField(
                    $"Wave {waveIndex + 1}: share {wave.BudgetShareBasisPoints} bp  requested {wave.RequestedBudget}  spent {wave.Budget}  entries {wave.Entries.Count}  total cap {capWeight}",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Roles: " + (string.IsNullOrEmpty(composition) ? "none" : composition));

                for (int index = 0; index < wave.Entries.Count; index++)
                {
                    FpgSpawnEntry entry = wave.Entries[index];
                    EditorGUILayout.LabelField(
                        $"  {entry.SpawnSequence:000}  {entry.EnemyDefinitionId}  role={entry.Role}  cost={entry.SpawnCost}  cap={entry.CapWeight}");
                }
            }

            if (plan.Diagnostics != null && plan.Diagnostics.Count > 0)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Decision Summary", EditorStyles.boldLabel);
                for (int index = 0; index < plan.Diagnostics.Count; index++)
                {
                    EditorGUILayout.LabelField("- " + plan.Diagnostics[index]);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void GeneratePreview()
        {
            FpgEncounterPreviewUtility.TryGenerate(
                room,
                profile,
                encounterOverride,
                runSeed,
                regionId,
                depth,
                difficultyBasisPoints,
                roomVisitOrdinal,
                out plan,
                out error);
        }
    }
}

