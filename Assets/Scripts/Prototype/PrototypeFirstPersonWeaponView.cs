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

        [Header("Interaction")]
#if ODIN_INSPECTOR
        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
#endif
        [SerializeField] private PrototypeFirstPersonWeaponInteractionConfig interactionConfig;

        [Header("Layout")]
        [SerializeField] private List<WeaponPanelPose> weapons = new List<WeaponPanelPose>
        {
            new WeaponPanelPose("Left Blade", 1, new Vector3(-0.43f, -0.39f, 1.35f), new Vector3(0f, 0f, -80f), 0.92f, 0),
            new WeaponPanelPose("Center Sword", 0, new Vector3(0.02f, -0.37f, 1.18f), new Vector3(0f, 0f, -90f), 0.72f, 2),
            new WeaponPanelPose("Right Brush", 6, new Vector3(0.41f, -0.58f, 1.28f), new Vector3(0f, 0f, -106.2f), 0.96f, 1),
        };

        private readonly List<GameObject> spawnedWeapons = new List<GameObject>();
        private readonly List<RuntimeWeapon> runtimeWeapons = new List<RuntimeWeapon>();
        private RuntimeWeapon pointedWeapon;
#if UNITY_EDITOR
        private bool syncingWeaponPosesFromScene;
#endif

        private void Reset()
        {
            LoadDefaultTexturesInEditor();
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
            weaponObject.transform.localPosition = pose.localPosition;
            weaponObject.transform.localRotation = Quaternion.Euler(pose.localEulerAngles);

            float aspect = texture.width > 0 && texture.height > 0 ? (float)texture.width / texture.height : 1f;
            weaponObject.transform.localScale = new Vector3(pose.width, pose.width / Mathf.Max(0.01f, aspect), 1f);

            Renderer renderer = weaponObject.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial(texture, pose.sortingOrder);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            DisableVisualColliders(weaponObject);
            GameObject hitboxObject = CreateOrUpdatePointerHitbox(weaponObject);

            spawnedWeapons.Add(weaponObject);
            runtimeWeapons.Add(new RuntimeWeapon(weaponObject, hitboxObject));
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

            hitboxObject.hideFlags = HideFlags.HideInHierarchy;
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
            pointedWeapon = null;
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
                runtimeWeapons.Add(new RuntimeWeapon(child.gameObject, hitboxObject));
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

            if (hitWeapon != null && hitWeapon.State != WeaponInteractionState.Attack && WasPointerPressedThisFrame())
            {
                BeginAttack(hitWeapon);
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

            Ray ray = weaponCamera.ScreenPointToRay(pointerPosition);
            float raycastDistance = farClipPlane + interactionConfig.AttackForwardOffset + interactionConfig.RaycastDistancePadding;
            if (!Physics.Raycast(ray, out RaycastHit hit, raycastDistance, 1 << weaponLayer, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return FindRuntimeWeapon(hit.collider.transform);
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

        private void BeginAttack(RuntimeWeapon weapon)
        {
            if (weapon.State == WeaponInteractionState.Attack)
            {
                return;
            }

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
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
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
            public RuntimeWeapon(GameObject gameObject, GameObject hitboxObject)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                HitboxObject = hitboxObject;
                HitTransform = hitboxObject.transform;
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
