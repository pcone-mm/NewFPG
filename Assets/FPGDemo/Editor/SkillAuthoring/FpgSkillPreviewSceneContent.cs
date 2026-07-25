using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillPreviewSceneContent :
        IFpgSkillPreviewPoseProvider,
        IDisposable
    {
        private const int MaximumTargets = 4;
        private const int MaximumLineVisuals = 72;
        private const int MaximumAreaVisuals = 24;
        private const int MaximumMarkerVisuals = 24;
        private const int MaximumSummonVisuals = 8;

        private static readonly Color BodyColor =
            new Color(0.28f, 0.34f, 0.4f, 1f);
        private static readonly Color BodyHitColor =
            new Color(0.86f, 0.26f, 0.22f, 1f);
        private static readonly Color WeakpointColor =
            new Color(0.96f, 0.72f, 0.18f, 1f);
        private static readonly Color WeakpointHitColor =
            new Color(1f, 0.94f, 0.45f, 1f);

        private readonly PreviewRenderUtility previewUtility;
        private readonly GameObject actor;
        private readonly GameObject targetRoot;
        private readonly GameObject geometryRoot;
        private readonly DummyVisual[] dummies =
            new DummyVisual[MaximumTargets];
        private readonly List<GameObject> lines =
            new List<GameObject>(MaximumLineVisuals);
        private readonly List<GameObject> areas =
            new List<GameObject>(MaximumAreaVisuals);
        private readonly List<GameObject> markers =
            new List<GameObject>(MaximumMarkerVisuals);
        private readonly List<GameObject> summons =
            new List<GameObject>(MaximumSummonVisuals);
        private readonly Material bodyMaterial;
        private readonly Material weakpointMaterial;
        private readonly Material lineMaterial;
        private readonly Material areaMaterial;
        private readonly Material projectileMaterial;
        private readonly Material summonMaterial;
        private readonly Material warningMaterial;
        private readonly MaterialPropertyBlock propertyBlock =
            new MaterialPropertyBlock();

        private Bounds actorBounds;
        private int targetCount = 1;
        private bool disposed;

        public FpgSkillPreviewSceneContent(
            PreviewRenderUtility utility,
            GameObject actorInstance)
        {
            previewUtility = utility
                ?? throw new ArgumentNullException(nameof(utility));
            actor = actorInstance
                ?? throw new ArgumentNullException(nameof(actorInstance));
            actorBounds = CalculateBounds(actor);
            float scale = ResolveScale(actorBounds);

            bodyMaterial = CreateMaterial(BodyColor);
            weakpointMaterial = CreateMaterial(WeakpointColor);
            lineMaterial = CreateMaterial(
                new Color(1f, 0.62f, 0.18f, 0.92f));
            areaMaterial = CreateMaterial(
                new Color(1f, 0.46f, 0.12f, 0.34f));
            projectileMaterial = CreateMaterial(
                new Color(1f, 0.86f, 0.3f, 1f));
            summonMaterial = CreateMaterial(
                new Color(0.26f, 0.84f, 0.66f, 0.72f));
            warningMaterial = CreateMaterial(
                new Color(0.94f, 0.18f, 0.16f, 0.28f));

            targetRoot = new GameObject("Skill Preview Targets")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            geometryRoot = new GameObject("Skill Preview Geometry")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            CreateDummies(scale);
            CreateVisualPools(scale);
            previewUtility.AddSingleGO(targetRoot);
            previewUtility.AddSingleGO(geometryRoot);
            SetTargetCount(1);
            ApplyFrame(null, false);
        }

        public int PreviewTargetCount => targetCount;

        public bool HasPreviewScene =>
            !disposed && previewUtility != null;

        public FpgSkillPreviewTarget GetPreviewTarget(int index)
        {
            if (index < 0 || index >= targetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return dummies[index].Target;
        }

        public bool TryResolvePreviewOrigin(
            string socketId,
            out Vector3 position,
            out Vector3 forward)
        {
            Transform socket = ResolveSocket(socketId);
            if (socket != null)
            {
                position = socket.position;
                forward = socket.right.sqrMagnitude > 0.000001f
                    ? socket.right.normalized
                    : Vector3.right;
                return true;
            }

            position = new Vector3(
                actorBounds.max.x,
                actorBounds.center.y,
                actorBounds.center.z);
            forward = Vector3.right;
            return string.IsNullOrWhiteSpace(socketId);
        }

        public void SetTargetCount(int count)
        {
            targetCount = Mathf.Clamp(count, 1, MaximumTargets);
            for (int index = 0; index < dummies.Length; index++)
            {
                dummies[index].Root.SetActive(index < targetCount);
            }
        }

        public void ApplyFrame(
            FpgSkillPreviewSimulationFrame frame,
            bool showGeometry)
        {
            Deactivate(lines);
            Deactivate(areas);
            Deactivate(markers);
            Deactivate(summons);
            ResetDummyColors();
            if (frame == null)
            {
                return;
            }

            for (int index = 0; index < frame.Hits.Count; index++)
            {
                FpgSkillPreviewHit hit = frame.Hits[index];
                if (hit.ExpectedHitTick != frame.Tick
                    || hit.TargetIndex < 0
                    || hit.TargetIndex >= targetCount)
                {
                    continue;
                }

                DummyVisual dummy = dummies[hit.TargetIndex];
                SetColor(
                    hit.Part == FpgSkillPreviewHitPart.Weakpoint
                        ? dummy.WeakpointRenderer
                        : dummy.BodyRenderer,
                    hit.Part == FpgSkillPreviewHitPart.Weakpoint
                        ? WeakpointHitColor
                        : BodyHitColor);
            }

            if (!showGeometry)
            {
                return;
            }

            int lineIndex = 0;
            int areaIndex = 0;
            int markerIndex = 0;
            int summonIndex = 0;
            for (int index = 0;
                index < frame.Geometries.Count;
                index++)
            {
                FpgSkillPreviewGeometry geometry =
                    frame.Geometries[index];
                switch (geometry.Kind)
                {
                    case FpgSkillPreviewGeometryKind.Ray:
                        if (lineIndex < lines.Count)
                        {
                            ConfigureLine(
                                lines[lineIndex++],
                                geometry.Start,
                                geometry.End,
                                Mathf.Max(0.018f, geometry.Radius));
                        }

                        break;

                    case FpgSkillPreviewGeometryKind.Area:
                    case FpgSkillPreviewGeometryKind.TimedImpact:
                    case FpgSkillPreviewGeometryKind.Warning:
                        if (areaIndex < areas.Count)
                        {
                            GameObject area = areas[areaIndex++];
                            SetSharedMaterial(
                                area,
                                geometry.Kind
                                    == FpgSkillPreviewGeometryKind.Warning
                                        ? warningMaterial
                                        : areaMaterial);
                            ConfigureDisc(
                                area,
                                geometry.Start,
                                geometry.Radius);
                        }

                        break;

                    case FpgSkillPreviewGeometryKind.Projectile:
                        if (markerIndex < markers.Count)
                        {
                            ConfigureMarker(
                                markers[markerIndex++],
                                geometry.Start,
                                geometry.Radius);
                        }

                        break;

                    case FpgSkillPreviewGeometryKind.Summon:
                        if (summonIndex < summons.Count)
                        {
                            ConfigureSummon(
                                summons[summonIndex++],
                                geometry.Start,
                                geometry.Radius);
                        }

                        break;
                }
            }
        }

        public Bounds GetCombinedBounds()
        {
            Bounds bounds = actorBounds;
            EncapsulateRenderers(targetRoot, ref bounds);
            return bounds;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DestroyMaterial(bodyMaterial);
            DestroyMaterial(weakpointMaterial);
            DestroyMaterial(lineMaterial);
            DestroyMaterial(areaMaterial);
            DestroyMaterial(projectileMaterial);
            DestroyMaterial(summonMaterial);
            DestroyMaterial(warningMaterial);
        }

        private void CreateDummies(float scale)
        {
            float floorY = actorBounds.min.y;
            float baseX = actorBounds.max.x + scale * 2.4f;
            Vector2[] offsets =
            {
                new Vector2(0f, 0f),
                new Vector2(scale * 1.65f, scale * 0.9f),
                new Vector2(scale * 1.8f, -scale * 0.9f),
                new Vector2(scale * 3.25f, scale * 0.28f)
            };
            string[] labels =
            {
                "主假人",
                "副假人 1",
                "副假人 2",
                "副假人 3"
            };

            for (int index = 0; index < dummies.Length; index++)
            {
                GameObject root = new GameObject("Skill Preview " + labels[index])
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                root.transform.SetParent(targetRoot.transform, false);
                root.transform.position = new Vector3(
                    baseX + offsets[index].x,
                    floorY + offsets[index].y,
                    actorBounds.center.z);

                GameObject body = CreatePrimitive(
                    PrimitiveType.Capsule,
                    labels[index] + " Body",
                    bodyMaterial,
                    root.transform);
                body.transform.localPosition =
                    new Vector3(0f, scale * 0.85f, 0f);
                body.transform.localScale =
                    new Vector3(scale * 0.72f, scale * 0.72f, scale * 0.52f);
                body.SetActive(true);

                GameObject weakpoint = CreatePrimitive(
                    PrimitiveType.Sphere,
                    labels[index] + " Weakpoint",
                    weakpointMaterial,
                    root.transform);
                weakpoint.transform.localPosition =
                    new Vector3(0f, scale * 1.78f, -scale * 0.08f);
                weakpoint.transform.localScale =
                    Vector3.one * scale * 0.48f;
                weakpoint.SetActive(true);

                Vector3 bodyCenter = root.transform.TransformPoint(
                    body.transform.localPosition);
                Vector3 weakpointCenter = root.transform.TransformPoint(
                    weakpoint.transform.localPosition);
                dummies[index] = new DummyVisual(
                    root,
                    body.GetComponent<Renderer>(),
                    weakpoint.GetComponent<Renderer>(),
                    new FpgSkillPreviewTarget(
                        index,
                        labels[index],
                        bodyCenter,
                        scale * 0.5f,
                        weakpointCenter,
                        scale * 0.24f));
            }
        }

        private void CreateVisualPools(float scale)
        {
            for (int index = 0; index < MaximumLineVisuals; index++)
            {
                lines.Add(CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Preview Ray " + index,
                    lineMaterial,
                    geometryRoot.transform));
            }

            for (int index = 0; index < MaximumAreaVisuals; index++)
            {
                areas.Add(CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Preview Area " + index,
                    areaMaterial,
                    geometryRoot.transform));
            }

            for (int index = 0; index < MaximumMarkerVisuals; index++)
            {
                markers.Add(CreatePrimitive(
                    PrimitiveType.Sphere,
                    "Preview Projectile " + index,
                    projectileMaterial,
                    geometryRoot.transform));
            }

            for (int index = 0; index < MaximumSummonVisuals; index++)
            {
                summons.Add(CreatePrimitive(
                    PrimitiveType.Capsule,
                    "Preview Summon " + index,
                    summonMaterial,
                    geometryRoot.transform));
            }
        }

        private Transform ResolveSocket(string socketId)
        {
            if (string.IsNullOrWhiteSpace(socketId))
            {
                return null;
            }

            Component[] components = actor.GetComponentsInChildren<Component>(
                true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null
                    || !string.Equals(
                        component.GetType().FullName,
                        "FPG.Demo.Unity.D0ActorSocketRegistry",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo method = component.GetType().GetMethod(
                    "TryResolve",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(Transform).MakeByRefType() },
                    null);
                if (method == null)
                {
                    continue;
                }

                object[] arguments = { socketId, null };
                if ((bool)method.Invoke(component, arguments))
                {
                    return arguments[1] as Transform;
                }
            }

            return null;
        }

        private void ResetDummyColors()
        {
            for (int index = 0; index < dummies.Length; index++)
            {
                SetColor(dummies[index].BodyRenderer, BodyColor);
                SetColor(dummies[index].WeakpointRenderer, WeakpointColor);
            }
        }

        private void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private static void ConfigureLine(
            GameObject line,
            Vector3 start,
            Vector3 end,
            float radius)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            line.SetActive(length > 0.0001f);
            if (!line.activeSelf)
            {
                return;
            }

            line.transform.position = (start + end) * 0.5f;
            line.transform.rotation = Quaternion.FromToRotation(
                Vector3.up,
                delta / length);
            line.transform.localScale =
                new Vector3(radius, length * 0.5f, radius);
        }

        private static void ConfigureDisc(
            GameObject disc,
            Vector3 center,
            float radius)
        {
            disc.SetActive(radius > 0.0001f);
            disc.transform.position = center;
            disc.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale =
                new Vector3(radius, 0.018f, radius);
        }

        private static void ConfigureMarker(
            GameObject marker,
            Vector3 position,
            float radius)
        {
            marker.SetActive(true);
            marker.transform.position = position;
            marker.transform.rotation = Quaternion.identity;
            marker.transform.localScale =
                Vector3.one * Mathf.Max(0.06f, radius * 2f);
        }

        private static void ConfigureSummon(
            GameObject summon,
            Vector3 position,
            float radius)
        {
            summon.SetActive(true);
            summon.transform.position = position;
            summon.transform.rotation = Quaternion.identity;
            summon.transform.localScale = new Vector3(
                Mathf.Max(0.2f, radius),
                Mathf.Max(0.3f, radius * 1.2f),
                Mathf.Max(0.15f, radius * 0.65f));
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Material material,
            Transform parent)
        {
            GameObject value = GameObject.CreatePrimitive(primitiveType);
            value.name = name;
            value.hideFlags = HideFlags.HideAndDontSave;
            Collider collider = value.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            value.transform.SetParent(parent, false);
            SetSharedMaterial(value, material);
            value.SetActive(false);
            return value;
        }

        private static void SetSharedMaterial(
            GameObject value,
            Material material)
        {
            Renderer renderer = value == null
                ? null
                : value.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Skill preview could not resolve a color shader.");
            }

            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void Deactivate(IReadOnlyList<GameObject> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                values[index].SetActive(false);
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Bounds bounds = new Bounds(root.transform.position, Vector3.one);
            bool found = false;
            EncapsulateRenderers(root, ref bounds, ref found);
            return found
                ? bounds
                : new Bounds(root.transform.position, new Vector3(2f, 2f, 1f));
        }

        private static void EncapsulateRenderers(
            GameObject root,
            ref Bounds bounds)
        {
            bool found = true;
            EncapsulateRenderers(root, ref bounds, ref found);
        }

        private static void EncapsulateRenderers(
            GameObject root,
            ref Bounds bounds,
            ref bool found)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (!renderers[index].enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderers[index].bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }
        }

        private static float ResolveScale(Bounds bounds)
        {
            return Mathf.Clamp(bounds.size.y * 0.34f, 0.45f, 1.25f);
        }

        private readonly struct DummyVisual
        {
            public DummyVisual(
                GameObject root,
                Renderer bodyRenderer,
                Renderer weakpointRenderer,
                FpgSkillPreviewTarget target)
            {
                Root = root;
                BodyRenderer = bodyRenderer;
                WeakpointRenderer = weakpointRenderer;
                Target = target;
            }

            public GameObject Root { get; }
            public Renderer BodyRenderer { get; }
            public Renderer WeakpointRenderer { get; }
            public FpgSkillPreviewTarget Target { get; }
        }
    }
}
