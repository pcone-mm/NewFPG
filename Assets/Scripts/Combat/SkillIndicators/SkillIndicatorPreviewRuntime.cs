using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorPreviewRuntime : MonoBehaviour
    {
        private const string PreviewRootName = "SkillIndicatorScenePreviewRoot";
        private const string AttachedPreviewRootName = "SkillIndicatorWorldPreviewRoot";
        private const string RangeBoundaryName = "SkillIndicatorRangeBoundary";
        private const string RuntimeConeMeshName = "SkillIndicatorRuntimeConeMesh";
        private const int RangeBoundarySegments = 96;
        private const int ConePreviewMaxSegments = 96;
        private const float ConeBoundaryHeight = 0.018f;
        private static readonly Color RangeBoundaryColor = new Color(0.35f, 0.95f, 1f, 0.88f);

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
        private LineRenderer rangeBoundaryRenderer;
        private Material rangeBoundaryMaterial;

        public bool HasPreview => hasCurrentFrame;
        public SkillIndicatorPreviewFrame CurrentFrame => currentFrame;

        public void Configure(SkillIndicatorTemporaryArtIndex artIndex, Camera camera)
        {
            temporaryArtIndex = artIndex != null ? artIndex : temporaryArtIndex;
            aimCamera = camera != null ? camera : aimCamera;
        }

#pragma warning disable CS0618
        public SkillIndicatorPreviewFrame Resolve(
            SkillIndicatorConfig config,
            WeaponDefinition weapon,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            return Resolve(
                weapon != null ? WeaponRuntimeResolver.Resolve(weapon) : null,
                owner,
                castOrigin,
                pointerPosition,
                hasPointerPosition,
                holdDuration);
        }

        public SkillIndicatorPreviewFrame Resolve(
            SkillIndicatorConfig config,
            WeaponRuntimeStats stats,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            return Resolve(stats, owner, castOrigin, pointerPosition, hasPointerPosition, holdDuration);
        }
#pragma warning restore CS0618

        public SkillIndicatorPreviewFrame Resolve(
            WeaponRuntimeStats stats,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            SkillIndicatorResolvedConfig resolved = SkillIndicatorResolvedConfig.From(stats);
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

#pragma warning disable CS0618
        public SkillIndicatorPreviewFrame ShowPreview(
            SkillIndicatorConfig config,
            WeaponDefinition weapon,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            return ShowPreview(
                weapon != null ? WeaponRuntimeResolver.Resolve(weapon) : null,
                owner,
                castOrigin,
                pointerPosition,
                hasPointerPosition,
                holdDuration);
        }

        public SkillIndicatorPreviewFrame ShowPreview(
            SkillIndicatorConfig config,
            WeaponRuntimeStats stats,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            return ShowPreview(stats, owner, castOrigin, pointerPosition, hasPointerPosition, holdDuration);
        }
#pragma warning restore CS0618

        public SkillIndicatorPreviewFrame ShowPreview(
            WeaponRuntimeStats stats,
            Transform owner,
            Transform castOrigin,
            Vector2 pointerPosition,
            bool hasPointerPosition,
            float holdDuration)
        {
            EnsurePool();
            currentFrame = Resolve(stats, owner, castOrigin, pointerPosition, hasPointerPosition, holdDuration);
            hasCurrentFrame = true;

            GameObject instance = rendererPool.Show(currentFrame.Config.previewPrefabResourceId, temporaryArtIndex);
            ApplyFrame(instance, currentFrame, castOrigin);
            return currentFrame;
        }

        public void HidePreview()
        {
            hasCurrentFrame = false;
            rendererPool?.HideActive();
            HideRangeBoundary();
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
            bool useInvalidAppearance = ShouldUseInvalidAppearance(frame);
            bool visualValid = !useInvalidAppearance;
            SkillIndicatorShapeType shapeType = command.ShapeType != SkillIndicatorShapeType.None
                ? command.ShapeType
                : frame.Config.shapeType;
            float radius = ResolvePreviewRadius(frame);
            float width = ResolvePreviewWidth(frame, radius);
            float length = ResolvePreviewLength(frame);
            float angle = ResolvePreviewAngle(frame);

            switch (shapeType)
            {
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                    instance.transform.position = ResolveBasePosition(command, sticksToGround) + flatDirection.normalized * (length * 0.5f) + normal * resolvedSurfaceOffset;
                    instance.transform.rotation = facingRotation;
                    instance.transform.localScale = new Vector3(Mathf.Max(0.05f, width), 1f, Mathf.Max(0.05f, length));
                    break;
                case SkillIndicatorShapeType.Cone:
                    instance.transform.position = ResolveBasePosition(command, sticksToGround) + normal * resolvedSurfaceOffset;
                    instance.transform.rotation = facingRotation;
                    instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, length);
                    ApplyConeGeometry(instance, angle);
                    break;
                case SkillIndicatorShapeType.TargetReticle:
                    instance.transform.position = ResolveTargetPosition(command, frame.Config, sticksToGround) + normal * (resolvedSurfaceOffset + 0.08f);
                    instance.transform.rotation = aimCamera != null
                        ? Quaternion.LookRotation(instance.transform.position - aimCamera.transform.position, aimCamera.transform.up)
                        : surfaceRotation;
                    instance.transform.localScale = Vector3.one * Mathf.Max(0.25f, radius);
                    break;
                default:
                    instance.transform.position = ResolveTargetPosition(command, frame.Config, sticksToGround) + normal * resolvedSurfaceOffset;
                    instance.transform.rotation = sticksToGround ? Quaternion.identity : surfaceRotation;
                    instance.transform.localScale = Vector3.one * Mathf.Max(0.05f, radius);
                    break;
            }

            Material material = rendererPool.ResolveMaterial(
                temporaryArtIndex,
                visualValid ? frame.Config.validMaterialResourceId : frame.Config.invalidMaterialResourceId,
                visualValid);
            ApplyMaterial(instance, material, visualValid);
            UpdateRangeBoundary(frame, sticksToGround, normal, resolvedSurfaceOffset);
        }

        private static float ResolvePreviewRadius(SkillIndicatorPreviewFrame frame)
        {
            return frame.Command.Radius > 0f ? frame.Command.Radius : frame.Config.radius;
        }

        private static float ResolvePreviewWidth(SkillIndicatorPreviewFrame frame, float radius)
        {
            return frame.Command.Width > 0f ? frame.Command.Width : radius * 2f;
        }

        private static float ResolvePreviewLength(SkillIndicatorPreviewFrame frame)
        {
            return frame.Command.Length > 0f ? frame.Command.Length : frame.Config.length;
        }

        private static float ResolvePreviewAngle(SkillIndicatorPreviewFrame frame)
        {
            return frame.Command.Angle > 0f ? frame.Command.Angle : frame.Config.angle;
        }

        private static void ApplyConeGeometry(GameObject instance, float angle)
        {
            MeshFilter meshFilter = instance.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null || mesh.name == null || !mesh.name.StartsWith(RuntimeConeMeshName))
                {
                    mesh = new Mesh
                    {
                        name = RuntimeConeMeshName,
                        hideFlags = HideFlags.DontSave,
                    };
                    meshFilter.sharedMesh = mesh;
                }

                RebuildConeMesh(mesh, angle);
            }

            ApplyConeBoundary(instance, angle);
        }

        private static void RebuildConeMesh(Mesh mesh, float angle)
        {
            if (mesh == null)
            {
                return;
            }

            float resolvedAngle = Mathf.Clamp(angle, 1f, 360f);
            int segments = ResolveConeSegmentCount(resolvedAngle);
            bool fullCircle = resolvedAngle >= 359.9f;
            int outerVertexCount = fullCircle ? segments : segments + 1;
            Vector3[] vertices = new Vector3[outerVertexCount + 1];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < outerVertexCount; i++)
            {
                Vector3 point = fullCircle
                    ? ConePoint((i / (float)outerVertexCount) * 360f)
                    : ConePoint(Mathf.Lerp(-resolvedAngle * 0.5f, resolvedAngle * 0.5f, i / (float)segments));
                vertices[i + 1] = point;
                uvs[i + 1] = new Vector2(point.x * 0.5f + 0.5f, point.z * 0.5f + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = fullCircle && i == segments - 1 ? 1 : i + 2;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void ApplyConeBoundary(GameObject instance, float angle)
        {
            LineRenderer[] lineRenderers = instance.GetComponentsInChildren<LineRenderer>(true);
            if (lineRenderers == null || lineRenderers.Length == 0)
            {
                return;
            }

            float resolvedAngle = Mathf.Clamp(angle, 1f, 360f);
            int segments = ResolveConeSegmentCount(resolvedAngle);
            bool fullCircle = resolvedAngle >= 359.9f;

            for (int i = 0; i < lineRenderers.Length; i++)
            {
                LineRenderer lineRenderer = lineRenderers[i];
                if (!IsBoundaryRenderer(lineRenderer))
                {
                    continue;
                }

                lineRenderer.useWorldSpace = false;
                lineRenderer.loop = fullCircle;
                lineRenderer.positionCount = fullCircle ? segments : segments + 3;
                if (fullCircle)
                {
                    for (int j = 0; j < segments; j++)
                    {
                        lineRenderer.SetPosition(j, ConePoint((j / (float)segments) * 360f, ConeBoundaryHeight));
                    }

                    continue;
                }

                lineRenderer.SetPosition(0, new Vector3(0f, ConeBoundaryHeight, 0f));
                for (int j = 0; j <= segments; j++)
                {
                    float pointAngle = Mathf.Lerp(-resolvedAngle * 0.5f, resolvedAngle * 0.5f, j / (float)segments);
                    lineRenderer.SetPosition(j + 1, ConePoint(pointAngle, ConeBoundaryHeight));
                }

                lineRenderer.SetPosition(segments + 2, new Vector3(0f, ConeBoundaryHeight, 0f));
            }
        }

        private static int ResolveConeSegmentCount(float angle)
        {
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp(angle, 1f, 360f) / 4f), 8, ConePreviewMaxSegments);
        }

        private static Vector3 ConePoint(float angle)
        {
            return ConePoint(angle, 0f);
        }

        private static Vector3 ConePoint(float angle, float y)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), y, Mathf.Cos(radians));
        }

        private static Vector3 ResolveBasePosition(CastCommandData command, bool sticksToGround)
        {
            return sticksToGround ? command.SceneOrigin : command.Origin;
        }

        private static Vector3 ResolveTargetPosition(CastCommandData command, SkillIndicatorResolvedConfig config, bool sticksToGround)
        {
            if (sticksToGround)
            {
                return command.TargetPoint;
            }

            if (config.placementMode == SkillIndicatorPlacementMode.AttachToCastOrigin)
            {
                return command.Origin;
            }

            return command.HasTargetPoint ? command.TargetPoint : command.Origin;
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

        private void UpdateRangeBoundary(
            SkillIndicatorPreviewFrame frame,
            bool sticksToGround,
            Vector3 normal,
            float resolvedSurfaceOffset)
        {
            float range = Mathf.Max(0f, frame.Config.range);
            if (range <= 0.01f)
            {
                HideRangeBoundary();
                return;
            }

            LineRenderer lineRenderer = EnsureRangeBoundaryRenderer();
            if (lineRenderer == null)
            {
                return;
            }

            Vector3 center = frame.Command.SceneOrigin;
            Vector3 boundaryNormal = Vector3.up;
            float offset = resolvedSurfaceOffset + 0.018f;
            center += boundaryNormal * offset;

            Quaternion rotation = Quaternion.identity;
            lineRenderer.positionCount = RangeBoundarySegments;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = Mathf.Clamp(range * 0.008f, 0.025f, 0.075f);
            lineRenderer.endWidth = lineRenderer.startWidth;
            lineRenderer.startColor = RangeBoundaryColor;
            lineRenderer.endColor = RangeBoundaryColor;

            for (int i = 0; i < RangeBoundarySegments; i++)
            {
                float angle = (i / (float)RangeBoundarySegments) * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle) * range, 0f, Mathf.Sin(angle) * range);
                lineRenderer.SetPosition(i, center + rotation * point);
            }

            lineRenderer.gameObject.SetActive(true);
        }

        private LineRenderer EnsureRangeBoundaryRenderer()
        {
            EnsurePool();
            if (rangeBoundaryRenderer != null)
            {
                SetLayerRecursively(rangeBoundaryRenderer.transform, ResolvePreviewLayer());
                return rangeBoundaryRenderer;
            }

            Transform parent = scenePreviewRoot != null ? scenePreviewRoot : ResolvePreviewRoot(PreviewRootName);
            GameObject boundary = new GameObject(RangeBoundaryName);
            boundary.transform.SetParent(parent, false);
            SetLayerRecursively(boundary.transform, ResolvePreviewLayer());

            rangeBoundaryRenderer = boundary.AddComponent<LineRenderer>();
            rangeBoundaryRenderer.sharedMaterial = ResolveRangeBoundaryMaterial();
            rangeBoundaryRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rangeBoundaryRenderer.receiveShadows = false;
            rangeBoundaryRenderer.numCornerVertices = 4;
            rangeBoundaryRenderer.numCapVertices = 4;
            rangeBoundaryRenderer.textureMode = LineTextureMode.Stretch;
            rangeBoundaryRenderer.alignment = LineAlignment.View;
            return rangeBoundaryRenderer;
        }

        private void HideRangeBoundary()
        {
            if (rangeBoundaryRenderer != null)
            {
                rangeBoundaryRenderer.gameObject.SetActive(false);
            }
        }

        private Material ResolveRangeBoundaryMaterial()
        {
            if (rangeBoundaryMaterial == null)
            {
                rangeBoundaryMaterial = CreateTransparentMaterial(RangeBoundaryColor);
            }

            return rangeBoundaryMaterial;
        }

        private static Material CreateTransparentMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            material.color = color;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private static bool ShouldUseInvalidAppearance(SkillIndicatorPreviewFrame frame)
        {
            if (frame.IsValid)
            {
                return false;
            }

            switch (frame.Validation.Reason)
            {
                case SkillIndicatorValidationReason.NoSurface:
                case SkillIndicatorValidationReason.OutOfRange:
                    return false;
                default:
                    return true;
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
                if (IsBoundaryRenderer(meshRenderers[i]))
                {
                    continue;
                }

                meshRenderers[i].sharedMaterial = material;
            }

            LineRenderer[] lineRenderers = instance.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                if (IsBoundaryRenderer(lineRenderers[i]))
                {
                    Color boundaryColor = valid ? new Color(0.1f, 0.72f, 1f, 0.96f) : new Color(1f, 0.12f, 0.08f, 0.96f);
                    lineRenderers[i].startColor = boundaryColor;
                    lineRenderers[i].endColor = boundaryColor;
                    continue;
                }

                lineRenderers[i].sharedMaterial = material;
                Color color = valid ? new Color(0.1f, 0.72f, 1f, 0.92f) : new Color(1f, 0.12f, 0.08f, 0.94f);
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

        private static bool IsBoundaryRenderer(Renderer renderer)
        {
            return renderer != null
                && renderer.transform != null
                && string.Equals(renderer.transform.name, "BoundaryRing", System.StringComparison.Ordinal);
        }

    }
}
