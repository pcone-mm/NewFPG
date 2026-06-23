using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    public struct CastCommandData
    {
        public string AbilityId;
        public Vector3 Origin;
        public Vector3 SceneOrigin;
        public Vector3 Direction;
        public Vector3 TargetPoint;
        public Vector3 SurfaceNormal;
        public int TargetEntityId;
        public SkillIndicatorPlacementMode PlacementMode;
        public float HoldDuration;
        public int LocalCastSequence;
        public SkillIndicatorShapeType ShapeType;
        public float Radius;
        public float Width;
        public float Length;
        public float Angle;
        public float Height;
        public float GroundOffset;
        public bool HasTargetPoint;
        public bool IsValid;

        public static CastCommandData Invalid(string abilityId)
        {
            return new CastCommandData
            {
                AbilityId = abilityId,
                Direction = Vector3.forward,
                SurfaceNormal = Vector3.up,
                IsValid = false,
            };
        }
    }

    public struct IndicatorValidationResult
    {
        public readonly bool IsValid;
        public readonly SkillIndicatorValidationReason Reason;
        public readonly string Message;

        public IndicatorValidationResult(bool isValid, SkillIndicatorValidationReason reason, string message)
        {
            IsValid = isValid;
            Reason = reason;
            Message = message;
        }

        public static IndicatorValidationResult Valid()
        {
            return new IndicatorValidationResult(true, SkillIndicatorValidationReason.None, string.Empty);
        }

        public static IndicatorValidationResult Invalid(SkillIndicatorValidationReason reason, string message)
        {
            return new IndicatorValidationResult(false, reason, message);
        }
    }

    public struct SkillIndicatorPreviewFrame
    {
        public CastCommandData Command;
        public IndicatorValidationResult Validation;
        public SkillIndicatorResolvedConfig Config;

        public bool IsValid => Validation.IsValid && Command.IsValid;
    }
}
