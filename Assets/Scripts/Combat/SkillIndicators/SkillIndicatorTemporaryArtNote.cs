using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorTemporaryArtNote : MonoBehaviour
    {
        [TextArea(2, 4)] public string note;
    }
}
