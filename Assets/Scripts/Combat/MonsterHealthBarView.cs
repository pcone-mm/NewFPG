using UnityEngine;
using UnityEngine.UI;

namespace NewFPG.Combat
{
    public sealed class MonsterHealthBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private RectTransform shieldFill;
        [SerializeField] private Text nameText;
        [SerializeField] private Text valueText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

        private CombatVitals vitals;
        private Transform target;
        private Camera targetCamera;
        private bool boss;
        private string displayName;

        public CombatVitals Vitals => vitals;
        public bool IsBoss => boss;
        public float DisplayedHealthRatio => vitals != null ? vitals.HealthRatio : 0f;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        public void Initialize(
            RectTransform nextHealthFill,
            RectTransform nextShieldFill,
            Text nextNameText,
            Text nextValueText,
            CanvasGroup nextCanvasGroup,
            Vector3 nextWorldOffset)
        {
            healthFill = nextHealthFill;
            shieldFill = nextShieldFill;
            nameText = nextNameText;
            valueText = nextValueText;
            canvasGroup = nextCanvasGroup;
            worldOffset = nextWorldOffset;
            CacheReferences();
        }

        public void Bind(CombatVitals vitals, Transform target, Camera camera, bool boss, string displayName)
        {
            CacheReferences();
            this.vitals = vitals;
            this.target = target;
            targetCamera = camera;
            this.boss = boss;
            this.displayName = string.IsNullOrWhiteSpace(displayName)
                ? target != null ? target.name : "Monster"
                : displayName;

            gameObject.SetActive(true);
            Refresh();
        }

        public void Unbind()
        {
            vitals = null;
            target = null;
            targetCamera = null;
            SetVisible(false);
            gameObject.SetActive(false);
        }

        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
        }

        public void Refresh()
        {
            RefreshFill(healthFill, vitals != null ? vitals.HealthRatio : 0f);
            RefreshFill(shieldFill, vitals != null ? vitals.ShieldRatio : 0f);

            if (shieldFill != null)
            {
                shieldFill.gameObject.SetActive(vitals != null && vitals.CurrentShield > 0f);
            }

            if (nameText != null)
            {
                nameText.text = displayName;
                nameText.gameObject.SetActive(boss);
            }

            if (valueText != null)
            {
                valueText.text = vitals != null
                    ? vitals.CurrentHealth.ToString("0") + "/" + vitals.MaxHealth.ToString("0")
                    : "0/0";
                valueText.gameObject.SetActive(boss);
            }
        }

        public void UpdatePosition(Camera camera, RectTransform canvasRect)
        {
            if (boss)
            {
                SetVisible(vitals != null && vitals.IsAlive);
                return;
            }

            if (vitals == null || target == null || !vitals.IsAlive)
            {
                SetVisible(false);
                return;
            }

            Camera projectionCamera = camera != null ? camera : targetCamera;
            Vector3 worldPosition = ResolveFollowWorldPosition();
            Vector2 screenPosition;
            if (!TryWorldToScreenPoint(projectionCamera, worldPosition, out screenPosition))
            {
                SetVisible(false);
                return;
            }

            if (rectTransform != null && canvasRect != null)
            {
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    null,
                    out localPoint);
                rectTransform.anchoredPosition = localPoint;
            }

            SetVisible(true);
        }

        private Vector3 ResolveFollowWorldPosition()
        {
            if (target == null)
            {
                return worldOffset;
            }

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + worldOffset;
            }

            Collider collider = target.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + worldOffset;
            }

            return target.position + worldOffset + Vector3.up * 1.4f;
        }

        private static bool TryWorldToScreenPoint(Camera camera, Vector3 worldPosition, out Vector2 screenPosition)
        {
            if (camera == null)
            {
                screenPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return Screen.width > 0 && Screen.height > 0;
            }

            Vector3 projected = camera.WorldToScreenPoint(worldPosition);
            screenPosition = projected;
            return projected.z > camera.nearClipPlane;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
            }
        }

        private void CacheReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private static void RefreshFill(RectTransform fill, float ratio)
        {
            if (fill == null)
            {
                return;
            }

            Vector2 anchorMax = fill.anchorMax;
            anchorMax.x = Mathf.Clamp01(ratio);
            fill.anchorMax = anchorMax;
        }
    }
}
