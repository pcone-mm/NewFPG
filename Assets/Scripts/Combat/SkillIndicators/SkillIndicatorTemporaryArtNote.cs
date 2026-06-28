using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorTemporaryArtNote : MonoBehaviour
    {
        [InspectorName("说明"), Tooltip("临时美术资源的使用说明。")]
        [TextArea(2, 4)] public string note;
    }
}
