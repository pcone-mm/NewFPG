using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class AttackWarningIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color warningColor = new Color(1f, 0.22f, 0.12f, 0.55f);
        [SerializeField] private bool detachOnPlay = true;
        [SerializeField] private bool faceCamera = true;
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(8)] private int circleSegments = 72;
        [SerializeField, Min(0.01f)] private float lineWidth = 0.08f;

        private float startRadius;
        private float duration;
        private float elapsed;
        private bool playing;
        private Transform followTarget;
        private Vector3 followOffset;
        private LineRenderer lineRenderer;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            lineRenderer = GetComponent<LineRenderer>();
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = warningColor;
            }

            ConfigureLineRenderer();
            gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            circleSegments = Mathf.Max(8, circleSegments);
            lineWidth = Mathf.Max(0.01f, lineWidth);
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            ConfigureLineRenderer();
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float ratio = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float radius = Mathf.Lerp(startRadius, 0f, ratio);
            RefreshWorldPosition();
            RefreshCameraFacing();
            DrawCircle(radius);

            if (ratio >= 1f)
            {
                Hide();
            }
        }

        public void Play(Vector3 position, float radius, float seconds)
        {
            if (detachOnPlay && transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            startRadius = Mathf.Max(0.01f, radius);
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            playing = true;
            followTarget = null;
            transform.position = position;
            RefreshCameraFacing();
            transform.localScale = new Vector3(startRadius * 2f, startRadius * 2f, 1f);
            DrawCircle(startRadius);
            gameObject.SetActive(true);
        }

        public void PlayFollow(Transform target, Vector3 offset, float radius, float seconds)
        {
            if (detachOnPlay && transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            followTarget = target;
            followOffset = offset;
            startRadius = Mathf.Max(0.01f, radius);
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            playing = true;
            transform.localScale = Vector3.one;
            RefreshWorldPosition();
            RefreshCameraFacing();
            DrawCircle(startRadius);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            playing = false;
            followTarget = null;
            gameObject.SetActive(false);
        }

        public static AttackWarningIndicator CreateRuntime(string name, Transform parent)
        {
            GameObject indicatorObject = new GameObject(name, typeof(LineRenderer), typeof(AttackWarningIndicator));
            if (parent != null)
            {
                indicatorObject.transform.SetParent(parent, false);
            }

            AttackWarningIndicator indicator = indicatorObject.GetComponent<AttackWarningIndicator>();
            indicator.ConfigureLineRenderer();
            return indicator;
        }

        private void RefreshWorldPosition()
        {
            if (followTarget != null)
            {
                transform.position = followTarget.position + followOffset;
            }
        }

        private void ConfigureLineRenderer()
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.widthMultiplier = lineWidth;
            lineRenderer.positionCount = Mathf.Max(8, circleSegments);
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.startColor = warningColor;
            lineRenderer.endColor = warningColor;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sortingOrder = 200;
            if (lineRenderer.sharedMaterial == null)
            {
                lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            }

            lineRenderer.sharedMaterial.color = warningColor;
            if (lineRenderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                lineRenderer.sharedMaterial.SetColor("_BaseColor", warningColor);
            }

            if (lineRenderer.sharedMaterial.HasProperty("_Color"))
            {
                lineRenderer.sharedMaterial.SetColor("_Color", warningColor);
            }
        }

        private void RefreshCameraFacing()
        {
            if (!faceCamera)
            {
                return;
            }

            Camera camera = ResolveFacingCamera();
            if (camera == null)
            {
                return;
            }

            Vector3 toCamera = camera.transform.position - transform.position;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(toCamera.normalized, camera.transform.up);
        }

        private Camera ResolveFacingCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera;
            }

            return Camera.current;
        }

        private void DrawCircle(float radius)
        {
            if (lineRenderer == null)
            {
                return;
            }

            int segments = Mathf.Max(8, circleSegments);
            if (lineRenderer.positionCount != segments)
            {
                lineRenderer.positionCount = segments;
            }

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                lineRenderer.SetPosition(i, point);
            }
        }
    }
}
