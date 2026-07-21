using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace NewFPG.CZN
{
    [Serializable]
    public sealed class CznSpineSkillPlayableAsset : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private CznSpineSkillSequence sequence;

        public CznSpineSkillSequence Sequence
        {
            get => sequence;
            set => sequence = value;
        }

        public ClipCaps clipCaps => ClipCaps.None;
        public override double duration => sequence != null ? sequence.Duration : base.duration;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<CznSpineSkillPlayableBehaviour> playable =
                ScriptPlayable<CznSpineSkillPlayableBehaviour>.Create(graph);
            playable.GetBehaviour().Sequence = sequence;
            return playable;
        }
    }

    public sealed class CznSpineSkillPlayableBehaviour : PlayableBehaviour
    {
        public CznSpineSkillSequence Sequence { get; set; }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (info.weight <= 0.0001f || Sequence == null)
            {
                return;
            }

            CznSpineSkillPlayer player = playerData as CznSpineSkillPlayer;
            if (player != null)
            {
                player.Evaluate(Sequence, playable.GetTime());
            }
        }
    }

}
