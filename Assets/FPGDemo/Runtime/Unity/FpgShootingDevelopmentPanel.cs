using UnityEngine;
using UnityEngine.InputSystem;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    [AddComponentMenu("FPG Demo/Diagnostics/Shooting Development Panel")]
    public sealed class FpgShootingDevelopmentPanel : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField]
        private bool visible;

        [Header("Source")]
        [SerializeField]
        [Tooltip("Component implementing IFpgShootingDiagnosticsProvider.")]
        private MonoBehaviour diagnosticsProvider;

        [Header("Layout")]
        [SerializeField]
        private Rect panelRect = new Rect(16f, 16f, 460f, 520f);

        private MonoBehaviour cachedProviderComponent;
        private IFpgShootingDiagnosticsProvider cachedProvider;
        private IFpgShootingTuningPreviewHost cachedPreviewHost;
        private FpgShootingDiagnosticsSnapshot latestSnapshot;
        private FpgShootingTuningSnapshot workingTuning;
        private string latestError = string.Empty;
        private bool hasLatestSnapshot;
        private bool hasWorkingTuning;
        private int lastSampleFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private Vector2 scrollPosition;
#endif

        public bool IsVisible => visible;
        public MonoBehaviour DiagnosticsProvider => diagnosticsProvider;
        public bool HasLatestSnapshot => hasLatestSnapshot;
        public FpgShootingDiagnosticsSnapshot LatestSnapshot =>
            latestSnapshot;
        public string LatestError => latestError;

        public bool TryConfigure(
            MonoBehaviour providerComponent,
            out string error)
        {
            if (providerComponent == null)
            {
                error =
                    "Shooting development panel requires a diagnostics provider component.";
                return false;
            }

            if (!(providerComponent is IFpgShootingDiagnosticsProvider provider))
            {
                error =
                    $"Component '{providerComponent.GetType().Name}' does not implement {nameof(IFpgShootingDiagnosticsProvider)}.";
                return false;
            }

            diagnosticsProvider = providerComponent;
            cachedProviderComponent = providerComponent;
            cachedProvider = provider;
            cachedPreviewHost = providerComponent
                as IFpgShootingTuningPreviewHost;
            ResetSample();
            error = string.Empty;
            return true;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (visible)
            {
                lastSampleFrame = -1;
            }
        }

        public void ToggleVisibility()
        {
            SetVisible(!visible);
        }

        private void OnEnable()
        {
            cachedProviderComponent = null;
            cachedProvider = null;
            cachedPreviewHost = null;
            ResetSample();
            TryResolveProvider(out _);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                ToggleVisibility();
            }
        }
