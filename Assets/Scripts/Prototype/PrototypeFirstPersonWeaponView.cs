using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
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
        private const string PointerHitboxSuffix = " Pointer Hitbox";
        private const float ScreenPointerPadding = 12f;

        [Header("Binding")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Camera weaponCamera;
        [SerializeField] private Transform weaponRig;
        [SerializeField] private bool parentModuleToWorldCamera = true;
        [SerializeField] private bool useUrpCameraStack = true;
        [SerializeField] private bool rebuildOnEnable = true;

        [Header("Rendering")]
        [SerializeField] private float weaponFieldOfView = 42f;
        [SerializeField] private float nearClipPlane = 0.01f;
        [SerializeField] private float farClipPlane = 8f;
        [SerializeField] private float weaponDepth = 1.35f;

        [Header("Interaction")]
#if ODIN_INSPECTOR
        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
#endif
        [SerializeField] private PrototypeFirstPersonWeaponInteractionConfig interactionConfig;

        [Header("Layout")]
        [SerializeField] private List<WeaponPanelPose> weapons = new List<WeaponPanelPose>
        {
            new WeaponPanelPose("Left Blade", new Vector3(-0.43f, -0.39f, 1.35f), new Vector3(0f, 0f, -80f), 0.92f, 0),
            new WeaponPanelPose("Center Sword", new Vector3(0.02f, -0.37f, 1.18f), new Vector3(0f, 0f, -90f), 0.72f, 2),
            new WeaponPanelPose("Right Brush", new Vector3(0.41f, -0.58f, 1.28f), new Vector3(0f, 0f, -106.2f), 0.96f, 1),
        };

        private readonly List<GameObject> spawnedWeapons = new List<GameObject>();
        private readonly List<RuntimeWeapon> runtimeWeapons = new List<RuntimeWeapon>();
        private static Mesh cameraFacingQuadMesh;
        private WeaponPresentation[] weaponPresentations;
        private RuntimeWeapon pointedWeapon;
        private RuntimeWeapon pressedWeapon;
        private float pointerPressedAt;
        private Vector2 pointerPressedPosition;
#if UNITY_EDITOR
        private bool syncingWeaponPosesFromScene;
#endif

        public event System.Func<WeaponPointerContext, bool> WeaponPointerPressed;
        public event System.Action<WeaponPointerContext> WeaponPointerHeld;
        public event System.Action<WeaponPointerContext> WeaponPointerReleased;
        public event System.Action<WeaponPointerContext> WeaponPointerCancelled;
        public event System.Func<WeaponAttackContext, bool> WeaponAttackRequested;
        public event System.Action<WeaponAttackContext> WeaponAttackStarted;

        private void Reset()
        {
            AssignDefaultInteractionConfigInEditor();
        }

        private void OnEnable()
        {
            EnsureWeaponView();
            if (rebuildOnEnable)
            {
                RebuildWeapons();
            }

            if (Application.isPlaying)
            {
                RegisterRuntimeWeaponsFromRig();
            }
        }

        private void OnDisable()
        {
            CancelActivePointerPress();
            KillRuntimeWeaponTweens();
            RemoveWeaponCameraFromStack();
        }

        private void OnValidate()
        {
            weaponFieldOfView = Mathf.Clamp(weaponFieldOfView, 20f, 80f);
            nearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            farClipPlane = Mathf.Max(nearClipPlane + 0.1f, farClipPlane);
            weaponDepth = Mathf.Clamp(weaponDepth, nearClipPlane + 0.05f, farClipPlane - 0.05f);
            AssignDefaultInteractionConfigInEditor();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                SyncWeaponPosesFromScene();
#endif
                return;
            }

            if (interactionConfig == null)
            {
                return;
            }

            UpdatePointerInteraction();
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

            ClearSpawnedWeapons();

            int weaponCount = HasWeaponPresentations()
                ? weaponPresentations.Length
                : weapons != null ? weapons.Count : 0;

            for (int i = 0; i < weaponCount; i++)
            {
                CreateWeapon(ResolveWeaponPanelPose(i, weaponCount), i);
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

        public void SetWorldCamera(Camera camera)
        {
            if (camera == null || camera == weaponCamera)
            {
                return;
            }

            worldCamera = camera;
        }

        public void RefreshRuntimeView(Camera camera)
        {
            SetWorldCamera(camera);
            EnsureWeaponView();
            RebuildWeapons();

            if (Application.isPlaying)
            {
                RegisterRuntimeWeaponsFromRig();
            }
        }

        public void SetWeaponPresentations(WeaponPresentation[] presentations)
        {
            if (presentations == null || presentations.Length == 0)
            {
                weaponPresentations = null;
            }
            else
            {
                weaponPresentations = new WeaponPresentation[presentations.Length];
                for (int i = 0; i < presentations.Length; i++)
                {
                    weaponPresentations[i] = presentations[i];
                }
            }

            RebuildWeapons();
        }

        public bool PlayWeaponAttack(int weaponIndex)
        {
            RuntimeWeapon weapon = FindRuntimeWeaponByIndex(weaponIndex);
            if (weapon == null)
            {
                return false;
            }

            BeginAttack(weapon, false);
            return true;
        }

        private bool CanModifySceneObject()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                {
                    return false;
                }

                if (EditorSceneManager.IsPreviewScene(gameObject.scene) || EditorSceneManager.IsPreviewSceneObject(gameObject))
                {
                    return false;
                }
            }
#endif
            return gameObject.scene.IsValid() && gameObject.scene.isLoaded;
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
                && candidate.gameObject.scene == gameObject.scene
                && !candidate.transform.IsChildOf(transform)
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
                RemoveInvalidStackCameras(worldCameraData);
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

        private void CreateWeapon(WeaponPanelPose pose, int poseIndex)
        {
            if (weaponRig == null || !TryResolveWeaponVisual(pose, poseIndex, out WeaponVisualSource visual))
            {
                return;
            }

            int weaponLayer = LayerMask.NameToLayer(ViewLayerName);
            GameObject weaponObject = new GameObject(
                HasWeaponPresentations()
                ? visual.name + " " + (poseIndex + 1).ToString()
                : visual.name);
            weaponObject.layer = weaponLayer;
            weaponObject.transform.SetParent(weaponRig, false);
            weaponObject.transform.localPosition = pose.localPosition;
            weaponObject.transform.localRotation = Quaternion.Euler(pose.localEulerAngles);

            float displayAspect = Mathf.Max(0.01f, visual.aspect);
            weaponObject.transform.localScale = new Vector3(pose.width, pose.width / displayAspect, 1f);
            MarkGeneratedWeaponObject(weaponObject, false);

            MeshFilter meshFilter = weaponObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetCameraFacingQuadMesh();

            Renderer renderer = weaponObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(visual.texture, visual.name, pose.sortingOrder, visual.textureScale, visual.textureOffset);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            DisableVisualColliders(weaponObject);
            GameObject hitboxObject = CreateOrUpdatePointerHitbox(weaponObject);

            spawnedWeapons.Add(weaponObject);
            runtimeWeapons.Add(new RuntimeWeapon(weaponObject, hitboxObject, poseIndex));
        }

        private bool HasWeaponPresentations()
        {
            return weaponPresentations != null && weaponPresentations.Length > 0;
        }

        private WeaponPanelPose ResolveWeaponPanelPose(int index, int totalCount)
        {
            if (weapons != null && index >= 0 && index < weapons.Count)
            {
                return weapons[index];
            }

            int safeTotalCount = Mathf.Max(1, totalCount);
            float center = (safeTotalCount - 1) * 0.5f;
            float x = (index - center) * 0.32f;
            float normalizedFromCenter = center > 0f ? Mathf.Abs(index - center) / center : 0f;
            return new WeaponPanelPose(
                "Weapon " + (index + 1).ToString(),
                new Vector3(x, -0.48f - normalizedFromCenter * 0.08f, weaponDepth),
                new Vector3(0f, 0f, -90f - x * 28f),
                0.72f,
                index);
        }

        private bool TryResolveWeaponVisual(WeaponPanelPose pose, int poseIndex, out WeaponVisualSource visual)
        {
            if (HasWeaponPresentations())
            {
                if (poseIndex < 0 || poseIndex >= weaponPresentations.Length)
                {
                    visual = default;
                    return false;
                }

                WeaponPresentation presentation = weaponPresentations[poseIndex];
                string presentationName = !string.IsNullOrWhiteSpace(presentation.DisplayName)
                    ? presentation.DisplayName
                    : pose.name;

                if (presentation.Icon != null && presentation.Icon.texture != null)
                {
                    visual = WeaponVisualSource.FromSprite(presentationName, presentation.Icon);
                    return true;
                }

                visual = default;
                return false;
            }

            visual = default;
            return false;
        }

        private GameObject CreateOrUpdatePointerHitbox(GameObject visualObject)
        {
            GameObject hitboxObject = FindPointerHitbox(visualObject.name);
            if (hitboxObject == null)
            {
                hitboxObject = new GameObject(visualObject.name + PointerHitboxSuffix);
                hitboxObject.transform.SetParent(weaponRig, false);
                spawnedWeapons.Add(hitboxObject);
            }

            int weaponLayer = LayerMask.NameToLayer(ViewLayerName);
            if (weaponLayer >= 0)
            {
                hitboxObject.layer = weaponLayer;
            }

            MarkGeneratedWeaponObject(hitboxObject, true);
            CopyPointerHitboxTransform(visualObject.transform, hitboxObject.transform);
            EnsurePointerCollider(hitboxObject);
            return hitboxObject;
        }

        private GameObject FindPointerHitbox(string visualName)
        {
            if (weaponRig == null)
            {
                return null;
            }

            Transform hitbox = weaponRig.Find(visualName + PointerHitboxSuffix);
            return hitbox != null ? hitbox.gameObject : null;
        }

        private static void CopyPointerHitboxTransform(Transform visualTransform, Transform hitboxTransform)
        {
            hitboxTransform.localPosition = visualTransform.localPosition;
            hitboxTransform.localRotation = visualTransform.localRotation;
            hitboxTransform.localScale = visualTransform.localScale;
        }

        private static void DisableVisualColliders(GameObject visualObject)
        {
            Collider[] colliders = visualObject.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static void EnsurePointerCollider(GameObject hitboxObject)
        {
            Collider[] colliders = hitboxObject.GetComponents<Collider>();
            BoxCollider boxCollider = hitboxObject.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = hitboxObject.AddComponent<BoxCollider>();
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != boxCollider)
                {
                    colliders[i].enabled = false;
                }
            }

            boxCollider.enabled = true;
            boxCollider.isTrigger = false;
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(1f, 1f, 0.2f);
        }

        private static void MarkGeneratedWeaponObject(GameObject target, bool hideInHierarchy)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            HideFlags flags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            if (hideInHierarchy)
            {
                flags |= HideFlags.HideInHierarchy;
            }

            target.hideFlags = flags;
