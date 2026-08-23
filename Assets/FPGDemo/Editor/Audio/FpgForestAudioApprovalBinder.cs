using System;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    internal static class FpgForestAudioApprovalBinder
    {
        private const string SkillPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Primary.asset";
        private const string ReloadSkillPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Reload.asset";
        private const string ImmediateSecondarySkillPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Immediate.asset";
        private const string ChargeSecondarySkillPath =
            "Assets/FPGDemo/Config/FormalEncounter/Characters/Skills/FPG_Fei_Secondary_Charge.asset";
        private const string AttackClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Primary_Attack_";
        private const string ReloadClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Reload_";
        private const string ImmediateSecondaryLaunchClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Immediate_Launch_";
        private const string ImmediateSecondaryHitClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Immediate_Hit_01.wav";
        private const string ImmediateSecondaryWeakpointClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Immediate_Weakpoint_01.wav";
        private const string ChargeSecondaryStartClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Start_01.wav";
        private const string ChargeSecondaryHoldClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Hold_01.wav";
        private const string ChargeSecondaryReleaseClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Release_";
        private const string ChargeSecondaryCancelClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Cancel_";
        private const string ChargeSecondaryHitClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Hit_02.wav";
        private const string ChargeSecondaryWeakpointClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Secondary_Charge_Weakpoint_02.wav";
        private const string HitClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Primary_Hit_";
        private const string WeakpointClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Primary_Weakpoint_";
        private const string EnvironmentHitClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Fei_Primary_EnvironmentHit_";
        private const string BurstbugFastAttackPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack.asset";
        private const string BurstbugVolleyAttackPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Burstbug_Attack_Volley.asset";
        private const string BurstbugFastHitClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Burstbug_Fast_Hit_";
        private const string BurstbugVolleyTelegraphClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Burstbug_Volley_Telegraph_";
        private const string BurstbugVolleyReleaseClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Burstbug_Volley_Release_";
        private const string BurstbugVolleyProjectileClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Burstbug_Volley_Projectile_";
        private const string BurstbugVolleyInterceptionClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Burstbug_Volley_Interception_";
        private const string BurstbugVolleyImpactClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Burstbug_Volley_Impact_";
        private const string HudieAttackPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Hudie_Attack.asset";
        private const string HudieImpactClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Hudie_Projectile_Impact_";
        private const string HudieWeakpointClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Hudie_Projectile_Weakpoint_";
        private const string LuanSummonAttackPath =
            "Assets/FPGDemo/Config/FormalEncounter/FPG_Luan_Attack_Summon.asset";
        private const string LuanSummonTelegraphClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Luan_Summon_Telegraph_";
        private const string LuanSummonCommitClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Luan_Summon_Commit_";
        private const string LuanSelfDestructClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Luan_SelfDestruct_";
        private const string CombatAudioBankPath =
            "Assets/FPGDemo/Audio/ForestCombatAudioBank.asset";
        private const string RoomAudioProfilePath =
            "Assets/FPGDemo/Audio/ForestAudioProfile.asset";
        private const string ForestAmbienceClipRoot =
            "Assets/FPGDemo/Audio/Forest/Ambience/AMB_Forest_Bed_";
        private const string ForestExplorationMusicPath =
            "Assets/FPGDemo/Audio/Forest/Music/MUS_Forest_Exploration_01.wav";
        private const string ForestCombatMusicPath =
            "Assets/FPGDemo/Audio/Forest/Music/MUS_Forest_Combat_01.wav";
        private const string ForestVictoryMusicPath =
            "Assets/FPGDemo/Audio/Forest/Music/MUS_Forest_Victory_01.wav";
        private const string PlayerBarrierBrokenClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Player_BarrierBreak_";
        private const string PlayerDamagedClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/VO_Fei_Damaged_01.wav";
        private const string EnemyBreakClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Enemy_Break_";
        private const string EnemySpawnClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Enemy_Spawn_";
        private const string EnemyDeathClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/SFX_Enemy_Death_";
        private const string RoomEnteredClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/UI_Room_Entered_03.wav";
        private const string ExitUnlockedClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/UI_Exit_Unlocked_01.wav";
        private const string ExitConfirmedClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/UI_Exit_Confirmed_01.wav";
        private const string InteractionFocusClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/UI_Interaction_Focus_01.wav";
        private const string InteractionConfirmClipRoot =
            "Assets/FPGDemo/Audio/Forest/SFX/UI_Interaction_Confirm_";
        private const string InteractionRejectClipPath =
            "Assets/FPGDemo/Audio/Forest/SFX/UI_Interaction_Reject_01.wav";

        [MenuItem("FPG/Audio/Apply Approved Forest Audio")]
        private static void ApplyApprovedForestAudio()
        {
            if (!TryBindFeiPrimaryAttack(out string error)
                || !TryBindFeiReload(out error)
                || !TryBindFeiImmediateSecondaryLaunch(out error)
                || !TryBindFeiImmediateSecondaryHit(out error)
                || !TryBindFeiSecondaryChargeStartHold(out error)
                || !TryBindFeiSecondaryChargeReleaseCancel(out error)
                || !TryBindFeiSecondaryChargeImpact(out error)
                || !TryBindFeiPrimaryHit(out error)
                || !TryBindBurstbugVolleyAudio(out error)
                || !TryBindBurstbugFastHit(out error)
                || !TryBindHudieProjectileImpact(out error)
                || !TryBindLuanSummonAudio(out error)
                || !TryBindLuanSelfDestructAudio(out error)
                || !TryBindPlayerDamaged(out error)
                || !TryBindPlayerBarrierBroken(out error)
                || !TryBindEnemyBreak(out error)
                || !TryBindRoomEntered(out error)
                || !TryBindExitUnlocked(out error)
                || !TryBindExitConfirmed(out error)
                || !TryBindInteractionFocus(out error)
                || !TryBindInteractionConfirm(out error)
                || !TryBindInteractionReject(out error)
                || !TryBindForestExplorationMusic(out error)
                || !TryBindForestCombatMusic(out error)
                || !TryBindForestVictoryMusic(out error)
                || !TryBindForestAmbience(out error))
            {
                throw new InvalidOperationException(error);
            }

            Debug.Log(
                "Applied the approved Forest combat and room-lifecycle audio groups.");
        }

        [MenuItem("FPG/Audio/Apply Approved Burstbug Volley Audio")]
        private static void ApplyApprovedBurstbugVolleyAudio()
        {
            if (!TryBindBurstbugVolleyAudio(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Burstbug Volley audio groups.");
        }

        [MenuItem("FPG/Audio/Apply Approved Hudie Impact Audio")]
        private static void ApplyApprovedHudieImpactAudio()
        {
            if (!TryBindHudieProjectileImpact(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Hudie impact audio groups.");
        }

        [MenuItem("FPG/Audio/Apply Approved Luan Summon Audio")]
        private static void ApplyApprovedLuanSummonAudio()
        {
            if (!TryBindLuanSummonAudio(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Luan summon audio groups.");
        }

        [MenuItem("FPG/Audio/Apply Approved Luan Self-Destruct Audio")]
        private static void ApplyApprovedLuanSelfDestructAudio()
        {
            if (!TryBindLuanSelfDestructAudio(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Luan self-destruct audio group.");
        }

        [MenuItem("FPG/Audio/Prepare Enemy Lifecycle Audio Mapping")]
        private static void PrepareEnemyLifecycleAudioMapping()
        {
            if (!TryPrepareEnemyLifecycleAudioMapping(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log(
                "Prepared empty Enemy Spawn and Enemy Death cue mappings for approval.");
        }

        [MenuItem("FPG/Audio/Apply Approved Enemy Spawn Audio")]
        private static void ApplyApprovedEnemySpawnAudio()
        {
            if (!TryBindEnemySpawn(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Enemy Spawn audio group.");
        }

        [MenuItem("FPG/Audio/Apply Approved Enemy Death Audio")]
        private static void ApplyApprovedEnemyDeathAudio()
        {
            if (!TryBindEnemyDeath(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Enemy Death audio group.");
        }

        [MenuItem("FPG/Audio/Apply Approved Fei Reload Audio")]
        private static void ApplyApprovedFeiReloadAudio()
        {
            if (!TryBindFeiReload(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Fei reload audio group.");
        }

        [MenuItem("FPG/Audio/Apply Approved Fei Immediate Secondary Launch Audio")]
        private static void ApplyApprovedFeiImmediateSecondaryLaunchAudio()
        {
            if (!TryBindFeiImmediateSecondaryLaunch(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log(
                "Applied the approved Fei immediate-secondary launch audio group.");
        }

        [MenuItem("FPG/Audio/Apply Approved Fei Immediate Secondary Impact Audio")]
        private static void ApplyApprovedFeiImmediateSecondaryImpactAudio()
        {
            if (!TryBindFeiImmediateSecondaryHit(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log(
                "Applied the approved Fei immediate-secondary impact audio pair.");
        }

        [MenuItem("FPG/Audio/Apply Approved Fei Charge Start And Hold Audio")]
        private static void ApplyApprovedFeiChargeStartAndHoldAudio()
        {
            if (!TryBindFeiSecondaryChargeStartHold(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Fei charge start and held-loop audio pair.");
        }

        [MenuItem("FPG/Audio/Apply Approved Fei Charge Release And Cancel Audio")]
        private static void ApplyApprovedFeiChargeReleaseAndCancelAudio()
        {
            if (!TryBindFeiSecondaryChargeReleaseCancel(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log(
                "Applied the approved Fei charge release and cancel audio groups.");
        }

        [MenuItem("FPG/Audio/Apply Approved Fei Charge Impact Audio")]
        private static void ApplyApprovedFeiChargeImpactAudio()
        {
            if (!TryBindFeiSecondaryChargeImpact(out string error))
            {
                Debug.LogError(error);
                return;
            }

            Debug.Log("Applied the approved Fei charged-impact audio pair.");
        }

        internal static bool TryBindRoomEntered(out string error)
        {
            return TryBindSingleUiCue(
                RoomEnteredClipPath,
                CombatAudioCue.RoomEntered,
                120,
                0.25f,
                "Room Entered",
                out error);
        }

        internal static bool TryBindExitUnlocked(out string error)
        {
            return TryBindSingleUiCue(
                ExitUnlockedClipPath,
                CombatAudioCue.ExitUnlocked,
                120,
                0.25f,
                "Exit Unlocked",
                out error);
        }

        internal static bool TryBindExitConfirmed(out string error)
        {
            return TryBindSingleUiCue(
                ExitConfirmedClipPath,
                CombatAudioCue.ExitConfirmed,
                110,
                0.25f,
                "Exit Confirmed",
                out error);
        }

        internal static bool TryBindInteractionFocus(out string error)
        {
            return TryBindSingleUiCue(
                InteractionFocusClipPath,
                CombatAudioCue.InteractionFocus,
                130,
                0.08f,
                "Interaction Focus",
                out error);
        }

        internal static bool TryBindInteractionConfirm(out string error)
        {
            const int ClipCount = 3;
            AudioClip[] clips = new AudioClip[ClipCount];
            for (int index = 0; index < clips.Length; index++)
            {
                string path = InteractionConfirmClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                if (!TryConfigureShortPcmImporter(path, out error))
                {
                    return false;
                }

                clips[index] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[index] == null)
                {
                    error = "Approved Interaction Confirm clip is missing: "
                        + path;
                    return false;
                }
            }

            return TryBindUiCue(
                clips,
                CombatAudioCue.InteractionConfirm,
                125,
                0.12f,
                "Interaction Confirm",
                out error);
        }

        internal static bool TryBindInteractionReject(out string error)
        {
            return TryBindSingleUiCue(
                InteractionRejectClipPath,
                CombatAudioCue.InteractionReject,
                125,
                0.12f,
                "Interaction Reject",
                out error);
        }

        internal static bool TryBindForestAmbience(out string error)
        {
            const int ClipCount = 3;
            AudioClip[] clips = new AudioClip[ClipCount];
            for (int index = 0; index < clips.Length; index++)
            {
                string path = ForestAmbienceClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                if (!TryConfigureLongStreamingImporter(path, out error))
                {
                    return false;
                }

                clips[index] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[index] == null)
                {
                    error = "Approved Forest ambience clip is missing: " + path;
                    return false;
                }
            }

            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(
                    RoomAudioProfilePath);
            if (profile == null)
            {
                error = "Forest room audio profile is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            SerializedProperty primary = serialized.FindProperty("ambienceLoop");
            SerializedProperty variations =
                serialized.FindProperty("ambienceVariations");
            if (primary == null || variations == null)
            {
                error = "Forest room audio profile has no ambience variation fields.";
                return false;
            }

            Undo.RecordObject(profile, "Apply Approved Forest Ambience");
            primary.objectReferenceValue = clips[0];
            variations.arraySize = clips.Length - 1;
            for (int index = 1; index < clips.Length; index++)
            {
                variations.GetArrayElementAtIndex(index - 1)
                    .objectReferenceValue = clips[index];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindForestExplorationMusic(out string error)
        {
            if (!TryConfigureLongStreamingImporter(
                    ForestExplorationMusicPath,
                    out error))
            {
                return false;
            }

            AudioClip approvedClip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    ForestExplorationMusicPath);
            if (approvedClip == null)
            {
                error = "Approved Forest exploration music is missing: "
                    + ForestExplorationMusicPath;
                return false;
            }

            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(
                    RoomAudioProfilePath);
            if (profile == null)
            {
                error = "Forest room audio profile is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            SerializedProperty primary =
                serialized.FindProperty("explorationMusic");
            SerializedProperty variations =
                serialized.FindProperty("explorationMusicVariations");
            if (primary == null || variations == null)
            {
                error = "Forest room audio profile has no exploration music fields.";
                return false;
            }

            Undo.RecordObject(profile, "Apply Approved Forest Exploration Music");
            primary.objectReferenceValue = approvedClip;
            variations.arraySize = 0;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindForestCombatMusic(out string error)
        {
            if (!TryConfigureLongStreamingImporter(
                    ForestCombatMusicPath,
                    out error))
            {
                return false;
            }

            AudioClip approvedClip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    ForestCombatMusicPath);
            if (approvedClip == null)
            {
                error = "Approved Forest combat music is missing: "
                    + ForestCombatMusicPath;
                return false;
            }

            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(
                    RoomAudioProfilePath);
            if (profile == null)
            {
                error = "Forest room audio profile is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            SerializedProperty primary =
                serialized.FindProperty("combatMusic");
            SerializedProperty variations =
                serialized.FindProperty("combatMusicVariations");
            if (primary == null || variations == null)
            {
                error = "Forest room audio profile has no combat music fields.";
                return false;
            }

            Undo.RecordObject(profile, "Apply Approved Forest Combat Music");
            primary.objectReferenceValue = approvedClip;
            variations.arraySize = 0;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindForestVictoryMusic(out string error)
        {
            if (!TryConfigureLongStreamingImporter(
                    ForestVictoryMusicPath,
                    out error))
            {
                return false;
            }

            AudioClip approvedClip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    ForestVictoryMusicPath);
            if (approvedClip == null)
            {
                error = "Approved Forest victory music is missing: "
                    + ForestVictoryMusicPath;
                return false;
            }

            FpgRoomAudioProfile profile =
                AssetDatabase.LoadAssetAtPath<FpgRoomAudioProfile>(
                    RoomAudioProfilePath);
            if (profile == null)
            {
                error = "Forest room audio profile is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(profile);
            SerializedProperty primary =
                serialized.FindProperty("victoryStinger");
            SerializedProperty variations =
                serialized.FindProperty("victoryStingerVariations");
            if (primary == null || variations == null)
            {
                error = "Forest room audio profile has no victory music fields.";
                return false;
            }

            Undo.RecordObject(profile, "Apply Approved Forest Victory Music");
            primary.objectReferenceValue = approvedClip;
            variations.arraySize = 0;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindEnemyBreak(out string error)
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            if (!TryLoadClipGroup(
                    EnemyBreakClipRoot,
                    3,
                    out AudioClip[] clips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            SerializedProperty entry = null;
            if (entries != null)
            {
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty candidate =
                        entries.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("cue")?.intValue
                        == (int)CombatAudioCue.EnemyBreak)
                    {
                        entry = candidate;
                        break;
                    }
                }
            }

            SerializedProperty clip = entry?.FindPropertyRelative("clip");
            if (entry == null || clip == null)
            {
                error = "EnemyBreak cue entry is missing from the Forest bank.";
                return false;
            }

            Undo.RecordObject(bank, "Apply Approved Enemy Break Audio");
            clip.objectReferenceValue = clips[0];
            SerializedProperty variations =
                entry.FindPropertyRelative("variations");
            variations.arraySize = clips.Length - 1;
            for (int index = 1; index < clips.Length; index++)
            {
                variations.GetArrayElementAtIndex(index - 1)
                    .objectReferenceValue = clips[index];
            }

            entry.FindPropertyRelative("priority").intValue = 20;
            entry.FindPropertyRelative("cooldownSeconds").floatValue = 0.25f;
            entry.FindPropertyRelative("maxConcurrentVoices").intValue = 1;
            entry.FindPropertyRelative("bus").intValue =
                (int)CombatAudioBus.Sfx;
            entry.FindPropertyRelative("space").intValue =
                (int)FpgAudioPresentationSpace.WorldPositioned;
            entry.FindPropertyRelative("minDistance").floatValue = 1f;
            entry.FindPropertyRelative("maxDistance").floatValue = 20f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryPrepareEnemyLifecycleAudioMapping(
            out string error)
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            if (entries == null)
            {
                error = "Forest combat audio bank has no cue entry array.";
                return false;
            }

            Undo.RecordObject(bank, "Prepare Enemy Lifecycle Audio Mapping");
            if (!TryEnsureEmptyCueEntry(
                    entries,
                    CombatAudioCue.EnemySpawn,
                    out error)
                || !TryEnsureEmptyCueEntry(
                    entries,
                    CombatAudioCue.EnemyDeath,
                    out error))
            {
                return false;
            }

            serialized.ApplyModifiedProperties();
            if (!bank.TryValidateMapping(out error))
            {
                return false;
            }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindEnemySpawn(out string error)
        {
            return TryBindRequiredSfxCueGroup(
                EnemySpawnClipRoot,
                4,
                CombatAudioCue.EnemySpawn,
                "Enemy Spawn",
                out error);
        }

        internal static bool TryBindEnemyDeath(out string error)
        {
            return TryBindRequiredSfxCueGroup(
                EnemyDeathClipRoot,
                6,
                CombatAudioCue.EnemyDeath,
                "Enemy Death",
                out error);
        }

        internal static bool TryBindPlayerBarrierBroken(out string error)
        {
            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            if (!TryLoadClipGroup(
                    PlayerBarrierBrokenClipRoot,
                    3,
                    out AudioClip[] clips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            SerializedProperty entry = null;
            if (entries != null)
            {
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty candidate =
                        entries.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("cue")?.intValue
                        == (int)CombatAudioCue.PlayerBarrierBroken)
                    {
                        entry = candidate;
                        break;
                    }
                }
            }

            SerializedProperty clip = entry?.FindPropertyRelative("clip");
            if (entry == null || clip == null)
            {
                error = "PlayerBarrierBroken cue entry is missing from the Forest bank.";
                return false;
            }

            Undo.RecordObject(bank, "Apply Approved Player Barrier Broken Audio");
            clip.objectReferenceValue = clips[0];
            SerializedProperty variations =
                entry.FindPropertyRelative("variations");
            variations.arraySize = clips.Length - 1;
            for (int index = 1; index < clips.Length; index++)
            {
                variations.GetArrayElementAtIndex(index - 1)
                    .objectReferenceValue = clips[index];
            }

            entry.FindPropertyRelative("priority").intValue = 15;
            entry.FindPropertyRelative("cooldownSeconds").floatValue = 0.25f;
            entry.FindPropertyRelative("maxConcurrentVoices").intValue = 1;
            entry.FindPropertyRelative("bus").intValue =
                (int)CombatAudioBus.Sfx;
            entry.FindPropertyRelative("space").intValue =
                (int)FpgAudioPresentationSpace.TwoDimensional;
            entry.FindPropertyRelative("minDistance").floatValue = 1f;
            entry.FindPropertyRelative("maxDistance").floatValue = 20f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindPlayerDamaged(out string error)
        {
            if (!TryConfigureShortPcmImporter(PlayerDamagedClipPath, out error))
            {
                return false;
            }

            AudioClip approvedClip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerDamagedClipPath);
            if (approvedClip == null)
            {
                error = "Approved Fei damage reaction is missing: "
                    + PlayerDamagedClipPath;
                return false;
            }

            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            SerializedProperty entry = null;
            if (entries != null)
            {
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty candidate =
                        entries.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("cue")?.intValue
                        == (int)CombatAudioCue.PlayerDamaged)
                    {
                        entry = candidate;
                        break;
                    }
                }
            }

            SerializedProperty clip = entry?.FindPropertyRelative("clip");
            SerializedProperty variations =
                entry?.FindPropertyRelative("variations");
            if (entry == null || clip == null || variations == null)
            {
                error = "PlayerDamaged cue entry is missing from the Forest bank.";
                return false;
            }

            Undo.RecordObject(bank, "Apply Approved Fei Damage Reaction");
            clip.objectReferenceValue = approvedClip;
            variations.arraySize = 0;
            entry.FindPropertyRelative("priority").intValue = 20;
            entry.FindPropertyRelative("cooldownSeconds").floatValue = 0.15f;
            entry.FindPropertyRelative("maxConcurrentVoices").intValue = 1;
            entry.FindPropertyRelative("bus").intValue =
                (int)CombatAudioBus.Sfx;
            entry.FindPropertyRelative("space").intValue =
                (int)FpgAudioPresentationSpace.WorldPositioned;
            entry.FindPropertyRelative("minDistance").floatValue = 1f;
            entry.FindPropertyRelative("maxDistance").floatValue = 20f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiPrimaryAttack(out string error)
        {
            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    SkillPath);
            if (skill == null)
            {
                error = "Fei Primary skill asset is missing.";
                return false;
            }

            if (!TryLoadClipGroup(
                    AttackClipRoot,
                    4,
                    out AudioClip[] attackClips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty sequence = sequences?.GetArrayElementAtIndex(0);
            SerializedProperty tracks =
                sequence?.FindPropertyRelative("activePresentationTracks");
            SerializedProperty track = tracks?.GetArrayElementAtIndex(0);
            SerializedProperty audioEvents =
                track?.FindPropertyRelative("audioEvents");
            if (audioEvents == null)
            {
                error = "Fei Primary active presentation track has no audio event array.";
                return false;
            }

            SerializedProperty audioEvent = null;
            int audioEventIndex = -1;
            for (int index = 0; index < audioEvents.arraySize; index++)
            {
                SerializedProperty candidate =
                    audioEvents.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("eventId")?.stringValue
                    == "presentation.fei.primary.audio.0")
                {
                    audioEvent = candidate;
                    audioEventIndex = index;
                    break;
                }
            }

            if (audioEvent == null)
            {
                for (int index = 0; index < audioEvents.arraySize; index++)
                {
                    SerializedProperty candidate =
                        audioEvents.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("eventId")?.stringValue
                        == "event.fei.primary.attack.0")
                    {
                        audioEvent = candidate;
                        audioEventIndex = index;
                        break;
                    }
                }
            }

            if (audioEvent == null)
            {
                audioEventIndex = audioEvents.arraySize;
                audioEvents.InsertArrayElementAtIndex(audioEventIndex);
                audioEvent = audioEvents.GetArrayElementAtIndex(
                    audioEventIndex);
            }

            for (int index = audioEvents.arraySize - 1; index >= 0; index--)
            {
                if (index == audioEventIndex)
                {
                    continue;
                }

                SerializedProperty candidate =
                    audioEvents.GetArrayElementAtIndex(index);
                string candidateId =
                    candidate.FindPropertyRelative("eventId")?.stringValue;
                if (candidateId != "event.fei.primary.attack.0"
                    && candidateId != "presentation.fei.primary.audio.0")
                {
                    continue;
                }

                audioEvents.DeleteArrayElementAtIndex(index);
                if (index < audioEventIndex)
                {
                    audioEventIndex--;
                }
            }

            audioEvent = audioEvents.GetArrayElementAtIndex(audioEventIndex);

            SerializedProperty eventId =
                audioEvent.FindPropertyRelative("eventId");
            SerializedProperty tick = audioEvent.FindPropertyRelative("tick");
            SerializedProperty ordinal =
                audioEvent.FindPropertyRelative("authoredOrdinal");
            SerializedProperty boundGameplayEventId =
                audioEvent.FindPropertyRelative("boundGameplayEventId");
            SerializedProperty presentation =
                audioEvent.FindPropertyRelative("presentation");
            if (eventId == null
                || tick == null
                || ordinal == null
                || boundGameplayEventId == null
                || presentation == null)
            {
                error = "Fei Primary audio event serialization is incomplete.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Fei Primary Attack Audio");
            eventId.stringValue = "presentation.fei.primary.audio.0";
            tick.intValue = 0;
            ordinal.intValue = 2;
            boundGameplayEventId.stringValue = "event.fei.primary.attack.0";
            SetAudioDefinition(
                presentation,
                attackClips,
                FpgAudioPresentationAnchor.OwnerSocket,
                "weapon.primary.muzzle");

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiImmediateSecondaryLaunch(
            out string error)
        {
            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    ImmediateSecondarySkillPath);
            if (skill == null)
            {
                error = "Fei Immediate Secondary skill asset is missing.";
                return false;
            }

            const int ClipCount = 5;
            for (int index = 0; index < ClipCount; index++)
            {
                string path = ImmediateSecondaryLaunchClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                if (!TryConfigureShortPcmImporter(path, out error))
                {
                    return false;
                }
            }

            if (!TryLoadClipGroup(
                    ImmediateSecondaryLaunchClipRoot,
                    ClipCount,
                    out AudioClip[] launchClips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty sequence = sequences?.GetArrayElementAtIndex(0);
            SerializedProperty tracks =
                sequence?.FindPropertyRelative("activePresentationTracks");
            if (tracks == null)
            {
                error =
                    "Fei Immediate Secondary has no active presentation tracks.";
                return false;
            }

            SerializedProperty track = null;
            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty candidate =
                    tracks.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("trackId")?.stringValue
                    == "track.fei.secondary.execute.active")
                {
                    track = candidate;
                    break;
                }
            }

            SerializedProperty audioEvents =
                track?.FindPropertyRelative("audioEvents");
            if (audioEvents == null)
            {
                error =
                    "Fei Immediate Secondary active track has no audio event array.";
                return false;
            }

            SerializedProperty audioEvent = null;
            int audioEventIndex = -1;
            for (int index = 0; index < audioEvents.arraySize; index++)
            {
                SerializedProperty candidate =
                    audioEvents.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("eventId")?.stringValue
                    == "presentation.fei.secondary.execute.audio.0")
                {
                    audioEvent = candidate;
                    audioEventIndex = index;
                    break;
                }
            }

            if (audioEvent == null)
            {
                audioEventIndex = audioEvents.arraySize;
                audioEvents.InsertArrayElementAtIndex(audioEventIndex);
                audioEvent = audioEvents.GetArrayElementAtIndex(
                    audioEventIndex);
            }

            SerializedProperty eventId =
                audioEvent.FindPropertyRelative("eventId");
            SerializedProperty tick = audioEvent.FindPropertyRelative("tick");
            SerializedProperty ordinal =
                audioEvent.FindPropertyRelative("authoredOrdinal");
            SerializedProperty boundGameplayEventId =
                audioEvent.FindPropertyRelative("boundGameplayEventId");
            SerializedProperty presentation =
                audioEvent.FindPropertyRelative("presentation");
            if (eventId == null
                || tick == null
                || ordinal == null
                || boundGameplayEventId == null
                || presentation == null)
            {
                error =
                    "Fei Immediate Secondary audio event serialization is incomplete.";
                return false;
            }

            Undo.RecordObject(
                skill,
                "Apply Approved Fei Immediate Secondary Launch Audio");
            eventId.stringValue =
                "presentation.fei.secondary.execute.audio.0";
            tick.intValue = 0;
            ordinal.intValue = 2;
            boundGameplayEventId.stringValue =
                "event.fei.secondary.execute.attack.0";
            SetAudioDefinition(
                presentation,
                launchClips,
                FpgAudioPresentationAnchor.OwnerSocket,
                "weapon.secondary.muzzle");

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiImmediateSecondaryHit(out string error)
        {
            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    ImmediateSecondarySkillPath);
            if (skill == null)
            {
                error = "Fei Immediate Secondary skill asset is missing.";
                return false;
            }

            if (!TryConfigureShortPcmImporter(
                    ImmediateSecondaryHitClipPath,
                    out error)
                || !TryConfigureShortPcmImporter(
                    ImmediateSecondaryWeakpointClipPath,
                    out error))
            {
                return false;
            }

            AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ImmediateSecondaryHitClipPath);
            AudioClip weakpointClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ImmediateSecondaryWeakpointClipPath);
            if (hitClip == null || weakpointClip == null)
            {
                error =
                    "Approved Fei Immediate Secondary impact audio pair is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty sequence = sequences?.GetArrayElementAtIndex(0);
            SerializedProperty projectileEvents =
                sequence?.FindPropertyRelative("projectileEvents");
            SerializedProperty projectile =
                projectileEvents?.GetArrayElementAtIndex(0);
            SerializedProperty collision =
                projectile?.FindPropertyRelative("collisionPresentation");
            SerializedProperty baseAudio =
                collision?.FindPropertyRelative("baseAudio");
            SerializedProperty weakpointAudio =
                collision?.FindPropertyRelative("weakpointAudioOverride");
            if (baseAudio == null || weakpointAudio == null)
            {
                error =
                    "Fei Immediate Secondary collision presentation is missing an audio field.";
                return false;
            }

            Undo.RecordObject(
                skill,
                "Apply Approved Fei Immediate Secondary Impact Audio");
            SetAudioDefinition(
                baseAudio,
                new[] { hitClip },
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                weakpointAudio,
                new[] { weakpointClip },
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiSecondaryChargeStartHold(
            out string error)
        {
            if (!TryConfigureShortPcmImporter(
                    ChargeSecondaryStartClipPath,
                    out error)
                || !TryConfigureShortPcmImporter(
                    ChargeSecondaryHoldClipPath,
                    out error))
            {
                return false;
            }

            AudioClip startClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ChargeSecondaryStartClipPath);
            AudioClip holdClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ChargeSecondaryHoldClipPath);
            if (startClip == null || holdClip == null)
            {
                error = "Approved Fei charge start/hold audio pair is missing.";
                return false;
            }

            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    ChargeSecondarySkillPath);
            SerializedObject serialized = skill == null
                ? null
                : new SerializedObject(skill);
            SerializedProperty sequences = serialized?.FindProperty("sequences");
            SerializedProperty chargeEnter = null;
            if (sequences != null)
            {
                for (int index = 0; index < sequences.arraySize; index++)
                {
                    SerializedProperty candidate =
                        sequences.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("kind")?.intValue
                        == (int)FpgSkillSequenceKind.ChargeEnter)
                    {
                        chargeEnter = candidate;
                        break;
                    }
                }
            }

            SerializedProperty tracks = chargeEnter?.FindPropertyRelative(
                "activePresentationTracks");
            SerializedProperty track = null;
            if (tracks != null)
            {
                for (int index = 0; index < tracks.arraySize; index++)
                {
                    SerializedProperty candidate =
                        tracks.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("trackId")?.stringValue
                        == "track.fei.secondary.charge-enter.active")
                    {
                        track = candidate;
                        break;
                    }
                }
            }

            SerializedProperty audioEvents =
                track?.FindPropertyRelative("audioEvents");
            if (skill == null || audioEvents == null)
            {
                error =
                    "Fei charge-enter active presentation track is missing.";
                return false;
            }

            Undo.RecordObject(
                skill,
                "Apply Approved Fei Charge Start And Hold Audio");
            SetChargeAudioEvent(
                audioEvents,
                "presentation.fei.secondary.charge.audio.0",
                1,
                startClip,
                FpgAudioPresentationPlaybackMode.OneShot);
            SetChargeAudioEvent(
                audioEvents,
                "presentation.fei.secondary.charge.hold.0",
                2,
                holdClip,
                FpgAudioPresentationPlaybackMode.HeldLoop);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiSecondaryChargeImpact(out string error)
        {
            if (!TryConfigureShortPcmImporter(
                    ChargeSecondaryHitClipPath,
                    out error)
                || !TryConfigureShortPcmImporter(
                    ChargeSecondaryWeakpointClipPath,
                    out error))
            {
                return false;
            }

            AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ChargeSecondaryHitClipPath);
            AudioClip weakpointClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ChargeSecondaryWeakpointClipPath);
            if (hitClip == null || weakpointClip == null)
            {
                error = "Approved Fei charged-impact audio pair is missing.";
                return false;
            }

            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    ChargeSecondarySkillPath);
            SerializedObject serialized = skill == null
                ? null
                : new SerializedObject(skill);
            SerializedProperty release = FindSequenceProperty(
                serialized?.FindProperty("sequences"),
                FpgSkillSequenceKind.Release);
            SerializedProperty projectileEvents =
                release?.FindPropertyRelative("projectileEvents");
            SerializedProperty projectile =
                projectileEvents?.GetArrayElementAtIndex(0);
            SerializedProperty collision =
                projectile?.FindPropertyRelative("collisionPresentation");
            SerializedProperty baseAudio =
                collision?.FindPropertyRelative("baseAudio");
            SerializedProperty weakpointAudio =
                collision?.FindPropertyRelative("weakpointAudioOverride");
            if (skill == null || baseAudio == null || weakpointAudio == null)
            {
                error =
                    "Fei charged projectile collision presentation is missing an audio field.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Fei Charge Impact Audio");
            SetAudioDefinition(
                baseAudio,
                new[] { hitClip },
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                weakpointAudio,
                new[] { weakpointClip },
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiSecondaryChargeReleaseCancel(
            out string error)
        {
            const int ReleaseClipCount = 7;
            const int CancelClipCount = 5;
            for (int index = 0; index < ReleaseClipCount; index++)
            {
                string path = ChargeSecondaryReleaseClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                if (!TryConfigureShortPcmImporter(path, out error))
                {
                    return false;
                }
            }

            for (int index = 0; index < CancelClipCount; index++)
            {
                string path = ChargeSecondaryCancelClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                if (!TryConfigureShortPcmImporter(path, out error))
                {
                    return false;
                }
            }

            if (!TryLoadClipGroup(
                    ChargeSecondaryReleaseClipRoot,
                    ReleaseClipCount,
                    out AudioClip[] releaseClips,
                    out error)
                || !TryLoadClipGroup(
                    ChargeSecondaryCancelClipRoot,
                    CancelClipCount,
                    out AudioClip[] cancelClips,
                    out error))
            {
                return false;
            }

            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    ChargeSecondarySkillPath);
            SerializedObject serialized = skill == null
                ? null
                : new SerializedObject(skill);
            SerializedProperty sequences = serialized?.FindProperty("sequences");
            SerializedProperty release = FindSequenceProperty(
                sequences,
                FpgSkillSequenceKind.Release);
            SerializedProperty cancel = FindSequenceProperty(
                sequences,
                FpgSkillSequenceKind.Cancel);
            SerializedProperty releaseTrack = FindOrCreateActiveTrack(
                release,
                "track.fei.secondary.release.active");
            SerializedProperty cancelTrack = FindOrCreateActiveTrack(
                cancel,
                "track.fei.secondary.cancel.active");
            SerializedProperty releaseAudioEvents =
                releaseTrack?.FindPropertyRelative("audioEvents");
            SerializedProperty cancelAudioEvents =
                cancelTrack?.FindPropertyRelative("audioEvents");
            if (skill == null
                || releaseAudioEvents == null
                || cancelAudioEvents == null)
            {
                error =
                    "Fei charge release/cancel presentation tracks are missing.";
                return false;
            }

            Undo.RecordObject(
                skill,
                "Apply Approved Fei Charge Release And Cancel Audio");
            SetChargeAudioEvent(
                releaseAudioEvents,
                "presentation.fei.secondary.release.audio.0",
                2,
                releaseClips,
                FpgAudioPresentationPlaybackMode.OneShot,
                FpgAudioPresentationAnchor.OwnerSocket,
                "weapon.secondary.muzzle",
                "event.fei.secondary.release.attack.0");
            SetChargeAudioEvent(
                cancelAudioEvents,
                "presentation.fei.secondary.cancel.audio.0",
                0,
                cancelClips,
                FpgAudioPresentationPlaybackMode.OneShot,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiReload(out string error)
        {
            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    ReloadSkillPath);
            if (skill == null)
            {
                error = "Fei Reload skill asset is missing.";
                return false;
            }

            const int ClipCount = 5;
            for (int index = 0; index < ClipCount; index++)
            {
                string path = ReloadClipRoot
                    + (index + 1).ToString("00")
                    + ".wav";
                if (!TryConfigureShortPcmImporter(path, out error))
                {
                    return false;
                }
            }

            if (!TryLoadClipGroup(
                    ReloadClipRoot,
                    ClipCount,
                    out AudioClip[] reloadClips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty sequence = sequences?.GetArrayElementAtIndex(0);
            SerializedProperty tracks =
                sequence?.FindPropertyRelative("activePresentationTracks");
            if (tracks == null)
            {
                error = "Fei Reload has no active presentation track array.";
                return false;
            }

            SerializedProperty track = null;
            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty candidate =
                    tracks.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("trackId")?.stringValue
                    == "track.fei.reload.active")
                {
                    track = candidate;
                    break;
                }
            }

            if (track == null)
            {
                int trackIndex = tracks.arraySize;
                tracks.InsertArrayElementAtIndex(trackIndex);
                track = tracks.GetArrayElementAtIndex(trackIndex);
            }

            SerializedProperty trackId = track.FindPropertyRelative("trackId");
            SerializedProperty displayName =
                track.FindPropertyRelative("displayName");
            SerializedProperty vfxEvents =
                track.FindPropertyRelative("vfxEvents");
            SerializedProperty audioEvents =
                track.FindPropertyRelative("audioEvents");
            SerializedProperty cameraShakeEvents =
                track.FindPropertyRelative("cameraShakeEvents");
            if (trackId == null
                || displayName == null
                || vfxEvents == null
                || audioEvents == null
                || cameraShakeEvents == null)
            {
                error = "Fei Reload presentation track serialization is incomplete.";
                return false;
            }

            audioEvents.arraySize = 1;
            vfxEvents.arraySize = 0;
            cameraShakeEvents.arraySize = 0;
            SerializedProperty audioEvent =
                audioEvents.GetArrayElementAtIndex(0);
            SerializedProperty eventId =
                audioEvent.FindPropertyRelative("eventId");
            SerializedProperty tick = audioEvent.FindPropertyRelative("tick");
            SerializedProperty ordinal =
                audioEvent.FindPropertyRelative("authoredOrdinal");
            SerializedProperty boundGameplayEventId =
                audioEvent.FindPropertyRelative("boundGameplayEventId");
            SerializedProperty presentation =
                audioEvent.FindPropertyRelative("presentation");
            if (eventId == null
                || tick == null
                || ordinal == null
                || boundGameplayEventId == null
                || presentation == null)
            {
                error = "Fei Reload audio event serialization is incomplete.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Fei Reload Audio");
            trackId.stringValue = "track.fei.reload.active";
            displayName.stringValue = "Active Presentation";
            eventId.stringValue = "presentation.fei.reload.commit.audio.0";
            tick.intValue = 40;
            ordinal.intValue = 1;
            boundGameplayEventId.stringValue = "event.fei.reload.commit.0";
            SetAudioDefinition(
                presentation,
                reloadClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindBurstbugFastHit(out string error)
        {
            FpgEnemyAttackDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(
                    BurstbugFastAttackPath);
            if (skill == null)
            {
                error = "Burstbug Fast attack asset is missing.";
                return false;
            }

            if (!TryLoadClipGroup(
                    BurstbugFastHitClipRoot,
                    6,
                    out AudioClip[] hitClips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty sequence = sequences?.GetArrayElementAtIndex(0);
            SerializedProperty projectileEvents =
                sequence?.FindPropertyRelative("projectileEvents");
            SerializedProperty projectile =
                projectileEvents?.GetArrayElementAtIndex(0);
            SerializedProperty collision =
                projectile?.FindPropertyRelative("collisionPresentation");
            SerializedProperty baseAudio =
                collision?.FindPropertyRelative("baseAudio");
            if (baseAudio == null)
            {
                error = "Burstbug Fast collision presentation has no base audio field.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Burstbug Fast Hit Audio");
            SetAudioDefinition(
                baseAudio,
                hitClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindBurstbugVolleyAudio(out string error)
        {
            if (!TryLoadShortPcmClipGroup(
                    BurstbugVolleyTelegraphClipRoot,
                    3,
                    out AudioClip[] telegraphClips,
                    out error)
                || !TryLoadShortPcmClipGroup(
                    BurstbugVolleyReleaseClipRoot,
                    5,
                    out AudioClip[] releaseClips,
                    out error)
                || !TryLoadShortPcmClipGroup(
                    BurstbugVolleyProjectileClipRoot,
                    6,
                    out AudioClip[] projectileClips,
                    out error)
                || !TryLoadShortPcmClipGroup(
                    BurstbugVolleyInterceptionClipRoot,
                    3,
                    out AudioClip[] interceptionClips,
                    out error)
                || !TryLoadShortPcmClipGroup(
                    BurstbugVolleyImpactClipRoot,
                    6,
                    out AudioClip[] impactClips,
                    out error))
            {
                return false;
            }

            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            FpgEnemyAttackDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(
                    BurstbugVolleyAttackPath);
            if (skill == null)
            {
                error = "Burstbug Volley attack asset is missing.";
                return false;
            }

            SerializedObject bankSerialized = new SerializedObject(bank);
            SerializedProperty entries =
                bankSerialized.FindProperty("cueEntries");
            SerializedProperty telegraphEntry = FindCueEntry(
                entries,
                CombatAudioCue.EnemyInterceptableThreatTelegraph);
            SerializedProperty releaseEntry = FindCueEntry(
                entries,
                CombatAudioCue.EnemyInterceptableThreatRelease);
            if (telegraphEntry == null || releaseEntry == null)
            {
                error = "Burstbug Volley cue entries are missing from the Forest bank.";
                return false;
            }

            SerializedObject skillSerialized = new SerializedObject(skill);
            SerializedProperty sequences =
                skillSerialized.FindProperty("sequences");
            SerializedProperty sequence =
                sequences?.GetArrayElementAtIndex(0);
            SerializedProperty projectileEvents =
                sequence?.FindPropertyRelative("projectileEvents");
            SerializedProperty projectile =
                projectileEvents?.GetArrayElementAtIndex(0);
            SerializedProperty flightAudio =
                projectile?.FindPropertyRelative("flightAudio");
            SerializedProperty collision =
                projectile?.FindPropertyRelative("collisionPresentation");
            SerializedProperty baseAudio =
                collision?.FindPropertyRelative("baseAudio");
            SerializedProperty interceptionAudio =
                collision?.FindPropertyRelative(
                    "interceptionAudioOverride");
            if (flightAudio == null
                || baseAudio == null
                || interceptionAudio == null)
            {
                error =
                    "Burstbug Volley projectile audio serialization is incomplete.";
                return false;
            }

            if (!TryGetRequiredCuePolicy(
                    CombatAudioCue.EnemyInterceptableThreatTelegraph,
                    out CombatAudioCuePolicy telegraphPolicy)
                || !TryGetRequiredCuePolicy(
                    CombatAudioCue.EnemyInterceptableThreatRelease,
                    out CombatAudioCuePolicy releasePolicy))
            {
                error = "Burstbug Volley cue policies are missing.";
                return false;
            }

            Undo.RecordObjects(
                new UnityEngine.Object[] { bank, skill },
                "Apply Approved Burstbug Volley Audio");
            SetCueEntry(telegraphEntry, telegraphClips, telegraphPolicy);
            SetCueEntry(releaseEntry, releaseClips, releasePolicy);
            SetAudioDefinition(
                flightAudio,
                projectileClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                baseAudio,
                impactClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                interceptionAudio,
                interceptionClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            bankSerialized.ApplyModifiedProperties();
            skillSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bank);
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindHudieProjectileImpact(out string error)
        {
            if (!TryLoadShortPcmClipGroup(
                    HudieImpactClipRoot,
                    4,
                    out AudioClip[] impactClips,
                    out error)
                || !TryLoadShortPcmClipGroup(
                    HudieWeakpointClipRoot,
                    4,
                    out AudioClip[] weakpointClips,
                    out error))
            {
                return false;
            }

            FpgEnemyAttackDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(
                    HudieAttackPath);
            if (skill == null)
            {
                error = "Hudie projectile attack asset is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            SerializedProperty sequence = sequences?.GetArrayElementAtIndex(0);
            SerializedProperty projectileEvents =
                sequence?.FindPropertyRelative("projectileEvents");
            SerializedProperty projectile =
                projectileEvents?.GetArrayElementAtIndex(0);
            SerializedProperty collision =
                projectile?.FindPropertyRelative("collisionPresentation");
            SerializedProperty baseAudio =
                collision?.FindPropertyRelative("baseAudio");
            SerializedProperty weakpointAudio =
                collision?.FindPropertyRelative("weakpointAudioOverride");
            if (baseAudio == null || weakpointAudio == null)
            {
                error =
                    "Hudie projectile collision audio serialization is incomplete.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Hudie Impact Audio");
            SetAudioDefinition(
                baseAudio,
                impactClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                weakpointAudio,
                weakpointClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindLuanSummonAudio(out string error)
        {
            if (!TryLoadShortPcmClipGroup(
                    LuanSummonTelegraphClipRoot,
                    7,
                    out AudioClip[] telegraphClips,
                    out error)
                || !TryLoadShortPcmClipGroup(
                    LuanSummonCommitClipRoot,
                    7,
                    out AudioClip[] commitClips,
                    out error))
            {
                return false;
            }

            FpgEnemyAttackDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(
                    LuanSummonAttackPath);
            SerializedObject serialized = skill == null
                ? null
                : new SerializedObject(skill);
            SerializedProperty execute = FindSequenceProperty(
                serialized?.FindProperty("sequences"),
                FpgSkillSequenceKind.Execute);
            SerializedProperty track = FindOrCreateActiveTrack(
                execute,
                "track.luan-summon.active");
            SerializedProperty audioEvents =
                track?.FindPropertyRelative("audioEvents");
            if (skill == null || audioEvents == null)
            {
                error = "Luan summon active presentation track is missing.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Luan Summon Audio");
            SetAudioEvent(
                audioEvents,
                "presentation.luan-summon.telegraph.0",
                0,
                4,
                telegraphClips,
                FpgAudioPresentationPlaybackMode.OneShot,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty,
                string.Empty);
            SetAudioEvent(
                audioEvents,
                "presentation.luan-summon.commit.0",
                44,
                5,
                commitClips,
                FpgAudioPresentationPlaybackMode.OneShot,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty,
                "event.luan-summon.attack.0");

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindLuanSelfDestructAudio(out string error)
        {
            if (!TryLoadShortPcmClipGroup(
                    LuanSelfDestructClipRoot,
                    4,
                    out AudioClip[] selfDestructClips,
                    out error))
            {
                return false;
            }

            FpgEnemyAttackDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgEnemyAttackDefinition>(
                    LuanSummonAttackPath);
            SerializedObject serialized = skill == null
                ? null
                : new SerializedObject(skill);
            SerializedProperty execute = FindSequenceProperty(
                serialized?.FindProperty("sequences"),
                FpgSkillSequenceKind.Execute);
            SerializedProperty track = FindOrCreateActiveTrack(
                execute,
                "track.luan-summon.active");
            SerializedProperty audioEvents =
                track?.FindPropertyRelative("audioEvents");
            if (skill == null || audioEvents == null)
            {
                error = "Luan self-destruct presentation track is missing.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Luan Self-Destruct Audio");
            SetAudioEvent(
                audioEvents,
                "presentation.luan-summon.self-destruct.1",
                71,
                6,
                selfDestructClips,
                FpgAudioPresentationPlaybackMode.OneShot,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty,
                "event.luan-summon.self-destruct.1");

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        internal static bool TryBindFeiPrimaryHit(out string error)
        {
            FpgPlayerSkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<FpgPlayerSkillDefinition>(
                    SkillPath);
            if (skill == null)
            {
                error = "Fei Primary skill asset is missing.";
                return false;
            }

            if (!TryLoadClipGroup(HitClipRoot, 4, out AudioClip[] hitClips, out error)
                || !TryLoadClipGroup(
                    WeakpointClipRoot,
                    4,
                    out AudioClip[] weakpointClips,
                    out error)
                || !TryLoadClipGroup(
                    EnvironmentHitClipRoot,
                    4,
                    out AudioClip[] environmentHitClips,
                    out error))
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty sequences = serialized.FindProperty("sequences");
            if (sequences == null || sequences.arraySize <= 0)
            {
                error = "Fei Primary has no authored sequence.";
                return false;
            }

            SerializedProperty attacks = sequences.GetArrayElementAtIndex(0)
                .FindPropertyRelative("attackEvents");
            if (attacks == null || attacks.arraySize <= 0)
            {
                error = "Fei Primary has no authored attack event.";
                return false;
            }

            SerializedProperty impact = attacks.GetArrayElementAtIndex(0)
                .FindPropertyRelative("impactPresentation");
            SerializedProperty baseAudio =
                impact?.FindPropertyRelative("baseAudio");
            SerializedProperty weakpointAudio =
                impact?.FindPropertyRelative("weakpointAudioOverride");
            SerializedProperty environmentAudio =
                impact?.FindPropertyRelative("environmentAudioOverride");
            if (baseAudio == null
                || weakpointAudio == null
                || environmentAudio == null)
            {
                error =
                    "Fei Primary impact presentation is missing an audio field.";
                return false;
            }

            Undo.RecordObject(skill, "Apply Approved Forest Audio");
            SetAudioDefinition(
                baseAudio,
                hitClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                weakpointAudio,
                weakpointClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);
            SetAudioDefinition(
                environmentAudio,
                environmentHitClips,
                FpgAudioPresentationAnchor.OwnerRoot,
                string.Empty);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        private static bool TryLoadClipGroup(
            string clipRoot,
            int clipCount,
            out AudioClip[] clips,
            out string error)
        {
            clips = new AudioClip[clipCount];
            for (int index = 0; index < clips.Length; index++)
            {
                string path = clipRoot + (index + 1).ToString("00") + ".wav";
                clips[index] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[index] != null)
                {
                    continue;
                }

                error = "Approved Forest audio clip is missing: " + path;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryLoadShortPcmClipGroup(
            string clipRoot,
            int clipCount,
            out AudioClip[] clips,
            out string error)
        {
            clips = Array.Empty<AudioClip>();
            for (int index = 0; index < clipCount; index++)
            {
                string path = clipRoot + (index + 1).ToString("00") + ".wav";
                if (!TryConfigureShortPcmImporter(path, out error))
                {
                    return false;
                }
            }

            return TryLoadClipGroup(clipRoot, clipCount, out clips, out error);
        }

        private static bool TryConfigureShortPcmImporter(
            string assetPath,
            out string error)
        {
            AudioImporter importer =
                AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                error = "Approved Forest audio importer is missing: "
                    + assetPath;
                return false;
            }

            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            bool changed = !importer.forceToMono
                || importer.loadInBackground
                || settings.loadType != AudioClipLoadType.DecompressOnLoad
                || settings.compressionFormat != AudioCompressionFormat.PCM
                || settings.sampleRateSetting
                    != AudioSampleRateSetting.PreserveSampleRate
                || !settings.preloadAudioData;
            if (changed)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting =
                    AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            error = string.Empty;
            return true;
        }

        private static bool TryConfigureLongStreamingImporter(
            string assetPath,
            out string error)
        {
            AudioImporter importer =
                AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                error = "Approved Forest audio importer is missing: "
                    + assetPath;
                return false;
            }

            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            bool changed = importer.forceToMono
                || !importer.loadInBackground
                || settings.loadType != AudioClipLoadType.Streaming
                || settings.compressionFormat != AudioCompressionFormat.Vorbis
                || settings.sampleRateSetting
                    != AudioSampleRateSetting.PreserveSampleRate
                || settings.preloadAudioData
                || !Mathf.Approximately(settings.quality, 0.7f);
            if (changed)
            {
                importer.forceToMono = false;
                importer.loadInBackground = true;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                settings.sampleRateSetting =
                    AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = false;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            error = string.Empty;
            return true;
        }

        private static bool TryBindSingleUiCue(
            string clipPath,
            CombatAudioCue cue,
            int priority,
            float cooldownSeconds,
            string cueLabel,
            out string error)
        {
            if (!TryConfigureShortPcmImporter(clipPath, out error))
            {
                return false;
            }

            AudioClip approvedClip =
                AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (approvedClip == null)
            {
                error = "Approved " + cueLabel + " clip is missing.";
                return false;
            }

            return TryBindUiCue(
                new[] { approvedClip },
                cue,
                priority,
                cooldownSeconds,
                cueLabel,
                out error);
        }

        private static bool TryBindRequiredSfxCueGroup(
            string clipRoot,
            int clipCount,
            CombatAudioCue cue,
            string cueLabel,
            out string error)
        {
            if (!TryLoadShortPcmClipGroup(
                    clipRoot,
                    clipCount,
                    out AudioClip[] approvedClips,
                    out error))
            {
                return false;
            }

            if (!TryGetRequiredCuePolicy(
                    cue,
                    out CombatAudioCuePolicy policy))
            {
                error = "No required cue policy exists for " + cue + ".";
                return false;
            }

            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            SerializedProperty entry = FindCueEntry(entries, cue);
            if (entry == null)
            {
                error = cueLabel
                    + " cue entry is missing from the Forest bank.";
                return false;
            }

            Undo.RecordObject(bank, "Apply Approved " + cueLabel + " Audio");
            SetCueEntry(entry, approvedClips, policy);
            serialized.ApplyModifiedProperties();
            if (!bank.TryValidateMapping(out error))
            {
                return false;
            }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        private static bool TryBindUiCue(
            AudioClip[] approvedClips,
            CombatAudioCue cue,
            int priority,
            float cooldownSeconds,
            string cueLabel,
            out string error)
        {
            if (approvedClips == null || approvedClips.Length == 0)
            {
                error = "Approved " + cueLabel + " clip group is empty.";
                return false;
            }

            CombatAudioBank bank =
                AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                    CombatAudioBankPath);
            if (bank == null)
            {
                error = "Forest combat audio bank is missing.";
                return false;
            }

            SerializedObject serialized = new SerializedObject(bank);
            SerializedProperty entries = serialized.FindProperty("cueEntries");
            SerializedProperty entry = null;
            if (entries != null)
            {
                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty candidate =
                        entries.GetArrayElementAtIndex(index);
                    if (candidate.FindPropertyRelative("cue")?.intValue
                        == (int)cue)
                    {
                        entry = candidate;
                        break;
                    }
                }
            }

            SerializedProperty clip = entry?.FindPropertyRelative("clip");
            SerializedProperty variations =
                entry?.FindPropertyRelative("variations");
            if (entry == null || clip == null || variations == null)
            {
                error = cueLabel
                    + " cue entry is missing from the Forest bank.";
                return false;
            }

            Undo.RecordObject(bank, "Apply Approved " + cueLabel + " Audio");
            clip.objectReferenceValue = approvedClips[0];
            variations.arraySize = approvedClips.Length - 1;
            for (int index = 1; index < approvedClips.Length; index++)
            {
                variations.GetArrayElementAtIndex(index - 1)
                    .objectReferenceValue = approvedClips[index];
            }
            entry.FindPropertyRelative("priority").intValue = priority;
            entry.FindPropertyRelative("cooldownSeconds").floatValue =
                cooldownSeconds;
            entry.FindPropertyRelative("maxConcurrentVoices").intValue = 1;
            entry.FindPropertyRelative("bus").intValue =
                (int)CombatAudioBus.Ui;
            entry.FindPropertyRelative("space").intValue =
                (int)FpgAudioPresentationSpace.TwoDimensional;
            entry.FindPropertyRelative("minDistance").floatValue = 1f;
            entry.FindPropertyRelative("maxDistance").floatValue = 20f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            error = string.Empty;
            return true;
        }

        private static SerializedProperty FindCueEntry(
            SerializedProperty entries,
            CombatAudioCue cue)
        {
            if (entries == null)
            {
                return null;
            }

            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("cue")?.intValue == (int)cue)
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool TryGetRequiredCuePolicy(
            CombatAudioCue cue,
            out CombatAudioCuePolicy policy)
        {
            for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
            {
                CombatAudioCuePolicy candidate =
                    CombatAudioBank.GetRequiredCuePolicy(index);
                if (candidate.Cue == cue)
                {
                    policy = candidate;
                    return true;
                }
            }

            policy = default(CombatAudioCuePolicy);
            return false;
        }

        private static bool TryEnsureEmptyCueEntry(
            SerializedProperty entries,
            CombatAudioCue cue,
            out string error)
        {
            if (!TryGetRequiredCuePolicy(cue, out CombatAudioCuePolicy policy))
            {
                error = "No required cue policy exists for " + cue + ".";
                return false;
            }

            SerializedProperty entry = FindCueEntry(entries, cue);
            if (entry == null)
            {
                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                entry = entries.GetArrayElementAtIndex(index);
            }

            entry.FindPropertyRelative("cue").intValue = (int)cue;
            entry.FindPropertyRelative("clip").objectReferenceValue = null;
            entry.FindPropertyRelative("variations").arraySize = 0;
            entry.FindPropertyRelative("priority").intValue = policy.Priority;
            entry.FindPropertyRelative("volume").floatValue = policy.Volume;
            entry.FindPropertyRelative("cooldownSeconds").floatValue =
                policy.CooldownSeconds;
            entry.FindPropertyRelative("maxConcurrentVoices").intValue =
                policy.MaxConcurrentVoices;
            entry.FindPropertyRelative("bus").intValue = (int)policy.Bus;
            entry.FindPropertyRelative("space").intValue = (int)policy.Space;
            entry.FindPropertyRelative("minDistance").floatValue =
                policy.MinDistance;
            entry.FindPropertyRelative("maxDistance").floatValue =
                policy.MaxDistance;
            error = string.Empty;
            return true;
        }

        private static void SetCueEntry(
            SerializedProperty entry,
            AudioClip[] clips,
            CombatAudioCuePolicy policy)
        {
            entry.FindPropertyRelative("clip").objectReferenceValue = clips[0];
            SerializedProperty variations =
                entry.FindPropertyRelative("variations");
            variations.arraySize = clips.Length - 1;
            for (int index = 1; index < clips.Length; index++)
            {
                variations.GetArrayElementAtIndex(index - 1)
                    .objectReferenceValue = clips[index];
            }

            entry.FindPropertyRelative("priority").intValue = policy.Priority;
            entry.FindPropertyRelative("volume").floatValue = policy.Volume;
            entry.FindPropertyRelative("cooldownSeconds").floatValue =
                policy.CooldownSeconds;
            entry.FindPropertyRelative("maxConcurrentVoices").intValue =
                policy.MaxConcurrentVoices;
            entry.FindPropertyRelative("bus").intValue = (int)policy.Bus;
            entry.FindPropertyRelative("space").intValue = (int)policy.Space;
            entry.FindPropertyRelative("minDistance").floatValue =
                policy.MinDistance;
            entry.FindPropertyRelative("maxDistance").floatValue =
                policy.MaxDistance;
        }

        private static void SetAudioDefinition(
            SerializedProperty audio,
            AudioClip[] clips,
            FpgAudioPresentationAnchor anchor,
            string socketId,
            FpgAudioPresentationPlaybackMode playbackMode =
                FpgAudioPresentationPlaybackMode.OneShot)
        {
            if (audio.propertyType == SerializedPropertyType.ManagedReference)
            {
                audio.managedReferenceValue =
                    new FpgAudioPresentationDefinition();
            }
            audio.FindPropertyRelative("clip").objectReferenceValue = clips[0];
            SerializedProperty variations =
                audio.FindPropertyRelative("variations");
            variations.arraySize = clips.Length - 1;
            for (int index = 1; index < clips.Length; index++)
            {
                variations.GetArrayElementAtIndex(index - 1)
                    .objectReferenceValue = clips[index];
            }

            audio.FindPropertyRelative("volume").floatValue = 1f;
            audio.FindPropertyRelative("space").intValue =
                (int)FpgAudioPresentationSpace.WorldPositioned;
            audio.FindPropertyRelative("anchor").intValue =
                (int)anchor;
            audio.FindPropertyRelative("playbackMode").intValue =
                (int)playbackMode;
            audio.FindPropertyRelative("socketId").stringValue = socketId;
            audio.FindPropertyRelative("minDistance").floatValue = 1f;
            audio.FindPropertyRelative("maxDistance").floatValue = 20f;
        }

        private static void SetChargeAudioEvent(
            SerializedProperty audioEvents,
            string eventId,
            int authoredOrdinal,
            AudioClip clip,
            FpgAudioPresentationPlaybackMode playbackMode)
        {
            SetChargeAudioEvent(
                audioEvents,
                eventId,
                authoredOrdinal,
                new[] { clip },
                playbackMode,
                FpgAudioPresentationAnchor.OwnerSocket,
                "weapon.secondary.muzzle",
                string.Empty);
        }

        private static void SetChargeAudioEvent(
            SerializedProperty audioEvents,
            string eventId,
            int authoredOrdinal,
            AudioClip[] clips,
            FpgAudioPresentationPlaybackMode playbackMode,
            FpgAudioPresentationAnchor anchor,
            string socketId,
            string boundGameplayEventId)
        {
            SetAudioEvent(
                audioEvents,
                eventId,
                0,
                authoredOrdinal,
                clips,
                playbackMode,
                anchor,
                socketId,
                boundGameplayEventId);
        }

        private static void SetAudioEvent(
            SerializedProperty audioEvents,
            string eventId,
            int tick,
            int authoredOrdinal,
            AudioClip[] clips,
            FpgAudioPresentationPlaybackMode playbackMode,
            FpgAudioPresentationAnchor anchor,
            string socketId,
            string boundGameplayEventId)
        {
            SerializedProperty audioEvent = null;
            for (int index = 0; index < audioEvents.arraySize; index++)
            {
                SerializedProperty candidate =
                    audioEvents.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("eventId")?.stringValue
                    == eventId)
                {
                    audioEvent = candidate;
                    break;
                }
            }

            if (audioEvent == null)
            {
                int index = audioEvents.arraySize;
                audioEvents.InsertArrayElementAtIndex(index);
                audioEvent = audioEvents.GetArrayElementAtIndex(index);
            }

            audioEvent.FindPropertyRelative("eventId").stringValue = eventId;
            audioEvent.FindPropertyRelative("tick").intValue = tick;
            audioEvent.FindPropertyRelative("authoredOrdinal").intValue =
                authoredOrdinal;
            audioEvent.FindPropertyRelative("boundGameplayEventId")
                .stringValue = boundGameplayEventId;
            SetAudioDefinition(
                audioEvent.FindPropertyRelative("presentation"),
                clips,
                anchor,
                socketId,
                playbackMode);
        }

        private static SerializedProperty FindSequenceProperty(
            SerializedProperty sequences,
            FpgSkillSequenceKind kind)
        {
            if (sequences == null)
            {
                return null;
            }

            for (int index = 0; index < sequences.arraySize; index++)
            {
                SerializedProperty sequence =
                    sequences.GetArrayElementAtIndex(index);
                if (sequence.FindPropertyRelative("kind")?.intValue
                    == (int)kind)
                {
                    return sequence;
                }
            }

            return null;
        }

        private static SerializedProperty FindOrCreateActiveTrack(
            SerializedProperty sequence,
            string trackId)
        {
            SerializedProperty tracks = sequence?.FindPropertyRelative(
                "activePresentationTracks");
            if (tracks == null)
            {
                return null;
            }

            for (int index = 0; index < tracks.arraySize; index++)
            {
                SerializedProperty candidate =
                    tracks.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("trackId")?.stringValue
                    == trackId)
                {
                    return candidate;
                }
            }

            int newIndex = tracks.arraySize;
            tracks.InsertArrayElementAtIndex(newIndex);
            SerializedProperty track = tracks.GetArrayElementAtIndex(newIndex);
            track.FindPropertyRelative("trackId").stringValue = trackId;
            track.FindPropertyRelative("displayName").stringValue =
                "Active Presentation";
            track.FindPropertyRelative("vfxEvents").arraySize = 0;
            track.FindPropertyRelative("audioEvents").arraySize = 0;
            track.FindPropertyRelative("cameraShakeEvents").arraySize = 0;
            return track;
        }
    }
}
