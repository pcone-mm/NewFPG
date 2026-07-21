using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation-only terminal screen treatment for the D0 slice. It is
    /// intentionally driven by the already-completed battle snapshot and owns
    /// no input, combat state, physics or scene-object lifetime outside its
    /// authored UI children.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0TerminalScreenFxPresenter : MonoBehaviour
    {
        private const float DefaultFadeSeconds = 0.22f;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Image dimmingImage;

        [SerializeField, Min(0.01f)]
        private float fadeSeconds = DefaultFadeSeconds;

        private float elapsed;
        private bool showing;
        private BattleCompletionReason completionReason;

        public bool IsShowing => showing;
        public BattleCompletionReason CompletionReason => completionReason;
        public float CurrentAlpha => canvasGroup == null ? 0f : canvasGroup.alpha;
        public Color DimmingColor => dimmingImage == null ? Color.clear : dimmingImage.color;

        public bool TryValidate(out string error)
        {
            if (canvasGroup == null || dimmingImage == null)
            {
                error = "D0 terminal screen FX requires a CanvasGroup and dimming Image.";
                return false;
            }

            if (fadeSeconds <= 0f)
            {
                error = "D0 terminal screen FX requires a positive fade duration.";
                return false;
            }

            if (GetComponentsInChildren<Collider>(true).Length > 0
                || GetComponentsInChildren<Collider2D>(true).Length > 0
                || GetComponentsInChildren<Rigidbody>(true).Length > 0
                || GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                error = "D0 terminal screen FX must not contain Collider or Rigidbody components.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Show(BattleCompletionReason nextCompletionReason)
        {
            if (nextCompletionReason != BattleCompletionReason.Victory
                && nextCompletionReason != BattleCompletionReason.Defeat)
            {
                return;
            }

            completionReason = nextCompletionReason;
            elapsed = 0f;
            showing = true;
            gameObject.SetActive(true);
            if (dimmingImage != null)
            {
                dimmingImage.color = nextCompletionReason == BattleCompletionReason.Defeat
                    ? new Color(0.015f, 0.025f, 0.07f, 0.82f)
                    : new Color(0.025f, 0.05f, 0.10f, 0.24f);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        public void Advance(float deltaTime, bool paused)
        {
            if (!showing || paused || canvasGroup == null)
            {
                return;
            }

            elapsed += Mathf.Max(0f, deltaTime);
            canvasGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeSeconds));
        }

        public void Clear()
        {
            elapsed = 0f;
            showing = false;
            completionReason = default(BattleCompletionReason);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (dimmingImage != null)
            {
                dimmingImage.color = Color.clear;
            }

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
