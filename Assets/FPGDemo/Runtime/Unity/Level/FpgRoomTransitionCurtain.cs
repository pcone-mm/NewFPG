using System.Collections;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgRoomTransitionCurtain : MonoBehaviour
    {
        public const float DefaultFadeDuration = 0.15f;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField, Min(0f)]
        private float fadeDuration = DefaultFadeDuration;

        public CanvasGroup CanvasGroup => canvasGroup;
        public float FadeDuration => fadeDuration;
        public bool IsOpaque => canvasGroup != null
            && canvasGroup.alpha >= 0.999f;

        public bool TryValidateAuthoring(out string error)
        {
            if (canvasGroup == null)
            {
                error = "Room transition curtain requires an explicit CanvasGroup.";
                return false;
            }

            if (canvasGroup.gameObject.scene != gameObject.scene)
            {
                error =
                    "Room transition curtain and CanvasGroup must belong to the same Boot scene.";
                return false;
            }

            if (float.IsNaN(fadeDuration) || float.IsInfinity(fadeDuration)
                || fadeDuration < 0f)
            {
                error = "Room transition curtain fade duration must be finite and non-negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public IEnumerator FadeToOpaque()
        {
            yield return FadeTo(1f, keepBlocking: true);
        }

        public IEnumerator FadeToTransparent()
        {
            yield return FadeTo(0f, keepBlocking: false);
        }

        public void SetImmediate(bool opaque)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = opaque ? 1f : 0f;
            canvasGroup.blocksRaycasts = opaque;
            canvasGroup.interactable = opaque;
        }

        private IEnumerator FadeTo(float targetAlpha, bool keepBlocking)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            float startAlpha = canvasGroup.alpha;
            float duration = Application.isBatchMode ? 0f : fadeDuration;
            if (duration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
            {
                canvasGroup.alpha = targetAlpha;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(
                        startAlpha,
                        targetAlpha,
                        Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                canvasGroup.alpha = targetAlpha;
            }

            canvasGroup.blocksRaycasts = keepBlocking;
            canvasGroup.interactable = keepBlocking;
        }

        private void Awake()
        {
            SetImmediate(false);
        }
    }
}
