using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Per-action authoring contract for optional Spine-authored position motion.
    /// Runtime state and accumulated offsets deliberately do not belong here.
    /// </summary>
    [Serializable]
    public struct D0AnimationMotionSettings
    {
        [D0PlannerField("启用动画位移", "启用后，从指定 Spine 动画的纯标记骨采样位移，并与程序行为位移、程序技能位移叠加。关闭时本配置始终贡献零位移。")]
        [SerializeField]
        private bool enabled;

        [D0PlannerField("动画名称", "要采样位移的 Spine 动画名称，必须与实际播放的表现动画一致；仅在启用动画位移时必填。")]
        [SerializeField]
        private string animationName;

        [D0PlannerField("位移标记骨", "只承载位移曲线的 Spine 独立顶层标记骨名称。该骨不能有父骨，且它与所有子骨都不能挂 Slot，否则父级变换或网格骨位移会与实体位移重复叠加；仅在启用时必填。")]
        [SerializeField]
        private string motionBoneName;

        [D0PlannerField("结束后保留位移", "启用后，动画正常结束时保留最终偏移，被战斗结束或组件停用中断时保留中断点偏移；关闭后结束或中断都会移除本次美术偏移。两种模式都不会覆盖程序位移。")]
        [SerializeField]
        private bool persistEndOffset;

        public D0AnimationMotionSettings(
            bool enabled,
            string animationName,
            string motionBoneName,
            bool persistEndOffset)
        {
            this.enabled = enabled;
            this.animationName = animationName;
            this.motionBoneName = motionBoneName;
            this.persistEndOffset = persistEndOffset;
        }

        public bool Enabled => enabled;
        public string AnimationName => animationName;
        public string MotionBoneName => motionBoneName;
        public bool PersistEndOffset => persistEndOffset;

        public bool TryValidate(out string error)
        {
            if (!enabled)
            {
                error = string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(animationName))
            {
                error = "Enabled animation motion requires an animation name.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(motionBoneName))
            {
                error = "Enabled animation motion requires a motion bone name.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
