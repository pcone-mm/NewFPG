using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgRoomArtRoot : MonoBehaviour
    {
        [SerializeField]
        private FpgRoomDefinition roomDefinition;

        [SerializeField]
        private Light mainDirectionalLight;

        private readonly List<IFpgRoomArtPresentationBinding> boundBindings =
            new List<IFpgRoomArtPresentationBinding>();

        private bool bindingInProgress;

        public FpgRoomDefinition RoomDefinition => roomDefinition;
        public Light MainDirectionalLight => mainDirectionalLight;
        public bool IsPresentationBound => boundBindings.Count > 0;

        public static bool TryResolve(
            Scene scene,
            FpgRoomDefinition expectedRoom,
            out FpgRoomArtRoot artRoot,
            out string error)
        {
            artRoot = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "Art Scene must be valid and loaded before resolving its root.";
                return false;
            }

            GameObject[] allSceneRoots = scene.GetRootGameObjects();
            List<GameObject> sceneRoots = new List<GameObject>(
                allSceneRoots.Length);
            for (int index = 0; index < allSceneRoots.Length; index++)
            {
                GameObject sceneRoot = allSceneRoots[index];
                if ((sceneRoot.hideFlags & HideFlags.DontSaveInEditor) == 0)
                {
                    sceneRoots.Add(sceneRoot);
                }
            }

            if (sceneRoots.Count != 1)
            {
                error =
                    $"Art Scene '{scene.path}' must contain exactly one scene root after excluding transient editor previews; found {sceneRoots.Count}.";
                return false;
            }

            FpgRoomArtRoot[] candidates =
                sceneRoots[0].GetComponentsInChildren<FpgRoomArtRoot>(true);
            if (candidates.Length != 1
                || candidates[0].gameObject != sceneRoots[0])
            {
                error = $"Art Scene '{scene.path}' must contain exactly one FpgRoomArtRoot on its sole scene root.";
                return false;
            }

            FpgRoomArtRoot candidate = candidates[0];
            if (!candidate.TryValidate(expectedRoom, out error))
            {
                return false;
            }

            artRoot = candidate;
            return true;
        }

        public bool TryValidate(
            FpgRoomDefinition expectedRoom,
            out string error)
        {
            if (transform.parent != null || !HasIdentityTransform(transform))
            {
                error = "FpgRoomArtRoot must be a scene root with identity position, rotation, and scale.";
                return false;
            }

            if (roomDefinition == null)
            {
                error = "FpgRoomArtRoot requires a RoomDefinition reference.";
                return false;
            }

            if (expectedRoom != null && roomDefinition != expectedRoom)
            {
                error = $"Art Scene root references room '{roomDefinition.RoomId}', but '{expectedRoom.RoomId}' was requested.";
                return false;
            }

            Scene scene = gameObject.scene;
            if (scene.IsValid()
                && !string.IsNullOrEmpty(scene.path)
                && !string.Equals(
                    roomDefinition.ArtScenePath,
                    scene.path,
                    StringComparison.Ordinal))
            {
                error = $"Room '{roomDefinition.RoomId}' expects Art Scene '{roomDefinition.ArtScenePath}', but root belongs to '{scene.path}'.";
                return false;
            }

            if (mainDirectionalLight == null
                || mainDirectionalLight.type != LightType.Directional)
            {
                error = "FpgRoomArtRoot requires an explicit directional main light.";
                return false;
            }

            if (mainDirectionalLight.gameObject.scene != scene)
            {
                error = "FpgRoomArtRoot main light must belong to the same Art Scene.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryBindPresentation(
            FpgRoomArtPresentationContext context,
            out string error)
        {
            if (bindingInProgress)
            {
                error = "Room art presentation binding is already in progress.";
                return false;
            }

            if (context == null)
            {
                error = "Room art presentation context is missing.";
                return false;
            }

            if (!context.TryValidate(out error))
            {
                return false;
            }

            if (context.MainLight != mainDirectionalLight)
            {
                error = "Room art presentation context must use the Art Scene root's main light.";
                return false;
            }

            bindingInProgress = true;
            try
            {
                TryUnbindPresentation(out _);
                MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    if (!(behaviours[index] is IFpgRoomArtPresentationBinding binding))
                    {
                        continue;
                    }

                    try
                    {
                        if (!binding.TryBind(context, out string bindingError))
                        {
                            TryUnbindPresentation(out _);
                            error = string.IsNullOrWhiteSpace(bindingError)
                                ? $"Room art binding '{behaviours[index].GetType().Name}' rejected the presentation context."
                                : bindingError;
                            return false;
                        }
                    }
                    catch (Exception exception)
                    {
                        TryUnbindPresentation(out _);
                        error = $"Room art binding '{behaviours[index].GetType().Name}' threw while binding: {exception.Message}";
                        return false;
                    }

                    boundBindings.Add(binding);
                }
            }
            finally
            {
                bindingInProgress = false;
            }

            error = string.Empty;
            return true;
        }

        public void UnbindPresentation()
        {
            TryUnbindPresentation(out _);
        }

        public bool TryUnbindPresentation(out string error)
        {
            string firstError = null;
            for (int index = boundBindings.Count - 1; index >= 0; index--)
            {
                IFpgRoomArtPresentationBinding binding = boundBindings[index];
                if (binding is UnityEngine.Object unityObject && unityObject == null)
                {
                    continue;
                }

                try
                {
                    binding.Unbind();
                }
                catch (Exception exception)
                {
                    if (firstError == null)
                    {
                        firstError = $"Room art binding '{binding.GetType().Name}' threw while unbinding: {exception.Message}";
                    }
                }
            }

            boundBindings.Clear();
            error = firstError ?? string.Empty;
            return firstError == null;
        }

        private static bool HasIdentityTransform(Transform target)
        {
            return FpgRoomValidationUtility.IsFinite(target.localPosition)
                && FpgRoomValidationUtility.IsFinite(target.localEulerAngles)
                && FpgRoomValidationUtility.IsFinite(target.localScale)
                && target.localPosition.sqrMagnitude <= 0.000001f
                && Quaternion.Angle(target.localRotation, Quaternion.identity) <= 0.001f
                && (target.localScale - Vector3.one).sqrMagnitude <= 0.000001f;
        }

        private void OnDestroy()
        {
            TryUnbindPresentation(out _);
        }
    }
}
