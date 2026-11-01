using System;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public interface IFpgFormalEnemyAttackSpatialSampler
    {
        DomainResult TrySample(
            TickIndex tick,
            RuntimeId ownerRuntimeId,
            RuntimeId currentTargetRuntimeId,
            string socketName,
            in FpgCompiledSkillEvent skillEvent,
            out FpgEnemyAttackSpatialContext context);
    }

    public static class FpgEnemySkillGameplayEventResolver
    {
        public static bool TryResolveSocketName(
            FpgEnemyAttackDefinition definition,
            in FpgCompiledSkillEvent compiledEvent,
            out string socketName)
        {
            socketName = string.Empty;
            if (definition == null
                || compiledEvent.Kind != FpgSkillEventKind.GameplayAction)
            {
                return false;
            }

            return TryResolveActionSocketName(
                definition,
                compiledEvent,
                out socketName);
        }

        private static bool TryResolveActionSocketName(
            FpgEnemyAttackDefinition definition,
            in FpgCompiledSkillEvent compiledEvent,
            out string socketName)
        {
            socketName = string.Empty;
            int actionOffset = 0;
            for (int sequenceIndex = 0;
                sequenceIndex < definition.Sequences.Count;
                sequenceIndex++)
            {
                FpgSkillSequenceDefinition sequence =
                    definition.Sequences[sequenceIndex];
                if (sequence == null)
                {
                    continue;
                }

                switch (compiledEvent.ActionKind)
                {
                    case FpgSkillActionKind.Attack:
                        if (TryResolveAction(
                                sequence.AttackEvents,
                                actionOffset,
                                compiledEvent,
                                out socketName))
                        {
                            return true;
                        }

                        actionOffset += sequence.AttackEvents.Count;
                        break;

                    case FpgSkillActionKind.LaunchProjectile:
                        if (TryResolveAction(
                                sequence.ProjectileEvents,
                                actionOffset,
                                compiledEvent,
                                out socketName))
                        {
                            return true;
                        }

                        actionOffset += sequence.ProjectileEvents.Count;
                        break;

                    case FpgSkillActionKind.SummonActors:
                        if (TryResolveAction(
                                sequence.SummonEvents,
                                actionOffset,
                                compiledEvent,
                                out socketName))
                        {
                            return true;
                        }

                        actionOffset += sequence.SummonEvents.Count;
                        break;

                    case FpgSkillActionKind.SelfDestructOwner:
                        if (TryResolveAction(
                                sequence.SelfDestructOwnerEvents,
                                actionOffset,
                                compiledEvent,
                                out socketName))
                        {
                            return true;
                        }

                        actionOffset += sequence.SelfDestructOwnerEvents.Count;
                        break;

                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool TryResolveAction<TAction>(
            System.Collections.Generic.IReadOnlyList<TAction> actions,
            int actionOffset,
            in FpgCompiledSkillEvent compiledEvent,
            out string socketName)
            where TAction : FpgSkillGameplayActionDefinition
        {
            int localIndex = compiledEvent.ActionIndex - actionOffset;
            if (localIndex < 0 || localIndex >= actions.Count)
            {
                socketName = string.Empty;
                return false;
            }

            TAction candidate = actions[localIndex];
            if (candidate != null
                && candidate.Tick == compiledEvent.Tick
                && candidate.AuthoredOrdinal == compiledEvent.SortOrder
                && FpgSkillStableId.CompileEvent(candidate.EventId)
                    == compiledEvent.EventId
                && FpgSkillStableId.CompileOptionalSocket(candidate.SocketId)
                    == compiledEvent.SocketId
                && candidate.TargetSource == compiledEvent.TargetSource
                && candidate.OffsetXMillimeters
                    == compiledEvent.Offset.XMillimeters
                && candidate.OffsetYMillimeters
                    == compiledEvent.Offset.YMillimeters
                && candidate.OffsetZMillimeters
                    == compiledEvent.Offset.ZMillimeters)
            {
                socketName = candidate.SocketId;
                return true;
            }

            socketName = string.Empty;
            return false;
        }
    }

    public sealed class FpgCombatantEnemyAttackSpatialSampler :
        IFpgFormalEnemyAttackSpatialSampler
    {
        private const float MinimumDirectionSqrMagnitude = 0.00000001f;

        private readonly FpgCombatantAnchorMap anchorMap;

        public FpgCombatantEnemyAttackSpatialSampler(
            FpgCombatantAnchorMap anchorMap)
        {
            this.anchorMap = anchorMap
                ?? throw new ArgumentNullException(nameof(anchorMap));
        }

        public DomainResult TrySample(
            TickIndex tick,
            RuntimeId ownerRuntimeId,
            RuntimeId currentTargetRuntimeId,
            string socketName,
            in FpgCompiledSkillEvent skillEvent,
            out FpgEnemyAttackSpatialContext context)
        {
            context = default(FpgEnemyAttackSpatialContext);
            if (!tick.IsValid
                || !ownerRuntimeId.IsValid
                || !currentTargetRuntimeId.IsValid
                || skillEvent.Kind != FpgSkillEventKind.GameplayAction
                || !anchorMap.TryGet(
                    ownerRuntimeId,
                    out FpgCombatantAnchorSnapshot owner)
                || owner.RuntimeId != ownerRuntimeId
                || owner.Actor == null
                || !owner.Actor.activeInHierarchy
                || owner.IsPresentationLeaseActive
                || owner.GameplayAnchor == null
                || !anchorMap.TryGet(
                    currentTargetRuntimeId,
                    out FpgCombatantAnchorSnapshot currentTarget)
                || currentTarget.RuntimeId != currentTargetRuntimeId
                || currentTarget.Actor == null
                || !currentTarget.Actor.activeInHierarchy
                || currentTarget.IsPresentationLeaseActive
                || currentTarget.GameplayAnchor == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            Transform origin = owner.ProjectileAnchor == null
                ? owner.GameplayAnchor
                : owner.ProjectileAnchor;
            if (!string.IsNullOrEmpty(socketName))
            {
                if (owner.SocketRegistry == null
                    || !owner.SocketRegistry.TryResolve(
                        socketName,
                        out origin))
                {
                    return DomainResult.Rejected(
                        RejectReason.InvalidDefinition);
                }
            }

            Transform targetAnchor = currentTarget.WeakpointAnchor == null
                ? currentTarget.GameplayAnchor
                : currentTarget.WeakpointAnchor;
            RuntimeId targetRuntimeId = currentTargetRuntimeId;
            Vector3 targetPoint;
            switch (skillEvent.TargetSource)
            {
                case FpgSkillTargetSource.CurrentTarget:
                    targetPoint = ApplyOffset(
                        targetAnchor.position,
                        targetAnchor.right,
                        targetAnchor.up,
                        targetAnchor.forward,
                        skillEvent.Offset);
                    break;

                case FpgSkillTargetSource.CurrentAim:
                    if (!TryCreateAimBasis(
                            origin.position,
                            targetAnchor.position,
                            origin.up,
                            out Vector3 aimRight,
                            out Vector3 aimUp,
                            out Vector3 aimForward))
                    {
                        return DomainResult.Rejected(
                            RejectReason.InvalidTarget);
                    }

                    targetPoint = ApplyOffset(
                        targetAnchor.position,
                        aimRight,
                        aimUp,
                        aimForward,
                        skillEvent.Offset);
                    break;

                case FpgSkillTargetSource.Self:
                    targetRuntimeId = ownerRuntimeId;
                    targetPoint = ApplyOffset(
                        owner.GameplayAnchor.position,
                        owner.GameplayAnchor.right,
                        owner.GameplayAnchor.up,
                        owner.GameplayAnchor.forward,
                        skillEvent.Offset);
                    break;

                case FpgSkillTargetSource.SocketForward:
                    if (string.IsNullOrEmpty(socketName))
                    {
                        return DomainResult.Rejected(
                            RejectReason.InvalidDefinition);
                    }

                    float distance = Vector3.Distance(
                        origin.position,
                        targetAnchor.position);
                    if (!IsFinite(distance)
                        || distance <= 0.0001f)
                    {
                        return DomainResult.Rejected(
                            RejectReason.InvalidTarget);
                    }

                    targetPoint = ApplyOffset(
                        origin.position + origin.forward * distance,
                        origin.right,
                        origin.up,
                        origin.forward,
                        skillEvent.Offset);
                    break;

                default:
                    return DomainResult.Rejected(
                        RejectReason.InvalidDefinition);
            }

            if (!TryQuantize(
                    origin.position,
                    out SpatialVectorKey quantizedOrigin)
                || !TryQuantize(
                    targetPoint,
                    out SpatialVectorKey quantizedTarget)
                || quantizedOrigin == quantizedTarget)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            try
            {
                context = new FpgEnemyAttackSpatialContext(
                    tick,
                    skillEvent.TargetSource,
                    skillEvent.SocketId,
                    skillEvent.Offset,
                    targetRuntimeId,
                    quantizedOrigin,
                    quantizedTarget);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is OverflowException)
            {
                context = default(FpgEnemyAttackSpatialContext);
                return DomainResult.Rejected(
                    RejectReason.InvalidDefinition);
            }

            return DomainResult.Success;
        }

        private static Vector3 ApplyOffset(
            Vector3 position,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            FpgSkillOffset offset)
        {
            const float MillimetersToMeters = 0.001f;
            return position
                + right * (offset.XMillimeters * MillimetersToMeters)
                + up * (offset.YMillimeters * MillimetersToMeters)
                + forward * (offset.ZMillimeters * MillimetersToMeters);
        }

        private static bool TryCreateAimBasis(
            Vector3 origin,
            Vector3 target,
            Vector3 preferredUp,
            out Vector3 right,
            out Vector3 up,
            out Vector3 forward)
        {
            right = default(Vector3);
            up = default(Vector3);
            forward = target - origin;
            if (!IsFinite(forward)
                || forward.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            forward.Normalize();
            right = Vector3.Cross(preferredUp, forward);
            if (right.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                right = Vector3.Cross(Vector3.up, forward);
            }

            if (right.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                right = Vector3.Cross(Vector3.right, forward);
            }

            if (right.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            right.Normalize();
            up = Vector3.Cross(forward, right).normalized;
            return IsFinite(right) && IsFinite(up) && IsFinite(forward);
        }

        private static bool TryQuantize(
            Vector3 position,
            out SpatialVectorKey key)
        {
            key = default(SpatialVectorKey);
            if (!TryQuantize(position.x, out int x)
                || !TryQuantize(position.y, out int y)
                || !TryQuantize(position.z, out int z))
            {
                return false;
            }

            key = new SpatialVectorKey(x, y, z);
            return true;
        }

        private static bool TryQuantize(float value, out int result)
        {
            double scaled =
                value * (double)SpatialContract.PositionUnitsPerMeter;
            if (double.IsNaN(scaled)
                || double.IsInfinity(scaled)
                || scaled > int.MaxValue
                || scaled < int.MinValue)
            {
                result = 0;
                return false;
            }

            result = (int)Math.Round(
                scaled,
                MidpointRounding.AwayFromZero);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }
    }
}
