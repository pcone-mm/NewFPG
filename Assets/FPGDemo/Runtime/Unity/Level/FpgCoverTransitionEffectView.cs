using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgCoverTransitionEffectView : MonoBehaviour
    {
        [SerializeField]
        private GameObject orbRoot;

        [SerializeField]
        private TrailRenderer trail;

        [SerializeField]
        private ParticleSystem departureEffect;

        [SerializeField]
        private ParticleSystem arrivalEffect;

        [SerializeField, Min(0.01f)]
        private float lingerSeconds = 0.5f;

        private float remainingLinger;

        private bool paused;
        private bool trailWasEmitting;

        public bool TryValidate(out string error)
        {
            if (orbRoot == null || trail == null
                || departureEffect == null || arrivalEffect == null)
            {
                error = "Cover transition effect requires orb, trail, departure and arrival references.";
                return false;
            }

            if (!orbRoot.transform.IsChildOf(transform)
                || !trail.transform.IsChildOf(orbRoot.transform))
            {
                error = "Cover transition orb and trail must belong to the wrapper Prefab.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void SetPaused(bool value)
        {
            if (paused == value)
            {
                return;
            }

            paused = value;
            if (paused)
            {
                trailWasEmitting = trail != null && trail.emitting;
                if (trail != null)
                {
                    trail.emitting = false;
                }

                PauseIfPlaying(departureEffect);
                PauseIfPlaying(arrivalEffect);
                return;
            }

            ResumeIfPaused(departureEffect);
            ResumeIfPaused(arrivalEffect);
            if (trail != null && orbRoot != null && orbRoot.activeSelf)
            {
                trail.emitting = trailWasEmitting;
            }

            trailWasEmitting = false;
        }

        public void Prepare()
        {
            paused = false;
            trailWasEmitting = false;
            remainingLinger = 0f;
            trail.emitting = false;
            trail.Clear();
            orbRoot.SetActive(false);
            StopAndClear(departureEffect);
            StopAndClear(arrivalEffect);
            gameObject.SetActive(false);
        }

        public void Begin(Vector3 position)
        {
            gameObject.SetActive(true);
            paused = false;
            trailWasEmitting = true;
            remainingLinger = 0f;
            transform.position = position;
            orbRoot.SetActive(true);
            orbRoot.transform.position = position;
            trail.Clear();
            trail.emitting = true;
            departureEffect.transform.position = position;
            departureEffect.Play(true);
        }

        public void SetOrbPosition(Vector3 position)
        {
            if (orbRoot != null)
            {
                orbRoot.transform.position = position;
            }
        }

        public void Complete(Vector3 position)
        {
            SetOrbPosition(position);
            trailWasEmitting = false;
            trail.emitting = false;
            orbRoot.SetActive(false);
            arrivalEffect.transform.position = position;
            arrivalEffect.Play(true);
            remainingLinger = lingerSeconds;
        }

        private void Update()
        {
            if (paused || remainingLinger <= 0f)
            {
                return;
            }

            remainingLinger -= Time.deltaTime;
            if (remainingLinger <= 0f)
            {
                Prepare();
            }
        }

        private static void PauseIfPlaying(ParticleSystem value)
        {
            if (value != null && value.isPlaying)
            {
                value.Pause(true);
            }
        }

        private static void ResumeIfPaused(ParticleSystem value)
        {
            if (value != null && value.isPaused)
            {
                value.Play(true);
            }
        }

        private static void StopAndClear(ParticleSystem value)
        {
            if (value != null)
            {
                value.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
