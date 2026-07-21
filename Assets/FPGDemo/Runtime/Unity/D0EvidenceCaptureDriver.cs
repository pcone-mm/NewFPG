using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Opt-in, player-side evidence capture for the G6 acceptance pass.
    /// The component is inert unless the executable is launched with
    /// <see cref="EvidenceCaptureArgument"/>. It deliberately owns no combat
    /// state and does not participate in normal gameplay or performance runs.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class D0EvidenceCaptureDriver : MonoBehaviour
    {
        public const string EvidenceCaptureArgument = "-d0-g6-evidence";
        public const int VideoCaptureFramesPerSecond = 10;

        private const string EvidenceFolderName = "D0G6Evidence";

        private bool captureActive;
        private bool videoRecording;
        private float nextVideoCaptureTime;
        private string runDirectory;
        private string screenshotDirectory;
        private string videoFrameDirectory;
        private string eventLogPath;
        private int stillCaptureCount;
        private int videoFrameCount;

        public bool CaptureActive => captureActive;

        public bool IsVideoRecording => videoRecording;

        public string RunDirectory => runDirectory;

        private void Awake()
        {
            captureActive = IsEvidenceCaptureBuild()
                || IsEvidenceCaptureRequested(Environment.GetCommandLineArgs());
            if (!captureActive)
            {
                enabled = false;
                return;
            }

            string runName = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            runDirectory = Path.Combine(
                Application.persistentDataPath,
                EvidenceFolderName,
                runName);
            screenshotDirectory = Path.Combine(runDirectory, "screenshots");
            videoFrameDirectory = Path.Combine(runDirectory, "video_frames");
            eventLogPath = Path.Combine(runDirectory, "events.tsv");

            Directory.CreateDirectory(screenshotDirectory);
            Directory.CreateDirectory(videoFrameDirectory);
            File.WriteAllText(
                eventLogPath,
                "event\tlabel\tframe\ttime_unscaled\tscreen\tpath\n");
            WriteEvent("capture_ready", "g6", 0, string.Empty);
            Debug.Log(
                $"[D0_EVIDENCE] capture_ready path={runDirectory} "
                + $"screen={Screen.width}x{Screen.height} fps={VideoCaptureFramesPerSecond}");
        }

        private void Update()
        {
            if (!captureActive)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(1);
            }
            else if (keyboard.f2Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(2);
            }
            else if (keyboard.f3Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(3);
            }
            else if (keyboard.f4Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(4);
            }
            else if (keyboard.f6Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(6);
            }
            else if (keyboard.f7Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(7);
            }
            else if (keyboard.f8Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(8);
            }
            else if (keyboard.f9Key.wasPressedThisFrame)
            {
                CaptureStillForFunctionKey(9);
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                StartVideoCapture();
            }
            else if (keyboard.f11Key.wasPressedThisFrame)
            {
                StopVideoCapture();
            }
        }

        private void LateUpdate()
        {
            if (!captureActive
                || !videoRecording
                || Time.unscaledTime < nextVideoCaptureTime)
            {
                return;
            }

            string path = Path.Combine(
                videoFrameDirectory,
                $"frame_{videoFrameCount:D5}.png");
            ScreenCapture.CaptureScreenshot(path);
            WriteEvent("video_frame", "full_flow", videoFrameCount, path);
            videoFrameCount++;
            nextVideoCaptureTime = Time.unscaledTime + (1f / VideoCaptureFramesPerSecond);
        }

        private void OnApplicationQuit()
        {
            StopVideoCapture();
        }

        public static bool IsEvidenceCaptureRequested(string[] commandLineArguments)
        {
            if (commandLineArguments == null)
            {
                return false;
            }

            for (int index = 0; index < commandLineArguments.Length; index++)
            {
                if (string.Equals(
                        commandLineArguments[index],
                        EvidenceCaptureArgument,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEvidenceCaptureBuild()
        {
#if FPG_D0_G6_EVIDENCE
            return true;
#else
            return false;
#endif
        }

        public static bool TryGetStillLabelForFunctionKey(
            int functionKey,
            out string label)
        {
            switch (functionKey)
            {
                case 1:
                    label = "initial";
                    return true;
                case 2:
                    label = "primary_hit";
                    return true;
                case 3:
                    label = "weakpoint_hit";
                    return true;
                case 4:
                    label = "interceptable_volley";
                    return true;
                case 6:
                    label = "heavy_warning";
                    return true;
                case 7:
                    label = "break";
                    return true;
                case 8:
                    label = "victory";
                    return true;
                case 9:
                    label = "defeat";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        private void CaptureStillForFunctionKey(int functionKey)
        {
            if (!TryGetStillLabelForFunctionKey(functionKey, out string label))
            {
                return;
            }

            CaptureNamedStill(label);
        }

        /// <summary>
        /// Captures one labelled acceptance still from the actual Player frame.
        /// This is intentionally presentation-only: it neither reads nor writes
        /// battle state, so an opt-in evidence scenario may call it after the
        /// normal session and presenters have produced a visual state.
        /// </summary>
        public bool CaptureNamedStill(string label)
        {
            if (!captureActive || !IsSafeCaptureLabel(label))
            {
                return false;
            }

            stillCaptureCount++;
            string path = Path.Combine(
                screenshotDirectory,
                $"{label}_{stillCaptureCount:D3}.png");
            ScreenCapture.CaptureScreenshot(path);
            WriteEvent("still", label, stillCaptureCount, path);
            Debug.Log($"[D0_EVIDENCE] still label={label} path={path}");
            return true;
        }

        /// <summary>
        /// Starts the one supported PNG-frame recording for this Player run.
        /// A caller must not start a second recording in the same run because
        /// frame numbering deliberately begins at zero for deterministic export.
        /// </summary>
        public bool StartVideoCapture()
        {
            if (!captureActive || videoRecording || videoFrameCount != 0)
            {
                return false;
            }

            videoRecording = true;
            videoFrameCount = 0;
            nextVideoCaptureTime = Time.unscaledTime;
            WriteEvent("video_start", "full_flow", 0, videoFrameDirectory);
            Debug.Log($"[D0_EVIDENCE] video_start path={videoFrameDirectory}");
            return true;
        }

        public bool StopVideoCapture()
        {
            if (!captureActive || !videoRecording)
            {
                return false;
            }

            videoRecording = false;
            WriteEvent("video_stop", "full_flow", videoFrameCount, videoFrameDirectory);
            Debug.Log(
                $"[D0_EVIDENCE] video_stop frames={videoFrameCount} "
                + $"path={videoFrameDirectory}");
            return true;
        }

        /// <summary>
        /// Writes a small, structured acceptance marker beside the captured
        /// images. It exists for evidence orchestration only and cannot affect
        /// the live battle session.
        /// </summary>
        public bool RecordMarker(string eventName, string label, int sequence)
        {
            if (!captureActive
                || !IsSafeCaptureLabel(eventName)
                || !IsSafeCaptureLabel(label)
                || sequence < 0)
            {
                return false;
            }

            WriteEvent(eventName, label, sequence, string.Empty);
            return true;
        }

        private void WriteEvent(string eventName, string label, int sequence, string path)
        {
            string screen = $"{Screen.width}x{Screen.height}";
            string line = $"{eventName}\t{label}\t{sequence}\t{Time.unscaledTime:F3}\t{screen}\t{path}\n";
            File.AppendAllText(eventLogPath, line);
        }

        private static bool IsSafeCaptureLabel(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character == '_'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
