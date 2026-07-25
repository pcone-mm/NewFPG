using FPG.Demo.Combat;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillDefinitionTests
    {
        [Test]
        public void PlayerSkillCompilesTimelineEventsAtSequenceEnd()
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.primary", "Primary");
                serialized.FindProperty("sequenceCooldownTicks").intValue = 15;
                ConfigurePelletPayload(
                    serialized.FindProperty("payloadSlots")
                        .GetArrayElementAtIndex(0),
                    "payload.primary",
                    2,
                    7,
                    3,
                    1);

                SerializedProperty sequences = serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty execute = sequences.GetArrayElementAtIndex(0);
                ConfigureSequence(
                    execute,
                    FpgSkillSequenceKind.Execute,
                    20,
                    "attack_play1",
                    false);
                ConfigureStandardPhases(execute, 5, 10, 20);
                ConfigureLogicEvent(
                    execute,
                    0,
                    "event.release",
                    20,
                    "payload.primary",
                    "weapon.primary.muzzle");
                ConfigureCue(
                    execute,
                    0,
                    "event.muzzle",
                    5,
                    "cue.player.muzzle",
                    "weapon.primary.muzzle");
                ConfigureWarning(
                    execute,
                    0,
                    "event.warning",
                    "warning.player.attack",
                    0,
                    20);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string validationError), Is.True,
                    validationError);
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                Assert.That(compiled.SequenceCooldownTicks, Is.EqualTo(15));
                Assert.That(compiled.Timeline.SequenceCount, Is.EqualTo(1));
                Assert.That(
                    compiled.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence sequence),
                    Is.True);
                Assert.That(sequence.DurationTicks, Is.EqualTo(20));
                Assert.That(sequence.EventCount, Is.EqualTo(4));
                Assert.That(sequence.PhaseCount, Is.EqualTo(3));
                FpgCompiledSkillPhase startup = sequence.GetPhase(0);
                FpgCompiledSkillPhase active = sequence.GetPhase(1);
                FpgCompiledSkillPhase recovery = sequence.GetPhase(2);
                Assert.That(startup.PhaseId, Is.GreaterThan(0));
                Assert.That(active.PhaseId, Is.GreaterThan(0));
                Assert.That(recovery.PhaseId, Is.GreaterThan(0));
                Assert.That(active.PhaseId, Is.Not.EqualTo(startup.PhaseId));
                Assert.That(recovery.PhaseId, Is.Not.EqualTo(active.PhaseId));
                Assert.That(startup.Kind, Is.EqualTo(FpgSkillPhaseKind.Startup));
                Assert.That(startup.StartTick, Is.Zero);
                Assert.That(startup.EndTick, Is.EqualTo(5));
                Assert.That(active.Kind, Is.EqualTo(FpgSkillPhaseKind.Active));
                Assert.That(active.StartTick, Is.EqualTo(5));
                Assert.That(active.EndTick, Is.EqualTo(10));
                Assert.That(recovery.Kind, Is.EqualTo(FpgSkillPhaseKind.Recovery));
                Assert.That(recovery.StartTick, Is.EqualTo(10));
                Assert.That(recovery.EndTick, Is.EqualTo(20));


                FpgCompiledSkillEvent payloadEvent = FindEvent(
                    sequence,
                    FpgSkillEventKind.GameplayPayload);
                Assert.That(payloadEvent.Tick, Is.EqualTo(sequence.DurationTicks));
                Assert.That(payloadEvent.PayloadSlotId, Is.GreaterThan(0));
                Assert.That(payloadEvent.SocketId, Is.GreaterThan(0));

                Assert.That(
                    compiled.TryGetPayloadSlot(
                        payloadEvent.PayloadSlotId,
                        out FpgCompiledPlayerSkillPayloadSlot payload),
                    Is.True);
                Assert.That(payload.Kind, Is.EqualTo(FpgPlayerSkillPayloadKind.PelletRay));
                Assert.That(payload.AmmoCost, Is.EqualTo(2));
                Assert.That(payload.Damage.BaseDamage, Is.EqualTo(7));
                Assert.That(payload.Damage.BreakDamage, Is.EqualTo(3));
                Assert.That(payload.QueryPolicy, Is.EqualTo(QueryPolicy.PelletRays));
                Assert.That(
                    payload.QueryMode,
                    Is.EqualTo(AttackQueryMode.FirstSurfacePenetration));
                Assert.That(payload.PayloadCount, Is.EqualTo(8));
                Assert.That(payload.MaxImpactCount, Is.EqualTo(16));

                Assert.That(
                    FindEvent(sequence, FpgSkillEventKind.WarningStarted).Tick,
                    Is.Zero);
                Assert.That(
                    FindEvent(sequence, FpgSkillEventKind.WarningEnded).Tick,
                    Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PlayerSkillSupportsEveryAuthoredSequenceKind()
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.charge", "Charge");
                ConfigurePelletPayload(
                    serialized.FindProperty("payloadSlots")
                        .GetArrayElementAtIndex(0),
                    "payload.release",
                    1,
                    4,
                    2,
                    0);

                FpgSkillSequenceKind[] kinds =
                {
                    FpgSkillSequenceKind.Execute,
                    FpgSkillSequenceKind.ChargeEnter,
                    FpgSkillSequenceKind.ChargeLoop,
                    FpgSkillSequenceKind.Release,
                    FpgSkillSequenceKind.Cancel
                };
                SerializedProperty sequences = serialized.FindProperty("sequences");
                sequences.arraySize = kinds.Length;
                for (int index = 0; index < kinds.Length; index++)
                {
                    SerializedProperty sequence = sequences.GetArrayElementAtIndex(index);
                    ConfigureSequence(
                        sequence,
                        kinds[index],
                        6 + index,
                        "animation_" + kinds[index].ToString().ToLowerInvariant(),
                        kinds[index] == FpgSkillSequenceKind.ChargeLoop);
                }

                ConfigureLogicEvent(
                    sequences.GetArrayElementAtIndex(3),
                    0,
                    "event.release",
                    9,
                    "payload.release",
                    string.Empty);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition compiled,
                        out string error),
                    Is.True,
                    error);
                Assert.That(compiled.Timeline.SequenceCount, Is.EqualTo(kinds.Length));
                for (int index = 0; index < kinds.Length; index++)
                {
                    Assert.That(
                        compiled.Timeline.TryGetSequence(kinds[index], out _),
                        Is.True,
                        kinds[index].ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PlayerSkillRejectsMissingExecuteAndMissingPayloadReferences()
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.invalid", "Invalid");
                ConfigurePelletPayload(
                    serialized.FindProperty("payloadSlots")
                        .GetArrayElementAtIndex(0),
                    "payload.valid",
                    1,
                    1,
                    0,
                    0);
                SerializedProperty sequences = serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty release = sequences.GetArrayElementAtIndex(0);
                ConfigureSequence(
                    release,
                    FpgSkillSequenceKind.Release,
                    1,
                    "release",
                    false);
                ConfigureLogicEvent(
                    release,
                    0,
                    "event.release",
                    1,
                    "payload.missing",
                    string.Empty);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string missingSlotError), Is.False);
                Assert.That(missingSlotError, Does.Contain("missing payload slot"));

                ConfigureLogicEvent(
                    release,
                    0,
                    "event.release",
                    1,
                    "payload.valid",
                    string.Empty);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(skill.TryValidate(out string executeError), Is.False);
                Assert.That(executeError, Does.Contain("Execute"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PlayerSkillRejectsDuplicateEventAndPayloadSlotIds()
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.duplicates", "Duplicates");
                SerializedProperty payloads = serialized.FindProperty("payloadSlots");
                payloads.arraySize = 2;
                ConfigurePelletPayload(
                    payloads.GetArrayElementAtIndex(0),
                    "payload.same",
                    1,
                    1,
                    0,
                    0);
                ConfigurePelletPayload(
                    payloads.GetArrayElementAtIndex(1),
                    "payload.same",
                    1,
                    1,
                    0,
                    0);
                ConfigureValidExecute(serialized, "payload.same");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string slotError), Is.False);
                Assert.That(slotError, Does.Contain("repeats payload slot"));

                payloads.arraySize = 1;
                SerializedProperty execute = serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0);
                ConfigureCue(
                    execute,
                    0,
                    "event.execute",
                    0,
                    "cue.same",
                    string.Empty);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string eventError), Is.False);
                Assert.That(eventError, Does.Contain("repeats event ID"));

                SerializedProperty cue = execute.FindPropertyRelative("presentationCues")
                    .GetArrayElementAtIndex(0);
                cue.FindPropertyRelative("eventId").stringValue = "event.cue";
                cue.FindPropertyRelative("tick").intValue = 1;
                cue.FindPropertyRelative("authoredOrdinal").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string ordinalError), Is.False);
                Assert.That(ordinalError, Does.Contain("authored ordinal"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PayloadDisplayNameDoesNotChangeStableReferencesOrGameplayHash()
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "player.payload-name", "Payload Name");
                SerializedProperty payload = serialized.FindProperty("payloadSlots")
                    .GetArrayElementAtIndex(0);
                ConfigurePelletPayload(
                    payload,
                    "payload.stable",
                    1,
                    4,
                    4,
                    0);
                ConfigureValidExecute(serialized, "payload.stable");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition before,
                        out string beforeError),
                    Is.True,
                    beforeError);
                Assert.That(
                    before.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence beforeSequence),
                    Is.True);
                int beforePayloadId = FindEvent(
                    beforeSequence,
                    FpgSkillEventKind.GameplayPayload).PayloadSlotId;

                serialized.Update();
                payload = serialized.FindProperty("payloadSlots")
                    .GetArrayElementAtIndex(0);
                payload.FindPropertyRelative("displayName").stringValue =
                    "Renamed Authoring Label";
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition after,
                        out string afterError),
                    Is.True,
                    afterError);
                Assert.That(
                    after.Timeline.TryGetSequence(
                        FpgSkillSequenceKind.Execute,
                        out FpgCompiledSkillSequence afterSequence),
                    Is.True);
                Assert.That(
                    FindEvent(
                        afterSequence,
                        FpgSkillEventKind.GameplayPayload).PayloadSlotId,
                    Is.EqualTo(beforePayloadId));
                Assert.That(after.GameplayHash, Is.EqualTo(before.GameplayHash));
                Assert.That(skill.PayloadSlots[0].DisplayName,
                    Is.EqualTo("Renamed Authoring Label"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void PlayerPayloadSlotsCompileCurrentQueryShapes()
        {
            FpgPlayerSkillDefinition skill = CreateSkill();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                serialized.FindProperty("secondaryTriggerMode").enumValueIndex =
                    (int)SecondaryTriggerMode.ImmediateRepeatWhileHeld;
                ConfigureIdentity(serialized, "player.payloads", "Payloads");
                SerializedProperty payloads = serialized.FindProperty("payloadSlots");
                payloads.arraySize = 3;
                ConfigurePelletPayload(
                    payloads.GetArrayElementAtIndex(0),
                    "payload.pellet",
                    1,
                    4,
                    4,
                    0);
                ConfigureAreaPayload(
                    payloads.GetArrayElementAtIndex(1),
                    "payload.area",
                    2,
                    28,
                    20,
                    4,
                    3);
                ConfigureReloadPayload(
                    payloads.GetArrayElementAtIndex(2),
                    "payload.reload");

                SerializedProperty sequences = serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty execute = sequences.GetArrayElementAtIndex(0);
                ConfigureSequence(
                    execute,
                    FpgSkillSequenceKind.Execute,
                    2,
                    "payload_test",
                    false);
                ConfigureLogicEvent(
                    execute,
                    0,
                    "event.pellet",
                    0,
                    "payload.pellet",
                    string.Empty);
                ConfigureLogicEvent(
                    execute,
                    1,
                    "event.area",
                    1,
                    "payload.area",
                    string.Empty);
                ConfigureLogicEvent(
                    execute,
                    2,
                    "event.reload",
                    2,
                    "payload.reload",
                    string.Empty);
                execute.FindPropertyRelative("logicEvents")
                    .GetArrayElementAtIndex(2)
                    .FindPropertyRelative("targetSource").enumValueIndex =
                    (int)FpgSkillTargetSource.Self;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledPlayerSkillDefinition compiled,
                        out string error),
                    Is.True,
                    error);
                Assert.That(compiled.PayloadSlotCount, Is.EqualTo(3));

                FpgCompiledPlayerSkillPayloadSlot area =
                    FindPayload(compiled, FpgPlayerSkillPayloadKind.AreaAtFirstSurface);
                Assert.That(area.QueryPolicy, Is.EqualTo(QueryPolicy.DirectThenArea));
                Assert.That(area.QueryMode, Is.EqualTo(AttackQueryMode.AreaAtFirstSurface));
                Assert.That(area.AreaCombatantLimit, Is.EqualTo(4));
                Assert.That(area.AreaProjectileLimit, Is.EqualTo(3));
                Assert.That(area.MaxImpactCount, Is.EqualTo(7));

                FpgCompiledPlayerSkillPayloadSlot reload =
                    FindPayload(compiled, FpgPlayerSkillPayloadKind.ReloadCommit);
                Assert.That(reload.AmmoCost, Is.Zero);
                Assert.That(reload.QueryPolicy, Is.EqualTo(QueryPolicy.None));
                Assert.That(reload.QueryMode, Is.EqualTo(AttackQueryMode.Legacy));
                Assert.That(reload.AllowedTargetKinds, Is.EqualTo(AttackTargetKinds.None));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void EnemySkillCompilesTypedPayloadsAndExecuteCapacities()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            FpgEnemyDefinition candidate =
                ScriptableObject.CreateInstance<FpgEnemyDefinition>();
            try
            {
                ConfigureEnemyIdentity(candidate, "enemy.summoned");

                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "enemy.combo", "Enemy Combo");
                serialized.FindProperty("sequenceCooldownTicks").intValue = 45;

                SerializedProperty payloads = serialized.FindProperty("payloadSlots");
                payloads.arraySize = 3;
                ConfigureEnemyProjectilePayload(
                    payloads.GetArrayElementAtIndex(0),
                    "payload.volley",
                    101,
                    201,
                    3,
                    12,
                    4);
                ConfigureEnemyTimedImpactPayload(
                    payloads.GetArrayElementAtIndex(1),
                    "payload.impact",
                    102,
                    18,
                    7,
                    6);
                ConfigureEnemySummonPayload(
                    payloads.GetArrayElementAtIndex(2),
                    "payload.summon",
                    candidate,
                    5,
                    FpgSummonOccupancyMode.AdditionalEntity,
                    FpgSummonOwnerOutcome.RemainAlive,
                    2,
                    8);

                SerializedProperty sequences = serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty execute = sequences.GetArrayElementAtIndex(0);
                ConfigureSequence(
                    execute,
                    FpgSkillSequenceKind.Execute,
                    30,
                    "enemy_combo",
                    false);
                ConfigureLogicEvent(
                    execute,
                    0,
                    "event.volley.first",
                    4,
                    "payload.volley",
                    "enemy.muzzle");
                ConfigureLogicEvent(
                    execute,
                    1,
                    "event.impact",
                    10,
                    "payload.impact",
                    string.Empty);
                ConfigureLogicEvent(
                    execute,
                    2,
                    "event.volley.second",
                    20,
                    "payload.volley",
                    "enemy.muzzle");
                ConfigureLogicEvent(
                    execute,
                    3,
                    "event.summon",
                    30,
                    "payload.summon",
                    string.Empty);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string error),
                    Is.True,
                    error);
                Assert.That(compiled.SequenceCooldownTicks, Is.EqualTo(45));
                Assert.That(compiled.TotalProjectileCapacity, Is.EqualTo(6));
                Assert.That(compiled.TotalImpactCapacity, Is.EqualTo(7));
                Assert.That(compiled.TotalSummonCapacity, Is.EqualTo(1));
                Assert.That(compiled.MaxHitCount, Is.EqualTo(3));
                Assert.That(compiled.LastAttackTick, Is.EqualTo(30));

                FpgCompiledEnemySkillPayloadSlot projectile =
                    FindEnemyPayload(
                        compiled,
                        FpgEnemySkillPayloadKind.Projectile);
                Assert.That(projectile.ThreatDefinitionId, Is.EqualTo(101));
                Assert.That(projectile.ThreatPayload.IsSweptProjectile, Is.True);
                Assert.That(projectile.ThreatPayload.PayloadCount, Is.EqualTo(3));
                Assert.That(
                    projectile.ThreatPayload.ProjectileDefinition.DefinitionId,
                    Is.EqualTo(201));
                Assert.That(
                    projectile.ThreatPayload.ProjectileDefinition.DamageSpec.BaseDamage,
                    Is.EqualTo(12));

                FpgCompiledEnemySkillPayloadSlot impact =
                    FindEnemyPayload(
                        compiled,
                        FpgEnemySkillPayloadKind.TimedImpact);
                Assert.That(impact.ThreatDefinitionId, Is.EqualTo(102));
                Assert.That(impact.ThreatPayload.IsTimedImpact, Is.True);
                Assert.That(impact.ThreatPayload.ImpactDelay.Value, Is.EqualTo(6));
                Assert.That(impact.ThreatPayload.TimedImpactDamage.BreakDamage, Is.EqualTo(7));

                FpgCompiledEnemySkillPayloadSlot summon =
                    FindEnemyPayload(compiled, FpgEnemySkillPayloadKind.Summon);
                Assert.That(summon.SummonPayload, Is.Not.Null);
                Assert.That(summon.SummonPayload.ActionId, Is.EqualTo("payload.summon"));
                Assert.That(summon.SummonPayload.CandidateCount, Is.EqualTo(1));
                Assert.That(summon.SummonPayload.TotalCandidateWeight, Is.EqualTo(5UL));
                Assert.That(
                    summon.SummonPayload.GetCandidate(0).EnemyDefinitionId,
                    Is.EqualTo("enemy.summoned"));
            }
            finally
            {
                Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void EnemyGameplayHashIsStableAcrossAuthoringOrderAndExcludesLabels()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(
                    serialized,
                    "enemy.hash.contract",
                    "Enemy Hash Contract");
                serialized.FindProperty("priority").intValue = 3;
                serialized.FindProperty("firstReadyOffsetTicks").intValue = 4;
                serialized.FindProperty("sequenceCooldownTicks").intValue = 5;

                SerializedProperty payloads =
                    serialized.FindProperty("payloadSlots");
                payloads.arraySize = 2;
                ConfigureEnemyProjectilePayload(
                    payloads.GetArrayElementAtIndex(0),
                    "payload.beta",
                    202,
                    302,
                    1,
                    7,
                    2);
                ConfigureEnemyProjectilePayload(
                    payloads.GetArrayElementAtIndex(1),
                    "payload.alpha",
                    201,
                    301,
                    1,
                    5,
                    1);

                SerializedProperty sequences =
                    serialized.FindProperty("sequences");
                sequences.arraySize = 1;
                SerializedProperty execute =
                    sequences.GetArrayElementAtIndex(0);
                ConfigureSequence(
                    execute,
                    FpgSkillSequenceKind.Execute,
                    2,
                    "enemy_hash",
                    false);
                ConfigureLogicEvent(
                    execute,
                    0,
                    "event.alpha",
                    1,
                    "payload.alpha",
                    string.Empty);
                ConfigureLogicEvent(
                    execute,
                    1,
                    "event.beta",
                    2,
                    "payload.beta",
                    string.Empty);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition original,
                        out string originalError),
                    Is.True,
                    originalError);

                serialized.Update();
                serialized.FindProperty("displayName").stringValue =
                    "Renamed Skill Label";
                payloads = serialized.FindProperty("payloadSlots");
                payloads.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("displayName").stringValue =
                    "Renamed Beta Label";
                payloads.GetArrayElementAtIndex(1)
                    .FindPropertyRelative("displayName").stringValue =
                    "Renamed Alpha Label";
                payloads.MoveArrayElement(0, 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition reordered,
                        out string reorderedError),
                    Is.True,
                    reorderedError);
                Assert.That(
                    reordered.GameplayHash,
                    Is.EqualTo(original.GameplayHash));
                Assert.That(
                    reordered.PayloadSlots[0].SlotId,
                    Is.EqualTo(original.PayloadSlots[0].SlotId));
                Assert.That(
                    reordered.PayloadSlots[1].SlotId,
                    Is.EqualTo(original.PayloadSlots[1].SlotId));

                serialized.Update();
                payloads = serialized.FindProperty("payloadSlots");
                payloads.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("baseDamage").intValue = 6;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition changedDamage,
                        out string damageError),
                    Is.True,
                    damageError);
                Assert.That(
                    changedDamage.GameplayHash,
                    Is.Not.EqualTo(reordered.GameplayHash));

                serialized.Update();
                payloads = serialized.FindProperty("payloadSlots");
                payloads.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("slotId").stringValue =
                    "payload.alpha.copy";
                serialized.FindProperty("sequences")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("logicEvents")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("payloadSlotId").stringValue =
                    "payload.alpha.copy";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition copiedSlot,
                        out string copiedError),
                    Is.True,
                    copiedError);
                Assert.That(
                    copiedSlot.GameplayHash,
                    Is.Not.EqualTo(changedDamage.GameplayHash));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void EnemySkillFormalValidationDoesNotFallbackToLegacyFields()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            try
            {
                SerializedObject serialized = new SerializedObject(skill);
                serialized.FindProperty("payloadSlots").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string formalError), Is.False);
                Assert.That(formalError, Does.Contain("typed payload slot"));
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string compileError),
                    Is.False);
                Assert.That(compiled, Is.Null);
                Assert.That(compileError, Does.Contain("typed payload slot"));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void EnemySummonSlotEnforcesOccupancyOwnerOutcomeAndQuotaPairing()
        {
            FpgEnemyAttackDefinition skill =
                ScriptableObject.CreateInstance<FpgEnemyAttackDefinition>();
            FpgEnemyDefinition candidate =
                ScriptableObject.CreateInstance<FpgEnemyDefinition>();
            try
            {
                ConfigureEnemyIdentity(candidate, "enemy.replacement");

                SerializedObject serialized = new SerializedObject(skill);
                ConfigureIdentity(serialized, "enemy.replace", "Replace");
                SerializedProperty payload = serialized.FindProperty("payloadSlots")
                    .GetArrayElementAtIndex(0);
                ConfigureEnemySummonPayload(
                    payload,
                    "payload.replace",
                    candidate,
                    1,
                    FpgSummonOccupancyMode.AdditionalEntity,
                    FpgSummonOwnerOutcome.DieAfterSuccessfulSummon,
                    1,
                    4);
                ConfigureValidExecute(serialized, "payload.replace");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(skill.TryValidate(out string pairError), Is.False);
                Assert.That(pairError, Does.Contain("pair ReplaceOwner"));

                payload.FindPropertyRelative("summonOccupancyMode").enumValueIndex =
                    (int)FpgSummonOccupancyMode.ReplaceOwner;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(skill.TryValidate(out string quotaError), Is.False);
                Assert.That(quotaError, Does.Contain("quotas at zero"));

                payload.FindPropertyRelative("maxSummonsPerOwner").intValue = 0;
                payload.FindPropertyRelative("maxTotalSummonsPerEncounter").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(
                    skill.TryCompile(
                        out FpgCompiledEnemySkillDefinition compiled,
                        out string compileError),
                    Is.True,
                    compileError);
                Assert.That(compiled.TotalSummonCapacity, Is.EqualTo(1));
                Assert.That(
                    FindEnemyPayload(compiled, FpgEnemySkillPayloadKind.Summon)
                        .SummonPayload.OwnerOutcome,
                    Is.EqualTo(FpgSummonOwnerOutcome.DieAfterSuccessfulSummon));
            }
            finally
            {
                Object.DestroyImmediate(candidate);
                Object.DestroyImmediate(skill);
            }
        }

        private static FpgPlayerSkillDefinition CreateSkill()
        {
            return ScriptableObject.CreateInstance<FpgPlayerSkillDefinition>();
        }

        private static void ConfigureEnemyIdentity(
            FpgEnemyDefinition enemy,
            string enemyDefinitionId)
        {
            SerializedObject serialized = new SerializedObject(enemy);
            serialized.FindProperty("enemyDefinitionId").stringValue =
                enemyDefinitionId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEnemyProjectilePayload(
            SerializedProperty payload,
            string slotId,
            int threatDefinitionId,
            int projectileDefinitionId,
            int projectileCount,
            int damage,
            int breakDamage)
        {
            payload.FindPropertyRelative("displayName").stringValue = slotId;
            payload.FindPropertyRelative("slotId").stringValue = slotId;
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgEnemySkillPayloadKind.Projectile;
            payload.FindPropertyRelative("threatDefinitionId").intValue =
                threatDefinitionId;
            payload.FindPropertyRelative("baseDamage").intValue = damage;
            payload.FindPropertyRelative("breakDamage").intValue = breakDamage;
            payload.FindPropertyRelative(
                "weakpointDamageMultiplierBasisPoints").intValue = 12000;
            payload.FindPropertyRelative(
                "weakpointBreakMultiplierBasisPoints").intValue = 15000;
            payload.FindPropertyRelative("projectileDefinitionId").intValue =
                projectileDefinitionId;
            payload.FindPropertyRelative("projectileCount").intValue =
                projectileCount;
            payload.FindPropertyRelative("projectileFlightTicks").intValue = 20;
            payload.FindPropertyRelative("projectileLifetimeTicks").intValue = 30;
            payload.FindPropertyRelative("projectileMaxHitPoints").intValue = 2;
            payload.FindPropertyRelative("projectileInterceptable").boolValue = true;
            payload.FindPropertyRelative("projectileBudgetUnits").intValue = 2;
            payload.FindPropertyRelative("projectilePresentationKey").intValue = 11;
            payload.FindPropertyRelative("projectileSweepRadiusKey").intValue = 12;
        }

        private static void ConfigureEnemyTimedImpactPayload(
            SerializedProperty payload,
            string slotId,
            int threatDefinitionId,
            int damage,
            int breakDamage,
            int delayTicks)
        {
            payload.FindPropertyRelative("displayName").stringValue = slotId;
            payload.FindPropertyRelative("slotId").stringValue = slotId;
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgEnemySkillPayloadKind.TimedImpact;
            payload.FindPropertyRelative("threatDefinitionId").intValue =
                threatDefinitionId;
            payload.FindPropertyRelative("baseDamage").intValue = damage;
            payload.FindPropertyRelative("breakDamage").intValue = breakDamage;
            payload.FindPropertyRelative(
                "weakpointDamageMultiplierBasisPoints").intValue = 11000;
            payload.FindPropertyRelative(
                "weakpointBreakMultiplierBasisPoints").intValue = 13000;
            payload.FindPropertyRelative("timedImpactTargetPolicy").enumValueIndex = 0;
            payload.FindPropertyRelative("timedImpactDelayTicks").intValue =
                delayTicks;
            payload.FindPropertyRelative("timedImpactPresentationKey").intValue = 21;
        }

        private static void ConfigureEnemySummonPayload(
            SerializedProperty payload,
            string slotId,
            FpgEnemyDefinition candidate,
            int weight,
            FpgSummonOccupancyMode occupancyMode,
            FpgSummonOwnerOutcome ownerOutcome,
            int maxPerOwner,
            int maxPerEncounter)
        {
            payload.FindPropertyRelative("displayName").stringValue = slotId;
            payload.FindPropertyRelative("slotId").stringValue = slotId;
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgEnemySkillPayloadKind.Summon;

            SerializedProperty candidates =
                payload.FindPropertyRelative("summonCandidates");
            candidates.arraySize = 1;
            candidates.GetArrayElementAtIndex(0).objectReferenceValue = candidate;

            SerializedProperty weights =
                payload.FindPropertyRelative("summonCandidateWeights");
            weights.arraySize = 1;
            weights.GetArrayElementAtIndex(0).intValue = weight;

            payload.FindPropertyRelative("summonOccupancyMode").enumValueIndex =
                (int)occupancyMode;
            payload.FindPropertyRelative("summonPlacementMode").enumValueIndex =
                (int)FpgSummonPlacementMode.EncounterSpawnPoint;
            payload.FindPropertyRelative("summonOwnerOutcome").enumValueIndex =
                (int)ownerOutcome;
            payload.FindPropertyRelative("maxSummonsPerOwner").intValue =
                maxPerOwner;
            payload.FindPropertyRelative(
                "maxTotalSummonsPerEncounter").intValue = maxPerEncounter;
            payload.FindPropertyRelative("maxSummonRecursionDepth").intValue = 2;
        }

        private static void ConfigureIdentity(
            SerializedObject serialized,
            string skillId,
            string displayName)
        {
            serialized.FindProperty("skillId").stringValue = skillId;
            serialized.FindProperty("displayName").stringValue = displayName;
        }

        private static void ConfigureValidExecute(
            SerializedObject serialized,
            string payloadSlotId)
        {
            SerializedProperty sequences = serialized.FindProperty("sequences");
            sequences.arraySize = 1;
            SerializedProperty execute = sequences.GetArrayElementAtIndex(0);
            ConfigureSequence(
                execute,
                FpgSkillSequenceKind.Execute,
                1,
                "execute",
                false);
            ConfigureLogicEvent(
                execute,
                0,
                "event.execute",
                1,
                payloadSlotId,
                string.Empty);
        }

        private static void ConfigureSequence(
            SerializedProperty sequence,
            FpgSkillSequenceKind kind,
            int durationTicks,
            string mainAnimation,
            bool loop)
        {
            sequence.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            sequence.FindPropertyRelative("durationTicks").intValue = durationTicks;
            sequence.FindPropertyRelative("mainAnimation").stringValue = mainAnimation;
            sequence.FindPropertyRelative("loop").boolValue = loop;
            sequence.FindPropertyRelative("phases").arraySize = 0;
            sequence.FindPropertyRelative("logicEvents").arraySize = 0;
            sequence.FindPropertyRelative("presentationCues").arraySize = 0;
            sequence.FindPropertyRelative("warnings").arraySize = 0;
        }

        private static void ConfigureStandardPhases(
            SerializedProperty sequence,
            int startupEnd,
            int activeEnd,
            int recoveryEnd)
        {
            SerializedProperty phases = sequence.FindPropertyRelative("phases");
            phases.arraySize = 3;
            ConfigurePhase(
                phases.GetArrayElementAtIndex(0),
                "phase.startup",
                FpgSkillPhaseKind.Startup,
                0,
                startupEnd);
            ConfigurePhase(
                phases.GetArrayElementAtIndex(1),
                "phase.active",
                FpgSkillPhaseKind.Active,
                startupEnd,
                activeEnd);
            ConfigurePhase(
                phases.GetArrayElementAtIndex(2),
                "phase.recovery",
                FpgSkillPhaseKind.Recovery,
                activeEnd,
                recoveryEnd);
        }

        private static void ConfigurePhase(
            SerializedProperty phase,
            string phaseId,
            FpgSkillPhaseKind kind,
            int startTick,
            int endTick)
        {
            phase.FindPropertyRelative("phaseId").stringValue = phaseId;
            phase.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            phase.FindPropertyRelative("startTick").intValue = startTick;
            phase.FindPropertyRelative("endTick").intValue = endTick;
        }

        private static void ConfigureLogicEvent(
            SerializedProperty sequence,
            int index,
            string eventId,
            int tick,
            string payloadSlotId,
            string socketId)
        {
            SerializedProperty events = sequence.FindPropertyRelative("logicEvents");
            events.arraySize = Mathf.Max(events.arraySize, index + 1);
            SerializedProperty value = events.GetArrayElementAtIndex(index);
            value.FindPropertyRelative("eventId").stringValue = eventId;
            value.FindPropertyRelative("tick").intValue = tick;
            value.FindPropertyRelative("payloadSlotId").stringValue = payloadSlotId;
            value.FindPropertyRelative("authoredOrdinal").intValue = index;
            value.FindPropertyRelative("socketId").stringValue = socketId;
            value.FindPropertyRelative("targetSource").enumValueIndex =
                (int)FpgSkillTargetSource.CurrentAim;
            value.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
        }

        private static void ConfigureCue(
            SerializedProperty sequence,
            int index,
            string eventId,
            int tick,
            string cueId,
            string socketId)
        {
            SerializedProperty cues = sequence.FindPropertyRelative("presentationCues");
            cues.arraySize = Mathf.Max(cues.arraySize, index + 1);
            SerializedProperty value = cues.GetArrayElementAtIndex(index);
            value.FindPropertyRelative("eventId").stringValue = eventId;
            value.FindPropertyRelative("tick").intValue = tick;
            value.FindPropertyRelative("cueId").stringValue = cueId;
            value.FindPropertyRelative("authoredOrdinal").intValue = index;
            value.FindPropertyRelative("socketId").stringValue = socketId;
        }

        private static void ConfigureWarning(
            SerializedProperty sequence,
            int index,
            string eventId,
            string warningId,
            int startTick,
            int endTick)
        {
            SerializedProperty warnings = sequence.FindPropertyRelative("warnings");
            warnings.arraySize = Mathf.Max(warnings.arraySize, index + 1);
            SerializedProperty value = warnings.GetArrayElementAtIndex(index);
            value.FindPropertyRelative("eventId").stringValue = eventId;
            value.FindPropertyRelative("warningId").stringValue = warningId;
            value.FindPropertyRelative("startTick").intValue = startTick;
            value.FindPropertyRelative("endTick").intValue = endTick;
            value.FindPropertyRelative("authoredOrdinal").intValue = index;
            value.FindPropertyRelative("socketId").stringValue = string.Empty;
        }

        private static void ConfigurePelletPayload(
            SerializedProperty payload,
            string slotId,
            int ammoCost,
            int damage,
            int breakDamage,
            int additionalPenetrationCount)
        {
            payload.FindPropertyRelative("displayName").stringValue = slotId;
            payload.FindPropertyRelative("slotId").stringValue = slotId;
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgPlayerSkillPayloadKind.PelletRay;
            payload.FindPropertyRelative("ammoCost").intValue = ammoCost;
            payload.FindPropertyRelative("baseDamage").intValue = damage;
            payload.FindPropertyRelative("breakDamage").intValue = breakDamage;
            payload.FindPropertyRelative("queryMode").enumValueIndex =
                (int)AttackQueryMode.FirstSurfacePenetration;
            payload.FindPropertyRelative("pelletCount").intValue =
                WeaponDefinition.PrimaryPelletCount;
            payload.FindPropertyRelative("additionalPenetrationCount").intValue =
                additionalPenetrationCount;
            payload.FindPropertyRelative("allowedTargetKinds").intValue =
                (int)WeaponDefinition.PlayerAttackTargetKinds;
        }

        private static void ConfigureAreaPayload(
            SerializedProperty payload,
            string slotId,
            int ammoCost,
            int damage,
            int breakDamage,
            int combatantLimit,
            int projectileLimit)
        {
            payload.FindPropertyRelative("displayName").stringValue = slotId;
            payload.FindPropertyRelative("slotId").stringValue = slotId;
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgPlayerSkillPayloadKind.AreaAtFirstSurface;
            payload.FindPropertyRelative("ammoCost").intValue = ammoCost;
            payload.FindPropertyRelative("baseDamage").intValue = damage;
            payload.FindPropertyRelative("breakDamage").intValue = breakDamage;
            payload.FindPropertyRelative("queryMode").enumValueIndex =
                (int)AttackQueryMode.AreaAtFirstSurface;
            payload.FindPropertyRelative("areaCombatantLimit").intValue = combatantLimit;
            payload.FindPropertyRelative("areaProjectileLimit").intValue = projectileLimit;
            payload.FindPropertyRelative("allowedTargetKinds").intValue =
                (int)WeaponDefinition.PlayerAttackTargetKinds;
        }

        private static void ConfigureReloadPayload(
            SerializedProperty payload,
            string slotId)
        {
            payload.FindPropertyRelative("displayName").stringValue = slotId;
            payload.FindPropertyRelative("slotId").stringValue = slotId;
            payload.FindPropertyRelative("kind").enumValueIndex =
                (int)FpgPlayerSkillPayloadKind.ReloadCommit;
            payload.FindPropertyRelative("ammoCost").intValue = 0;
            payload.FindPropertyRelative("baseDamage").intValue = 0;
            payload.FindPropertyRelative("breakDamage").intValue = 0;
            payload.FindPropertyRelative("queryMode").enumValueIndex =
                (int)AttackQueryMode.Legacy;
            payload.FindPropertyRelative("allowedTargetKinds").intValue =
                (int)AttackTargetKinds.None;
        }

        private static FpgCompiledSkillEvent FindEvent(
            FpgCompiledSkillSequence sequence,
            FpgSkillEventKind kind)
        {
            for (int index = 0; index < sequence.EventCount; index++)
            {
                FpgCompiledSkillEvent value = sequence.GetEvent(index);
                if (value.Kind == kind)
                {
                    return value;
                }
            }

            Assert.Fail($"Expected compiled skill event '{kind}'.");
            return default(FpgCompiledSkillEvent);
        }

        private static FpgCompiledPlayerSkillPayloadSlot FindPayload(
            FpgCompiledPlayerSkillDefinition definition,
            FpgPlayerSkillPayloadKind kind)
        {
            for (int index = 0; index < definition.PayloadSlots.Count; index++)
            {
                FpgCompiledPlayerSkillPayloadSlot value =
                    definition.PayloadSlots[index];
                if (value.Kind == kind)
                {
                    return value;
                }
            }

            Assert.Fail($"Expected compiled player payload '{kind}'.");
            return default(FpgCompiledPlayerSkillPayloadSlot);
        }

        private static FpgCompiledEnemySkillPayloadSlot FindEnemyPayload(
            FpgCompiledEnemySkillDefinition definition,
            FpgEnemySkillPayloadKind kind)
        {
            for (int index = 0; index < definition.PayloadSlots.Count; index++)
            {
                FpgCompiledEnemySkillPayloadSlot value =
                    definition.PayloadSlots[index];
                if (value.Kind == kind)
                {
                    return value;
                }
            }

            Assert.Fail($"Expected compiled enemy payload '{kind}'.");
            return default(FpgCompiledEnemySkillPayloadSlot);
        }
    }
}
