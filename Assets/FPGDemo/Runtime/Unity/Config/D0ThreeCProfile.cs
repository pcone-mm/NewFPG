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
        public static readonly Vector3 DefaultCoverLocalPosition =
            new Vector3(0.30f, 1.075f, 0.25f);

        [D0PlannerSection("3C 配置标识")]
        [D0PlannerField("3C 配置 ID", "用于场景关联、校验和日志定位的稳定标识，不是战斗数值。创建后保持非空且稳定。")]
        [SerializeField]
        private string profileId = "fei-combatlab-2p5d";

        [D0PlannerField("显示名称", "供策划和验证日志识别的 3C 配置名称，不参与战斗计算。")]
        [SerializeField]
        private string displayName = "Fei CombatLab 2.5D";

        [TextArea]
        [D0PlannerField("策划说明", "记录准星、护盾显示和射击反馈的调参意图；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

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

        [D0PlannerField("掩体移动时长（秒）", "玩家在相邻掩体到达点之间化为光球移动的默认时长。")]
        [SerializeField, Min(0.01f)]
        private float coverTraversalSeconds = 0.25f;

        [D0PlannerSection("护盾显示")]
        [D0PlannerField("掩体局部位置", "掩体相对玩家实体的位置。X 控制横向，Y 控制高度，Z 控制画面深度；当前正式镜头位于玩家负 Z 方向，增大 Z 会把掩体移到角色画面后方，避免遮挡角色。仅移动表现，不改变命中体或弹道。")]
        [SerializeField]
        private Vector3 coverLocalPosition =
            new Vector3(0.30f, 1.075f, 0.25f);

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
        public Rect ReticleSafeViewport => reticleSafeViewport;
        public float ReticleSensitivity => reticleSensitivity;
        public float MaximumAimDistance => maximumAimDistance;
        public int InputBufferTicks => inputBufferTicks;
        public float PeekTransitionSeconds => peekTransitionSeconds;
        public float RetractTransitionSeconds => retractTransitionSeconds;
        public float CoverTraversalSeconds => coverTraversalSeconds;
        public Vector3 CoverLocalPosition => coverLocalPosition;
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

            if (!IsValidViewportRect(reticleSafeViewport))
            {
                error = "D0 3C profile requires finite normalized reticle-safe viewport values.";
                return false;
            }

            if (!IsFinitePositive(reticleSensitivity)
                || !IsFinitePositive(maximumAimDistance)
                || inputBufferTicks < 1
                || inputBufferTicks > 32
                || !IsFiniteNonNegative(peekTransitionSeconds)
                || !IsFiniteNonNegative(retractTransitionSeconds)
                || !IsFinitePositive(coverTraversalSeconds)
                || !IsFinite(coverLocalPosition)
                || !IsFinitePositive(barrierFadeInSeconds)
                || !IsFinitePositive(barrierFadeOutSeconds)
                || !IsFiniteNonNegative(barrierMaximumOpacity)
                || barrierMaximumOpacity > 1f
                || !IsFinite(barrierColor)
                || !IsFiniteNonNegative(primaryShotCameraKick)
                || !IsFiniteNonNegative(secondaryShotCameraKick)
                || !IsFinitePositive(shotCameraKickRecoverySeconds))
            {
                error = "D0 3C profile contains invalid tuning values.";
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
