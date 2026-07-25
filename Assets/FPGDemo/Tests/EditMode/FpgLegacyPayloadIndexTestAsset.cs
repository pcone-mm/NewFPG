using System;
using FPG.Demo.Combat;
using FPG.Demo.Skills;
using FPG.Demo.Unity;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    internal sealed class FpgLegacyPayloadIndexTestAsset : ScriptableObject
    {
        [SerializeField]
        private string skillId = "player.legacy-index-test";

        [SerializeField]
        private string displayName = "Legacy Index Test";

        [SerializeField]
        private FpgLegacyPayloadSlot[] payloadSlots =
        {
            new FpgLegacyPayloadSlot("payload.a", "Payload A"),
            new FpgLegacyPayloadSlot("payload.b", "Payload B")
        };

        [SerializeField]
        private FpgLegacyPayloadSequence[] sequences =
        {
            new FpgLegacyPayloadSequence()
        };
    }

    [Serializable]
    internal sealed class FpgLegacyPayloadSlot
    {
        public FpgLegacyPayloadSlot(string id, string name)
        {
            slotId = id;
            displayName = name;
        }

        [SerializeField]
        private string slotId;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private FpgPlayerSkillPayloadKind kind =
            FpgPlayerSkillPayloadKind.PelletRay;

        [SerializeField]
        private int ammoCost = 1;

        [SerializeField]
        private int baseDamage = 4;

        [SerializeField]
        private int breakDamage = 4;

        [SerializeField]
        private int weakpointDamageMultiplierBasisPoints = 12000;

        [SerializeField]
        private int weakpointBreakMultiplierBasisPoints = 25000;

        [SerializeField]
        private AttackQueryMode queryMode =
            AttackQueryMode.FirstSurfacePenetration;

        [SerializeField]
        private int pelletCount = 8;

        [SerializeField]
        private int additionalPenetrationCount;

        [SerializeField]
        private int areaCombatantLimit = 4;

        [SerializeField]
        private int areaProjectileLimit;

        [SerializeField]
        private AttackTargetKinds allowedTargetKinds =
            AttackTargetKinds.Combatant | AttackTargetKinds.Projectile;
    }

    [Serializable]
    internal sealed class FpgLegacyPayloadSequence
    {
        [SerializeField]
        private FpgSkillSequenceKind kind = FpgSkillSequenceKind.Execute;

        [SerializeField]
        private int durationTicks = 10;

        [SerializeField]
        private string mainAnimation = "idle";

        [SerializeField]
        private FpgLegacyPayloadEvent[] logicEvents =
        {
            new FpgLegacyPayloadEvent()
        };
    }

    [Serializable]
    internal sealed class FpgLegacyPayloadEvent
    {
        [SerializeField]
        private string eventId = "event.legacy-index";

        [SerializeField]
        private int tick;

        [SerializeField]
        private string payloadSlotId = "payload.b";

        [SerializeField]
        private int payloadIndex;

        [SerializeField]
        private int authoredOrdinal;

        [SerializeField]
        private string socketId = string.Empty;

        [SerializeField]
        private FpgSkillTargetSource targetSource =
            FpgSkillTargetSource.CurrentAim;

        [SerializeField]
        private Vector3 targetOffset;
    }
}
