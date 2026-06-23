using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorPreviewRuntime : MonoBehaviour
    {
        private const string PreviewRootName = "SkillIndicatorScenePreviewRoot";
        private const string AttachedPreviewRootName = "SkillIndicatorWorldPreviewRoot";

        [SerializeField] private SkillIndicatorTemporaryArtIndex temporaryArtIndex;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float surfaceOffset = 0.06f;
        [SerializeField] private string previewLayerName = "Default";

        private SkillIndicatorRendererPool rendererPool;
        private Transform scenePreviewRoot;
        private Transform worldPreviewRoot;
        private SkillIndicatorPreviewFrame currentFrame;
        private bool hasCurrentFrame;
        private int localCastSequence;

        public bool HasPreview => hasCurrentFrame;
        public SkillIndicatorPreviewFrame CurrentFrame => currentFrame;

        public void Configure(SkillIndicatorTemporaryArtIndex artIndex, Camera camera)
        {
            temporaryArtIndex = artIndex != null ? artIndex : temporaryArtIndex;
            aimCamera = camera != null ? camera : aimCamera;
        }

        public SkillIndicatorPreviewFrame Resolve(
            SkillIndicatorConfig config,
            WeaponDefinition weapon,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            SkillIndicatorResolvedConfig resolved = SkillIndicatorResolvedConfig.From(config, weapon);
            return SkillIndicatorAimSolver.Resolve(
                resolved,
                owner,
                castOrigin,
                aimCamera != null ? aimCamera : Camera.main,
                pointerPosition,
                hasPointerPosition,
                holdDuration,
                localCastSequence);
        }

        public SkillIndicatorPreviewFrame ShowPreview(
            SkillIndicatorConfig config,
            WeaponDefinition weapon,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            EnsurePool();
            currentFrame = Resolve(config, weapon, owner, castOrigin, pointerPosition, hasPointerPosition, holdDuration);
            hasCurrentFrame = true;

            GameObject instance = rendererPool.Show(currentFrame.Config.previewPrefabResourceId, temporaryArtIndex);
            ApplyFrame(instance, currentFrame, castOrigin);
            return currentFrame;
        }

        public void HidePreview()
        {
            hasCurrentFrame = false;
            rendererPool?.HideActive();
        }

        public int NextCastSequence()
        {
            localCastSequence++;
            return localCastSequence;
        }

        private void OnDisable()
        {
            HidePreview();
        }

        private void EnsurePool()
        {
            if (rendererPool == null)
            {
                scenePreviewRoot = ResolvePreviewRoot(PreviewRootName);
                rendererPool = new SkillIndicatorRendererPool(scenePreviewRoot);
            }
        }

        private Transform ResolvePreviewRoot(string rootName)
        {
            int previewLayer = ResolvePreviewLayer();
            GameObject existingRoot = GameObject.Find(rootName);
            if (existingRoot != null)
            {
                NormalizePreviewRoot(existingRoot.transform);
                SetLayerRecursively(existingRoot.transform, previewLayer);
                return existingRoot.transform;
            }

            GameObject root = new GameObject(rootName);
            NormalizePreviewRoot(root.transform);
            SetLayerRecursively(root.transform, previewLayer);
            return root.transform;
        }

        private Transform ResolveWorldPreviewRoot()
        {
            if (worldPreviewRoot == null)
            {
                worldPreviewRoot = ResolvePreviewRoot(AttachedPreviewRootName);
            }

            return worldPreviewRoot;
        }

        private static void NormalizePreviewRoot(Transform root)
        {
            root.SetParent(null, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private int ResolvePreviewLayer()
        {
            int layer = !string.IsNullOrWhiteSpace(previewLayerName)
                ? LayerMask.NameToLayer(previewLayerName)
                : -1;
            return layer >= 0 ? layer : 0;
        }

        private void ApplyFrame(GameObject instance, SkillIndicatorPreviewFrame frame, Transform castOrigin)
        {
            if (instance == null)
            {
                return;
            }

            SetLayerRecursively(instance.transform, ResolvePreviewLayer());
            CastCommandData command = frame.Command;
            bool sticksToGround = frame.Config.SticksToGround();
            Transform previewParent = ResolvePreviewParent(frame.Config, castOrigin);
            if (previewParent != null && instance.transform.parent != previewParent)
            {
                instance.transform.SetParent(previewParent, true);
            }

            Vector3 normal = sticksToGround || command.SurfaceNormal.sqrMagnitude <= 0.001f
                ? Vector3.up
                : command.SurfaceNormal.normalized;
            float resolvedSurfaceOffset = sticksToGround ? Mathf.Max(surfaceOffset, command.GroundOffset) : 0f;
            Vector3 flatDirection = command.Direction;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= 0.001f)
            {
                flatDirection = Vector3.forward;
            }

            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion facingRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            bool valid = frame.IsValid;

            switch (frame.Config.shapeType)
            {
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                    instance.transform.position = ResolveBasePosition(command, sticksToGround) + flatDirection.normalized * (frame.Config.length * 0.5f) + normal * resolvedSurfaceOffset;
                    instance.transform.rotation = facingRotation;
                    instance.transform.localScale = new Vector3(Mathf.Max(0.05f, frame.Config.width), 1f, Mathf.Max(0.05f, frame.Config.length));
                    break;
                case SkillIndicatorShapeType.Cone:
                    instance.transform.position = ResolveBasePosition(command, sticksToGround) + normal * resolvedSurfaceOffset;
                    instance.transform.rotation = facingRotation;
                    instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, frame.Config.range);
                    break;
                case SkillIndicatorShapeType.TargetReticle:
                    instance.transform.position = ResolveTargetPosition(command, sticksToGround) + normal * (resolvedSurfaceOffset + 0.08f);
                    instance.transform.rotation = aimCamera != null
                        ? Quaternion.LookRotation(instance.transform.position - aimCamera.transform.position, aimCamera.transform.up)
                        : surfaceRotation;
                    instance.transform.localScale = Vector3.one * Mathf.Max(0.25f, frame.Config.radius);
                    break;
                default:
                    instance.transform.position = ResolveTargetPosition(command, sticksToGround) + normal * resolvedSurfaceOffset;
                    instance.transform.rotation = sticksToGround ? Quaternion.identity : surfaceRotation;
                    instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, frame.Config.radius);
                    break;
            }

            Material material = rendererPool.ResolveMaterial(
                temporaryArtIndex,
                valid ? frame.Config.validMaterialResourceId : frame.Config.invalidMaterialResourceId,
                valid);
            ApplyMaterial(instance, material, valid);
        }

        private static Vector3 ResolveBasePosition(CastCommandData command, bool sticksToGround)
        {
            return sticksToGround ? command.SceneOrigin : command.Origin;
        }

        private static Vector3 ResolveTargetPosition(CastCommandData command, bool sticksToGround)
        {
            return sticksToGround ? command.TargetPoint : command.Origin;
        }

        private Transform ResolvePreviewParent(SkillIndicatorResolvedConfig config, Transform castOrigin)
        {
            switch (config.placementMode)
            {
                case SkillIndicatorPlacementMode.AttachToCastOrigin:
                    return castOrigin != null ? castOrigin : ResolveWorldPreviewRoot();
                case SkillIndicatorPlacementMode.WorldSpace:
                    return ResolveWorldPreviewRoot();
                default:
                    return scenePreviewRoot;
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

        private static void ApplyMaterial(GameObject instance, Material material, bool valid)
        {
            if (material == null || instance == null)
            {
                return;
            }

            MeshRenderer[] meshRenderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                meshRenderers[i].sharedMaterial = material;
            }

            LineRenderer[] lineRenderers = instance.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                lineRenderers[i].sharedMaterial = material;
                Color color = valid ? new Color(0.1f, 0.72f, 1f, 0.75f) : new Color(1f, 0.12f, 0.08f, 0.78f);
                lineRenderers[i].startColor = color;
                lineRenderers[i].endColor = color;
            }

            SpriteRenderer[] spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].sharedMaterial = material;
                spriteRenderers[i].color = valid ? Color.white : new Color(1f, 0.25f, 0.18f, 1f);
            }
        }
    }
}
