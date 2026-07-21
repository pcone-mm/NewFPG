using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "FpgRoomGroupDefinition",
        menuName = "FPG Demo/Level/Room Group Definition")]
    public sealed class FpgRoomGroupDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("分组 ID", "分组的稳定唯一标识。房间只保存对本资产的直接引用，分组资产不反向保存房间列表。")]
        [SerializeField]
        private string groupId;

        [D0PlannerField("显示名称", "供房间编辑器筛选和策划识别的中文或英文名称。")]
        [SerializeField]
        private string displayName;

        [TextArea]
        [D0PlannerField("策划说明", "记录该主分组的用途和内容边界；运行时不读取此文本。")]
        [SerializeField]
        private string designerNotes;

        public string GroupId => groupId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                error = "Room group requires a stable ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = $"Room group '{groupId}' requires a display name.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
