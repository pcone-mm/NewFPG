using System;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0PlannerInspectorLocalizationTests
    {
        private static readonly Type[] PlannerTypes =
        {
            typeof(D0CombatScenarioDefinition),
            typeof(D0CharacterDefinition),
            typeof(D0WeaponDefinition),
            typeof(D0CombatFeelProfile),
            typeof(D0ThreeCProfile),
            typeof(D0EnemyDefinition),
            typeof(D0EnemyBehaviorProfile),
            typeof(D0EnemyAttackDefinition),
            typeof(D0EncounterAttackScheduleEntry),
            typeof(D0EncounterSpawnSlot),
            typeof(D0EncounterDefinition),
            typeof(D0ActorPresentationDefinition),
            typeof(D0EnemyEffectPresentationDefinition),
            typeof(D0EnemyEffectPoolDefinition),
            typeof(PlayerActorPresentationDefinition),

            typeof(EnemyActorPresentationDefinition),
            typeof(D0StageDefinition),
            typeof(D0StageSpawnPointDefinition),
            typeof(D0StageForestLayerDefinition),
            typeof(ThreatScheduleEntryAuthoring),
        };

        [Test]
        public void EveryD0PlannerSerializedFieldHasChineseDisplayMetadataOrAnExplicitTechnicalBoundary()
        {
            for (int typeIndex = 0; typeIndex < PlannerTypes.Length; typeIndex++)
            {
                Type type = PlannerTypes[typeIndex];
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (!IsSerializedUnityField(field))
                    {
                        continue;
                    }

                    D0PlannerTechnicalFieldAttribute technical =
                        field.GetCustomAttribute<D0PlannerTechnicalFieldAttribute>();
                    if (technical != null)
                    {
                        Assert.That(
                            ContainsChinese(technical.Reason),
                            Is.True,
                            $"Technical boundary '{type.Name}.{field.Name}' requires a Chinese reason.");
                        continue;
                    }

                    D0PlannerFieldAttribute metadata =
                        field.GetCustomAttribute<D0PlannerFieldAttribute>();
                    Assert.That(
                        metadata,
                        Is.Not.Null,
                        $"Planner field '{type.Name}.{field.Name}' must expose Chinese display metadata.");
                    Assert.That(
                        ContainsChinese(metadata.DisplayName),
                        Is.True,
                        $"Planner field '{type.Name}.{field.Name}' requires a Chinese display name.");
                    Assert.That(
                        ContainsChinese(metadata.Tooltip),
                        Is.True,
                        $"Planner field '{type.Name}.{field.Name}' requires a Chinese explanation.");
                }
            }
        }

        [Test]
        public void SecondaryFireMetadataStatesTheSharedMagazineAndCancellationRule()
        {
            D0PlannerFieldAttribute secondaryAmmo = GetPlannerField(
                typeof(D0WeaponDefinition),
                "secondaryAmmoCost");
            D0PlannerFieldAttribute minimumCharge = GetPlannerField(
                typeof(D0WeaponDefinition),
                "secondaryMinimumChargeTicks");

            Assert.That(secondaryAmmo.DisplayName, Is.EqualTo("副射弹药消耗"));
            Assert.That(secondaryAmmo.Tooltip, Does.Contain("共享弹匣"));
            Assert.That(secondaryAmmo.Tooltip, Does.Contain("取消蓄力"));
            Assert.That(minimumCharge.Tooltip, Does.Contain("独立副射攻击"));
            Assert.That(minimumCharge.Tooltip, Does.Not.Contain("瞄准模式"));
        }

        [Test]
        public void CollisionAndCapacityKeysRemainExplicitlyOutsideThePlannerPanel()
        {
            Assert.That(
                GetTechnicalField(typeof(ThreatScheduleEntryAuthoring), "projectileBudgetUnits").Reason,
                Does.Contain("技术容量"));
            Assert.That(
                GetTechnicalField(typeof(ThreatScheduleEntryAuthoring), "sweepRadiusKey").Reason,
                Does.Contain("物理"));
            Assert.That(
                GetField(typeof(ThreatScheduleEntryAuthoring), "projectileBudgetUnits")
                    .GetCustomAttribute<D0PlannerFieldAttribute>(),
                Is.Null);
            Assert.That(
                GetField(typeof(ThreatScheduleEntryAuthoring), "sweepRadiusKey")
                    .GetCustomAttribute<D0PlannerFieldAttribute>(),
                Is.Null);
        }

        [Test]
        public void CameraSpawnAndEntityPrefabFieldsDeclareTheirActualBoundary()
        {
            string[] cameraFields =
            {
                "cameraPivotLocalPosition",
                "cameraPivotLocalEulerAngles",
                "cameraLocalPosition",
                "cameraLocalEulerAngles",
                "cameraFieldOfView",
                "cameraNearClipPlane",
                "cameraFarClipPlane",
            };

            for (int index = 0; index < cameraFields.Length; index++)
            {
                D0PlannerFieldAttribute cameraField = GetPlannerField(
                    typeof(D0ThreeCProfile),
                    cameraFields[index]);
                Assert.That(
                    cameraField.Tooltip,
                    Is.Not.Empty,
                    $"D0 3C camera field '{cameraFields[index]}' requires planner guidance.");
            }

            D0PlannerFieldAttribute stageSpawnPoints = GetPlannerField(
                typeof(D0StageDefinition),
                "spawnPoints");
            D0PlannerFieldAttribute entityPrefab = GetPlannerField(
                typeof(D0EnemyDefinition),
                "entityPrefab");
            Assert.That(stageSpawnPoints.Tooltip, Does.Contain("SpawnPoint"));
            Assert.That(entityPrefab.DisplayName, Does.Contain("Entity Prefab"));
            Assert.That(entityPrefab.Tooltip, Does.Contain("关卡"));
            Assert.That(
                GetField(typeof(D0StageDefinition), "spawnPoints")
                    .GetCustomAttribute<D0PlannerTechnicalFieldAttribute>(),
                Is.Null);
            Assert.That(
                GetField(typeof(D0EnemyDefinition), "entityPrefab")
                    .GetCustomAttribute<D0PlannerTechnicalFieldAttribute>(),
                Is.Null);
            Assert.That(
                GetTechnicalField(typeof(D0EnemyAttackDefinition), "projectileBudgetUnits").Reason,
                Does.Contain("容量"));
            Assert.That(
                GetTechnicalField(typeof(D0EnemyAttackDefinition), "sweepRadiusKey").Reason,
                Does.Contain("物理"));
        }

        [Test]
        public void MetadataUsesACustomEditorInsteadOfReplacingUnitysNativePropertyDrawers()
        {
            Assert.That(
                typeof(D0PlannerFieldAttribute).IsSubclassOf(typeof(global::UnityEngine.PropertyAttribute)),
                Is.False,
                "The metadata must not take over Unity's Min, Range, or TextArea property drawers.");
            Assert.That(
                FindLoadedType("FPG.Demo.Editor.D0PlannerConfigurationInspector"),
                Is.Not.Null,
                "D0 assets require the shared custom Inspector that supplies Chinese GUI labels.");
            Assert.That(
                FindLoadedType("FPG.Demo.Editor.BattleScenarioConfigD0EntryInspector"),
                Is.Not.Null,
                "BattleScenarioConfig requires the Chinese D0 asset entry Inspector.");
            Assert.That(
                FindLoadedType("FPG.Demo.Editor.CombatPresentationProfileD0EntryInspector"),
                Is.Not.Null,
                "The global combat presentation profile requires its dedicated D0 Inspector.");
        }

        [Test]
        public void GlobalPresentationProfileDoesNotExposeActorOverrideFields()
        {
            Assert.That(
                typeof(CombatPresentationProfile).GetField(
                    "playerPresentationOverride",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(CombatPresentationProfile).GetField(
                    "enemyPresentationOverride",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(CombatPresentationProfile).GetField(
                    "player",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(CombatPresentationProfile).GetField(
                    "enemy",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void PlannerRootAssetsResolveToTheirDedicatedChineseInspectors()
        {
            AssertCustomEditor<D0CombatScenarioDefinition>("FPG.Demo.Editor.D0CombatScenarioDefinitionInspector");
            AssertCustomEditor<D0CharacterDefinition>("FPG.Demo.Editor.D0CharacterDefinitionInspector");
            AssertCustomEditor<D0WeaponDefinition>("FPG.Demo.Editor.D0WeaponDefinitionInspector");
            AssertCustomEditor<D0CombatFeelProfile>("FPG.Demo.Editor.D0CombatFeelProfileInspector");
            AssertCustomEditor<D0ThreeCProfile>("FPG.Demo.Editor.D0ThreeCProfileInspector");
            AssertCustomEditor<D0EnemyDefinition>("FPG.Demo.Editor.D0EnemyDefinitionInspector");
            AssertCustomEditor<D0EnemyBehaviorProfile>("FPG.Demo.Editor.D0EnemyBehaviorProfileInspector");
            AssertCustomEditor<D0EnemyAttackDefinition>("FPG.Demo.Editor.D0EnemyAttackDefinitionInspector");
            AssertCustomEditor<D0EncounterDefinition>("FPG.Demo.Editor.D0EncounterDefinitionInspector");
            AssertCustomEditor<D0ActorPresentationDefinition>("FPG.Demo.Editor.D0ActorPresentationDefinitionInspector");
            AssertCustomEditor<D0StageDefinition>("FPG.Demo.Editor.D0StageDefinitionInspector");
            AssertCustomEditor<CombatPresentationProfile>("FPG.Demo.Editor.CombatPresentationProfileD0EntryInspector");
            AssertCustomEditor<BattleScenarioConfig>("FPG.Demo.Editor.BattleScenarioConfigD0EntryInspector");
        }

        private static bool IsSerializedUnityField(FieldInfo field)
        {
            return !field.IsStatic
                && !field.IsInitOnly
                && !field.IsNotSerialized
                && field.GetCustomAttribute<HideInInspector>() == null
                && (field.IsPublic
                    || field.GetCustomAttribute<SerializeField>() != null
                    || field.GetCustomAttribute<SerializeReference>() != null);
        }

        private static D0PlannerFieldAttribute GetPlannerField(Type type, string fieldName)
        {
            D0PlannerFieldAttribute metadata = GetField(type, fieldName)
                .GetCustomAttribute<D0PlannerFieldAttribute>();
            Assert.That(metadata, Is.Not.Null, $"Missing planner metadata for {type.Name}.{fieldName}.");
            return metadata;
        }

        private static D0PlannerTechnicalFieldAttribute GetTechnicalField(Type type, string fieldName)
        {
            D0PlannerTechnicalFieldAttribute metadata = GetField(type, fieldName)
                .GetCustomAttribute<D0PlannerTechnicalFieldAttribute>();
            Assert.That(metadata, Is.Not.Null, $"Missing technical boundary metadata for {type.Name}.{fieldName}.");
            return metadata;
        }

        private static FieldInfo GetField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {type.Name}.{fieldName}.");
            return field;
        }

        private static bool ContainsChinese(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character >= '\u4e00' && character <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertCustomEditor<T>(string expectedFullName)
            where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            try
            {
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(asset);
                try
                {
                    Assert.That(editor, Is.Not.Null, $"Unable to create an Inspector for {typeof(T).Name}.");
                    Assert.That(editor.GetType().FullName, Is.EqualTo(expectedFullName));
                }
                finally
                {
                    global::UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                global::UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
