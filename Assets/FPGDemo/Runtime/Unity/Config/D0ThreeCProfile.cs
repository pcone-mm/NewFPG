using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Planner-owned 2.5D player-control contract for a D0 combat scenario.
    /// The profile deliberately describes a fixed player composition: free aim
    /// moves only the virtual reticle and never the player root.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0ThreeCProfile",
        menuName = "FPG Demo/Config/D0 3C Profile")]
    public sealed class D0ThreeCProfile : ScriptableObject
    {
        [D0PlannerSection("3C 配置标识与镜头构图")]
        [D0PlannerField("3C 配置 ID", "用于场景关联、校验和日志定位的稳定标识，不是战斗数值。创建后保持非空且稳定。")]
        [SerializeField]
        private string profileId = "fei-combatlab-2p5d";

        [D0PlannerField("显示名称", "供策划和验证日志识别的 3C 配置名称，不参与战斗计算。")]
        [SerializeField]
        private string displayName = "Fei CombatLab 2.5D";

        [TextArea]
        [D0PlannerField("策划说明", "记录镜头、准星、护盾显示和射击反馈的调参意图；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("相机构图验收")]
        [D0PlannerField("固定玩家构图锚点", "标准战斗中 Fei 固定在画面中下方的归一化视口坐标（x、y 均为 0～1）。它是场景构图和试玩验收锚点，不会移动出生点、玩家模型或命中位置。")]
        [SerializeField]
        private Vector2 fixedPlayerViewportAnchor = new Vector2(0.5f, 0.22f);

        [D0PlannerField("镜头关注构图点", "标准战斗中 Burstbug 活动区的归一化视口关注点（x、y 均为 0～1）。它用于 CombatLab 构图验收；实际镜头摆位由舞台和相机安装共同确定。")]
        [SerializeField]
        private Vector2 cameraFocusViewport = new Vector2(0.5f, 0.56f);

        [D0PlannerSection("相机安装参数")]
        [D0PlannerField("相机枢轴位置（相对玩家）", "CameraPivot \u76f8\u5bf9 PlayerAnchor \u7684\u5c40\u90e8\u4f4d\u7f6e\u3002Play Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\u5230\u5f53\u524d\u8fd0\u884c\u573a\u666f\uff0c\u4e0b\u6b21\u542f\u52a8\u4e5f\u4f1a\u4ece 3C \u8d44\u4ea7\u8bfb\u53d6\uff1b\u4fee\u6539\u540e\u8bf7\u9a8c\u8bc1\u5c04\u51fb\u3002")]
        [SerializeField]
        private Vector3 cameraPivotLocalPosition = new Vector3(0f, 2.1f, -9f);

        [D0PlannerField("相机枢轴旋转（度）", "CameraPivot \u76f8\u5bf9 PlayerAnchor \u7684\u5c40\u90e8\u6b27\u62c9\u89d2\u65cb\u8f6c\u3002Play Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\u5230\u5f53\u524d\u8fd0\u884c\u573a\u666f\uff0c\u4e0b\u6b21\u542f\u52a8\u4e5f\u4f1a\u4ece 3C \u8d44\u4ea7\u8bfb\u53d6\uff1b\u4fee\u6539\u540e\u8bf7\u9a8c\u8bc1\u5c04\u51fb\u3002")]
        [SerializeField]
        private Vector3 cameraPivotLocalEulerAngles = new Vector3(-1.85f, 0f, 0f);

        [D0PlannerField("主相机相对枢轴位置", "Main Camera \u6302\u5230 CameraPivot \u540e\u7684\u5c40\u90e8\u4f4d\u7f6e\u3002Play Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\u5230\u5f53\u524d\u8fd0\u884c\u573a\u666f\uff0c\u4e0b\u6b21\u542f\u52a8\u4e5f\u4f1a\u4ece 3C \u8d44\u4ea7\u8bfb\u53d6\uff1b\u901a\u5e38\u4fdd\u6301\u96f6\u503c\u3002")]
        [SerializeField]
        private Vector3 cameraLocalPosition = Vector3.zero;

        [D0PlannerField("主相机相对枢轴旋转（度）", "Main Camera \u6302\u5230 CameraPivot \u540e\u7684\u5c40\u90e8\u6b27\u62c9\u89d2\u65cb\u8f6c\u3002Play Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\u5230\u5f53\u524d\u8fd0\u884c\u573a\u666f\uff0c\u4e0b\u6b21\u542f\u52a8\u4e5f\u4f1a\u4ece 3C \u8d44\u4ea7\u8bfb\u53d6\uff1b\u901a\u5e38\u4fdd\u6301\u96f6\u503c\u3002")]
        [SerializeField]
        private Vector3 cameraLocalEulerAngles = Vector3.zero;

        [D0PlannerField("相机视野角（度）", "Play Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\u5230\u4e3b\u76f8\u673a\uff0c\u4e0b\u6b21\u542f\u52a8\u4e5f\u4f1a\u4ece 3C \u8d44\u4ea7\u8bfb\u53d6\u3002\u5b83\u4f1a\u6539\u53d8\u5c4f\u5e55\u51c6\u661f\u6362\u7b97\u51fa\u7684\u7784\u51c6\u5c04\u7ebf\uff0c\u5fc5\u987b\u8fde\u540c\u5c04\u51fb\u9a8c\u8bc1\u4e00\u8d77\u68c0\u67e5\u3002")]
        [SerializeField, Range(1f, 179f)]
        private float cameraFieldOfView = 34f;

        [D0PlannerField("相机近裁剪距离（世界单位）", "\u76f8\u673a\u5f00\u59cb\u6e32\u67d3\u7684\u6700\u8fd1\u8ddd\u79bb\u3002Play Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\uff1b\u53ea\u5f71\u54cd\u6e32\u67d3\u88c1\u526a\uff0c\u4e0d\u6539\u53d8\u653b\u51fb\u67e5\u8be2\u6700\u8fdc\u8ddd\u79bb\u3001\u547d\u4e2d\u6216\u4f24\u5bb3\u3002")]
        [SerializeField, Min(0.01f)]
        private float cameraNearClipPlane = 0.1f;

        [D0PlannerField("相机远裁剪距离（世界单位）", "\u76f8\u673a\u505c\u6b62\u6e32\u67d3\u7684\u6700\u8fdc\u8ddd\u79bb\u3002\u5fc5\u987b\u5927\u4e8e\u8fd1\u88c1\u526a\u8ddd\u79bb\uff1bPlay Mode \u4e2d\u4fee\u6539\u4f1a\u81ea\u52a8\u5e94\u7528\u3002\u53ea\u5f71\u54cd\u6e32\u67d3\u88c1\u526a\uff0c\u4e0d\u6539\u53d8\u653b\u51fb\u67e5\u8be2\u6700\u8fdc\u8ddd\u79bb\u3001\u547d\u4e2d\u6216\u4f24\u5bb3\u3002")]
        [SerializeField, Min(0.02f)]
        private float cameraFarClipPlane = 80f;

        [D0PlannerSection("自由准星与攻击查询")]
        [D0PlannerField("准星活动安全区域（视口）", "虚拟准星可活动的归一化视口矩形；x、y、宽、高都以 0～1 表示。运行时会把准星限制在此区域内。")]
        [SerializeField]
        private Rect reticleSafeViewport = new Rect(0.08f, 0.12f, 0.84f, 0.76f);

        [D0PlannerField("准星移动灵敏度", "鼠标或指针驱动虚拟准星时使用的倍率。数值越大，同样输入带来的准星位移越大。")]
        [SerializeField, Min(0.01f)]
        private float reticleSensitivity = 1f;

        [D0PlannerField("攻击查询最远距离（世界单位）", "\u4e3b\u5c04\u548c\u526f\u5c04\u5171\u7528\u7684\u7a7a\u95f4\u67e5\u8be2\u6700\u8fdc\u8ddd\u79bb\u3002D0 3C \u662f\u552f\u4e00\u751f\u6548\u6765\u6e90\uff1b\u4fee\u6539\u540e\u70b9\u51fb\u201c\u91cd\u542f\u6218\u6597\u5e76\u5e94\u7528\u5168\u90e8\u201d\uff08\u6216\u6309 F5\uff09\u91cd\u5efa\u67e5\u8be2\u4f1a\u8bdd\u3002\u5b83\u4e0d\u662f\u76f8\u673a\u88c1\u526a\u8ddd\u79bb\u3002")]
        [SerializeField, Min(0.01f)]
        private float maximumAimDistance = 50f;

        [D0PlannerField("输入缓冲时长（Tick）", "\u64cd\u4f5c\u8bf7\u6c42\u53ef\u5728\u5171\u4eab\u6b66\u5668\u53d8\u4e3a\u53ef\u6267\u884c\u524d\u4fdd\u7559\u7684\u6700\u957f\u65f6\u957f\u3002\u5f53\u524d\u8303\u56f4\u4e3a 1\uFF5E32 Tick\uff1b\u4fee\u6539\u540e\u70b9\u51fb\u201c\u91cd\u542f\u6218\u6597\u5e76\u5e94\u7528\u5168\u90e8\u201d\uff08\u6216\u6309 F5\uff09\u91cd\u5efa\u8f93\u5165\u6e90\u3002")]
        [SerializeField, Range(1, 32)]
        private int inputBufferTicks = 4;

        [D0PlannerSection("探身／缩回表现衔接")]
        [D0PlannerField("探身过渡时长（秒）", "探身时护盾从可见到隐藏的最短表现过渡时长。D0 护盾表现会在此值与“护盾隐藏淡出时长”中取较慢者；不改变暴露度、承伤通道或完美回撤结算。")]
        [SerializeField, Min(0f)]
        private float peekTransitionSeconds = 0.08f;

        [D0PlannerField("收回护盾过渡时长（秒）", "缩回时护盾从隐藏到可见的最短表现过渡时长。D0 护盾表现会在此值与“护盾显示淡入时长”中取较慢者；不改变暴露度、承伤通道或完美回撤结算。")]
        [SerializeField, Min(0f)]
        private float retractTransitionSeconds = 0.10f;

        [D0PlannerSection("护盾显示")]
        [D0PlannerField("护盾显示淡入时长（秒）", "护盾视觉从透明到目标不透明度的时长。仅影响显示，不改变护盾数值、承伤通道或完美回撤结算。")]
        [SerializeField, Min(0.01f)]
        private float barrierFadeInSeconds = 0.18f;

        [D0PlannerField("护盾隐藏淡出时长（秒）", "护盾视觉从当前不透明度淡出到透明的时长。仅影响显示，不改变护盾数值、承伤通道或完美回撤结算。")]
        [SerializeField, Min(0.01f)]
        private float barrierFadeOutSeconds = 0.12f;

        [D0PlannerField("护盾最大不透明度", "护盾显示可达到的最高不透明度，范围为 0～1；最终 Alpha 还会乘以“护盾显示颜色”的 Alpha。")]
        [SerializeField, Range(0f, 1f)]
        private float barrierMaximumOpacity = 0.72f;

        [D0PlannerField("护盾显示颜色", "护盾视觉的颜色与基础 Alpha。运行时仅在战斗进行中、玩家处于护盾收回状态且护盾值大于 0 时显示护盾。")]
        [SerializeField]
        private Color barrierColor = new Color(0.34f, 0.88f, 1f, 1f);

        [D0PlannerSection("射击镜头后移反馈")]
        [D0PlannerField("主射镜头后移距离（相机局部单位）", "主射成功释放后，镜头沿自身局部 -Z 方向后移的距离。它不是旋转角度或 FOV 改变量。")]
        [SerializeField, Min(0f)]
        private float primaryShotCameraKick = 0.035f;

        [D0PlannerField("副射镜头后移距离（相机局部单位）", "副射成功释放后，镜头沿自身局部 -Z 方向后移的距离。副射是独立攻击，取消蓄力不会触发此反馈。")]
        [SerializeField, Min(0f)]
        private float secondaryShotCameraKick = 0.09f;

        [D0PlannerField("镜头后移恢复基准时长（秒）", "镜头按主射和副射中较大的后移距离计算回零速度；较小的后移量会更快恢复，因此不保证每次都恰好在此时长内回零。")]
        [SerializeField, Min(0.01f)]
        private float shotCameraKickRecoverySeconds = 0.11f;

        public string ProfileId => profileId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public Vector2 FixedPlayerViewportAnchor => fixedPlayerViewportAnchor;
        public Vector2 CameraFocusViewport => cameraFocusViewport;
        public Vector3 CameraPivotLocalPosition => cameraPivotLocalPosition;
        public Vector3 CameraPivotLocalEulerAngles => cameraPivotLocalEulerAngles;
        public Vector3 CameraLocalPosition => cameraLocalPosition;
        public Vector3 CameraLocalEulerAngles => cameraLocalEulerAngles;
        public float CameraFieldOfView => cameraFieldOfView;
        public float CameraNearClipPlane => cameraNearClipPlane;
        public float CameraFarClipPlane => cameraFarClipPlane;
        public Rect ReticleSafeViewport => reticleSafeViewport;
        public float ReticleSensitivity => reticleSensitivity;
        public float MaximumAimDistance => maximumAimDistance;
        public int InputBufferTicks => inputBufferTicks;
        public float PeekTransitionSeconds => peekTransitionSeconds;
        public float RetractTransitionSeconds => retractTransitionSeconds;
        public float BarrierFadeInSeconds => barrierFadeInSeconds;
        public float BarrierFadeOutSeconds => barrierFadeOutSeconds;
        public float BarrierMaximumOpacity => barrierMaximumOpacity;
        public Color BarrierColor => barrierColor;
        public float PrimaryShotCameraKick => primaryShotCameraKick;
        public float SecondaryShotCameraKick => secondaryShotCameraKick;
        public float ShotCameraKickRecoverySeconds => shotCameraKickRecoverySeconds;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "D0 3C profile requires stable ID and display name values.";
                return false;
            }

            if (!IsViewportPoint(fixedPlayerViewportAnchor)
                || !IsViewportPoint(cameraFocusViewport)
                || !IsValidViewportRect(reticleSafeViewport))
            {
                error = "D0 3C profile requires finite normalized composition and reticle-safe viewport values.";
                return false;
            }

            if (!IsFinite(cameraPivotLocalPosition)
                || !IsFinite(cameraPivotLocalEulerAngles)
                || !IsFinite(cameraLocalPosition)
                || !IsFinite(cameraLocalEulerAngles))
            {
                error = "D0 3C profile requires finite camera transform values.";
                return false;
            }

            if (!IsFinitePositive(cameraFieldOfView)
                || cameraFieldOfView <= 1f
                || cameraFieldOfView >= 179f
                || !IsFinitePositive(cameraNearClipPlane)
                || !IsFinitePositive(cameraFarClipPlane)
                || cameraFarClipPlane <= cameraNearClipPlane
                || !IsFinitePositive(reticleSensitivity)
                || !IsFinitePositive(maximumAimDistance)
                || inputBufferTicks < 1
                || inputBufferTicks > 32
                || !IsFiniteNonNegative(peekTransitionSeconds)
                || !IsFiniteNonNegative(retractTransitionSeconds)
                || !IsFinitePositive(barrierFadeInSeconds)
                || !IsFinitePositive(barrierFadeOutSeconds)
                || !IsFiniteNonNegative(barrierMaximumOpacity)
                || barrierMaximumOpacity > 1f
                || !IsFinite(barrierColor)
                || !IsFiniteNonNegative(primaryShotCameraKick)
                || !IsFiniteNonNegative(secondaryShotCameraKick)
                || !IsFinitePositive(shotCameraKickRecoverySeconds))
            {
                error = "D0 3C profile contains invalid camera or tuning values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidViewportRect(Rect value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.width)
                && IsFinite(value.height)
                && value.x >= 0f
                && value.y >= 0f
                && value.width > 0f
                && value.height > 0f
                && value.xMax <= 1f
                && value.yMax <= 1f;
        }

        private static bool IsViewportPoint(Vector2 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && value.x >= 0f
                && value.x <= 1f
                && value.y >= 0f
                && value.y <= 1f;
        }

        private static bool IsFinite(Color value)
        {
            return IsFinite(value.r)
                && IsFinite(value.g)
                && IsFinite(value.b)
                && IsFinite(value.a);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
