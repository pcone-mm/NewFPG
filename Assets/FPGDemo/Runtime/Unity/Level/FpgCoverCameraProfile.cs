using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "FpgCoverCameraProfile",
        menuName = "FPG Demo/Level/Cover Camera Profile")]
    public sealed class FpgCoverCameraProfile : ScriptableObject
    {
        [D0PlannerSection("镜头配置说明")]
        [TextArea]
        [D0PlannerField("策划说明", "记录当前掩体镜头的构图意图和验收注意事项；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("镜头安装参数")]
        [D0PlannerField("镜头 Rig 位置（相对玩家）", "镜头 Rig 相对玩家到达 Pose 的局部位置。")]
        [SerializeField]
        private Vector3 cameraRigLocalPosition =
            new Vector3(0f, 5.74f, -9.96f);

        [D0PlannerField("镜头 Rig 旋转（度）", "镜头 Rig 相对玩家到达 Pose 的局部欧拉角。")]
        [SerializeField]
        private Vector3 cameraRigLocalEulerAngles =
            new Vector3(0.86f, 0f, 0f);

        [D0PlannerField("主相机局部位置", "主相机挂到镜头 Rig 后的局部位置。")]
        [SerializeField]
        private Vector3 cameraLocalPosition = Vector3.zero;

        [D0PlannerField("主相机局部旋转（度）", "主相机挂到镜头 Rig 后的局部欧拉角。")]
        [SerializeField]
        private Vector3 cameraLocalEulerAngles = Vector3.zero;

        [D0PlannerField("视野角（度）", "当前掩体镜头使用的透视相机视野角。")]
        [SerializeField, Range(1f, 179f)]
        private float fieldOfView = 65f;

        [D0PlannerField("近裁剪距离", "相机开始渲染的最近距离，必须大于零。")]
        [SerializeField, Min(0.01f)]
        private float nearClipPlane = 0.1f;

        [D0PlannerField("远裁剪距离", "相机停止渲染的最远距离，必须大于近裁剪距离。")]
        [SerializeField, Min(0.02f)]
        private float farClipPlane = 80f;

        [D0PlannerSection("构图预览参考")]
        [D0PlannerField("玩家视口锚点", "用于编辑器构图预览的玩家归一化视口参考点。")]
        [SerializeField]
        private Vector2 playerViewportAnchor = new Vector2(0.5f, 0.22f);

        [D0PlannerField("关注点视口锚点", "用于编辑器构图预览的关注点归一化视口参考点。")]
        [SerializeField]
        private Vector2 focusViewportAnchor = new Vector2(0.5f, 0.56f);

        public string DesignerNotes => designerNotes;
        public Vector3 CameraRigLocalPosition => cameraRigLocalPosition;
        public Vector3 CameraRigLocalEulerAngles =>
            cameraRigLocalEulerAngles;
        public Vector3 CameraLocalPosition => cameraLocalPosition;
        public Vector3 CameraLocalEulerAngles => cameraLocalEulerAngles;
        public float FieldOfView => fieldOfView;
        public float NearClipPlane => nearClipPlane;
        public float FarClipPlane => farClipPlane;
        public Vector2 PlayerViewportAnchor => playerViewportAnchor;
        public Vector2 FocusViewportAnchor => focusViewportAnchor;

        public bool TryValidate(out string error)
        {
            if (!IsFinite(cameraRigLocalPosition)
                || !IsFinite(cameraRigLocalEulerAngles)
                || !IsFinite(cameraLocalPosition)
                || !IsFinite(cameraLocalEulerAngles))
            {
                error = "Cover camera profile requires finite camera transform values.";
                return false;
            }

            if (!IsFinite(fieldOfView)
                || fieldOfView <= 1f
                || fieldOfView >= 179f)
            {
                error = "Cover camera profile requires a field of view greater than 1 and less than 179 degrees.";
                return false;
            }

            if (!IsFinite(nearClipPlane)
                || !IsFinite(farClipPlane)
                || nearClipPlane <= 0f
                || farClipPlane <= nearClipPlane)
            {
                error = "Cover camera profile requires finite clip planes with 0 < near < far.";
                return false;
            }

            if (!IsViewportPoint(playerViewportAnchor)
                || !IsViewportPoint(focusViewportAnchor))
            {
                error = "Cover camera profile requires finite normalized viewport anchors.";
                return false;
            }

            error = string.Empty;
            return true;
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

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
