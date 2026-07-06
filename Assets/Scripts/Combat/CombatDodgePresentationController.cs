using NewFPG.CameraRig;
using NewFPG.Level;
using NewFPG.Prototype;
using NewFPG.Rendering;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatDodgePresentationController : MonoBehaviour
    {
        private enum DodgeDirection
        {
            Back,
            Left,
            Right,
        }

        private enum DodgePhase
        {
            None,
            Dodge,
            Return,
        }

        [Header("Combat Gate")]
        [SerializeField] private LevelFlowDirector levelFlowDirector;
        [SerializeField] private CinemachineCameraModeController cameraModeController;
        [SerializeField] private bool requireCombatState = true;

        [Header("Config")]
        [SerializeField] private CombatDodgePresentationConfig presentationConfig;

        [Header("Targets")]
        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;
        [SerializeField] private CinemachineCamera battleCamera;
        [SerializeField] private Transform weaponRig;
        [SerializeField] private Camera weaponCamera;

        [Header("Speed Lines")]
        [SerializeField] private Volume speedLineVolume;

        private Transform battleCameraTransform;
        private Vector3 cameraBasePosition;
        private Quaternion cameraBaseRotation;
        private float battleCameraBaseFov;
        private bool hasCameraBase;

        private Vector3 weaponBasePosition;
        private Quaternion weaponBaseRotation;
        private float weaponCameraBaseFov;
        private bool hasWeaponBase;

        private DodgeDirection activeDirection;
        private DodgePhase dodgePhase;
        private float dodgeTime;
        private float returnStartAmount;
        private float nextDodgeAllowedAt;
        private float cooldownStartedAt = -1f;
        private float cooldownDisplayDuration;
        private bool cooldownRunning;
        private bool dodgeActive;

        private DodgeSpeedLinesVolume speedLineSettings;
        private GameObject runtimeSpeedLineVolumeObject;
        private float speedLineRemaining;

        private const float DefaultDodgeDuration = 0.34f;
        private const float DefaultReturnDuration = 0.22f;
        private const float DefaultCooldownDuration = 0.85f;
        private const float DefaultBattleCameraFovKick = 4f;
        private const float DefaultWeaponCameraFovKick = 2f;
        private const bool DefaultCreateRuntimeSpeedLines = true;
        private const float DefaultSpeedLineDuration = 0.16f;
        private const float DefaultSpeedLineOpacity = 0.72f;

        private float DodgeDuration => presentationConfig != null ? presentationConfig.DodgeDuration : DefaultDodgeDuration;
        private float ReturnDuration => presentationConfig != null ? presentationConfig.ReturnDuration : DefaultReturnDuration;
        private float CooldownDuration => presentationConfig != null ? presentationConfig.CooldownDuration : DefaultCooldownDuration;
        private float BattleCameraFovKick => presentationConfig != null ? presentationConfig.BattleCameraFovKick : DefaultBattleCameraFovKick;
        private float WeaponCameraFovKick => presentationConfig != null ? presentationConfig.WeaponCameraFovKick : DefaultWeaponCameraFovKick;
        private bool CreateRuntimeSpeedLines => presentationConfig != null ? presentationConfig.CreateRuntimeSpeedLines : DefaultCreateRuntimeSpeedLines;
        private float SpeedLineDuration => presentationConfig != null ? presentationConfig.SpeedLineDuration : DefaultSpeedLineDuration;
        private float SpeedLineOpacity => presentationConfig != null ? presentationConfig.SpeedLineOpacity : DefaultSpeedLineOpacity;

        private void Reset()
        {
            weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            RestoreDodgePose(false);
            DisableSpeedLines();
        }

        private void OnDestroy()
        {
            if (runtimeSpeedLineVolumeObject == null)
            {
                return;
            }

            Destroy(runtimeSpeedLineVolumeObject);
            runtimeSpeedLineVolumeObject = null;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ResolveReferences();
            UpdateDodgePose();
            UpdateSpeedLines();

            if (!CanReadDodgeInput() || !TryReadDodgeInput(out DodgeDirection direction))
            {
                return;
            }

            PlayDodge(direction);
        }

        public void PlayBackDodge()
        {
            PlayDodge(DodgeDirection.Back);
        }

        public void PlayLeftDodge()
        {
            PlayDodge(DodgeDirection.Left);
        }

        public void PlayRightDodge()
        {
            PlayDodge(DodgeDirection.Right);
        }

        public bool IsDodgeReady => !dodgeActive && Time.time >= nextDodgeAllowedAt && CanShowDodge();

        public float DodgeCooldownProgress
        {
            get
            {
                if (!cooldownRunning || cooldownDisplayDuration <= 0.0001f)
                {
                    return 1f;
                }

                return Mathf.Clamp01((Time.time - cooldownStartedAt) / cooldownDisplayDuration);
            }
        }

        public float DodgeCooldownRemaining => Mathf.Max(0f, nextDodgeAllowedAt - Time.time);

        private void PlayDodge(DodgeDirection direction)
        {
            if (dodgeActive || Time.time < nextDodgeAllowedAt || !CanShowDodge())
            {
                return;
            }

            CaptureDodgeBase();
            activeDirection = direction;
            dodgePhase = DodgePhase.Dodge;
            dodgeTime = 0f;
            returnStartAmount = 0f;
            dodgeActive = true;
            cooldownStartedAt = -1f;
            cooldownDisplayDuration = CooldownDuration;
            cooldownRunning = false;
            speedLineRemaining = SpeedLineDuration;
        }

        private bool CanReadDodgeInput()
        {
            return !dodgeActive && Time.time >= nextDodgeAllowedAt && CanShowDodge();
        }

        private bool CanShowDodge()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!requireCombatState)
            {
                return true;
            }

            if (levelFlowDirector != null)
            {
                return levelFlowDirector.IsInCombat;
            }

            return cameraModeController != null
                && cameraModeController.CurrentMode == GameplayCameraMode.Battle;
        }

        private float EvaluateDodgeCurve(float normalizedTime)
        {
            return presentationConfig != null
                ? presentationConfig.EvaluateDodge(normalizedTime)
                : Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI);
        }

        private float EvaluateReturnCurve(float normalizedTime)
        {
            return presentationConfig != null
                ? presentationConfig.EvaluateReturn(normalizedTime)
                : Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(normalizedTime));
        }

        private bool TryReadDodgeInput(out DodgeDirection direction)
        {
            direction = DodgeDirection.Back;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool aPressed = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
                bool dPressed = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
                bool sPressed = keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame;
                bool spacePressed = keyboard.spaceKey.wasPressedThisFrame;

                bool aHeld = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                bool dHeld = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                bool sHeld = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;

                if (sPressed)
                {
                    direction = DodgeDirection.Back;
                    return true;
                }

                if (aPressed && !dHeld)
                {
                    direction = DodgeDirection.Left;
                    return true;
                }

                if (dPressed && !aHeld)
                {
                    direction = DodgeDirection.Right;
                    return true;
                }

                if (spacePressed)
                {
                    if (aHeld && !dHeld)
                    {
                        direction = DodgeDirection.Left;
                    }
                    else if (dHeld && !aHeld)
                    {
                        direction = DodgeDirection.Right;
                    }
                    else if (sHeld)
                    {
                        direction = DodgeDirection.Back;
                    }

                    return true;
                }

                return false;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                direction = DodgeDirection.Back;
                return true;
            }

            if ((Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                && !Input.GetKey(KeyCode.D)
                && !Input.GetKey(KeyCode.RightArrow))
            {
                direction = DodgeDirection.Left;
                return true;
            }

            if ((Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                && !Input.GetKey(KeyCode.A)
                && !Input.GetKey(KeyCode.LeftArrow))
            {
                direction = DodgeDirection.Right;
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                    && !Input.GetKey(KeyCode.D)
                    && !Input.GetKey(KeyCode.RightArrow))
                {
                    direction = DodgeDirection.Left;
                }
                else if ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                    && !Input.GetKey(KeyCode.A)
                    && !Input.GetKey(KeyCode.LeftArrow))
                {
                    direction = DodgeDirection.Right;
                }

                return true;
            }
