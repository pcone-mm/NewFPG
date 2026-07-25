using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal static class FpgSkillTimelineV1Migration
    {
        private const string WeaponPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/FPG_Fei_Weapon.asset";
        private const string SkillFolder =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills";
        private const string PrimaryPath = SkillFolder + "/FPG_Fei_Primary.asset";
        private const string SecondaryPath = SkillFolder + "/FPG_Fei_Secondary.asset";
        private const string ReloadPath = SkillFolder + "/FPG_Fei_Reload.asset";
        private const string PlayerSkillTypeName =
            "FPG.Demo.Unity.FpgPlayerSkillDefinition";
        private const string EnemySkillTypeName =
            "FPG.Demo.Unity.FpgEnemyAttackDefinition";

        [MenuItem(
            "FPG Demo/Skill Editor/Migrate Formal Skills V1",
            priority = 126)]
        private static void RunFromMenu()
        {
            if (Run(out string report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        public static bool Run(out string report)
        {
            List<string> messages = new List<string>();
            List<string> errors = new List<string>();

            try
            {
                EnsureFolder(SkillFolder);
                MigrateFei(messages, errors);
                MigrateEnemies(messages, errors);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }

            report = string.Join("\n", messages);
            if (errors.Count == 0)
            {
                report = "Skill timeline V1 migration completed.\n" + report;
                return true;
            }

            report = "Skill timeline V1 migration failed.\n"
                + string.Join("\n", errors)
                + (messages.Count == 0
                    ? string.Empty
                    : "\nCompleted steps:\n" + string.Join("\n", messages));
            return false;
        }

        private static void MigrateFei(
            List<string> messages,
            List<string> errors)
        {
            UnityEngine.Object weaponAsset =
                AssetDatabase.LoadMainAssetAtPath(WeaponPath);
            if (weaponAsset == null)
            {
                errors.Add("Missing Fei weapon asset: " + WeaponPath);
                return;
            }

            SerializedObject weapon = new SerializedObject(weaponAsset);
            weapon.Update();

            if (HasMigratedFeiSkills(weapon, errors))
            {
                messages.Add(
                    "Fei formal skill references already exist; validated without rewriting.");
                return;
            }


            int primaryAmmo = ReadInt(weapon, "primaryAmmoCost", 1);
            int primaryDamage = ReadInt(weapon, "primaryDamage", 4);
            int primaryBreak = ReadInt(weapon, "primaryBreakDamage", 4);
            int primaryWeakDamage = ReadInt(
                weapon,
                "primaryWeakpointDamageMultiplierBasisPoints",
                12000);
            int primaryWeakBreak = ReadInt(
                weapon,
                "primaryWeakpointBreakMultiplierBasisPoints",
                25000);
            int primaryQuery = ReadInt(weapon, "primaryQueryMode", 1);
            int primaryPenetration = ReadInt(
                weapon,
                "primaryAdditionalPenetrationCount",
                0);
            int secondaryMinimumCharge = ReadInt(
                weapon,
                "secondaryMinimumChargeTicks",
                0);
            int secondaryAmmo = ReadInt(weapon, "secondaryAmmoCost", 2);
            int secondaryDamage = ReadInt(weapon, "secondaryDamage", 28);
            int secondaryBreak = ReadInt(weapon, "secondaryBreakDamage", 20);
            int secondaryWeakDamage = ReadInt(
                weapon,
                "secondaryWeakpointDamageMultiplierBasisPoints",
                12000);
            int secondaryWeakBreak = ReadInt(
                weapon,
                "secondaryWeakpointBreakMultiplierBasisPoints",
                25000);
            int secondaryQuery = ReadInt(weapon, "secondaryQueryMode", 2);
            int secondaryCombatantLimit = ReadInt(
                weapon,
                "secondaryMaxImpactCount",
                4);
            int secondaryProjectileLimit = ReadInt(
                weapon,
                "secondaryProjectileMaxImpactCount",
                4);
            int reloadDuration = ReadInt(weapon, "reloadDurationTicks", 84);

            SerializedProperty primaryPresentation =
                weapon.FindProperty("primaryPresentation");
            SerializedProperty secondaryPresentation =
                weapon.FindProperty("secondaryPresentation");
            SerializedProperty secondaryShot = secondaryPresentation == null
                ? null
                : secondaryPresentation.FindPropertyRelative("shot");
            SerializedProperty reloadPresentation =
                weapon.FindProperty("reloadPresentation");

            string primarySocket = ReadString(
                primaryPresentation,
                "socketId",
                "weapon.primary.muzzle");
            string primaryMuzzleCue = ReadString(
                primaryPresentation,
                "muzzleVfxKey",
                "player.weapon.primary.muzzle");
            string primaryTracerCue = ReadString(
                primaryPresentation,
                "tracerVfxKey",
                "player.weapon.primary.tracer");
            string primaryAnimation = ReadString(
                primaryPresentation,
                "animationName",
                "attack_play1");
            string primaryAlternate = ReadString(
                primaryPresentation,
                "alternateAnimationName",
                "attack_play2");

            string secondarySocket = ReadString(
                secondaryShot,
                "socketId",
                "weapon.secondary.muzzle");
            string secondaryMuzzleCue = ReadString(
                secondaryShot,
                "muzzleVfxKey",
                "player.weapon.secondary.muzzle");
            string secondaryTracerCue = ReadString(
                secondaryShot,
                "tracerVfxKey",
                "player.weapon.secondary.tracer");
            string secondaryChargeCue = ReadString(
                secondaryPresentation,
                "chargeVfxKey",
                "player.weapon.secondary.charge");
            string secondaryBurstCue = ReadString(
                secondaryPresentation,
                "targetBurstVfxKey",
                "player.weapon.secondary.target-burst");
            string chargeAnimation = ReadString(
                secondaryPresentation,
                "chargeAnimation",
                "u4_attack_ready");
            string releaseAnimation = ReadString(
                secondaryPresentation,
                "releaseAnimation",
                "defense_play");
            string cancelAnimation = ReadString(
                secondaryPresentation,
                "endAnimation",
                "u4_attack_end");
            string reloadAnimation = ReadString(
                reloadPresentation,
                "playAnimation",
                "u1_buff_play");
            string reloadReadyAnimation = ReadString(
                reloadPresentation,
                "readyAnimation",
                "u1_buff_ready");

            UnityEngine.Object primary = LoadOrCreateSkill(
                PrimaryPath,
                PlayerSkillTypeName,
                errors);
            UnityEngine.Object secondary = LoadOrCreateSkill(
                SecondaryPath,
                PlayerSkillTypeName,
                errors);
            UnityEngine.Object reload = LoadOrCreateSkill(
                ReloadPath,
                PlayerSkillTypeName,
                errors);
            if (primary == null || secondary == null || reload == null)
            {
                return;
            }

            ConfigurePlayerSkill(
                primary,
                "fei.primary",
                "Fei Primary",
                "Migrated from FPG_Fei_Weapon. Tick 0 attack; cooldown starts from the planned final attack tick.",
                12,
                new PlayerPayloadSpec
                {
                    SlotId = "payload.fei.primary",
                    Kind = 1,
                    AmmoCost = primaryAmmo,
                    BaseDamage = primaryDamage,
                    BreakDamage = primaryBreak,
                    WeakpointDamage = primaryWeakDamage,
                    WeakpointBreak = primaryWeakBreak,
                    QueryMode = primaryQuery,
                    PelletCount = 8,
                    AdditionalPenetration = primaryPenetration,
                    AreaCombatantLimit = 4,
                    AreaProjectileLimit = 4,
                    AllowedTargetKinds = 3
                },
                new[]
                {
                    new SequenceSpec
                    {
                        Kind = 1,
                        Duration = 11,
                        Animation = primaryAnimation,
                        AlternateAnimations = string.IsNullOrWhiteSpace(
                            primaryAlternate)
                                ? Array.Empty<string>()
                                : new[] { primaryAlternate },
                        Phases = new[]
                        {
                            new PhaseSpec("active", 2, 0, 0),
                            new PhaseSpec("recovery", 3, 0, 11)
                        },
                        Logic = new[]
                        {
                            new LogicSpec(
                                "event.fei.primary.attack.0",
                                0,
                                0,
                                "payload.fei.primary",
                                primarySocket,
                                1)
                        },
                        Cues = new[]
                        {
                            new CueSpec(
                                "cue.fei.primary.muzzle.0",
                                0,
                                1,
                                primaryMuzzleCue,
                                primarySocket,
                                "event.fei.primary.attack.0"),
                            new CueSpec(
                                "cue.fei.primary.tracer.0",
                                0,
                                2,
                                primaryTracerCue,
                                primarySocket,
                                "event.fei.primary.attack.0")
                        }
                    }
                });

            ConfigurePlayerSkill(
                secondary,
                "fei.secondary",
                "Fei Secondary",
                "Migrated charged secondary. ChargeEnter and ChargeLoop feed Release; Release attacks at Tick 0.",
                30,
                new PlayerPayloadSpec
                {
                    SlotId = "payload.fei.secondary",
                    Kind = 2,
                    AmmoCost = secondaryAmmo,
                    BaseDamage = secondaryDamage,
                    BreakDamage = secondaryBreak,
                    WeakpointDamage = secondaryWeakDamage,
                    WeakpointBreak = secondaryWeakBreak,
                    QueryMode = secondaryQuery,
                    PelletCount = 1,
                    AdditionalPenetration = 0,
                    AreaCombatantLimit = secondaryCombatantLimit,
                    AreaProjectileLimit = secondaryProjectileLimit,
                    AllowedTargetKinds = 3
                },
                new[]
                {
                    new SequenceSpec
                    {
                        Kind = 1,
                        Duration = 0,
                        Animation = chargeAnimation
                    },
                    new SequenceSpec
                    {
                        Kind = 2,
                        Duration = 0,
                        Animation = chargeAnimation,
                        Cues = new[]
                        {
                            new CueSpec(
                                "cue.fei.secondary.charge.enter",
                                0,
                                0,
                                secondaryChargeCue,
                                secondarySocket)
                        }
                    },
                    new SequenceSpec
                    {
                        Kind = 3,
                        Duration = 0,
                        Animation = chargeAnimation,
                        Loop = true
                    },
                    new SequenceSpec
                    {
                        Kind = 4,
                        Duration = 29,
                        Animation = releaseAnimation,
                        Phases = new[]
                        {
                            new PhaseSpec("active", 2, 0, 0),
                            new PhaseSpec("recovery", 3, 0, 29)
                        },
                        Logic = new[]
                        {
                            new LogicSpec(
                                "event.fei.secondary.release.attack.0",
                                0,
                                0,
                                "payload.fei.secondary",
                                secondarySocket,
                                1)
                        },
                        Cues = new[]
                        {
                            new CueSpec(
                                "cue.fei.secondary.release.muzzle.0",
                                0,
                                1,
                                secondaryMuzzleCue,
                                secondarySocket,
                                "event.fei.secondary.release.attack.0"),
                            new CueSpec(
                                "cue.fei.secondary.release.tracer.0",
                                0,
                                2,
                                secondaryTracerCue,
                                secondarySocket,
                                "event.fei.secondary.release.attack.0"),
                            new CueSpec(
                                "cue.fei.secondary.release.target.0",
                                0,
                                3,
                                secondaryBurstCue,
                                string.Empty,
                                "event.fei.secondary.release.attack.0")
                        }
                    },
                    new SequenceSpec
                    {
                        Kind = 5,
                        Duration = 0,
                        Animation = cancelAnimation
                    }
                });

            ConfigurePlayerSkill(
                reload,
                "fei.reload",
                "Fei Reload",
                "Migrated reload. The magazine commit is an endpoint event at Tick 84.",
                0,
                new PlayerPayloadSpec
                {
                    SlotId = "payload.fei.reload",
                    Kind = 3,
                    AmmoCost = 0,
                    BaseDamage = 0,
                    BreakDamage = 0,
                    WeakpointDamage = 10000,
                    WeakpointBreak = 10000,
                    QueryMode = 0,
                    PelletCount = 1,
                    AdditionalPenetration = 0,
                    AreaCombatantLimit = 1,
                    AreaProjectileLimit = 0,
                    AllowedTargetKinds = 0
                },
                new[]
                {
                    new SequenceSpec
                    {
                        Kind = 1,
                        Duration = reloadDuration,
                        Animation = reloadAnimation,
                        Phases = new[]
                        {
                            new PhaseSpec(
                                "startup",
                                1,
                                0,
                                reloadDuration),
                            new PhaseSpec(
                                "active",
                                2,
                                reloadDuration,
                                reloadDuration)
                        },
                        Logic = new[]
                        {
                            new LogicSpec(
                                "event.fei.reload.commit.0",
                                reloadDuration,
                                0,
                                "payload.fei.reload",
                                string.Empty,
                                3)
                        },
                        Cues = new[]
                        {
                            new CueSpec(
                                "cue.fei.reload.ready.0",
                                reloadDuration,
                                1,
                                "animation." + reloadReadyAnimation,
                                string.Empty,
                                "event.fei.reload.commit.0")
                        }
                    }
                });

            ConfigurePlayerMetadata(
                primary,
                0,
                0,
                weaponAsset,
                "primaryPresentation",
                "shotPresentation");
            ConfigurePlayerMetadata(
                secondary,
                0,
                secondaryMinimumCharge,
                weaponAsset,
                "secondaryPresentation",
                "secondaryPresentation");
            ConfigurePlayerMetadata(
                reload,
                0,
                0,
                weaponAsset,
                "reloadPresentation",
                "reloadPresentation");

            
            SetObject(weapon, "primarySkill", primary);
            SetObject(weapon, "secondarySkill", secondary);
            SetObject(weapon, "reloadSkill", reload);
            weapon.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weaponAsset);

            ValidateSkill(primary, errors);
            ValidateSkill(secondary, errors);
            ValidateSkill(reload, errors);
            messages.Add("Migrated Fei Primary, Secondary and Reload assets.");
        }

        private static void MigrateEnemies(
            List<string> messages,
            List<string> errors)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:FpgEnemyAttackDefinition",
                new[] { "Assets/FPGDemo/Config/FormalEncounter" });
            Array.Sort(guids, StringComparer.Ordinal);

            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                UnityEngine.Object attack =
                    AssetDatabase.LoadMainAssetAtPath(path);
                if (attack == null)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(attack);
                serialized.Update();

                if (IsMigratedTimeline(serialized))
                {
                    ValidateMigratedAsset(attack, path, errors);
                    messages.Add(
                        "Enemy skill already migrated; preserved GUID and authored timeline: "
                        + path);
                    continue;
                }


                string attackId = ReadString(serialized, "attackId", attack.name);
                int legacyKind = ReadInt(serialized, "kind", 0);
                int telegraph = ReadInt(serialized, "telegraphTicks", 0);
                int windup = ReadInt(serialized, "windupTicks", 0);
                int recovery = ReadInt(serialized, "recoveryTicks", 0);
                int attackTick = checked(telegraph + windup);
                int duration = checked(attackTick + recovery);
                int cooldown = ReadInt(serialized, "cooldownTicks", 0);
                int damage = ReadInt(serialized, "damage", 0);
                int breakDamage = ReadInt(serialized, "breakDamage", 0);
                int projectileCount = ReadInt(
                    serialized,
                    "projectileCount",
                    1);
                int projectileDefinitionId = ReadInt(
                    serialized,
                    "projectileDefinitionId",
                    1);
                int projectileFlightTicks = ReadInt(
                    serialized,
                    "projectileFlightTicks",
                    30);
                int projectileLifetimeTicks = ReadInt(
                    serialized,
                    "projectileLifetimeTicks",
                    45);
                bool interceptable = ReadBool(
                    serialized,
                    "interceptable",
                    false);
                string animation = ReadString(
                    serialized,
                    "animationSlot",
                    "attack");
                string warning = ReadString(
                    serialized,
                    "warningSlot",
                    "enemy-warning");
                int ownerOutcome = ReadInt(
                    serialized,
                    "summonOwnerOutcome",
                    0);
                UnityEngine.Object summon = ReadObject(
                    serialized,
                    "summon");
                CatalogSpec runtime =
                    CatalogSpec.Default(projectileDefinitionId);

                SetString(serialized, "skillId", attackId);
                SetInt(serialized, "sequenceCooldownTicks", cooldown);
                SetString(
                    serialized,
                    "designerNotes",
                    "Migrated to the unified 60Hz skill timeline. "
                    + "Legacy attack tick was telegraph + windup.");

                SerializedProperty payloads =
                    Require(serialized, "payloadSlots");
                payloads.arraySize = 1;
                SerializedProperty payload =
                    payloads.GetArrayElementAtIndex(0);
                string slotId = "payload." + attackId;
                SetString(payload, "displayName", attackId + " Payload");
                SetString(payload, "slotId", slotId);
                SetInt(payload, "kind", legacyKind + 1);
                SetInt(
                    payload,
                    "threatDefinitionId",
                    runtime.ThreatDefinitionId);
                SetInt(payload, "baseDamage", damage);
                SetInt(payload, "breakDamage", breakDamage);
                SetInt(
                    payload,
                    "weakpointDamageMultiplierBasisPoints",
                    runtime.WeakpointDamage);
                SetInt(
                    payload,
                    "weakpointBreakMultiplierBasisPoints",
                    runtime.WeakpointBreak);
                SetInt(
                    payload,
                    "projectileDefinitionId",
                    projectileDefinitionId);
                SetInt(payload, "projectileCount", projectileCount);
                SetInt(
                    payload,
                    "projectileFlightTicks",
                    projectileFlightTicks);
                SetInt(
                    payload,
                    "projectileLifetimeTicks",
                    projectileLifetimeTicks);
                SetInt(
                    payload,
                    "projectileMaxHitPoints",
                    runtime.ProjectileMaxHitPoints);
                SetBool(
                    payload,
                    "projectileInterceptable",
                    interceptable);
                SetInt(
                    payload,
                    "projectileBudgetUnits",
                    runtime.ProjectileBudgetUnits);
                SetInt(
                    payload,
                    "projectilePresentationKey",
                    runtime.ProjectilePresentationKey);
                SetInt(
                    payload,
                    "projectileSweepRadiusKey",
                    runtime.ProjectileSweepRadiusKey);
                SetInt(payload, "timedImpactTargetPolicy", 0);
                SetInt(
                    payload,
                    "timedImpactDelayTicks",
                    runtime.TimedImpactDelayTicks);
                SetInt(
                    payload,
                    "timedImpactPresentationKey",
                    runtime.TimedImpactPresentationKey);

                if (legacyKind == 2)
                {
                    if (summon == null)
                    {
                        errors.Add("Summon attack has no source action: " + path);
                    }
                    else
                    {
                        CopySummon(payload, summon, ownerOutcome);
                    }
                }
                else
                {
                    ClearSummon(payload);
                }

                SequenceSpec sequence = new SequenceSpec
                {
                    Kind = 1,
                    Duration = duration,
                    Animation = animation,
                    Phases = CreateEnemyPhases(attackTick, duration),
                    Logic = new[]
                    {
                        new LogicSpec(
                            "event." + attackId + ".attack.0",
                            attackTick,
                            0,
                            slotId,
                            string.Empty,
                            2)
                    },
                    Warnings = new[]
                    {
                        new WarningSpec(
                            "warning." + attackId,
                            warning,
                            0,
                            attackTick,
                            0,
                            string.Empty)
                    }
                };
                WriteSequences(serialized, new[] { sequence });
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(attack);
                ValidateSkill(attack, errors);
                messages.Add("Migrated enemy skill in place: " + path);
            }
        }

        private static PhaseSpec[] CreateEnemyPhases(
            int attackTick,
            int duration)
        {
            List<PhaseSpec> phases = new List<PhaseSpec>(3);
            if (attackTick > 0)
            {
                phases.Add(new PhaseSpec(
                    "startup",
                    1,
                    0,
                    attackTick));
            }

            phases.Add(new PhaseSpec(
                "active",
                2,
                attackTick,
                attackTick));
            if (duration > attackTick)
            {
                phases.Add(new PhaseSpec(
                    "recovery",
                    3,
                    attackTick,
                    duration));
            }

            return phases.ToArray();
        }

        private static void ConfigurePlayerSkill(
            UnityEngine.Object asset,
            string skillId,
            string displayName,
            string notes,
            int cooldown,
            PlayerPayloadSpec payloadSpec,
            SequenceSpec[] sequences)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.Update();
            SetString(serialized, "skillId", skillId);
            SetString(serialized, "displayName", displayName);
            SetString(serialized, "designerNotes", notes);
            SetInt(serialized, "sequenceCooldownTicks", cooldown);

            SerializedProperty payloads =
                Require(serialized, "payloadSlots");
            payloads.arraySize = 1;
            SerializedProperty payload =
                payloads.GetArrayElementAtIndex(0);
            SetString(payload, "displayName", displayName + " Payload");
            SetString(payload, "slotId", payloadSpec.SlotId);
            SetInt(payload, "kind", payloadSpec.Kind);
            SetInt(payload, "ammoCost", payloadSpec.AmmoCost);
            SetInt(payload, "baseDamage", payloadSpec.BaseDamage);
            SetInt(payload, "breakDamage", payloadSpec.BreakDamage);
            SetInt(
                payload,
                "weakpointDamageMultiplierBasisPoints",
                payloadSpec.WeakpointDamage);
            SetInt(
                payload,
                "weakpointBreakMultiplierBasisPoints",
                payloadSpec.WeakpointBreak);
            SetInt(payload, "queryMode", payloadSpec.QueryMode);
            SetInt(payload, "pelletCount", payloadSpec.PelletCount);
            SetInt(
                payload,
                "additionalPenetrationCount",
                payloadSpec.AdditionalPenetration);
            SetInt(
                payload,
                "areaCombatantLimit",
                payloadSpec.AreaCombatantLimit);
            SetInt(
                payload,
                "areaProjectileLimit",
                payloadSpec.AreaProjectileLimit);
            SetInt(
                payload,
                "allowedTargetKinds",
                payloadSpec.AllowedTargetKinds);

            WriteSequences(serialized, sequences);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void WriteSequences(
            SerializedObject serialized,
            SequenceSpec[] specs)
        {
            SerializedProperty sequences =
                Require(serialized, "sequences");
            sequences.arraySize = specs.Length;
            for (int index = 0; index < specs.Length; index++)
            {
                WriteSequence(
                    sequences.GetArrayElementAtIndex(index),
                    specs[index]);
            }
        }

        private static void WriteSequence(
            SerializedProperty sequence,
            SequenceSpec spec)
        {
            SetInt(sequence, "kind", spec.Kind);
            SetInt(sequence, "durationTicks", spec.Duration);
            SetString(sequence, "mainAnimation", spec.Animation);
            SetStringArray(
                Require(sequence, "alternateAnimations"),
                spec.AlternateAnimations ?? Array.Empty<string>());
            SetBool(sequence, "loop", spec.Loop);
            SetInt(sequence, "animationPlaybackMode", 0);
            SetInt(sequence, "animationStartTick", 0);
            SetInt(sequence, "animationEndTick", spec.Duration);

            PhaseSpec[] phases = spec.Phases ?? Array.Empty<PhaseSpec>();
            SerializedProperty phaseArray = Require(sequence, "phases");
            phaseArray.arraySize = phases.Length;
            for (int index = 0; index < phases.Length; index++)
            {
                SerializedProperty phase =
                    phaseArray.GetArrayElementAtIndex(index);
                SetString(phase, "phaseId", phases[index].Id);
                SetInt(phase, "kind", phases[index].Kind);
                SetInt(phase, "startTick", phases[index].Start);
                SetInt(phase, "endTick", phases[index].End);
            }

            LogicSpec[] logic = spec.Logic ?? Array.Empty<LogicSpec>();
            SerializedProperty logicArray =
                Require(sequence, "logicEvents");
            logicArray.arraySize = logic.Length;
            for (int index = 0; index < logic.Length; index++)
            {
                SerializedProperty value =
                    logicArray.GetArrayElementAtIndex(index);
                SetString(value, "eventId", logic[index].EventId);
                SetInt(value, "tick", logic[index].Tick);
                SetString(
                    value,
                    "payloadSlotId",
                    logic[index].PayloadSlotId);
                SetInt(
                    value,
                    "authoredOrdinal",
                    logic[index].Ordinal);
                SetString(value, "socketId", logic[index].SocketId);
                SetInt(
                    value,
                    "targetSource",
                    logic[index].TargetSource);
                SetVector3(value, "targetOffset", Vector3.zero);
            }

            CueSpec[] cues = spec.Cues ?? Array.Empty<CueSpec>();
            SerializedProperty cueArray =
                Require(sequence, "presentationCues");
            cueArray.arraySize = cues.Length;
            for (int index = 0; index < cues.Length; index++)
            {
                SerializedProperty value =
                    cueArray.GetArrayElementAtIndex(index);
                SetString(value, "eventId", cues[index].EventId);
                SetInt(value, "tick", cues[index].Tick);
                SetString(value, "cueId", cues[index].CueId);
                SetInt(
                    value,
                    "authoredOrdinal",
                    cues[index].Ordinal);
                SetString(value, "socketId", cues[index].SocketId);
                SetString(
                    value,
                    "bindGameplayEventId",
                    cues[index].BoundGameplayEventId);
            }

            WarningSpec[] warnings =
                spec.Warnings ?? Array.Empty<WarningSpec>();
            SerializedProperty warningArray =
                Require(sequence, "warnings");
            warningArray.arraySize = warnings.Length;
            for (int index = 0; index < warnings.Length; index++)
            {
                SerializedProperty value =
                    warningArray.GetArrayElementAtIndex(index);
                SetString(value, "eventId", warnings[index].EventId);
                SetString(value, "warningId", warnings[index].WarningId);
                SetInt(value, "startTick", warnings[index].Start);
                SetInt(value, "endTick", warnings[index].End);
                SetInt(
                    value,
                    "authoredOrdinal",
                    warnings[index].Ordinal);
                SetString(value, "socketId", warnings[index].SocketId);
            }
        }

        private static void CopySummon(
            SerializedProperty payload,
            UnityEngine.Object summon,
            int ownerOutcome)
        {
            SerializedObject source = new SerializedObject(summon);
            source.Update();

            CopyObjectArray(
                Require(source, "candidateEnemies"),
                Require(payload, "summonCandidates"));
            CopyIntArray(
                Require(source, "candidateWeights"),
                Require(payload, "summonCandidateWeights"));
            SetInt(
                payload,
                "summonOccupancyMode",
                ReadInt(source, "occupancyMode", 0));
            SetInt(
                payload,
                "summonPlacementMode",
                ReadInt(source, "placementMode", 0));
            SetInt(payload, "summonOwnerOutcome", ownerOutcome);
            SetInt(
                payload,
                "maxSummonsPerOwner",
                ReadInt(source, "maxSummonsPerOwner", 0));
            SetInt(
                payload,
                "maxTotalSummonsPerEncounter",
                ReadInt(source, "maxTotalSummonsPerEncounter", 0));
            SetInt(
                payload,
                "maxSummonRecursionDepth",
                ReadInt(source, "maxRecursionDepth", 0));
        }

        private static void ClearSummon(SerializedProperty payload)
        {
            Require(payload, "summonCandidates").arraySize = 0;
            Require(payload, "summonCandidateWeights").arraySize = 0;
            SetInt(payload, "summonOccupancyMode", 0);
            SetInt(payload, "summonPlacementMode", 0);
            SetInt(payload, "summonOwnerOutcome", 0);
            SetInt(payload, "maxSummonsPerOwner", 0);
            SetInt(payload, "maxTotalSummonsPerEncounter", 0);
            SetInt(payload, "maxSummonRecursionDepth", 0);
        }

        private static UnityEngine.Object LoadOrCreateSkill(
            string path,
            string typeName,
            List<string> errors)
        {
            Type type = FindType(typeName);
            if (type == null)
            {
                errors.Add("Could not resolve skill type: " + typeName);
                return null;
            }

            UnityEngine.Object existing =
                AssetDatabase.LoadAssetAtPath(path, type);
            if (existing != null)
            {
                return existing;
            }

            ScriptableObject created =
                ScriptableObject.CreateInstance(type);
            if (created == null)
            {
                errors.Add("Could not create skill asset: " + path);
                return null;
            }

            created.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void ValidateSkill(
            UnityEngine.Object asset,
            List<string> errors)
        {
            MethodInfo method = asset.GetType().GetMethod(
                "TryValidate",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string).MakeByRefType() },
                null);
            if (method == null)
            {
                errors.Add(asset.name + " has no TryValidate method.");
                return;
            }

            object[] arguments = { null };
            bool valid = (bool)method.Invoke(asset, arguments);
            if (!valid)
            {
                errors.Add(
                    asset.name + " validation failed: "
                    + (arguments[0] as string ?? "unknown error"));
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static SerializedProperty Require(
            SerializedObject serialized,
            string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.name
                    + " is missing serialized property '" + name + "'.");
            }

            return property;
        }

        private static SerializedProperty Require(
            SerializedProperty parent,
            string name)
        {
            SerializedProperty property =
                parent.FindPropertyRelative(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    parent.propertyPath
                    + " is missing serialized property '" + name + "'.");
            }

            return property;
        }

        private static int ReadInt(
            SerializedObject serialized,
            string name,
            int fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? fallback : property.intValue;
        }

        private static int ReadInt(
            SerializedProperty parent,
            string name,
            int fallback)
        {
            SerializedProperty property =
                parent == null ? null : parent.FindPropertyRelative(name);
            return property == null ? fallback : property.intValue;
        }

        private static bool ReadBool(
            SerializedObject serialized,
            string name,
            bool fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null ? fallback : property.boolValue;
        }

        private static string ReadString(
            SerializedObject serialized,
            string name,
            string fallback)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null || string.IsNullOrWhiteSpace(
                    property.stringValue)
                ? fallback
                : property.stringValue;
        }

        private static string ReadString(
            SerializedProperty parent,
            string name,
            string fallback)
        {
            SerializedProperty property =
                parent == null ? null : parent.FindPropertyRelative(name);
            return property == null || string.IsNullOrWhiteSpace(
                    property.stringValue)
                ? fallback
                : property.stringValue;
        }

        private static UnityEngine.Object ReadObject(
            SerializedObject serialized,
            string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property == null
                ? null
                : property.objectReferenceValue;
        }

        private static void SetInt(
            SerializedObject serialized,
            string name,
            int value)
        {
            Require(serialized, name).intValue = value;
        }

        private static void SetInt(
            SerializedProperty parent,
            string name,
            int value)
        {
            Require(parent, name).intValue = value;
        }

        private static void SetBool(
            SerializedProperty parent,
            string name,
            bool value)
        {
            Require(parent, name).boolValue = value;
        }

        private static void SetString(
            SerializedObject serialized,
            string name,
            string value)
        {
            Require(serialized, name).stringValue = value ?? string.Empty;
        }

        private static void SetString(
            SerializedProperty parent,
            string name,
            string value)
        {
            Require(parent, name).stringValue = value ?? string.Empty;
        }

        private static void SetVector3(
            SerializedProperty parent,
            string name,
            Vector3 value)
        {
            Require(parent, name).vector3Value = value;
        }

        private static void SetObject(
            SerializedObject serialized,
            string name,
            UnityEngine.Object value)
        {
            Require(serialized, name).objectReferenceValue = value;
        }

        private static void SetStringArray(
            SerializedProperty array,
            string[] values)
        {
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                array.GetArrayElementAtIndex(index).stringValue =
                    values[index] ?? string.Empty;
            }
        }

        private static void CopyObjectArray(
            SerializedProperty source,
            SerializedProperty destination)
        {
            destination.arraySize = source.arraySize;
            for (int index = 0; index < source.arraySize; index++)
            {
                destination.GetArrayElementAtIndex(index)
                    .objectReferenceValue = source.GetArrayElementAtIndex(index)
                    .objectReferenceValue;
            }
        }

        private static void CopyIntArray(
            SerializedProperty source,
            SerializedProperty destination)
        {
            destination.arraySize = source.arraySize;
            for (int index = 0; index < source.arraySize; index++)
            {
                destination.GetArrayElementAtIndex(index).intValue =
                    source.GetArrayElementAtIndex(index).intValue;
            }
        }

        private sealed class SequenceSpec
        {
            public int Kind;
            public int Duration;
            public string Animation = "idle";
            public string[] AlternateAnimations = Array.Empty<string>();
            public bool Loop;
            public PhaseSpec[] Phases = Array.Empty<PhaseSpec>();
            public LogicSpec[] Logic = Array.Empty<LogicSpec>();
            public CueSpec[] Cues = Array.Empty<CueSpec>();
            public WarningSpec[] Warnings = Array.Empty<WarningSpec>();
        }

        private readonly struct PhaseSpec
        {
            public PhaseSpec(
                string id,
                int kind,
                int start,
                int end)
            {
                Id = id;
                Kind = kind;
                Start = start;
                End = end;
            }

            public string Id { get; }
            public int Kind { get; }
            public int Start { get; }
            public int End { get; }
        }

        private readonly struct LogicSpec
        {
            public LogicSpec(
                string eventId,
                int tick,
                int ordinal,
                string payloadSlotId,
                string socketId,
                int targetSource)
            {
                EventId = eventId;
                Tick = tick;
                Ordinal = ordinal;
                PayloadSlotId = payloadSlotId;
                SocketId = socketId;
                TargetSource = targetSource;
            }

            public string EventId { get; }
            public int Tick { get; }
            public int Ordinal { get; }
            public string PayloadSlotId { get; }
            public string SocketId { get; }
            public int TargetSource { get; }
        }

        private readonly struct CueSpec
        {
            public CueSpec(
                string eventId,
                int tick,
                int ordinal,
                string cueId,
                string socketId,
                string boundGameplayEventId = "")
            {
                EventId = eventId;
                Tick = tick;
                Ordinal = ordinal;
                CueId = cueId;
                SocketId = socketId;
                BoundGameplayEventId = boundGameplayEventId;
            }

            public string EventId { get; }
            public int Tick { get; }
            public int Ordinal { get; }
            public string CueId { get; }
            public string SocketId { get; }
            public string BoundGameplayEventId { get; }
        }

        private readonly struct WarningSpec
        {
            public WarningSpec(
                string eventId,
                string warningId,
                int start,
                int end,
                int ordinal,
                string socketId)
            {
                EventId = eventId;
                WarningId = warningId;
                Start = start;
                End = end;
                Ordinal = ordinal;
                SocketId = socketId;
            }

            public string EventId { get; }
            public string WarningId { get; }
            public int Start { get; }
            public int End { get; }
            public int Ordinal { get; }
            public string SocketId { get; }
        }

        private struct PlayerPayloadSpec
        {
            public string SlotId;
            public int Kind;
            public int AmmoCost;
            public int BaseDamage;
            public int BreakDamage;
            public int WeakpointDamage;
            public int WeakpointBreak;
            public int QueryMode;
            public int PelletCount;
            public int AdditionalPenetration;
            public int AreaCombatantLimit;
            public int AreaProjectileLimit;
            public int AllowedTargetKinds;
        }

        private struct CatalogSpec
        {
            public int ThreatDefinitionId;
            public int WeakpointDamage;
            public int WeakpointBreak;
            public int ProjectileMaxHitPoints;
            public int ProjectileBudgetUnits;
            public int ProjectilePresentationKey;
            public int ProjectileSweepRadiusKey;
            public int TimedImpactDelayTicks;
            public int TimedImpactPresentationKey;

            public static CatalogSpec Default(int definitionId)
            {
                return new CatalogSpec
                {
                    ThreatDefinitionId = Math.Max(1, definitionId),
                    WeakpointDamage = 10000,
                    WeakpointBreak = 10000,
                    ProjectileMaxHitPoints = 0,
                    ProjectileBudgetUnits = 1,
                    ProjectilePresentationKey = 1,
                    ProjectileSweepRadiusKey = 1,
                    TimedImpactDelayTicks = 0,
                    TimedImpactPresentationKey = 1
                };
            }
        }

        private static void ConfigurePlayerMetadata(
            UnityEngine.Object skillAsset,
            int triggerMode,
            int minimumChargeTicks,
            UnityEngine.Object sourceAsset,
            string sourcePresentationField,
            string destinationPresentationField)
        {
            SerializedObject serialized = new SerializedObject(skillAsset);
            serialized.Update();
            SetInt(serialized, "secondaryTriggerMode", triggerMode);
            SetInt(serialized, "minimumChargeTicks", minimumChargeTicks);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            FieldInfo source = sourceAsset.GetType().GetField(
                sourcePresentationField,
                flags);
            FieldInfo destination = skillAsset.GetType().GetField(
                destinationPresentationField,
                flags);
            if (source == null || destination == null)
            {
                throw new InvalidOperationException(
                    "Could not migrate player presentation field '"
                    + sourcePresentationField + "'.");
            }

            destination.SetValue(skillAsset, source.GetValue(sourceAsset));
            EditorUtility.SetDirty(skillAsset);
        }

        private static bool HasMigratedFeiSkills(
            SerializedObject weapon,
            List<string> errors)
        {
            string[] propertyNames =
            {
                "primarySkill",
                "secondarySkill",
                "reloadSkill"
            };
            UnityEngine.Object[] skills =
                new UnityEngine.Object[propertyNames.Length];
            for (int index = 0; index < propertyNames.Length; index++)
            {
                SerializedProperty property =
                    weapon.FindProperty(propertyNames[index]);
                if (property == null
                    || property.objectReferenceValue == null)
                {
                    return false;
                }

                skills[index] = property.objectReferenceValue;
            }

            for (int index = 0; index < skills.Length; index++)
            {
                ValidateMigratedAsset(
                    skills[index],
                    AssetDatabase.GetAssetPath(skills[index]),
                    errors);
            }

            return true;
        }

        private static bool IsMigratedTimeline(SerializedObject serialized)
        {
            SerializedProperty skillId = serialized.FindProperty("skillId");
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty payloadSlots =
                serialized.FindProperty("payloadSlots");
            return skillId != null
                && !string.IsNullOrWhiteSpace(skillId.stringValue)
                && sequences != null
                && sequences.isArray
                && sequences.arraySize > 0
                && payloadSlots != null
                && payloadSlots.isArray
                && payloadSlots.arraySize > 0;
        }

        private static void ValidateMigratedAsset(
            UnityEngine.Object asset,
            string path,
            List<string> errors)
        {
            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            MethodInfo method = asset.GetType().GetMethod(
                "TryValidate",
                flags,
                null,
                new[] { typeof(string).MakeByRefType() },
                null);
            if (method == null)
            {
                errors.Add(
                    "Migrated skill does not expose TryValidate: " + path);
                return;
            }

            object[] arguments = { string.Empty };
            bool valid = (bool)method.Invoke(asset, arguments);
            if (!valid)
            {
                errors.Add(
                    "Migrated skill is invalid: "
                    + path
                    + " - "
                    + (arguments[0] as string ?? "unknown error"));
            }
        }
}
}