#endif

        private void OnValidate()
        {
            if (!IsFinite(panelRect.x))
            {
                panelRect.x = 16f;
            }

            if (!IsFinite(panelRect.y))
            {
                panelRect.y = 16f;
            }

            panelRect.width = IsFinite(panelRect.width)
                ? Mathf.Max(320f, panelRect.width)
                : 460f;
            panelRect.height = IsFinite(panelRect.height)
                ? Mathf.Max(240f, panelRect.height)
                : 520f;

            cachedProviderComponent = null;
            cachedProvider = null;
            cachedPreviewHost = null;
            workingTuning = default(FpgShootingTuningSnapshot);
            hasWorkingTuning = false;
            lastSampleFrame = -1;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            SampleOncePerFrame();
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("\u5c04\u51fb\u8c03\u8bd5");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("\u9690\u85cf", GUILayout.Width(52f)))
            {
                SetVisible(false);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            if (!hasLatestSnapshot)
            {
                DrawError(string.IsNullOrWhiteSpace(latestError)
                    ? "Diagnostics are unavailable."
                    : latestError);
            }
            else
            {
                DrawSnapshot(latestSnapshot);
                DrawTuningControls();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void DrawSnapshot(
            FpgShootingDiagnosticsSnapshot snapshot)
        {
            GUILayout.Label($"Tick: {snapshot.Tick}");
            GUILayout.Label(
                $"Ammo: {snapshot.Ammo} / {snapshot.MagazineCapacity}");
            GUILayout.Label($"Weapon: {snapshot.WeaponState}");
            GUILayout.Label($"Reticle: {snapshot.ReticleState}");
            GUILayout.Label($"Exposure: {snapshot.ExposureState}");
            GUILayout.Label(snapshot.IsReloading
                ? $"Reload: {snapshot.ReloadProgress01:P0}"
                : "Reload: Inactive");
            GUILayout.Label(snapshot.IsCoverPeekRequested
                ? $"Peek: Requested since tick {snapshot.CoverPeekStartedTick}"
                : "Peek: Inactive");

            GUILayout.Space(8f);
            GUILayout.Label("Attack Availability");
            if (snapshot.HasAuthoritativeAim)
            {
                DrawAvailability(
                    "Primary",
                    snapshot.PrimaryAttackAvailability);
                DrawAvailability(
                    "Secondary",
                    snapshot.SecondaryAttackAvailability);
            }
            else
            {
                Color previousColor = GUI.color;
                GUI.color = snapshot.CanAttack
                    ? new Color(0.40f, 1f, 0.55f)
                    : new Color(1f, 0.45f, 0.40f);
                GUILayout.Label(snapshot.CanAttack
                    ? "Attack: Ready"
                    : "Attack: Blocked - "
                        + snapshot.AttackBlockReason);
                GUI.color = previousColor;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Authoritative Aim");
            GUILayout.Label(
                $"Versions: live {snapshot.LiveAimVersion}, "
                + $"resolved {snapshot.ResolvedAimVersion}, "
                + (snapshot.IsAimFrozen
                    ? $"frozen {snapshot.FrozenAimVersion}"
                    : "not frozen"));
            GUILayout.Label(
                "Reticle viewport: "
                + FormatVector(snapshot.ReticleViewport));
            GUILayout.Label(
                "Camera origin: "
                + FormatVector(snapshot.CameraRayOrigin));
            GUILayout.Label(
                "Camera direction: "
                + FormatVector(snapshot.CameraRayDirection));
            GUILayout.Label(
                "Intent point: " + FormatVector(snapshot.TargetPoint));
            GUILayout.Label(
                "Shot origin: " + FormatVector(snapshot.ShotOrigin));
            GUILayout.Label(
                "Center direction: "
                + FormatVector(snapshot.CenterDirection));
            GUILayout.Label(
                "Surface point: " + FormatVector(snapshot.SurfacePoint));
            GUILayout.Label($"Distance: {snapshot.AimDistance:0.###} m");

            GUILayout.Space(8f);
            GUILayout.Label("Pellet Cone");
            GUILayout.Label(
                $"{snapshot.PelletCount} pellets, "
                + $"{snapshot.PelletConeHalfAngleDegrees:0.###} deg "
                + $"(tangent {snapshot.PrimarySpreadTangent:0.#####})");
            GUILayout.Label(
                $"Radius at aim distance: "
                + $"{snapshot.PelletConeRadiusAtAimDistance:0.###} m");

            GUILayout.Space(8f);
            GUILayout.Label("Target and Cover");
            GUILayout.Label(
                string.IsNullOrEmpty(snapshot.TargetLabel)
                    ? $"Target: {snapshot.TargetType}"
                    : $"Target: {snapshot.TargetType} "
                        + $"({snapshot.TargetLabel})");
            GUILayout.Label($"Query kind: {snapshot.TargetKind}");
            GUILayout.Label($"Hit part: {snapshot.HitPart}");
            GUILayout.Label(snapshot.TargetId.IsValid
                ? "RuntimeId: " + snapshot.TargetId
                : "RuntimeId: None");
            GUILayout.Label(snapshot.GeometryId.IsValid
                ? "GeometryId: " + snapshot.GeometryId
                : "GeometryId: None");
            GUILayout.Label(string.IsNullOrEmpty(snapshot.CurrentCoverId)
                ? "Current CoverId: None"
                : "Current CoverId: " + snapshot.CurrentCoverId);
            GUILayout.Label(string.IsNullOrEmpty(snapshot.TargetCoverId)
                ? "Target CoverId: None"
                : "Target CoverId: " + snapshot.TargetCoverId);
            if (snapshot.IsCurrentCoverBlocked)
            {
                Color previousColor = GUI.color;
                GUI.color = new Color(1f, 0.45f, 0.40f);
                GUILayout.Label("Current cover blocks this shot");
                GUI.color = previousColor;
            }

            GUILayout.Space(8f);
            GUILayout.Label("Last Shot");
            if (!snapshot.HasLastShot)
            {
                GUILayout.Label("None");
            }
            else
            {
                GUILayout.Label($"Tick: {snapshot.LastShotTick}");
                GUILayout.Label(
                    "Origin: " + FormatVector(snapshot.LastShotOrigin));
                GUILayout.Label(
                    "Direction: "
                    + FormatVector(snapshot.LastShotDirection));
                GUILayout.Label(
                    "End: " + FormatVector(snapshot.LastShotEndPoint));
            }

            GUILayout.Space(8f);
            GUILayout.Label("Last Hit");
            if (!snapshot.HasLastHit)
            {
                GUILayout.Label("None");
            }
            else
            {
                GUILayout.Label($"Tick: {snapshot.LastHitTick}");
                GUILayout.Label("Target: " + snapshot.LastHitTarget);
                GUILayout.Label(
                    "Point: " + FormatVector(snapshot.LastHitPoint));
                GUILayout.Label($"Damage: {snapshot.LastHitDamage:0.###}");
            }
        }

        private static void DrawAvailability(
            string label,
            in FpgAttackAvailability availability)
        {
            Color previousColor = GUI.color;
            GUI.color = availability.Ready
                ? new Color(0.40f, 1f, 0.55f)
                : new Color(1f, 0.45f, 0.40f);
            GUILayout.Label(availability.Ready
                ? $"{label}: Ready "
                    + $"({availability.Ammo}/{availability.RequiredAmmo})"
                : $"{label}: {availability.Reason} "
                    + $"({availability.Ammo}/{availability.RequiredAmmo})");
            GUI.color = previousColor;
        }

        private void DrawTuningControls()
        {
            if (!TryResolvePreviewHost(out string error))
            {
                GUILayout.Space(8f);
                DrawError(error);
                return;
            }

            if (!hasWorkingTuning
                && !TryRefreshWorkingTuning(out error))
            {
                GUILayout.Space(8f);
                DrawError(error);
                return;
            }

            GUILayout.Space(10f);
            GUILayout.Label("\u4e34\u65f6\u624b\u611f\u53c2\u6570");
            string primaryTimingMode = workingTuning.PrimaryAttackTimingMode
                    == FPG.Demo.Skills.FpgAttackTimingMode
                        .CharacterAttackSpeed
                ? "角色攻击速度"
                : "固定冷却";
            GUILayout.Label(
                $"主射时序：{primaryTimingMode}");
            GUILayout.Label(
                $"有效攻速：{workingTuning.PrimaryEffectiveAttackSpeed:0.###} 次/秒，"
                + $"间隔 {workingTuning.PrimaryIntervalTicks} Tick");
            GUILayout.Label(
                $"前摇 {workingTuning.PrimaryWindupTicks} Tick，"
                + $"后摇 {workingTuning.PrimaryRecoveryTicks} Tick，"
                + $"同攻击可再次施放 {workingTuning.PrimarySameAttackReadyTick} Tick");
            GUILayout.Label(
                "不同攻击可打断点：第 "
                + workingTuning.PrimaryDifferentAttackInterruptTick
                + " Tick");
            GUILayout.Space(5f);
            float mouseSensitivity = DrawSlider(
                "\u9f20\u6807\u7075\u654f\u5ea6",
                workingTuning.MouseReticleSensitivity,
                0.05f,
                5f);
            float gamepadSpeed = DrawSlider(
                "\u624b\u67c4\u6700\u5927\u901f\u5ea6",
                workingTuning.GamepadReticleSpeed,
                0.05f,
                2f);
            float gamepadDeadzone = DrawSlider(
                "\u624b\u67c4\u6b7b\u533a",
                workingTuning.GamepadReticleDeadzone,
                0f,
                0.6f);
            float gamepadResponse = DrawSlider(
                "\u624b\u67c4\u54cd\u5e94\u66f2\u7ebf",
                workingTuning.GamepadReticleResponseExponent,
                0.3f,
                4f);
            float maximumAimDistance = DrawSlider(
                "\u653b\u51fb\u8ddd\u79bb",
                workingTuning.MaximumAimDistance,
                5f,
                100f);
            float spreadDegrees = DrawSlider(
                "\u4e3b\u5c04\u6563\u5e03\u534a\u89d2",
                workingTuning.PrimarySpreadHalfAngleDegrees,
                0f,
                12f);
            float secondaryRadius = DrawSlider(
                "\u526f\u5c04\u8303\u56f4",
                workingTuning.SecondaryAreaRadius,
                0.5f,
                10f);
            int minimumMagazine = Mathf.Max(
                workingTuning.PrimaryAmmoCost,
                workingTuning.SecondaryAmmoCost);
            int magazineCapacity = Mathf.RoundToInt(DrawSlider(
                "\u5f39\u5323\u5bb9\u91cf",
                workingTuning.MagazineCapacity,
                minimumMagazine,
                64f));
            int inputBufferTicks = Mathf.RoundToInt(DrawSlider(
                "\u8f93\u5165\u7f13\u51b2 Tick",
                workingTuning.InputBufferTicks,
                1f,
                32f));
            float peekSeconds = DrawSlider(
                "\u63a2\u8eab\u65f6\u957f",
                workingTuning.PeekTransitionSeconds,
                0f,
                0.5f);
            float facingFlipDelaySeconds = DrawSlider(
                "\u8f6c\u5411\u5ef6\u8fdf",
                workingTuning.FacingFlipDelaySeconds,
                0f,
                0.5f);
            float facingFlipDurationSeconds = DrawSlider(
                "\u8f6c\u5411\u65f6\u957f",
                workingTuning.FacingFlipDurationSeconds,
                0f,
                0.5f);
            float retractSeconds = DrawSlider(
                "\u56de\u63a9\u4f53\u65f6\u957f",
                workingTuning.RetractTransitionSeconds,
                0f,
                0.5f);
            float coverTraversalSeconds = DrawSlider(
                "\u63a9\u4f53\u79fb\u52a8\u65f6\u957f",
                workingTuning.CoverTraversalSeconds,
                0.05f,
                1f);
            float primaryKick = DrawSlider(
                "\u4e3b\u5c04\u955c\u5934\u540e\u5750",
                workingTuning.PrimaryCameraKick,
                0f,
                0.3f);
            float secondaryKick = DrawSlider(
                "\u526f\u5c04\u955c\u5934\u540e\u5750",
                workingTuning.SecondaryCameraKick,
                0f,
                0.3f);
            float recoverySeconds = DrawSlider(
                "\u955c\u5934\u6062\u590d\u65f6\u957f",
                workingTuning.CameraKickRecoverySeconds,
                0.02f,
                0.5f);

            FpgShootingTuningSnapshot next = workingTuning
                .WithInputAndMovement(
                    mouseSensitivity,
                    workingTuning.MouseReferenceResolution,
                    gamepadSpeed,
                    gamepadDeadzone,
                    gamepadResponse,
                    inputBufferTicks,
                    peekSeconds,
                    facingFlipDelaySeconds,
                    facingFlipDurationSeconds,
                    retractSeconds,
                    coverTraversalSeconds)
                .WithBallistics(
                    maximumAimDistance,
                    Mathf.Tan(spreadDegrees * Mathf.Deg2Rad),
                    secondaryRadius)
                .WithMagazineCapacity(magazineCapacity)
                .WithCameraFeedback(
                    primaryKick,
                    secondaryKick,
                    recoverySeconds);
            if (!AreEquivalent(workingTuning, next))
            {
                if (next.TryValidate(out error)
                    && cachedPreviewHost.TryApplyShootingLivePreview(
                        next,
                        out error))
                {
                    workingTuning = next;
                    latestError = string.Empty;
                }
                else
                {
                    latestError = error;
                }
            }

            GUILayout.Space(5f);
            if (GUILayout.Button("\u5e94\u7528\u9884\u89c8\u5e76\u91cd\u5efa\u6218\u6597"))
            {
                if (!cachedPreviewHost.TryApplyShootingPreviewAndRebuild(
                        workingTuning,
                        out error))
                {
                    latestError = error;
                }
                else
                {
                    latestError = string.Empty;
                    lastSampleFrame = -1;
                }
            }

            if (GUILayout.Button("\u4ece\u5f53\u524d\u8fd0\u884c\u65f6\u91cd\u8f7d"))
            {
                TryRefreshWorkingTuning(out latestError);
            }

            if (!string.IsNullOrWhiteSpace(latestError))
            {
                DrawError(latestError);
            }
        }

        private bool TryRefreshWorkingTuning(out string error)
        {
            if (!TryResolvePreviewHost(out error)
                || !cachedPreviewHost.TryGetShootingTuning(
                    out workingTuning,
                    out error))
            {
                hasWorkingTuning = false;
                return false;
            }

            hasWorkingTuning = true;
            return true;
        }

        private bool TryResolvePreviewHost(out string error)
        {
            if (!TryResolveProvider(out error))
            {
                return false;
            }

            cachedPreviewHost = cachedProvider
                as IFpgShootingTuningPreviewHost;
            if (cachedPreviewHost == null)
            {
                error = "\u8bca\u65ad\u6e90\u4e0d\u652f\u6301\u5c04\u51fb\u8c03\u53c2\u9884\u89c8\u3002";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static float DrawSlider(
            string label,
            float value,
            float minimum,
            float maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150f));
            float result = GUILayout.HorizontalSlider(
                value,
                minimum,
                maximum);
            GUILayout.Label(result.ToString("0.###"), GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            return result;
        }

        private static bool AreEquivalent(
            in FpgShootingTuningSnapshot left,
            in FpgShootingTuningSnapshot right)
        {
            return Mathf.Approximately(
                    left.MouseReticleSensitivity,
                    right.MouseReticleSensitivity)
                && left.MouseReferenceResolution
                    == right.MouseReferenceResolution
                && Mathf.Approximately(
                    left.GamepadReticleSpeed,
                    right.GamepadReticleSpeed)
                && Mathf.Approximately(
                    left.GamepadReticleDeadzone,
                    right.GamepadReticleDeadzone)
                && Mathf.Approximately(
                    left.GamepadReticleResponseExponent,
                    right.GamepadReticleResponseExponent)
                && Mathf.Approximately(
                    left.MaximumAimDistance,
                    right.MaximumAimDistance)
                && Mathf.Approximately(
                    left.PrimarySpreadTangent,
                    right.PrimarySpreadTangent)
                && Mathf.Approximately(
                    left.SecondaryAreaRadius,
                    right.SecondaryAreaRadius)
                && left.MagazineCapacity == right.MagazineCapacity
                && left.InputBufferTicks == right.InputBufferTicks
                && Mathf.Approximately(
                    left.PeekTransitionSeconds,
                    right.PeekTransitionSeconds)
                && Mathf.Approximately(
                    left.FacingFlipDelaySeconds,
                    right.FacingFlipDelaySeconds)
                && Mathf.Approximately(
                    left.FacingFlipDurationSeconds,
                    right.FacingFlipDurationSeconds)
                && Mathf.Approximately(
                    left.RetractTransitionSeconds,
                    right.RetractTransitionSeconds)
                && Mathf.Approximately(
                    left.CoverTraversalSeconds,
                    right.CoverTraversalSeconds)
                && Mathf.Approximately(
                    left.PrimaryCameraKick,
                    right.PrimaryCameraKick)
                && Mathf.Approximately(
                    left.SecondaryCameraKick,
                    right.SecondaryCameraKick)
                && Mathf.Approximately(
                    left.CameraKickRecoverySeconds,
                    right.CameraKickRecoverySeconds);
        }

        private static void DrawError(string error)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 0.45f, 0.40f);
            GUILayout.Label(error);
            GUI.color = previousColor;
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string FormatVector(Vector2 value)
        {
            return $"({value.x:0.###}, {value.y:0.###})";
        }
#endif

        private void SampleOncePerFrame()
        {
            if (lastSampleFrame == Time.frameCount)
            {
                return;
            }

            lastSampleFrame = Time.frameCount;
            if (!TryResolveProvider(out string error))
            {
                hasLatestSnapshot = false;
                latestError = error;
                return;
            }

            if (!cachedProvider.TryGetShootingDiagnostics(
                    out FpgShootingDiagnosticsSnapshot snapshot,
                    out error))
            {
                hasLatestSnapshot = false;
                latestError = string.IsNullOrWhiteSpace(error)
                    ? "The shooting diagnostics provider returned no snapshot."
                    : error;
                return;
            }

            if (!snapshot.TryValidate(out error))
            {
                hasLatestSnapshot = false;
                latestError =
                    "The shooting diagnostics provider returned an invalid snapshot: "
                    + error;
                return;
            }

            latestSnapshot = snapshot;
            hasLatestSnapshot = true;
            latestError = string.Empty;
        }

        private bool TryResolveProvider(out string error)
        {
            if (diagnosticsProvider == null)
            {
                cachedProviderComponent = null;
                cachedProvider = null;
                cachedPreviewHost = null;
                error =
                    "No shooting diagnostics provider is assigned to the panel.";
                return false;
            }

            if (ReferenceEquals(
                    cachedProviderComponent,
                    diagnosticsProvider)
                && cachedProvider != null)
            {
                error = string.Empty;
                return true;
            }

            if (!(diagnosticsProvider is IFpgShootingDiagnosticsProvider provider))
            {
                cachedProviderComponent = null;
                cachedProvider = null;
                cachedPreviewHost = null;
                error =
                    $"Assigned component '{diagnosticsProvider.GetType().Name}' does not implement {nameof(IFpgShootingDiagnosticsProvider)}.";
                return false;
            }

            cachedProviderComponent = diagnosticsProvider;
            cachedProvider = provider;
            cachedPreviewHost = provider
                as IFpgShootingTuningPreviewHost;
            error = string.Empty;
            return true;
        }

        private void ResetSample()
        {
            latestSnapshot = default(FpgShootingDiagnosticsSnapshot);
            workingTuning = default(FpgShootingTuningSnapshot);
            latestError = string.Empty;
            hasLatestSnapshot = false;
            hasWorkingTuning = false;
            lastSampleFrame = -1;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