#endif

            return false;
        }

        private void CaptureDodgeBase()
        {
            battleCameraTransform = battleCamera != null ? battleCamera.transform : null;
            if (battleCameraTransform != null)
            {
                cameraBasePosition = battleCameraTransform.localPosition;
                cameraBaseRotation = battleCameraTransform.localRotation;
                battleCameraBaseFov = battleCamera.Lens.FieldOfView;
                hasCameraBase = true;
            }
            else
            {
                hasCameraBase = false;
            }

            if (weaponRig != null)
            {
                weaponBasePosition = weaponRig.localPosition;
                weaponBaseRotation = weaponRig.localRotation;
                weaponCameraBaseFov = weaponCamera != null ? weaponCamera.fieldOfView : 0f;
                hasWeaponBase = true;
            }
            else
            {
                hasWeaponBase = false;
            }
        }

        private void UpdateDodgePose()
        {
            if (!dodgeActive)
            {
                return;
            }

            dodgeTime += Time.deltaTime;
            if (dodgePhase == DodgePhase.Dodge)
            {
                float duration = DodgeDuration;
                float t = duration <= 0.0001f ? 1f : Mathf.Clamp01(dodgeTime / duration);
                float amount = EvaluateDodgeCurve(t);
                ApplyDodgePose(amount);

                if (t < 1f)
                {
                    return;
                }

                BeginReturn(amount);
                return;
            }

            if (dodgePhase != DodgePhase.Return)
            {
                RestoreDodgePose(true);
                return;
            }

            float returnDuration = ReturnDuration;
            float returnTime = returnDuration <= 0.0001f ? 1f : Mathf.Clamp01(dodgeTime / returnDuration);
            float returnAmount = returnStartAmount * EvaluateReturnCurve(returnTime);
            ApplyDodgePose(returnAmount);

            if (returnTime >= 1f)
            {
                RestoreDodgePose(true);
            }
        }

        private void BeginReturn(float amount)
        {
            returnStartAmount = amount;
            dodgeTime = 0f;

            if (ReturnDuration <= 0.0001f || Mathf.Abs(returnStartAmount) <= 0.0001f)
            {
                RestoreDodgePose(true);
                return;
            }

            dodgePhase = DodgePhase.Return;
        }

        private void ApplyDodgePose(float amount)
        {
            ResolveDodgeOffsets(
                activeDirection,
                out Vector3 cameraOffset,
                out Vector3 cameraEuler,
                out Vector3 weaponOffset,
                out Vector3 weaponEuler);

            if (hasCameraBase && battleCameraTransform != null)
            {
                battleCameraTransform.localPosition = cameraBasePosition + cameraOffset * amount;
                battleCameraTransform.localRotation = cameraBaseRotation * Quaternion.Euler(cameraEuler * amount);
                SetBattleCameraFov(battleCameraBaseFov + BattleCameraFovKick * amount);
            }

            if (hasWeaponBase && weaponRig != null)
            {
                weaponRig.localPosition = weaponBasePosition + weaponOffset * amount;
                weaponRig.localRotation = weaponBaseRotation * Quaternion.Euler(weaponEuler * amount);

                if (weaponCamera != null)
                {
                    weaponCamera.fieldOfView = weaponCameraBaseFov + WeaponCameraFovKick * amount;
                }
            }
        }

        private void ResolveDodgeOffsets(
            DodgeDirection direction,
            out Vector3 cameraOffset,
            out Vector3 cameraEuler,
            out Vector3 weaponOffset,
            out Vector3 weaponEuler)
        {
            if (direction == DodgeDirection.Back)
            {
                cameraOffset = presentationConfig != null
                    ? presentationConfig.BackCameraOffset
                    : new Vector3(0f, -0.025f, -0.48f);
                cameraEuler = presentationConfig != null
                    ? presentationConfig.BackCameraEuler
                    : new Vector3(-1.5f, 0f, 0f);
                weaponOffset = presentationConfig != null
                    ? presentationConfig.BackWeaponOffset
                    : new Vector3(0f, -0.08f, -0.1f);
                weaponEuler = presentationConfig != null
                    ? presentationConfig.BackWeaponEuler
                    : new Vector3(4f, 0f, 0f);
                return;
            }

            float sign = direction == DodgeDirection.Right ? 1f : -1f;
            Vector3 sideCameraOffset = presentationConfig != null
                ? presentationConfig.SideCameraOffset
                : new Vector3(0.36f, -0.015f, 0.04f);
            Vector3 sideCameraEuler = presentationConfig != null
                ? presentationConfig.SideCameraEuler
                : new Vector3(0.5f, 0f, -2.6f);
            Vector3 sideWeaponLag = presentationConfig != null
                ? presentationConfig.SideWeaponLag
                : new Vector3(0.16f, -0.02f, 0.03f);
            Vector3 sideWeaponEuler = presentationConfig != null
                ? presentationConfig.SideWeaponEuler
                : new Vector3(0f, 0f, 6.5f);
            cameraOffset = new Vector3(sideCameraOffset.x * sign, sideCameraOffset.y, sideCameraOffset.z);
            cameraEuler = new Vector3(sideCameraEuler.x, sideCameraEuler.y, -sideCameraEuler.z * sign);
            weaponOffset = new Vector3(-sideWeaponLag.x * sign, sideWeaponLag.y, sideWeaponLag.z);
            weaponEuler = new Vector3(sideWeaponEuler.x, sideWeaponEuler.y, sideWeaponEuler.z * sign);
        }

        private void RestoreDodgePose(bool startCooldown)
        {
            bool completedActiveDodge = dodgeActive;

            if (hasCameraBase && battleCameraTransform != null)
            {
                battleCameraTransform.localPosition = cameraBasePosition;
                battleCameraTransform.localRotation = cameraBaseRotation;
                SetBattleCameraFov(battleCameraBaseFov);
            }

            if (hasWeaponBase && weaponRig != null)
            {
                weaponRig.localPosition = weaponBasePosition;
                weaponRig.localRotation = weaponBaseRotation;

                if (weaponCamera != null)
                {
                    weaponCamera.fieldOfView = weaponCameraBaseFov;
                }
            }

            dodgeActive = false;
            dodgePhase = DodgePhase.None;
            returnStartAmount = 0f;
            hasCameraBase = false;
            hasWeaponBase = false;

            if (startCooldown && completedActiveDodge)
            {
                StartCooldown();
            }
        }

        private void StartCooldown()
        {
            cooldownDisplayDuration = CooldownDuration;
            cooldownStartedAt = Time.time;

            if (cooldownDisplayDuration <= 0.0001f)
            {
                cooldownRunning = false;
                nextDodgeAllowedAt = Time.time;
                return;
            }

            cooldownRunning = true;
            nextDodgeAllowedAt = cooldownStartedAt + cooldownDisplayDuration;
        }

        private void SetBattleCameraFov(float fov)
        {
            if (battleCamera == null)
            {
                return;
            }

            LensSettings lens = battleCamera.Lens;
            lens.FieldOfView = Mathf.Clamp(fov, 1f, 120f);
            battleCamera.Lens = lens;
        }

        private void UpdateSpeedLines()
        {
            if (speedLineRemaining <= 0f)
            {
                DisableSpeedLines();
                return;
            }

            speedLineRemaining = Mathf.Max(0f, speedLineRemaining - Time.deltaTime);
            float amount = speedLineRemaining / SpeedLineDuration;
            SetSpeedLines(amount, true);
        }

        private void DisableSpeedLines()
        {
            if (speedLineSettings == null)
            {
                return;
            }

            speedLineSettings.effectEnabled.Override(false);
            speedLineSettings.intensity.Override(0f);
            speedLineSettings.opacity.Override(0f);
        }

        private void SetSpeedLines(float amount, bool enabled)
        {
            if (!EnsureSpeedLineSettings())
            {
                return;
            }

            bool active = enabled && amount > 0.001f;
            speedLineSettings.effectEnabled.Override(active);
            speedLineSettings.intensity.Override(Mathf.Clamp01(amount));
            speedLineSettings.opacity.Override(SpeedLineOpacity * Mathf.Clamp01(amount));
        }

        private bool EnsureSpeedLineSettings()
        {
            if (speedLineSettings != null)
            {
                return true;
            }

            if (speedLineVolume != null && TryResolveSpeedLineSettings(speedLineVolume))
            {
                return true;
            }

            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                if (volumes[i] != null && TryResolveSpeedLineSettings(volumes[i]))
                {
                    speedLineVolume = volumes[i];
                    return true;
                }
            }

            if (!CreateRuntimeSpeedLines)
            {
                return false;
            }

            runtimeSpeedLineVolumeObject = new GameObject("Runtime Dodge Speed Lines Volume");
            speedLineVolume = runtimeSpeedLineVolumeObject.AddComponent<Volume>();
            speedLineVolume.isGlobal = true;
            speedLineVolume.priority = 80f;
            speedLineVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            speedLineSettings = speedLineVolume.profile.Add<DodgeSpeedLinesVolume>(true);
            speedLineSettings.effectEnabled.Override(false);
            return true;
        }

        private bool TryResolveSpeedLineSettings(Volume volume)
        {
            if (volume == null)
            {
                return false;
            }

            if (volume.profile == null)
            {
                if (!CreateRuntimeSpeedLines)
                {
                    return false;
                }

                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            if (!volume.profile.TryGet(out speedLineSettings))
            {
                if (!CreateRuntimeSpeedLines)
                {
                    return false;
                }

                speedLineSettings = volume.profile.Add<DodgeSpeedLinesVolume>(true);
            }

            speedLineSettings.effectEnabled.Override(false);
            return true;
        }

        private void ResolveReferences()
        {
            if (weaponView == null)
            {
                weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            }

            if (levelFlowDirector == null)
            {
                levelFlowDirector = FindFirstObjectByType<LevelFlowDirector>(FindObjectsInactive.Include);
            }

            if (cameraModeController == null)
            {
                cameraModeController = FindFirstObjectByType<CinemachineCameraModeController>(FindObjectsInactive.Include);
            }

            if (battleCamera == null && cameraModeController != null)
            {
                battleCamera = cameraModeController.BattleCamera;
            }

            if (weaponRig == null)
            {
                Transform rig = transform.Find("FirstPersonWeaponRig");
                if (rig != null)
                {
                    weaponRig = rig;
                }
            }

            if (weaponCamera == null)
            {
                Transform cameraTransform = transform.Find("FirstPersonWeaponCamera");
                if (cameraTransform != null)
                {
                    weaponCamera = cameraTransform.GetComponent<Camera>();
                }
            }
        }
    }
}
