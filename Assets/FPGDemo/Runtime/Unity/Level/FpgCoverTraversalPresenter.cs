using UnityEngine;

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class FpgCoverTraversalPresenter : MonoBehaviour
    {
        [SerializeField]
        private FpgCoverTransitionEffectView transitionEffectPrefab;

        private FpgCoverTransitionEffectView transitionEffect;
        private Transform playerVisualRoot;
        private Pose sourcePose;
        private Pose targetPose;
        private float duration;
        private float elapsed;
        private bool visualWasActive = true;

        private bool paused;

        public bool IsPlaying { get; private set; }
        public bool HasReachedVisualEnd => IsPlaying && elapsed >= duration;

        public bool IsPaused => paused;

        public void SetPaused(bool value)
        {
            transitionEffect?.SetPaused(value);
            paused = value;
        }

        public bool TryConfigure(Transform visualRoot, out string error)
        {
            error = string.Empty;
            if (visualRoot == null || transitionEffectPrefab == null
                || !transitionEffectPrefab.TryValidate(out error))
            {
                error = string.IsNullOrEmpty(error)
                    ? "Cover traversal presenter requires player visual and a valid effect Prefab."
                    : error;
                return false;
            }

            playerVisualRoot = visualRoot;
            EnsureEffectInstance();
            error = string.Empty;
            return true;
        }

        public bool TryBegin(
            Pose source,
            Pose target,
            float traversalSeconds,
            out string error)
        {
            if (IsPlaying || playerVisualRoot == null
                || transitionEffect == null
                || float.IsNaN(traversalSeconds)
                || float.IsInfinity(traversalSeconds)
                || traversalSeconds <= 0f)
            {
                error = "Cover traversal presentation is not configured or is already active.";
                return false;
            }

            paused = false;
            sourcePose = source;
            targetPose = target;
            duration = traversalSeconds;
            elapsed = 0f;
            visualWasActive = playerVisualRoot.gameObject.activeSelf;
            playerVisualRoot.gameObject.SetActive(false);

            transitionEffect.Begin(source.position);
            IsPlaying = true;
            error = string.Empty;
            return true;
        }

        public void Complete(Pose destination)
        {
            if (!IsPlaying)
            {
                return;
            }

            transitionEffect.SetOrbPosition(destination.position);
            transitionEffect.Complete(destination.position);
            RestorePlayerPresentation();
        }

        public void Cancel()
        {
            if (transitionEffect != null)
            {
                transitionEffect.Prepare();
            }

            if (!IsPlaying)
            {
                paused = false;
                return;
            }

            RestorePlayerPresentation();
        }

        private void Awake()
        {
            EnsureEffectInstance();
        }

        private void Update()
        {
            if (!IsPlaying || paused)
            {
                return;
            }

            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float progress = duration <= 0f ? 1f : elapsed / duration;
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 orbPosition = Vector3.LerpUnclamped(
                sourcePose.position,
                targetPose.position,
                eased);
            transitionEffect.SetOrbPosition(orbPosition);
        }

        private void EnsureEffectInstance()
        {
            if (transitionEffect != null || transitionEffectPrefab == null)
            {
                return;
            }

            transitionEffect = Instantiate(transitionEffectPrefab, transform);
            transitionEffect.name = transitionEffectPrefab.name + " [Runtime]";
            transitionEffect.Prepare();
        }

        private void RestorePlayerPresentation()
        {
            if (playerVisualRoot != null)
            {
                playerVisualRoot.gameObject.SetActive(visualWasActive);
            }

            paused = false;
            IsPlaying = false;
            elapsed = 0f;
        }
    }
}
