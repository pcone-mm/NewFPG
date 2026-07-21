using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Opt-in player-side frame pacing and managed-allocation sampler for the
    /// D0 acceptance build. It is installed only when the process is launched
    /// with <c>-d0-perf</c>, so normal play has no sampler buffers or runtime
    /// work. The resulting one-line record is intentionally written to
    /// Player.log, which is the G5/G6 evidence source.
    /// </summary>
    [DefaultExecutionOrder(2000)]
    [DisallowMultipleComponent]
    public sealed class D0RuntimePerformanceSampler : MonoBehaviour
    {
        public const string EnableArgument = "-d0-perf";
        public const string DurationArgument = "-d0-perf-duration";

        private const float DefaultDurationSeconds = 60f;
        private const int FrameSampleCapacity = 8192;
        private GameBootstrap bootstrap;
        private float[] frameMilliseconds;
        private bool managedAllocationCounterAvailable;
        private bool isCapturing;
        private bool completed;
        private float captureDurationSeconds;
        private float captureElapsedSeconds;
        private int frameSampleCount;
        private int droppedFrameSampleCount;
        private long previousManagedAllocatedBytes;
        private long totalGcAllocatedBytes;
        private long peakGcAllocatedBytes;

        public bool IsCapturing => isCapturing;
        public bool IsComplete => completed;
        public int FrameSampleCount => frameSampleCount;
        public int DroppedFrameSampleCount => droppedFrameSampleCount;
        public bool HasGcAllocationCounter => managedAllocationCounterAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterForRequestedPlayerRun()
        {
            if (!IsRequested())
            {
                return;
            }

            SceneManager.sceneLoaded -= TryAttachToBootScene;
            SceneManager.sceneLoaded += TryAttachToBootScene;
        }

        private static void TryAttachToBootScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Boot" || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameBootstrap candidate = roots[index].GetComponentInChildren<GameBootstrap>(true);
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.GetComponent<D0RuntimePerformanceSampler>() == null)
                {
                    candidate.gameObject.AddComponent<D0RuntimePerformanceSampler>();
                }

                SceneManager.sceneLoaded -= TryAttachToBootScene;
                return;
            }
        }

        private void Awake()
        {
            bootstrap = GetComponent<GameBootstrap>();
            captureDurationSeconds = GetRequestedDurationSeconds();
        }

        private void Update()
        {
            if (completed)
            {
                return;
            }

            if (!isCapturing)
            {
                if (bootstrap == null
                    || bootstrap.State != BootstrapState.Running
                    || bootstrap.ActiveHost == null
                    || !bootstrap.ActiveHost.IsSessionRunning)
                {
                    return;
                }

                BeginCapture();
                return;
            }

            if (!IsSessionHealthy())
            {
                FailCapture("session_stopped");
                return;
            }

            float frameMillisecondsValue = Mathf.Max(0f, Time.unscaledDeltaTime * 1000f);
            if (frameSampleCount < frameMilliseconds.Length)
            {
                frameMilliseconds[frameSampleCount++] = frameMillisecondsValue;
            }
            else
            {
                droppedFrameSampleCount++;
            }

            if (managedAllocationCounterAvailable)
            {
                long currentManagedAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                long bytes = currentManagedAllocatedBytes - previousManagedAllocatedBytes;
                previousManagedAllocatedBytes = currentManagedAllocatedBytes;
                if (bytes > 0L)
                {
                    totalGcAllocatedBytes += bytes;
                    if (bytes > peakGcAllocatedBytes)
                    {
                        peakGcAllocatedBytes = bytes;
                    }
                }
            }

            captureElapsedSeconds += Time.unscaledDeltaTime;
            if (captureElapsedSeconds >= captureDurationSeconds)
            {
                CompleteCapture();
            }
        }

        private void BeginCapture()
        {
            frameMilliseconds = new float[FrameSampleCapacity];
            frameSampleCount = 0;
            droppedFrameSampleCount = 0;
            totalGcAllocatedBytes = 0L;
            peakGcAllocatedBytes = 0L;
            captureElapsedSeconds = 0f;
            Debug.Log(
                "[D0_PERF] capture_started duration_s="
                + captureDurationSeconds.ToString("F2", CultureInfo.InvariantCulture),
                this);
            TryBeginManagedAllocationCounter();
            if (!managedAllocationCounterAvailable)
            {
                FailCapture("managed_counter_unavailable");
                return;
            }

            isCapturing = true;
        }

        private void CompleteCapture()
        {
            isCapturing = false;
            completed = true;
            if (frameSampleCount <= 0)
            {
                Debug.LogError("[D0_PERF] capture_failed reason=no_frame_samples", this);
                return;
            }

            if (!IsSessionHealthy())
            {
                Debug.LogError("[D0_PERF] capture_failed reason=session_stopped", this);
                return;
            }

            Array.Sort(frameMilliseconds, 0, frameSampleCount);
            float p95 = GetPercentile(0.95f);
            float p99 = GetPercentile(0.99f);
            float maximum = frameMilliseconds[frameSampleCount - 1];
            string message = string.Format(
                CultureInfo.InvariantCulture,
                "[D0_PERF] capture_complete duration_s={0:F2} samples={1} dropped_samples={2} p95_ms={3:F3} p99_ms={4:F3} max_ms={5:F3} gc_counter_valid={6} gc_total_bytes={7} gc_peak_frame_bytes={8}",
                captureElapsedSeconds,
                frameSampleCount,
                droppedFrameSampleCount,
                p95,
                p99,
                maximum,
                HasGcAllocationCounter ? "true" : "false",
                totalGcAllocatedBytes,
                peakGcAllocatedBytes);
            Debug.Log(message, this);
        }

        private float GetPercentile(float percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt(frameSampleCount * percentile) - 1,
                0,
                frameSampleCount - 1);
            return frameMilliseconds[index];
        }

        private bool IsSessionHealthy()
        {
            return bootstrap != null
                && bootstrap.ActiveHost != null
                && bootstrap.ActiveHost.IsSessionRunning
                && string.IsNullOrEmpty(bootstrap.ActiveHost.LastError);
        }

        private void TryBeginManagedAllocationCounter()
        {
            try
            {
                previousManagedAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                managedAllocationCounterAvailable = true;
            }
            catch (Exception exception)
            {
                managedAllocationCounterAvailable = false;
                Debug.LogWarning(
                    "[D0_PERF] managed allocation counter unavailable: " + exception.Message,
                    this);
            }
        }

        private void FailCapture(string reason)
        {
            isCapturing = false;
            completed = true;
            Debug.LogError(
                "[D0_PERF] capture_failed reason=" + reason
                + " samples=" + frameSampleCount.ToString(CultureInfo.InvariantCulture),
                this);
        }

        private static bool IsRequested()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], EnableArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetRequestedDurationSeconds()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], DurationArgument, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (float.TryParse(
                        arguments[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float requested)
                    && requested > 0f
                    && !float.IsInfinity(requested)
                    && !float.IsNaN(requested))
                {
                    return requested;
                }
            }

            return DefaultDurationSeconds;
        }
    }
}
