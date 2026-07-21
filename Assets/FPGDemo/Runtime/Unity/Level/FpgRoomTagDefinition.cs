using UnityEngine;

namespace FPG.Demo.Unity
{
    [CreateAssetMenu(
        fileName = "FpgRoomTagDefinition",
        menuName = "FPG Demo/Level/Room Tag Definition")]
    public sealed class FpgRoomTagDefinition : ScriptableObject
    {
        [D0PlannerSection("基础信息")]
        [D0PlannerField("标签 ID", "标签的稳定唯一标识。标签用于组合筛选，不承担房间主分类职责。")]
        [SerializeField]
        private string tagId;

        [D0PlannerField("显示名称", "供房间编辑器筛选和策划识别的中文或英文名称。")]
        [SerializeField]
        private string displayName;

        [TextArea]
        [D0PlannerField("策划说明", "记录标签的使用约定；运行时不读取此文本。")]
        [SerializeField]
        private string designerNotes;

        public string TagId => tagId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(tagId))
            {
                error = "Room tag requires a stable ID.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = $"Room tag '{tagId}' requires a display name.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
