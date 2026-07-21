using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0SliceInstallationStateTests
    {
        [Test]
        public void InstallationStateFailsClosedUntilItOwnsCombatLabWithBothBanks()
        {
            D0SliceInstallationState state = ScriptableObject.CreateInstance<D0SliceInstallationState>();
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
            CombatAudioBank audioBank = ScriptableObject.CreateInstance<CombatAudioBank>();
            try
            {
                Assert.That(state.ProtectsCombatLab, Is.False);
                Assert.That(state.TryValidate(out string initialError), Is.False);
                Assert.That(initialError, Does.Contain("not complete"));

                ConfigureState(
                    state,
                    profile,
                    audioBank,
                    "Assets/FPGDemo/Scenes/CombatLab.unity",
                    installationComplete: true,
                    installationRevision: 1);

                Assert.That(state.ProtectsCombatLab, Is.True);
                Assert.That(state.TryValidate(out string error), Is.True, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioBank);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(state);
            }
        }

        [TestCase(typeof(BoxCollider))]
        [TestCase(typeof(BoxCollider2D))]
        [TestCase(typeof(Rigidbody))]
        [TestCase(typeof(Rigidbody2D))]
        public void MarkerRejectsEveryPhysicsFamilyAnywhereInItsOwnedPresentationTree(
            System.Type forbiddenComponentType)
        {
            D0SliceInstallationState state = ScriptableObject.CreateInstance<D0SliceInstallationState>();
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
            CombatAudioBank audioBank = ScriptableObject.CreateInstance<CombatAudioBank>();
            GameObject root = new GameObject("D0Slice2DTestRoot");
            try
            {
                ConfigureState(
                    state,
                    profile,
                    audioBank,
                    "Assets/FPGDemo/Scenes/CombatLab.unity",
                    installationComplete: true,
                    installationRevision: 1);
                D0SliceInstallationMarker marker = root.AddComponent<D0SliceInstallationMarker>();
                ConfigureMarker(marker, profile, audioBank, state);

                Assert.That(marker.TryValidate(out string initialError), Is.True, initialError);

                GameObject invalidChild = new GameObject("InvalidPhysicsChild");
                invalidChild.transform.SetParent(root.transform, false);
                invalidChild.AddComponent(forbiddenComponentType);

                Assert.That(marker.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("collider or rigidbody"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(audioBank);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(state);
            }
        }

        [Test]
        public void InstallationStateDoesNotProtectAnyOtherScenePath()
        {
            D0SliceInstallationState state = ScriptableObject.CreateInstance<D0SliceInstallationState>();
            CombatPresentationProfile profile = ScriptableObject.CreateInstance<CombatPresentationProfile>();
            CombatAudioBank audioBank = ScriptableObject.CreateInstance<CombatAudioBank>();
            try
            {
                ConfigureState(
                    state,
                    profile,
                    audioBank,
                    "Assets/FPGDemo/Scenes/OtherScene.unity",
                    installationComplete: true,
                    installationRevision: 1);

                Assert.That(state.ProtectsCombatLab, Is.False);
                Assert.That(state.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("explicitly own CombatLab"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(audioBank);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(state);
            }
        }

        private static void ConfigureState(
            D0SliceInstallationState state,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank,
            string scenePath,
            bool installationComplete,
            int installationRevision)
        {
            SerializedObject serializedState = new SerializedObject(state);
            serializedState.FindProperty("installationComplete").boolValue = installationComplete;
            serializedState.FindProperty("ownedScenePath").stringValue = scenePath;
            serializedState.FindProperty("presentationProfile").objectReferenceValue = profile;
            serializedState.FindProperty("audioBank").objectReferenceValue = audioBank;
            serializedState.FindProperty("installationRevision").intValue = installationRevision;
            serializedState.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMarker(
            D0SliceInstallationMarker marker,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank,
            D0SliceInstallationState state)
        {
            SerializedObject serializedMarker = new SerializedObject(marker);
            serializedMarker.FindProperty("presentationProfile").objectReferenceValue = profile;
            serializedMarker.FindProperty("audioBank").objectReferenceValue = audioBank;
            serializedMarker.FindProperty("installationState").objectReferenceValue = state;
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
