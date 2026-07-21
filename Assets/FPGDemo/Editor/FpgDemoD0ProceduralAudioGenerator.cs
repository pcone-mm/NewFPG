using System;
using System.IO;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// Generates D0's replaceable placeholder SFX as deterministic PCM WAV
    /// assets in the Editor. Runtime code only plays imported AudioClips; it
    /// never synthesizes wave data.
    /// </summary>
    public static class FpgDemoD0ProceduralAudioGenerator
    {
        public const string AudioFolder = "Assets/FPGDemo/Presentation/D0Slice/Audio";

        private const int SampleRate = 44100;
        private const int BitsPerSample = 16;
        private const int ChannelCount = 1;

        private const string ReticleLockClipPath =
            "Assets/Art/SkillIndicators/Temporary/Audio/S_IND_TargetLock.wav";
        private const string DangerTickClipPath =
            "Assets/Art/SkillIndicators/Temporary/Audio/S_IND_DangerTick.wav";
        private const string ConfirmReleaseClipPath =
            "Assets/Art/SkillIndicators/Temporary/Audio/S_IND_ConfirmRelease.wav";

        [MenuItem("FPG Demo/D0 2.5D/Generate Missing Procedural Audio")]
        public static void GenerateMissingProceduralAudio()
        {
            CombatAudioBank bank = AssetDatabase.LoadAssetAtPath<CombatAudioBank>(
                FpgDemoD0AudioBankAuthoring.AudioBankPath);
            if (!TryGenerateMissingProceduralAudio(bank, out string error))
            {
                throw new InvalidOperationException(error);
            }

            Debug.Log("D0 procedural combat audio is ready: " + AudioFolder);
        }

        /// <summary>
        /// Generates only unassigned required cue clips. Existing references are
        /// deliberately left untouched so an approved manual replacement keeps
        /// surviving both this command and a D0 installer rerun.
        /// </summary>
        public static bool TryGenerateMissingProceduralAudio(
            CombatAudioBank bank,
            out string error)
        {
            if (bank == null)
            {
                error = "D0 CombatAudioBank is missing. Run the D0 slice installer before generating audio.";
                return false;
            }

            try
            {
                FpgDemoD0AudioBankAuthoring.EnsureCueMappings(bank);
                if (!bank.TryValidateMapping(out error))
                {
                    error = "D0 CombatAudioBank mapping is invalid: " + error;
                    return false;
                }

                EnsureFolder("Assets/FPGDemo/Presentation", "D0Slice");
                EnsureFolder("Assets/FPGDemo/Presentation/D0Slice", "Audio");

                SerializedObject serializedBank = new SerializedObject(bank);
                SerializedProperty entries = serializedBank.FindProperty("cueEntries");
                if (entries == null)
                {
                    error = "CombatAudioBank no longer exposes its cueEntries array.";
                    return false;
                }

                for (int index = 0; index < CombatAudioBank.RequiredCueCount; index++)
                {
                    CombatAudioCue cue = CombatAudioBank.GetRequiredCue(index);
                    SerializedProperty entry = FindCueEntry(entries, cue);
                    if (entry == null)
                    {
                        error = "CombatAudioBank is missing the required cue " + cue + ".";
                        return false;
                    }

                    SerializedProperty clipProperty = entry.FindPropertyRelative("clip");
                    if (clipProperty == null)
                    {
                        error = "CombatAudioCueEntry no longer exposes its clip field.";
                        return false;
                    }

                    if (clipProperty.objectReferenceValue != null)
                    {
                        continue;
                    }

                    AudioClip clip = LoadReusableClip(cue);
                    if (clip == null)
                    {
                        string assetPath = AudioFolder + "/" + GetGeneratedFileName(cue) + ".wav";
                        clip = EnsureGeneratedClip(assetPath, GetRecipe(cue));
                    }

                    if (clip == null)
                    {
                        error = "D0 procedural audio could not create a clip for " + cue + ".";
                        return false;
                    }

                    clipProperty.objectReferenceValue = clip;
                }

                serializedBank.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssets();
                if (!bank.TryValidatePlayback(out error))
                {
                    error = "D0 CombatAudioBank is not playback-ready after generation: " + error;
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException
                || exception is InvalidOperationException
                || exception is ArgumentException)
            {
                error = "D0 procedural audio generation failed: " + exception.Message;
                return false;
            }
        }

        private static SerializedProperty FindCueEntry(
            SerializedProperty entries,
            CombatAudioCue cue)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty candidate = entries.GetArrayElementAtIndex(index);
                SerializedProperty candidateCue = candidate.FindPropertyRelative("cue");
                if (candidateCue != null && candidateCue.intValue == (int)cue)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static AudioClip LoadReusableClip(CombatAudioCue cue)
        {
            switch (cue)
            {
                case CombatAudioCue.ReticleTargetLock:
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(ReticleLockClipPath);

                case CombatAudioCue.EnemyDangerTick:
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(DangerTickClipPath);

                case CombatAudioCue.PlayerConfirmRelease:
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(ConfirmReleaseClipPath);

                default:
                    return null;
            }
        }

        private static AudioClip EnsureGeneratedClip(
            string assetPath,
            in ProceduralAudioRecipe recipe)
        {
            AudioClip existing = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            WritePcmWave(ToProjectPath(assetPath), recipe);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer != null)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
                sampleSettings.preloadAudioData = true;
                importer.defaultSampleSettings = sampleSettings;
                importer.ambisonic = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static void WritePcmWave(string filePath, in ProceduralAudioRecipe recipe)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(recipe.DurationSeconds * SampleRate));
            int dataSize = sampleCount * ChannelCount * (BitsPerSample / 8);
            using (BinaryWriter writer = new BinaryWriter(
                       File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)ChannelCount);
                writer.Write(SampleRate);
                writer.Write(SampleRate * ChannelCount * (BitsPerSample / 8));
                writer.Write((short)(ChannelCount * (BitsPerSample / 8)));
                writer.Write((short)BitsPerSample);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                uint noiseState = recipe.NoiseSeed;
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float time = sampleIndex / (float)SampleRate;
                    float normalizedTime = sampleIndex / (float)sampleCount;
                    float sample = EvaluateSample(
                        recipe,
                        time,
                        normalizedTime,
                        ref noiseState);
                    writer.Write((short)Mathf.Clamp(
                        sample * short.MaxValue,
                        short.MinValue,
                        short.MaxValue));
                }
            }
        }

        private static float EvaluateSample(
            in ProceduralAudioRecipe recipe,
            float time,
            float normalizedTime,
            ref uint noiseState)
        {
            float chirpRange = recipe.EndFrequency - recipe.StartFrequency;
            float phase = Mathf.PI * 2f * (recipe.StartFrequency * time
                + 0.5f * chirpRange * time * normalizedTime);
            float tonal = Mathf.Sin(phase);
            float harmonic = Mathf.Sin(phase * 2.01f + recipe.HarmonicPhase) * 0.35f;
            float noise = NextSignedNoise(ref noiseState);
            float envelope = EvaluateEnvelope(
                normalizedTime,
                recipe.AttackFraction,
                recipe.ReleaseFraction);
            float pulse = recipe.PulseCount <= 1
                ? 1f
                : Mathf.SmoothStep(
                    0.14f,
                    1f,
                    Mathf.Abs(Mathf.Sin(Mathf.PI * recipe.PulseCount * normalizedTime)));
            float mixed = (tonal + harmonic) * (1f - recipe.NoiseMix)
                + noise * recipe.NoiseMix;
            return Mathf.Clamp(mixed * envelope * pulse * recipe.Gain, -0.92f, 0.92f);
        }

        private static float EvaluateEnvelope(
            float normalizedTime,
            float attackFraction,
            float releaseFraction)
        {
            float attack = Mathf.Clamp01(attackFraction);
            float release = Mathf.Clamp01(releaseFraction);
            float rise = attack <= 0f
                ? 1f
                : Mathf.Clamp01(normalizedTime / attack);
            float fallStart = Mathf.Clamp01(1f - release);
            float fall = normalizedTime <= fallStart || release <= 0f
                ? 1f
                : Mathf.Clamp01((1f - normalizedTime) / release);
            return rise * fall;
        }

        private static float NextSignedNoise(ref uint state)
        {
            // Xorshift is deterministic and local to editor-side asset
            // generation; it never affects gameplay randomness or replay.
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return ((state & 0x00ffffffU) / 8388607.5f) - 1f;
        }

        private static ProceduralAudioRecipe GetRecipe(CombatAudioCue cue)
        {
            switch (cue)
            {
                case CombatAudioCue.PlayerPrimaryShot:
                    return new ProceduralAudioRecipe(0.085f, 1020f, 620f, 0.34f, 0.46f, 0.02f, 0.58f, 1, 0x10a5f31u, 0.2f);
                case CombatAudioCue.PlayerSecondaryCharge:
                    return new ProceduralAudioRecipe(0.32f, 330f, 1120f, 0.25f, 0.12f, 0.08f, 0.28f, 2, 0x21a5f31u, 0.65f);
                case CombatAudioCue.PlayerSecondaryRelease:
                    return new ProceduralAudioRecipe(0.36f, 980f, 240f, 0.38f, 0.4f, 0.01f, 0.5f, 1, 0x31a5f31u, 0.4f);
                case CombatAudioCue.PlayerBodyHit:
                    return new ProceduralAudioRecipe(0.075f, 270f, 130f, 0.33f, 0.64f, 0.01f, 0.72f, 1, 0x41a5f31u, 0.3f);
                case CombatAudioCue.PlayerWeakpointHit:
                    return new ProceduralAudioRecipe(0.13f, 920f, 1640f, 0.3f, 0.08f, 0.01f, 0.64f, 1, 0x51a5f31u, 0.9f);
                case CombatAudioCue.ProjectileIntercept:
                    return new ProceduralAudioRecipe(0.11f, 1420f, 720f, 0.29f, 0.3f, 0.01f, 0.66f, 2, 0x61a5f31u, 0.75f);
                case CombatAudioCue.PlayerReload:
                    return new ProceduralAudioRecipe(0.22f, 240f, 360f, 0.27f, 0.18f, 0.01f, 0.48f, 2, 0x71a5f31u, 0.2f);
                case CombatAudioCue.EnemyFastThreatTelegraph:
                    return new ProceduralAudioRecipe(0.20f, 180f, 280f, 0.3f, 0.22f, 0.02f, 0.48f, 2, 0x81a5f31u, 0.1f);
                case CombatAudioCue.EnemyFastThreatRelease:
                    return new ProceduralAudioRecipe(0.15f, 360f, 110f, 0.36f, 0.44f, 0.01f, 0.68f, 1, 0x91a5f31u, 0.25f);
                case CombatAudioCue.EnemyInterceptableThreatTelegraph:
                    return new ProceduralAudioRecipe(0.23f, 420f, 620f, 0.28f, 0.16f, 0.03f, 0.44f, 3, 0xa1a5f31u, 0.55f);
                case CombatAudioCue.EnemyInterceptableThreatRelease:
                    return new ProceduralAudioRecipe(0.26f, 740f, 430f, 0.31f, 0.28f, 0.01f, 0.62f, 3, 0xb1a5f31u, 0.5f);
                case CombatAudioCue.EnemyHeavyThreatTelegraph:
                    return new ProceduralAudioRecipe(0.42f, 95f, 150f, 0.36f, 0.24f, 0.03f, 0.34f, 2, 0xc1a5f31u, 0.05f);
                case CombatAudioCue.EnemyHeavyThreatRelease:
                    return new ProceduralAudioRecipe(0.33f, 220f, 70f, 0.44f, 0.5f, 0.01f, 0.76f, 1, 0xd1a5f31u, 0.12f);
                case CombatAudioCue.PlayerDamaged:
                    return new ProceduralAudioRecipe(0.18f, 180f, 82f, 0.37f, 0.46f, 0.01f, 0.68f, 1, 0xe1a5f31u, 0.1f);
                case CombatAudioCue.PlayerBarrierBroken:
                    return new ProceduralAudioRecipe(0.24f, 1180f, 180f, 0.35f, 0.52f, 0.01f, 0.72f, 2, 0xf1a5f31u, 0.8f);
                case CombatAudioCue.EnemyBreak:
                    return new ProceduralAudioRecipe(0.30f, 780f, 105f, 0.4f, 0.38f, 0.01f, 0.68f, 2, 0x01a6f31u, 0.6f);
                case CombatAudioCue.Victory:
                    return new ProceduralAudioRecipe(0.62f, 520f, 1280f, 0.32f, 0.08f, 0.02f, 0.32f, 3, 0x11a6f31u, 0.95f);
                case CombatAudioCue.Defeat:
                    return new ProceduralAudioRecipe(0.68f, 420f, 75f, 0.36f, 0.2f, 0.01f, 0.54f, 2, 0x21a6f31u, 0.15f);
                case CombatAudioCue.ReticleTargetLock:
                    return new ProceduralAudioRecipe(0.09f, 780f, 1120f, 0.22f, 0.04f, 0.01f, 0.72f, 1, 0x31a6f31u, 0.8f);
                case CombatAudioCue.EnemyDangerTick:
                    return new ProceduralAudioRecipe(0.08f, 360f, 520f, 0.24f, 0.1f, 0.01f, 0.68f, 1, 0x41a6f31u, 0.2f);
                case CombatAudioCue.PlayerConfirmRelease:
                    return new ProceduralAudioRecipe(0.11f, 960f, 1420f, 0.24f, 0.05f, 0.01f, 0.66f, 1, 0x51a6f31u, 0.9f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue, "D0 audio recipe is missing.");
            }
        }

        private static string GetGeneratedFileName(CombatAudioCue cue)
        {
            return "S_D0_" + cue;
        }

        private static string ToProjectPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected an Assets-relative audio path.", nameof(assetPath));
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private readonly struct ProceduralAudioRecipe
        {
            public ProceduralAudioRecipe(
                float durationSeconds,
                float startFrequency,
                float endFrequency,
                float gain,
                float noiseMix,
                float attackFraction,
                float releaseFraction,
                int pulseCount,
                uint noiseSeed,
                float harmonicPhase)
            {
                DurationSeconds = durationSeconds;
                StartFrequency = startFrequency;
                EndFrequency = endFrequency;
                Gain = gain;
                NoiseMix = noiseMix;
                AttackFraction = attackFraction;
                ReleaseFraction = releaseFraction;
                PulseCount = pulseCount;
                NoiseSeed = noiseSeed == 0U ? 0x6d2b79f5U : noiseSeed;
                HarmonicPhase = harmonicPhase;
            }

            public float DurationSeconds { get; }
            public float StartFrequency { get; }
            public float EndFrequency { get; }
            public float Gain { get; }
            public float NoiseMix { get; }
            public float AttackFraction { get; }
            public float ReleaseFraction { get; }
            public int PulseCount { get; }
            public uint NoiseSeed { get; }
            public float HarmonicPhase { get; }
        }
    }
}
