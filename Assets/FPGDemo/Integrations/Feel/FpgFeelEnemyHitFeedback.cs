using System;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgFeelEnemyHitFeedback : MonoBehaviour
    {
        [SerializeField] private MMF_Player hitPlayer;
        [SerializeField] private FpgFeelRenderScaleSpringTarget renderScaleSpring;
        [SerializeField, Min(0.01f)] private float oneShotDuration = 0.06f;
        [SerializeField, Min(0f)] private float cooldownDuration = 0.06f;

        private FpgEnemyEntityView entityView;
        private float lastPlayTime = float.NegativeInfinity;
        private float stopTime = float.PositiveInfinity;
        private bool oneShotActive;

        public MMF_Player HitPlayer => hitPlayer;
        public FpgFeelRenderScaleSpringTarget RenderScaleSpring =>
            renderScaleSpring;
        public float OneShotDuration => oneShotDuration;
        public float CooldownDuration => cooldownDuration;

        public bool TryValidate(out string error)
        {
            if (!TryResolveReferences())
            {
                error =
                    "Enemy hit Feel requires an MMF_Player, render-scale spring and parent FpgEnemyEntityView.";
                return false;
            }

            if (entityView.SkeletonAnimation == null)
            {
                error =
                    "Enemy hit Feel requires the parent enemy's SkeletonAnimation VisualRoot.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPlayHit()
        {
            if (!isActiveAndEnabled
                || !TryResolveReferences()
                || entityView == null
                || !entityView.GameplayEnabled
                || entityView.SkeletonAnimation == null
                || Time.time - lastPlayTime < cooldownDuration)
            {
                return false;
            }

            Transform visualRoot = entityView.SkeletonAnimation.transform;
            renderScaleSpring.BindVisualRoot(visualRoot);
            lastPlayTime = Time.time;
            stopTime = Time.time + Mathf.Max(0.01f, oneShotDuration);
            oneShotActive = true;
            hitPlayer.PlayFeedbacks(visualRoot.position);
            return true;
        }

        public void StopAndRestore()
        {
            oneShotActive = false;
            stopTime = float.PositiveInfinity;
            lastPlayTime = float.NegativeInfinity;
            try
            {
                if (hitPlayer != null)
                {
                    hitPlayer.StopFeedbacks();
                    hitPlayer.ResetFeedbacks();
                    hitPlayer.RestoreInitialValues();
                }
            }
            finally
            {
                if (renderScaleSpring != null)
                {
                    try
                    {
                        renderScaleSpring.StopAndRestore();
                    }
                    finally
                    {
                        renderScaleSpring.ForceRestoreRenderedScale();
                    }
                }
            }
        }

        private void Awake()
        {
            TryResolveReferences();
        }

        private void OnEnable()
        {
            TryResolveReferences();
        }

        private void Update()
        {
            if (oneShotActive && Time.time >= stopTime)
            {
                StopAndRestore();
            }
        }

        private void OnDisable()
        {
            try
            {
                StopAndRestore();
            }
            catch (Exception)
            {
                renderScaleSpring?.ForceRestoreRenderedScale();
            }
        }

        private bool TryResolveReferences()
        {
            if (entityView == null)
            {
                entityView = GetComponentInParent<FpgEnemyEntityView>(true);
            }

            if (hitPlayer == null)
            {
                hitPlayer = GetComponentInChildren<MMF_Player>(true);
            }

            if (renderScaleSpring == null)
            {
                renderScaleSpring =
                    GetComponent<FpgFeelRenderScaleSpringTarget>();
            }

            if (entityView != null
                && entityView.SkeletonAnimation != null
                && renderScaleSpring != null)
            {
                renderScaleSpring.BindVisualRoot(
                    entityView.SkeletonAnimation.transform);
            }

            return entityView != null
                && hitPlayer != null
                && renderScaleSpring != null;
        }
    }
}
