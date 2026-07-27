using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity, presentation-only pool for player-fired ray visuals.
    /// It is deliberately independent of combat, Physics and replay state.
    /// </summary>
    public sealed class PlayerShotTracerPool : IDisposable
    {
        private PlayerShotTracerView[] views;
        private bool[] activeSlots;
        private bool prepared;

        public bool IsPrepared => prepared;
        public int Capacity => views == null ? 0 : views.Length;
        public int SpawnRejectCount { get; private set; }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Capacity; index++)
                {
                    if (activeSlots[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryPrepare(
            Transform root,
            Material material,
            int capacity,
            out string error)
        {
            return TryPrepare(root, material, capacity, "Default", 0, out error);
        }

        public bool TryPrepare(
            Transform root,
            Material material,
            int capacity,
            string sortingLayerName,
            int sortingOrder,
            out string error)
        {
            if (root == null || material == null || capacity <= 0)
            {
                error = "Player tracer root, material and a positive capacity are required.";
                return false;
            }

            if (prepared)
            {
                if (Capacity < capacity)
                {
                    error = "Prepared player tracer pool capacity is below the requested capacity.";
                    return false;
                }

                for (int index = 0; index < Capacity; index++)
                {
                    views[index].ApplySorting(sortingLayerName, sortingOrder);
                }

                error = string.Empty;
                return true;
            }

            views = new PlayerShotTracerView[capacity];
            activeSlots = new bool[capacity];
            try
            {
                for (int index = 0; index < capacity; index++)
                {
                    GameObject viewObject = new GameObject($"PlayerShotTracer_{index}");
                    viewObject.transform.SetParent(root, false);
                    PlayerShotTracerView view = viewObject.AddComponent<PlayerShotTracerView>();
                    if (!view.TryPrepare(
                            material,
                            sortingLayerName,
                            sortingOrder,
                            out string viewError))
                    {
                        DestroyObject(viewObject);
                        error = $"Unable to prepare player tracer {index}: {viewError}";
                        Dispose();
                        return false;
                    }

                    views[index] = view;
                }
            }
            catch (Exception exception)
            {
                error = $"Unable to prewarm player tracer views: {exception.Message}";
                Dispose();
                return false;
            }

            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TrySpawn(
            Vector3 start,
            Vector3 end,
            Color color,
            float duration,
            float width,
            float endpointIntensity)
        {
            if (!prepared || duration <= 0f || width <= 0f)
            {
                SpawnRejectCount++;
                return false;
            }

            int slot = FindFreeSlot();
            if (slot < 0)
            {
                SpawnRejectCount++;
                return false;
            }

            views[slot].Activate(start, end, color, duration, width, endpointIntensity);
            activeSlots[slot] = true;
            return true;
        }

        public void Advance(float deltaTime)
        {
            if (!prepared || deltaTime < 0f)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index] && !views[index].Advance(deltaTime))
                {
                    activeSlots[index] = false;
                }
            }
        }

        public void Clear()
        {
            if (!prepared)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index])
                {
                    views[index].Deactivate();
                    activeSlots[index] = false;
                }
            }
        }

        public void Dispose()
        {
            if (views != null)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (views[index] != null)
                    {
                        DestroyObject(views[index].gameObject);
                    }
                }
            }

            views = null;
            activeSlots = null;
            prepared = false;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }

    /// <summary>
    /// A single short-lived, frozen player trajectory. Its line endpoints are
    /// supplied by the committed query capture; this component performs no
    /// spatial query and never follows a target after activation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShotTracerView : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Light endpointLight;
        private Transform cachedTransform;
        private Color baseColor;
        private float duration;
        private float elapsed;
        private float baseEndpointIntensity;
        private bool prepared;

        public bool IsPrepared => prepared;
        public bool IsActive => prepared && gameObject.activeSelf;

        public bool TryPrepare(Material material, out string error)
        {
            return TryPrepare(material, "Default", 0, out error);
        }

        public bool TryPrepare(
            Material material,
            string sortingLayerName,
            int sortingOrder,
            out string error)
        {
            if (material == null)
            {
                error = "Player tracer view requires a material.";
                return false;
            }

            if (prepared)
            {
                ApplySorting(sortingLayerName, sortingOrder);
                error = string.Empty;
                return true;
            }

            cachedTransform = transform;
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.sharedMaterial = material;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 0;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            ApplySorting(sortingLayerName, sortingOrder);

            endpointLight = GetComponent<Light>();
            if (endpointLight == null)
            {
                endpointLight = gameObject.AddComponent<Light>();
            }

            endpointLight.type = LightType.Point;
            endpointLight.shadows = LightShadows.None;
            endpointLight.renderMode = LightRenderMode.ForcePixel;
            prepared = true;
            Deactivate();
            error = string.Empty;
            return true;
        }

        public void ApplySorting(string sortingLayerName, int sortingOrder)
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
                ? "Default"
                : sortingLayerName;
            lineRenderer.sortingOrder = sortingOrder;
        }

        public void Activate(
            Vector3 start,
            Vector3 end,
            Color color,
            float nextDuration,
            float width,
            float endpointIntensity)
        {
            if (!prepared)
            {
                return;
            }

            duration = Mathf.Max(0.01f, nextDuration);
            elapsed = 0f;
            baseColor = color;
            baseEndpointIntensity = Mathf.Max(0f, endpointIntensity);
            cachedTransform.position = end;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width * 0.52f;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            endpointLight.color = color;
            endpointLight.range = Mathf.Clamp(Vector3.Distance(start, end) * 0.075f, 0.55f, 2.1f);
            endpointLight.intensity = baseEndpointIntensity;
            lineRenderer.enabled = true;
            endpointLight.enabled = baseEndpointIntensity > 0f;
            gameObject.SetActive(true);
        }

        public bool Advance(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(elapsed / duration);
            Color color = baseColor;
            color.a = baseColor.a * (1f - progress);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            endpointLight.intensity = baseEndpointIntensity * (1f - progress) * (1f - progress);
            if (progress < 1f)
            {
                return true;
            }

            Deactivate();
            return false;
        }

        public void Deactivate()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (endpointLight != null)
            {
                endpointLight.enabled = false;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Fixed-capacity pool for the player secondary's committed area feedback.
    /// A full pool drops only the visual request and exposes the count; it does
    /// not recycle or mutate any gameplay event.
    /// </summary>
    public sealed class PlayerShotAreaPool : IDisposable
    {
        private PlayerShotAreaView[] views;
        private bool[] activeSlots;
        private bool prepared;

        public bool IsPrepared => prepared;
        public int Capacity => views == null ? 0 : views.Length;
        public int SpawnRejectCount { get; private set; }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < Capacity; index++)
                {
                    if (activeSlots[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryPrepare(
            Transform root,
            Material material,
            int capacity,
            out string error)
        {
            if (root == null || material == null || capacity <= 0)
            {
                error = "Player area root, material and a positive capacity are required.";
                return false;
            }

            if (prepared)
            {
                if (Capacity < capacity)
                {
                    error = "Prepared player area pool capacity is below the requested capacity.";
                    return false;
                }

                error = string.Empty;
                return true;
            }

            views = new PlayerShotAreaView[capacity];
            activeSlots = new bool[capacity];
            try
            {
                for (int index = 0; index < capacity; index++)
                {
                    GameObject viewObject = new GameObject($"PlayerShotArea_{index}");
                    viewObject.transform.SetParent(root, false);
                    PlayerShotAreaView view = viewObject.AddComponent<PlayerShotAreaView>();
                    if (!view.TryPrepare(material, out string viewError))
                    {
                        DestroyObject(viewObject);
                        error = $"Unable to prepare player area view {index}: {viewError}";
                        Dispose();
                        return false;
                    }

                    views[index] = view;
                }
            }
            catch (Exception exception)
            {
                error = $"Unable to prewarm player area views: {exception.Message}";
                Dispose();
                return false;
            }

            prepared = true;
            error = string.Empty;
            return true;
        }

        public bool TrySpawn(Vector3 center, float radius, Color color, float duration)
        {
            if (!prepared || radius <= 0f || duration <= 0f)
            {
                SpawnRejectCount++;
                return false;
            }

            int slot = FindFreeSlot();
            if (slot < 0)
            {
                SpawnRejectCount++;
                return false;
            }

            views[slot].Activate(center, radius, color, duration);
            activeSlots[slot] = true;
            return true;
        }

        public void Advance(float deltaTime)
        {
            if (!prepared || deltaTime < 0f)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index] && !views[index].Advance(deltaTime))
                {
                    activeSlots[index] = false;
                }
            }
        }

        public void Clear()
        {
            if (!prepared)
            {
                return;
            }

            for (int index = 0; index < Capacity; index++)
            {
                if (activeSlots[index])
                {
                    views[index].Deactivate();
                    activeSlots[index] = false;
                }
            }
        }

        public void Dispose()
        {
            if (views != null)
            {
                for (int index = 0; index < views.Length; index++)
                {
                    if (views[index] != null)
                    {
                        DestroyObject(views[index].gameObject);
                    }
                }
            }

            views = null;
            activeSlots = null;
            prepared = false;
        }

        private int FindFreeSlot()
        {
            for (int index = 0; index < Capacity; index++)
            {
                if (!activeSlots[index])
                {
                    return index;
                }
            }

            return -1;
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }

    /// <summary>
    /// A horizontal ring that marks the frozen secondary-area center and radius.
    /// It is feedback for the player's release, never an enemy dodge telegraph.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShotAreaView : MonoBehaviour
    {
        private const int CircleSegmentCount = 32;

        private readonly Vector3[] circlePoints = new Vector3[CircleSegmentCount + 1];
        private LineRenderer lineRenderer;
        private Light centerLight;
        private Transform cachedTransform;
        private Color baseColor;
        private Vector3 center;
        private float baseRadius;
        private float duration;
        private float elapsed;
        private bool prepared;

        public bool IsPrepared => prepared;
        public bool IsActive => prepared && gameObject.activeSelf;

        public bool TryPrepare(Material material, out string error)
        {
            if (material == null)
            {
                error = "Player area view requires a material.";
                return false;
            }

            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            cachedTransform = transform;
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.sharedMaterial = material;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = CircleSegmentCount + 1;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            centerLight = GetComponent<Light>();
            if (centerLight == null)
            {
                centerLight = gameObject.AddComponent<Light>();
            }

            centerLight.type = LightType.Point;
            centerLight.shadows = LightShadows.None;
            centerLight.renderMode = LightRenderMode.ForcePixel;
            prepared = true;
            Deactivate();
            error = string.Empty;
            return true;
        }

        public void Activate(Vector3 nextCenter, float radius, Color color, float nextDuration)
        {
            if (!prepared)
            {
                return;
            }

            center = nextCenter;
            baseRadius = Mathf.Max(0.01f, radius);
            duration = Mathf.Max(0.01f, nextDuration);
            elapsed = 0f;
            baseColor = color;
            cachedTransform.position = center;
            lineRenderer.startWidth = 0.085f;
            lineRenderer.endWidth = 0.085f;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            centerLight.color = color;
            centerLight.range = Mathf.Clamp(baseRadius * 0.32f, 0.8f, 3.2f);
            centerLight.intensity = 1.15f;
            WriteCircle(baseRadius * 0.82f);
            lineRenderer.enabled = true;
            centerLight.enabled = true;
            gameObject.SetActive(true);
        }

        public bool Advance(float deltaTime)
        {
            if (!IsActive)
            {
                return false;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(elapsed / duration);
            Color color = baseColor;
            color.a = baseColor.a * (1f - progress);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            centerLight.intensity = 1.15f * (1f - progress) * (1f - progress);
            WriteCircle(baseRadius * Mathf.Lerp(0.82f, 1f, progress));
            if (progress < 1f)
            {
                return true;
            }

            Deactivate();
            return false;
        }

        public void Deactivate()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (centerLight != null)
            {
                centerLight.enabled = false;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void WriteCircle(float radius)
        {
            for (int index = 0; index <= CircleSegmentCount; index++)
            {
                float fraction = index == CircleSegmentCount
                    ? 0f
                    : index / (float)CircleSegmentCount;
                float angle = fraction * Mathf.PI * 2f;
                circlePoints[index] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.055f,
                    Mathf.Sin(angle) * radius);
            }

            lineRenderer.SetPositions(circlePoints);
        }
    }

    /// <summary>
    /// A single reusable muzzle flash. It is independent from the frozen combat
    /// ray so authored weapon placement cannot change attack geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMuzzleFlashView : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Light muzzleLight;
        private Color baseColor;
        private float duration;
        private float elapsed;
        private float baseIntensity;
        private bool prepared;

        public bool IsPrepared => prepared;
        public bool IsActive => prepared && gameObject.activeSelf;

        public bool TryPrepare(Material material, out string error)
        {
            if (material == null)
            {
                error = "Muzzle flash requires a material.";
                return false;
            }

            if (prepared)
            {
                error = string.Empty;
                return true;
            }

            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.sharedMaterial = material;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 3;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            muzzleLight = GetComponent<Light>();
            if (muzzleLight == null)
            {
                muzzleLight = gameObject.AddComponent<Light>();
            }

            muzzleLight.type = LightType.Point;
            muzzleLight.shadows = LightShadows.None;
            muzzleLight.renderMode = LightRenderMode.ForcePixel;
            prepared = true;
            Deactivate();
            error = string.Empty;
            return true;
        }

        public void Activate(
            Vector3 position,
            Vector3 forward,
            Color color,
            float nextDuration,
            float length,
            float width,
            float intensity)
        {
            if (!prepared)
            {
                return;
            }

            if (forward.sqrMagnitude <= 0.000001f)
            {
                forward = Vector3.forward;
            }

            duration = Mathf.Max(0.01f, nextDuration);
            elapsed = 0f;
            baseColor = color;
            baseIntensity = Mathf.Max(0f, intensity);
            Vector3 end = position + forward.normalized * Mathf.Max(0.01f, length);
            lineRenderer.SetPosition(0, position);
            lineRenderer.SetPosition(1, end);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width * 0.06f;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            muzzleLight.transform.position = position;
            muzzleLight.color = color;
            muzzleLight.range = Mathf.Clamp(length * 4.5f, 1.1f, 3f);
            muzzleLight.intensity = baseIntensity;
            lineRenderer.enabled = true;
            muzzleLight.enabled = baseIntensity > 0f;
            gameObject.SetActive(true);
        }

        public void Advance(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(elapsed / duration);
            Color color = baseColor;
            color.a = baseColor.a * (1f - progress);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            muzzleLight.intensity = baseIntensity * (1f - progress) * (1f - progress);
            if (progress >= 1f)
            {
                Deactivate();
            }
        }

        public void Deactivate()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
