using UnityEngine.Timeline;

namespace NewFPG.CZN
{
    [TrackColor(0.55f, 0.2f, 0.78f)]
    [TrackClipType(typeof(CznSpineSkillPlayableAsset))]
    [TrackBindingType(typeof(CznSpineSkillPlayer))]
    public sealed class CznSpineSkillTrack : TrackAsset
    {
    }
}