#endif
        }

        private Material CreateMaterial(
            Texture2D texture,
            string materialSuffix,
            int renderQueueOffset,
            Vector2 textureScale,
            Vector2 textureOffset)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            Material material = new Material(shader);
            material.name = MaterialName + "_" + (!string.IsNullOrWhiteSpace(materialSuffix) ? materialSuffix : texture.name);
            material.mainTexture = texture;
            material.mainTextureScale = textureScale;
            material.mainTextureOffset = textureOffset;
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
                material.SetTextureScale("_BaseMap", textureScale);
                material.SetTextureOffset("_BaseMap", textureOffset);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", textureScale);
                material.SetTextureOffset("_MainTex", textureOffset);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            return material;
        }

        private static Mesh GetCameraFacingQuadMesh()
        {
            if (cameraFacingQuadMesh != null)
            {
                return cameraFacingQuadMesh;
            }

            cameraFacingQuadMesh = new Mesh
            {
                name = "FirstPersonWeaponCameraFacingQuad",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                },
                triangles = new[]
                {
                    0, 2, 1,
                    2, 3, 1,
                },
            };
            cameraFacingQuadMesh.RecalculateNormals();
            cameraFacingQuadMesh.RecalculateBounds();
            return cameraFacingQuadMesh;
        }

        private void ClearSpawnedWeapons()
        {
            pointedWeapon = null;
            ClearActivePointerPress();
            KillRuntimeWeaponTweens();
            runtimeWeapons.Clear();

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
                if (child != null && (child.GetComponent<Renderer>() != null || child.name.EndsWith(PointerHitboxSuffix, System.StringComparison.Ordinal)))
                {
                    DestroyImmediateSafe(child.gameObject);
                }
            }
        }

        private void RegisterRuntimeWeaponsFromRig()
        {
            KillRuntimeWeaponTweens();
            pointedWeapon = null;
            ClearActivePointerPress();
            runtimeWeapons.Clear();

            if (weaponRig == null)
            {
                return;
            }

            for (int i = 0; i < weaponRig.childCount; i++)
            {
                Transform child = weaponRig.GetChild(i);
                if (child == null || child.GetComponent<Renderer>() == null)
                {
                    continue;
                }

                DisableVisualColliders(child.gameObject);
                GameObject hitboxObject = CreateOrUpdatePointerHitbox(child.gameObject);
                runtimeWeapons.Add(new RuntimeWeapon(child.gameObject, hitboxObject, runtimeWeapons.Count));
            }
        }

        private static void RemoveInvalidStackCameras(UniversalAdditionalCameraData cameraData)
        {
            if (cameraData == null)
            {
                return;
            }

            for (int i = cameraData.cameraStack.Count - 1; i >= 0; i--)
            {
                if (cameraData.cameraStack[i] == null)
                {
                    cameraData.cameraStack.RemoveAt(i);
                }
            }
        }

        private void UpdatePointerInteraction()
        {
            if (runtimeWeapons.Count == 0)
            {
                RegisterRuntimeWeaponsFromRig();
            }

            if (weaponCamera == null || runtimeWeapons.Count == 0)
            {
                pointedWeapon = null;
                ReturnUnpointedHoverWeapons();
                return;
            }

            RuntimeWeapon hitWeapon = FindPointerWeapon();
            pointedWeapon = hitWeapon;

            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                RuntimeWeapon weapon = runtimeWeapons[i];
                if (weapon.State == WeaponInteractionState.Attack)
                {
                    continue;
                }

                if (weapon == hitWeapon)
                {
                    BeginHover(weapon);
                }
                else
                {
                    BeginReturn(weapon);
                }
            }

            if (pressedWeapon != null)
            {
                UpdateActivePointerPress();
            }

            if (hitWeapon != null && hitWeapon.State != WeaponInteractionState.Attack && WasPointerPressedThisFrame())
            {
                BeginPointerPress(hitWeapon);
            }
        }

        private RuntimeWeapon FindPointerWeapon()
        {
            int weaponLayer = LayerMask.NameToLayer(ViewLayerName);
            if (weaponLayer < 0 || weaponCamera == null || !weaponCamera.isActiveAndEnabled)
            {
                return null;
            }

            if (!TryReadPointerPosition(out Vector2 pointerPosition))
            {
                return null;
            }

            RuntimeWeapon physicsHit = FindPointerWeaponByPhysics(pointerPosition, weaponLayer);
            if (physicsHit != null)
            {
                return physicsHit;
            }

            return FindPointerWeaponByScreenRect(pointerPosition);
        }

        private RuntimeWeapon FindPointerWeaponByPhysics(Vector2 pointerPosition, int weaponLayer)
        {
            Ray ray = weaponCamera.ScreenPointToRay(pointerPosition);
            float raycastDistance = farClipPlane + interactionConfig.AttackForwardOffset + interactionConfig.RaycastDistancePadding;
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, 1 << weaponLayer, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return FindRuntimeWeapon(hit.collider.transform);
        }

        private RuntimeWeapon FindPointerWeaponByScreenRect(Vector2 pointerPosition)
        {
            RuntimeWeapon bestWeapon = null;
            float bestDistanceSqr = float.MaxValue;

            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                RuntimeWeapon weapon = runtimeWeapons[i];
                if (!TryGetWeaponScreenRect(weapon, out Rect screenRect))
                {
                    continue;
                }

                screenRect.xMin -= ScreenPointerPadding;
                screenRect.xMax += ScreenPointerPadding;
                screenRect.yMin -= ScreenPointerPadding;
                screenRect.yMax += ScreenPointerPadding;

                if (!screenRect.Contains(pointerPosition))
                {
                    continue;
                }

                Vector2 center = screenRect.center;
                float distanceSqr = (pointerPosition - center).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestWeapon = weapon;
                }
            }

            return bestWeapon;
        }

        private bool TryGetWeaponScreenRect(RuntimeWeapon weapon, out Rect rect)
        {
            rect = default;
            if (weapon == null || weapon.Transform == null || weaponCamera == null)
            {
                return false;
            }

            Renderer renderer = weapon.Transform.GetComponent<Renderer>();
            Bounds bounds;
            if (renderer != null)
            {
                bounds = renderer.bounds;
            }
            else if (weapon.HitTransform != null)
            {
                Collider collider = weapon.HitTransform.GetComponent<Collider>();
                if (collider == null)
                {
                    return false;
                }

                bounds = collider.bounds;
            }
            else
            {
                return false;
            }

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            bool hasPoint = false;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 screenPoint = weaponCamera.WorldToScreenPoint(corners[i]);
                if (screenPoint.z <= 0f)
                {
                    continue;
                }

                hasPoint = true;
                minX = Mathf.Min(minX, screenPoint.x);
                minY = Mathf.Min(minY, screenPoint.y);
                maxX = Mathf.Max(maxX, screenPoint.x);
                maxY = Mathf.Max(maxY, screenPoint.y);
            }

            if (!hasPoint)
            {
                return false;
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 0.1f && rect.height > 0.1f;
        }

        private RuntimeWeapon FindRuntimeWeapon(Transform hitTransform)
        {
            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                RuntimeWeapon weapon = runtimeWeapons[i];
                if (hitTransform == weapon.HitTransform || hitTransform.IsChildOf(weapon.HitTransform))
                {
                    return weapon;
                }
            }

            return null;
        }

        private RuntimeWeapon FindRuntimeWeaponByIndex(int weaponIndex)
        {
            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                RuntimeWeapon weapon = runtimeWeapons[i];
                if (weapon != null && weapon.WeaponIndex == weaponIndex)
                {
                    return weapon;
                }
            }

            return null;
        }

        private void ReturnUnpointedHoverWeapons()
        {
            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                RuntimeWeapon weapon = runtimeWeapons[i];
                if (weapon.State != WeaponInteractionState.Attack)
                {
                    BeginReturn(weapon);
                }
            }
        }

        private void BeginHover(RuntimeWeapon weapon)
        {
            if (weapon.State == WeaponInteractionState.HoverRise || weapon.State == WeaponInteractionState.HoverLoop)
            {
                return;
            }

            weapon.KillTweens();
            weapon.CaptureAnimationStart();
            weapon.State = WeaponInteractionState.HoverRise;
            weapon.ActiveTween = DOTween.To(
                    () => 0f,
                    t => ApplyHoverFrame(weapon, t),
                    1f,
                    interactionConfig.HoverEnterDuration)
                .SetEase(Ease.Linear)
                .SetTarget(weapon.GameObject)
                .OnComplete(() =>
                {
                    weapon.ActiveTween = null;
                    BeginHoverLoop(weapon);
                });
        }

        private void BeginPointerPress(RuntimeWeapon weapon)
        {
            if (weapon == null || weapon.State == WeaponInteractionState.Attack)
            {
                return;
            }

            TryReadPointerPosition(out pointerPressedPosition);
            pressedWeapon = weapon;
            pointerPressedAt = Time.time;
            WeaponPointerContext context = CreatePointerContext(weapon, pointerPressedPosition);
            if (CanUseDefaultPointerAttack(context))
            {
                ClearActivePointerPress();
                BeginAttack(weapon, true);
            }
        }

        private void UpdateActivePointerPress()
        {
            if (pressedWeapon == null)
            {
                return;
            }

            TryReadPointerPosition(out Vector2 pointerPosition);
            if (WasPointerReleasedThisFrame())
            {
                WeaponPointerReleased?.Invoke(CreatePointerContext(pressedWeapon, pointerPosition));
                ClearActivePointerPress();
                return;
            }

            if (!IsPointerDown())
            {
                CancelActivePointerPress();
                return;
            }

            WeaponPointerHeld?.Invoke(CreatePointerContext(pressedWeapon, pointerPosition));
        }

        private bool CanUseDefaultPointerAttack(WeaponPointerContext context)
        {
            System.Func<WeaponPointerContext, bool> handlers = WeaponPointerPressed;
            if (handlers == null)
            {
                return true;
            }

            foreach (System.Func<WeaponPointerContext, bool> handler in handlers.GetInvocationList())
            {
                if (!handler(context))
                {
                    return false;
                }
            }

            return true;
        }

        private void CancelActivePointerPress()
        {
            if (pressedWeapon != null)
            {
                TryReadPointerPosition(out Vector2 pointerPosition);
                WeaponPointerCancelled?.Invoke(CreatePointerContext(pressedWeapon, pointerPosition));
            }

            ClearActivePointerPress();
        }

        private void ClearActivePointerPress()
        {
            pressedWeapon = null;
            pointerPressedAt = 0f;
            pointerPressedPosition = Vector2.zero;
        }

        private WeaponPointerContext CreatePointerContext(RuntimeWeapon weapon, Vector2 pointerPosition)
        {
            return new WeaponPointerContext(
                weapon.GameObject.name,
                weapon.Transform,
                weapon.HitTransform,
                weapon.WeaponIndex,
                pointerPressedPosition,
                pointerPosition,
                Mathf.Max(0f, Time.time - pointerPressedAt));
        }

        private void BeginAttack(RuntimeWeapon weapon)
        {
            BeginAttack(weapon, true);
        }

        private void BeginAttack(RuntimeWeapon weapon, bool requireRequestApproval)
        {
            if (weapon.State == WeaponInteractionState.Attack)
            {
                return;
            }

            WeaponAttackContext context = new WeaponAttackContext(
                weapon.GameObject.name,
                weapon.Transform,
                weapon.HitTransform,
                weapon.WeaponIndex);

            if (requireRequestApproval && !CanStartWeaponAttack(context))
            {
                return;
            }

            WeaponAttackStarted?.Invoke(context);
            weapon.KillTweens();
            weapon.CaptureAnimationStart();
            weapon.State = WeaponInteractionState.Attack;
            weapon.ActiveTween = DOTween.To(
                    () => 0f,
                    t => ApplyAttackFrame(weapon, t),
                    1f,
                    interactionConfig.AttackDuration)
                .SetEase(Ease.Linear)
                .SetTarget(weapon.GameObject)
                .OnComplete(() =>
                {
                    weapon.ActiveTween = null;
                    if (pointedWeapon == weapon)
                    {
                        BeginHoverLoop(weapon);
                    }
                    else
                    {
                        BeginReturn(weapon);
                    }
                });
        }

        private bool CanStartWeaponAttack(WeaponAttackContext context)
        {
            System.Func<WeaponAttackContext, bool> handlers = WeaponAttackRequested;
            if (handlers == null)
            {
                return true;
            }

            foreach (System.Func<WeaponAttackContext, bool> handler in handlers.GetInvocationList())
            {
                if (!handler(context))
                {
                    return false;
                }
            }

            return true;
        }

        private void BeginReturn(RuntimeWeapon weapon)
        {
            if (weapon.State == WeaponInteractionState.Idle || weapon.State == WeaponInteractionState.Return)
            {
                return;
            }

            weapon.KillTweens();
            weapon.CaptureAnimationStart();
            weapon.State = WeaponInteractionState.Return;
            weapon.ActiveTween = DOTween.To(
                    () => 0f,
                    t => ApplyReturnFrame(weapon, t),
                    1f,
                    interactionConfig.HoverReturnDuration)
                .SetEase(Ease.Linear)
                .SetTarget(weapon.GameObject)
                .OnComplete(() =>
                {
                    weapon.ActiveTween = null;
                    weapon.Transform.localPosition = weapon.BasePosition;
                    weapon.Transform.localRotation = weapon.BaseRotation;
                    weapon.Transform.localScale = weapon.BaseScale;
                    weapon.State = WeaponInteractionState.Idle;
                });
        }

        private void BeginHoverLoop(RuntimeWeapon weapon)
        {
            if (weapon.Transform == null)
            {
                return;
            }

            weapon.KillTweens();
            weapon.ActiveTween = null;
            weapon.State = WeaponInteractionState.HoverLoop;
            weapon.Transform.localPosition = HoverPosition(weapon);
            weapon.Transform.localRotation = weapon.BaseRotation;
            weapon.Transform.localScale = weapon.BaseScale;

            float spinSpeed = interactionConfig.HoverSpinSpeed;
            if (Mathf.Approximately(spinSpeed, 0f))
            {
                return;
            }

            float cycleDuration = 360f / Mathf.Abs(spinSpeed);
            Vector3 loopEuler = weapon.BaseEulerAngles + interactionConfig.HoverSpinAxis * (360f * Mathf.Sign(spinSpeed));
            weapon.HoverLoopTween = weapon.Transform
                .DOLocalRotate(loopEuler, cycleDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetTarget(weapon.GameObject);
        }

        private void ApplyHoverFrame(RuntimeWeapon weapon, float normalizedTime)
        {
            float t = interactionConfig.EvaluateHoverEnter(normalizedTime);
            weapon.Transform.localPosition = Vector3.LerpUnclamped(weapon.StartPosition, HoverPosition(weapon), t);
            weapon.Transform.localRotation = Quaternion.SlerpUnclamped(weapon.StartRotation, weapon.BaseRotation, t);
            weapon.Transform.localScale = Vector3.LerpUnclamped(weapon.StartScale, weapon.BaseScale, t);
        }

        private void ApplyAttackFrame(RuntimeWeapon weapon, float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float recoverT = interactionConfig.EvaluateAttackRecover(t);
            float attackArc = interactionConfig.EvaluateAttackArc(t);
            Vector3 hoverPosition = HoverPosition(weapon);

            weapon.Transform.localPosition = Vector3.LerpUnclamped(weapon.StartPosition, hoverPosition, recoverT)
                + Vector3.forward * (interactionConfig.AttackForwardOffset * attackArc);
            weapon.Transform.localRotation = Quaternion.SlerpUnclamped(weapon.StartRotation, weapon.BaseRotation, recoverT)
                * Quaternion.Euler(interactionConfig.AttackRotationAxis * (interactionConfig.AttackRotation * attackArc));
            weapon.Transform.localScale = Vector3.LerpUnclamped(weapon.StartScale, weapon.BaseScale, recoverT)
                * Mathf.Lerp(1f, interactionConfig.AttackScale, attackArc);
        }

        private void ApplyReturnFrame(RuntimeWeapon weapon, float normalizedTime)
        {
            float t = interactionConfig.EvaluateHoverReturn(normalizedTime);
            weapon.Transform.localPosition = Vector3.LerpUnclamped(weapon.StartPosition, weapon.BasePosition, t);
            weapon.Transform.localRotation = Quaternion.SlerpUnclamped(weapon.StartRotation, weapon.BaseRotation, t);
            weapon.Transform.localScale = Vector3.LerpUnclamped(weapon.StartScale, weapon.BaseScale, t);
        }

        private Vector3 HoverPosition(RuntimeWeapon weapon)
        {
            return weapon.BasePosition + Vector3.up * interactionConfig.HoverLift;
        }

        private void KillRuntimeWeaponTweens()
        {
            for (int i = 0; i < runtimeWeapons.Count; i++)
            {
                runtimeWeapons[i].KillTweens();
            }
        }

        private static bool TryReadPointerPosition(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                pointerPosition = mouse.position.ReadValue();
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                pointerPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            pointerPosition = Input.mousePosition;
            return true;
#else
            pointerPosition = Vector2.zero;
            return false;
#endif
        }

        private static bool WasPointerPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasPressedThisFrame;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                return touchscreen.primaryTouch.press.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static bool IsPointerDown()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.isPressed;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                return touchscreen.primaryTouch.press.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        private static bool WasPointerReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasReleasedThisFrame;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                return touchscreen.primaryTouch.press.wasReleasedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(0);
#else
            return false;
#endif
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
                if (target is GameObject targetGameObject)
                {
                    targetGameObject.transform.SetParent(null, false);
                }

                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void AssignDefaultInteractionConfigInEditor()
        {
#if UNITY_EDITOR
            if (interactionConfig != null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:" + nameof(PrototypeFirstPersonWeaponInteractionConfig),
                new[] { "Assets/Settings" });

            if (guids.Length == 0)
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            interactionConfig = AssetDatabase.LoadAssetAtPath<PrototypeFirstPersonWeaponInteractionConfig>(path);
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("Sync HUD Weapon Poses From Scene")]
        public void SyncWeaponPosesFromScene()
        {
            if (Application.isPlaying || syncingWeaponPosesFromScene || weaponRig == null || weapons == null || weapons.Count == 0)
            {
                return;
            }

            if (!CanModifySceneObject())
            {
                return;
            }

            syncingWeaponPosesFromScene = true;
            bool changed = false;

            for (int i = 0; i < weapons.Count; i++)
            {
                Transform weaponTransform = FindEditableWeaponTransform(weapons[i], i);
                if (weaponTransform == null)
                {
                    continue;
                }

                WeaponPanelPose pose = weapons[i];
                Vector3 localPosition = weaponTransform.localPosition;
                Vector3 localEulerAngles = weaponTransform.localEulerAngles;
                float width = Mathf.Max(0.01f, weaponTransform.localScale.x);
                GameObject hitboxObject = FindPointerHitbox(weaponTransform.name);
                if (hitboxObject != null)
                {
                    CopyPointerHitboxTransform(weaponTransform, hitboxObject.transform);
                }

                if (!PoseMatchesTransform(pose, localPosition, weaponTransform.localRotation, width))
                {
                    if (!changed)
                    {
                        Undo.RecordObject(this, "Sync First Person Weapon Poses");
                        changed = true;
                    }

                    pose.localPosition = localPosition;
                    pose.localEulerAngles = localEulerAngles;
                    pose.width = width;
                    weapons[i] = pose;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(this);
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            syncingWeaponPosesFromScene = false;
        }

        private Transform FindEditableWeaponTransform(WeaponPanelPose pose, int index)
        {
            if (!string.IsNullOrWhiteSpace(pose.name))
            {
                Transform namedChild = weaponRig.Find(pose.name);
                if (IsEditableWeaponTransform(namedChild))
                {
                    return namedChild;
                }
            }

            int weaponIndex = 0;
            for (int i = 0; i < weaponRig.childCount; i++)
            {
                Transform child = weaponRig.GetChild(i);
                if (!IsEditableWeaponTransform(child))
                {
                    continue;
                }

                if (weaponIndex == index)
                {
                    return child;
                }

                weaponIndex++;
            }

            return null;
        }

        private static bool IsEditableWeaponTransform(Transform candidate)
        {
            return candidate != null && candidate.GetComponent<Renderer>() != null;
        }

        private static bool PoseMatchesTransform(
            WeaponPanelPose pose,
            Vector3 localPosition,
            Quaternion localRotation,
            float width)
        {
            return (pose.localPosition - localPosition).sqrMagnitude < 0.000001f
                && Quaternion.Angle(Quaternion.Euler(pose.localEulerAngles), localRotation) < 0.01f
                && Mathf.Abs(pose.width - width) < 0.0001f;
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
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public float width;
            public int sortingOrder;

            public WeaponPanelPose(
                string name,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                float width,
                int sortingOrder)
            {
                this.name = name;
                this.localPosition = localPosition;
                this.localEulerAngles = localEulerAngles;
                this.width = width;
                this.sortingOrder = sortingOrder;
            }
        }

        public struct WeaponPresentation
        {
            public string DisplayName { get; }
            public Sprite Icon { get; }

            public WeaponPresentation(string displayName, Sprite icon)
            {
                DisplayName = displayName;
                Icon = icon;
            }
        }

        private struct WeaponVisualSource
        {
            public string name;
            public Texture2D texture;
            public float aspect;
            public Vector2 textureScale;
            public Vector2 textureOffset;

            public static WeaponVisualSource FromSprite(string name, Sprite sprite)
            {
                Texture2D texture = sprite.texture;
                Rect textureRect = sprite.textureRect;
                float textureWidth = Mathf.Max(1f, texture != null ? texture.width : 1f);
                float textureHeight = Mathf.Max(1f, texture != null ? texture.height : 1f);
                float spriteWidth = Mathf.Max(1f, textureRect.width);
                float spriteHeight = Mathf.Max(1f, textureRect.height);

                return new WeaponVisualSource
                {
                    name = ResolveName(name, sprite != null ? sprite.name : null),
                    texture = texture,
                    aspect = spriteWidth / spriteHeight,
                    textureScale = new Vector2(spriteWidth / textureWidth, spriteHeight / textureHeight),
                    textureOffset = new Vector2(textureRect.x / textureWidth, textureRect.y / textureHeight),
                };
            }

            private static string ResolveName(string preferredName, string fallbackName)
            {
                if (!string.IsNullOrWhiteSpace(preferredName))
                {
                    return preferredName;
                }

                return !string.IsNullOrWhiteSpace(fallbackName) ? fallbackName : "Weapon";
            }

        }

        public struct WeaponAttackContext
        {
            public readonly string weaponName;
            public readonly Transform weaponTransform;
            public readonly Transform hitTransform;
            public readonly int weaponIndex;

            public WeaponAttackContext(string weaponName, Transform weaponTransform, Transform hitTransform, int weaponIndex = -1)
            {
                this.weaponName = weaponName;
                this.weaponTransform = weaponTransform;
                this.hitTransform = hitTransform;
                this.weaponIndex = weaponIndex;
            }
        }

        public struct WeaponPointerContext
        {
            public readonly string weaponName;
            public readonly Transform weaponTransform;
            public readonly Transform hitTransform;
            public readonly int weaponIndex;
            public readonly Vector2 pressScreenPosition;
            public readonly Vector2 currentScreenPosition;
            public readonly float holdDuration;

            public WeaponPointerContext(
                string weaponName,
                Transform weaponTransform,
                Transform hitTransform,
                int weaponIndex,
                Vector2 pressScreenPosition,
                Vector2 currentScreenPosition,
                float holdDuration)
            {
                this.weaponName = weaponName;
                this.weaponTransform = weaponTransform;
                this.hitTransform = hitTransform;
                this.weaponIndex = weaponIndex;
                this.pressScreenPosition = pressScreenPosition;
                this.currentScreenPosition = currentScreenPosition;
                this.holdDuration = holdDuration;
            }
        }

        private enum WeaponInteractionState
        {
            Idle,
            HoverRise,
            HoverLoop,
            Attack,
            Return,
        }

        private sealed class RuntimeWeapon
        {
            public RuntimeWeapon(GameObject gameObject, GameObject hitboxObject, int weaponIndex)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                HitboxObject = hitboxObject;
                HitTransform = hitboxObject.transform;
                WeaponIndex = weaponIndex;
                BasePosition = Transform.localPosition;
                BaseRotation = Transform.localRotation;
                BaseEulerAngles = Transform.localEulerAngles;
                BaseScale = Transform.localScale;
                CaptureAnimationStart();
            }

            public GameObject GameObject { get; }
            public Transform Transform { get; }
            public GameObject HitboxObject { get; }
            public Transform HitTransform { get; }
            public int WeaponIndex { get; }
            public Vector3 BasePosition { get; }
            public Quaternion BaseRotation { get; }
            public Vector3 BaseEulerAngles { get; }
            public Vector3 BaseScale { get; }
            public Vector3 StartPosition { get; private set; }
            public Quaternion StartRotation { get; private set; }
            public Vector3 StartScale { get; private set; }
            public WeaponInteractionState State { get; set; }
            public Tween ActiveTween { get; set; }
            public Tween HoverLoopTween { get; set; }

            public void CaptureAnimationStart()
            {
                StartPosition = Transform.localPosition;
                StartRotation = Transform.localRotation;
                StartScale = Transform.localScale;
            }

            public void KillTweens()
            {
                ActiveTween?.Kill();
                HoverLoopTween?.Kill();
                ActiveTween = null;
                HoverLoopTween = null;
            }
        }
    }
}
