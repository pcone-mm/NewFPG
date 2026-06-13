using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NewFPG.Prototype
{
    [ExecuteAlways]
    public sealed class PrototypeFirstPersonWeaponView : MonoBehaviour
    {
        private const string ViewLayerName = "FirstPersonWeapon";
        private const string RigName = "FirstPersonWeaponRig";
        private const string CameraName = "FirstPersonWeaponCamera";
        private const string MaterialName = "FirstPersonWeaponMaterial";

        [Header("Binding")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Camera weaponCamera;
        [SerializeField] private Transform weaponRig;
        [SerializeField] private bool parentModuleToWorldCamera = true;
        [SerializeField] private bool useUrpCameraStack = true;
        [SerializeField] private bool rebuildOnEnable = true;

        [Header("Rendering")]
        [SerializeField] private Texture2D[] weaponTextures;
        [SerializeField] private float weaponFieldOfView = 42f;
        [SerializeField] private float nearClipPlane = 0.01f;
        [SerializeField] private float farClipPlane = 8f;
        [SerializeField] private float weaponDepth = 1.35f;

        [Header("Layout")]
        [SerializeField] private List<WeaponPanelPose> weapons = new List<WeaponPanelPose>
        {
            new WeaponPanelPose("Left Blade", 1, new Vector3(-0.43f, -0.39f, 1.35f), new Vector3(0f, 0f, -80f), 0.92f, 0),
            new WeaponPanelPose("Center Sword", 0, new Vector3(0.02f, -0.37f, 1.18f), new Vector3(0f, 0f, -90f), 0.72f, 2),
            new WeaponPanelPose("Right Brush", 6, new Vector3(0.41f, -0.58f, 1.28f), new Vector3(0f, 0f, -106.2f), 0.96f, 1),
        };

        private readonly List<GameObject> spawnedWeapons = new List<GameObject>();

        private void Reset()
        {
            LoadDefaultTexturesInEditor();
        }

        private void OnEnable()
        {
            EnsureWeaponView();
            if (rebuildOnEnable)
            {
                RebuildWeapons();
            }
        }

        private void OnDisable()
        {
            RemoveWeaponCameraFromStack();
        }

        private void OnValidate()
        {
            weaponFieldOfView = Mathf.Clamp(weaponFieldOfView, 20f, 80f);
            nearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            farClipPlane = Mathf.Max(nearClipPlane + 0.1f, farClipPlane);
            weaponDepth = Mathf.Clamp(weaponDepth, nearClipPlane + 0.05f, farClipPlane - 0.05f);
        }

        [ContextMenu("Rebuild First Person Weapon View")]
        public void RebuildWeapons()
        {
            if (!CanModifySceneObject())
            {
                return;
            }

            EnsureWeaponView();
            if (weaponRig == null)
            {
                return;
            }

            LoadDefaultTexturesInEditor();
            ClearSpawnedWeapons();

            for (int i = 0; i < weapons.Count; i++)
            {
                CreateWeapon(weapons[i]);
            }
        }

        public void EnsureWeaponView()
        {
            if (!CanModifySceneObject())
            {
                return;
            }

            ResolveWorldCamera();
            if (worldCamera == null)
            {
                return;
            }

            int weaponLayer = LayerMask.NameToLayer(ViewLayerName);
#if UNITY_EDITOR
            if (weaponLayer < 0)
            {
                EnsureLayerExistsInEditor(ViewLayerName);
                weaponLayer = LayerMask.NameToLayer(ViewLayerName);
            }
#endif
            if (weaponLayer < 0)
            {
                Debug.LogWarning("Missing layer '" + ViewLayerName + "'. Add it in Project Settings > Tags and Layers.", this);
                return;
            }

            AttachModuleToWorldCamera();
            EnsureRig(weaponLayer);
            EnsureWeaponCamera(weaponLayer);
            worldCamera.cullingMask &= ~(1 << weaponLayer);
        }

        private bool CanModifySceneObject()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return false;
            }
#endif
            return gameObject.scene.IsValid();
        }

        private void ResolveWorldCamera()
        {
            if (IsValidWorldCamera(worldCamera))
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (IsValidWorldCamera(mainCamera))
            {
                worldCamera = mainCamera;
                return;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (IsValidWorldCamera(cameras[i]))
                {
                    worldCamera = cameras[i];
                    return;
                }
            }

            worldCamera = null;
        }

        private bool IsValidWorldCamera(Camera candidate)
        {
            return candidate != null
                && candidate != weaponCamera
                && candidate.transform != transform
                && candidate.cameraType == CameraType.Game
                && candidate.isActiveAndEnabled;
        }

        private void AttachModuleToWorldCamera()
        {
            if (!parentModuleToWorldCamera || worldCamera == null || transform == worldCamera.transform)
            {
                return;
            }

            if (transform.parent != worldCamera.transform)
            {
                transform.SetParent(worldCamera.transform, false);
            }

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = InverseLossyScale(worldCamera.transform);
        }

        private void EnsureRig(int weaponLayer)
        {
            if (weaponRig == null)
            {
                Transform existing = transform.Find(RigName);
                if (existing != null)
                {
                    weaponRig = existing;
                }
            }

            if (weaponRig == null)
            {
                GameObject rigObject = new GameObject(RigName);
                weaponRig = rigObject.transform;
                weaponRig.SetParent(transform, false);
            }

            weaponRig.localPosition = Vector3.zero;
            weaponRig.localRotation = Quaternion.identity;
            weaponRig.localScale = Vector3.one;
            SetLayerRecursively(weaponRig, weaponLayer);
        }

        private void EnsureWeaponCamera(int weaponLayer)
        {
            if (weaponCamera == null)
            {
                Transform existing = transform.Find(CameraName);
                if (existing != null)
                {
                    weaponCamera = existing.GetComponent<Camera>();
                }
            }

            if (weaponCamera == null)
            {
                GameObject cameraObject = new GameObject(CameraName, typeof(Camera));
                cameraObject.transform.SetParent(transform, false);
                weaponCamera = cameraObject.GetComponent<Camera>();
            }

            weaponCamera.transform.localPosition = Vector3.zero;
            weaponCamera.transform.localRotation = Quaternion.identity;
            weaponCamera.transform.localScale = Vector3.one;
            weaponCamera.clearFlags = CameraClearFlags.Depth;
            weaponCamera.cullingMask = 1 << weaponLayer;
            weaponCamera.nearClipPlane = nearClipPlane;
            weaponCamera.farClipPlane = farClipPlane;
            weaponCamera.fieldOfView = weaponFieldOfView;
            weaponCamera.depth = worldCamera.depth + 10f;
            weaponCamera.allowHDR = worldCamera.allowHDR;
            weaponCamera.allowMSAA = worldCamera.allowMSAA;
            weaponCamera.useOcclusionCulling = false;
            weaponCamera.enabled = true;

            UniversalAdditionalCameraData worldCameraData = worldCamera.GetUniversalAdditionalCameraData();
            worldCameraData.renderType = CameraRenderType.Base;

            UniversalAdditionalCameraData weaponCameraData = weaponCamera.GetUniversalAdditionalCameraData();
            weaponCameraData.renderType = useUrpCameraStack ? CameraRenderType.Overlay : CameraRenderType.Base;
            weaponCameraData.renderPostProcessing = false;

            if (useUrpCameraStack)
            {
                if (!worldCameraData.cameraStack.Contains(weaponCamera))
                {
                    worldCameraData.cameraStack.Add(weaponCamera);
                }
            }
            else
            {
                worldCameraData.cameraStack.Remove(weaponCamera);
            }
        }

        private void RemoveWeaponCameraFromStack()
        {
            if (worldCamera == null || weaponCamera == null)
            {
                return;
            }

            UniversalAdditionalCameraData worldCameraData = worldCamera.GetUniversalAdditionalCameraData();
            if (worldCameraData != null)
            {
                worldCameraData.cameraStack.Remove(weaponCamera);
            }
        }

        private void CreateWeapon(WeaponPanelPose pose)
        {
            if (weaponRig == null || weaponTextures == null || pose.textureIndex < 0 || pose.textureIndex >= weaponTextures.Length)
            {
                return;
            }

            Texture2D texture = weaponTextures[pose.textureIndex];
            if (texture == null)
            {
                return;
            }

            int weaponLayer = LayerMask.NameToLayer(ViewLayerName);
            GameObject weaponObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            weaponObject.name = pose.name;
            weaponObject.layer = weaponLayer;
            weaponObject.transform.SetParent(weaponRig, false);
            weaponObject.transform.localPosition = new Vector3(pose.localPosition.x, pose.localPosition.y, Mathf.Max(pose.localPosition.z, weaponDepth));
            weaponObject.transform.localRotation = Quaternion.Euler(pose.localEulerAngles);

            float aspect = texture.width > 0 && texture.height > 0 ? (float)texture.width / texture.height : 1f;
            weaponObject.transform.localScale = new Vector3(pose.width, pose.width / Mathf.Max(0.01f, aspect), 1f);

            Collider weaponCollider = weaponObject.GetComponent<Collider>();
            if (weaponCollider != null)
            {
                DestroyImmediateSafe(weaponCollider);
            }

            Renderer renderer = weaponObject.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(texture, pose.sortingOrder);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            spawnedWeapons.Add(weaponObject);
        }

        private Material CreateMaterial(Texture2D texture, int renderQueueOffset)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            Material material = new Material(shader);
            material.name = MaterialName + "_" + texture.name;
            material.mainTexture = texture;
            material.renderQueue = 3000 + renderQueueOffset;
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            return material;
        }

        private void ClearSpawnedWeapons()
        {
            for (int i = spawnedWeapons.Count - 1; i >= 0; i--)
            {
                if (spawnedWeapons[i] != null)
                {
                    DestroyImmediateSafe(spawnedWeapons[i]);
                }
            }

            spawnedWeapons.Clear();

            if (weaponRig == null)
            {
                return;
            }

            for (int i = weaponRig.childCount - 1; i >= 0; i--)
            {
                Transform child = weaponRig.GetChild(i);
                if (child != null && child.GetComponent<Renderer>() != null)
                {
                    DestroyImmediateSafe(child.gameObject);
                }
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static Vector3 InverseLossyScale(Transform target)
        {
            Vector3 scale = target.lossyScale;
            return new Vector3(SafeInverse(scale.x), SafeInverse(scale.y), SafeInverse(scale.z));
        }

        private static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        private static void DestroyImmediateSafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void LoadDefaultTexturesInEditor()
        {
#if UNITY_EDITOR
            if (!NeedsDefaultTextures())
            {
                return;
            }

            weaponTextures = new[]
            {
                LoadWeaponTexture("1.png"),
                LoadWeaponTexture("2.png"),
                LoadWeaponTexture("3.png"),
                LoadWeaponTexture("4.png"),
                LoadWeaponTexture("5.png"),
                LoadWeaponTexture("6.png"),
                LoadWeaponTexture("7.png"),
            };
#endif
        }

#if UNITY_EDITOR
        private bool NeedsDefaultTextures()
        {
            if (weaponTextures == null || weaponTextures.Length < 7)
            {
                return true;
            }

            return weaponTextures[0] == null || weaponTextures[1] == null || weaponTextures[6] == null;
        }

        private static Texture2D LoadWeaponTexture(string fileName)
        {
            string assetName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets(assetName + " t:Texture2D", new[] { "Assets/Art" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/" + fileName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }

        private static void EnsureLayerExistsInEditor(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == layerName)
                {
                    return;
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    return;
                }
            }
        }
#endif

        [System.Serializable]
        public struct WeaponPanelPose
        {
            public string name;
            public int textureIndex;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public float width;
            public int sortingOrder;

            public WeaponPanelPose(
                string name,
                int textureIndex,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                float width,
                int sortingOrder)
            {
                this.name = name;
                this.textureIndex = textureIndex;
                this.localPosition = localPosition;
                this.localEulerAngles = localEulerAngles;
                this.width = width;
                this.sortingOrder = sortingOrder;
            }
        }
    }
}
