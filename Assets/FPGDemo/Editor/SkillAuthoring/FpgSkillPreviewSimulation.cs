using System;
using System.Collections.Generic;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal enum FpgSkillPreviewHitPart
    {
        None = 0,
        Body,
        Weakpoint
    }

    internal enum FpgSkillPreviewGeometryKind
    {
        Ray = 0,
        Area,
        Projectile,
        TimedImpact,
        Summon,
        Warning
    }

    internal readonly struct FpgSkillPreviewTarget
    {
        public FpgSkillPreviewTarget(
            int index,
            string label,
            Vector3 bodyCenter,
            float bodyRadius,
            Vector3 weakpointCenter,
            float weakpointRadius)
        {
            Index = index;
            Label = label ?? string.Empty;
            BodyCenter = bodyCenter;
            BodyRadius = Mathf.Max(0.01f, bodyRadius);
            WeakpointCenter = weakpointCenter;
            WeakpointRadius = Mathf.Max(0.01f, weakpointRadius);
        }

        public int Index { get; }
        public string Label { get; }
        public Vector3 BodyCenter { get; }
        public float BodyRadius { get; }
        public Vector3 WeakpointCenter { get; }
        public float WeakpointRadius { get; }
    }

    internal readonly struct FpgSkillPreviewGeometry
    {
        public FpgSkillPreviewGeometry(
            FpgSkillPreviewGeometryKind kind,
            Vector3 start,
            Vector3 end,
            float radius,
            int targetIndex = -1)
        {
            Kind = kind;
            Start = start;
            End = end;
            Radius = Mathf.Max(0f, radius);
            TargetIndex = targetIndex;
        }

        public FpgSkillPreviewGeometryKind Kind { get; }
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public float Radius { get; }
        public int TargetIndex { get; }
    }

    internal readonly struct FpgSkillPreviewHit
    {
        public FpgSkillPreviewHit(
            int compiledEventId,
            int targetIndex,
            FpgSkillPreviewHitPart part,
            Vector3 position,
            int expectedHitTick,
            int damage,
            int breakDamage)
        {
            CompiledEventId = compiledEventId;
            TargetIndex = targetIndex;
            Part = part;
            Position = position;
            ExpectedHitTick = expectedHitTick;
            Damage = Mathf.Max(0, damage);
            BreakDamage = Mathf.Max(0, breakDamage);
        }

        public int CompiledEventId { get; }
        public int TargetIndex { get; }
        public FpgSkillPreviewHitPart Part { get; }
        public Vector3 Position { get; }
        public int ExpectedHitTick { get; }
        public int Damage { get; }
        public int BreakDamage { get; }
    }

    internal sealed class FpgSkillPreviewEventResult
    {
        private readonly List<FpgSkillPreviewHit> hits =
            new List<FpgSkillPreviewHit>();

        public int CompiledEventId;
        public int AuthoredEventIndex = -1;
        public int AuthoredOrdinal;
        public int LaunchTick;
        public int ExpectedHitTick;
        public string EventName;
        public string PayloadName;
        public FpgSkillPreviewPayloadKind PayloadKind;
        public bool IsInFlight;
        public bool IsImpactTick;
        public bool IsSummon;
        public bool IsReload;

        public IReadOnlyList<FpgSkillPreviewHit> Hits => hits;

        public void AddHit(in FpgSkillPreviewHit hit)
        {
            hits.Add(hit);
        }

        public string BuildSummary()
        {
            if (IsReload)
            {
                return "装填提交 Tick " + ExpectedHitTick;
            }

            if (IsSummon)
            {
                return "召唤提交 Tick " + ExpectedHitTick;
            }

            int bodyCount = 0;
            int weakpointCount = 0;
            int bodyDamage = 0;
            int bodyBreak = 0;
            int weakpointDamage = 0;
            int weakpointBreak = 0;
            for (int index = 0; index < hits.Count; index++)
            {
                FpgSkillPreviewHit hit = hits[index];
                if (hit.Part == FpgSkillPreviewHitPart.Weakpoint)
                {
                    weakpointCount++;
                    weakpointDamage += hit.Damage;
                    weakpointBreak += hit.BreakDamage;
                }
                else if (hit.Part == FpgSkillPreviewHitPart.Body)
                {
                    bodyCount++;
                    bodyDamage += hit.Damage;
                    bodyBreak += hit.BreakDamage;
                }
            }

            string state = IsInFlight
                ? "弹道飞行中"
                : IsImpactTick
                    ? "命中"
                    : "已提交";
            string summary = state + " · 预计命中 Tick " + ExpectedHitTick;
            if (bodyCount > 0)
            {
                summary += " · Body x" + bodyCount
                    + " 生命 " + bodyDamage
                    + " / 削韧 " + bodyBreak;
            }

            if (weakpointCount > 0)
            {
                summary += " · Weakpoint x" + weakpointCount
                    + " 生命 " + weakpointDamage
                    + " / 削韧 " + weakpointBreak;
            }

            if (bodyCount == 0 && weakpointCount == 0)
            {
                summary += " · 未命中";
            }

            return summary;
        }

        public string BuildLogMessage()
        {
            string name = string.IsNullOrWhiteSpace(EventName)
                ? "Gameplay Event"
                : EventName;
            string payload = string.IsNullOrWhiteSpace(PayloadName)
                ? PayloadKind.ToString()
                : PayloadName;
            return "#" + AuthoredOrdinal + " " + name
                + " · " + payload + " · " + BuildSummary();
        }
    }

    internal sealed class FpgSkillPreviewSimulationFrame
    {
        private readonly List<FpgSkillPreviewGeometry> geometries =
            new List<FpgSkillPreviewGeometry>();
        private readonly List<FpgSkillPreviewHit> hits =
            new List<FpgSkillPreviewHit>();
        private readonly List<FpgSkillPreviewEventResult> eventResults =
            new List<FpgSkillPreviewEventResult>();

        public FpgSkillPreviewSimulationFrame(int tick)
        {
            Tick = Mathf.Max(0, tick);
        }

        public int Tick { get; }
        public IReadOnlyList<FpgSkillPreviewGeometry> Geometries => geometries;
        public IReadOnlyList<FpgSkillPreviewHit> Hits => hits;
        public IReadOnlyList<FpgSkillPreviewEventResult> EventResults =>
            eventResults;

        public void AddGeometry(in FpgSkillPreviewGeometry geometry)
        {
            geometries.Add(geometry);
        }

        public void AddEventResult(FpgSkillPreviewEventResult result)
        {
            if (result == null)
            {
                return;
            }

            eventResults.Add(result);
            for (int index = 0; index < result.Hits.Count; index++)
            {
                hits.Add(result.Hits[index]);
            }
        }

        public bool TryGetEventResult(
            int compiledEventId,
            out FpgSkillPreviewEventResult result)
        {
            for (int index = eventResults.Count - 1; index >= 0; index--)
            {
                if (eventResults[index].CompiledEventId == compiledEventId)
                {
                    result = eventResults[index];
                    return true;
                }
            }

            result = null;
            return false;
        }

        public string BuildSummary()
        {
            for (int index = eventResults.Count - 1; index >= 0; index--)
            {
                FpgSkillPreviewEventResult result = eventResults[index];
                if (result.LaunchTick == Tick
                    || result.ExpectedHitTick == Tick
                    || result.IsInFlight)
                {
                    return result.BuildSummary();
                }
            }

            return geometries.Count > 0
                ? "预警/持续几何正在求值"
                : string.Empty;
        }
    }

    internal interface IFpgSkillPreviewPoseProvider
    {
        int PreviewTargetCount { get; }

        FpgSkillPreviewTarget GetPreviewTarget(int index);

        bool TryResolvePreviewOrigin(
            string socketId,
            out Vector3 position,
            out Vector3 forward);
    }

    internal static class FpgSkillPreviewSimulator
    {
        private const int MaximumPreviewPellets = 64;
        private const int MaximumPreviewProjectiles = 16;
        private const float PelletSpreadDegrees = 7f;

        public static FpgSkillPreviewSimulationFrame Evaluate(
            FpgCompiledSkillSequence sequence,
            int currentTick,
            IReadOnlyList<FpgSkillCompiledTriggerRecord> compiledTriggers,
            IReadOnlyList<FpgSkillEventRecord> authoredEvents,
            IReadOnlyList<FpgSkillPayloadRecord> payloads,
            IFpgSkillPreviewPoseProvider poseProvider)
        {
            FpgSkillPreviewSimulationFrame frame =
                new FpgSkillPreviewSimulationFrame(currentTick);
            if (!sequence.IsValid
                || compiledTriggers == null
                || authoredEvents == null
                || payloads == null
                || poseProvider == null)
            {
                return frame;
            }

            List<FpgSkillPreviewTarget> targets =
                CollectTargets(poseProvider);
            for (int eventIndex = 0;
                eventIndex < sequence.EventCount;
                eventIndex++)
            {
                FpgCompiledSkillEvent compiledEvent =
                    sequence.GetEvent(eventIndex);
                if (compiledEvent.Tick > currentTick)
                {
                    break;
                }

                if (compiledEvent.Kind
                    != FpgSkillEventKind.GameplayPayload)
                {
                    continue;
                }

                FpgSkillCompiledTriggerRecord trigger =
                    FindTrigger(compiledTriggers, compiledEvent.EventId);
                FpgSkillEventRecord authored = trigger == null
                    ? null
                    : FindAuthoredEvent(
                        authoredEvents,
                        trigger.EventIndex);
                FpgSkillPayloadRecord payload = authored == null
                    || authored.PayloadIndex < 0
                    || authored.PayloadIndex >= payloads.Count
                        ? null
                        : payloads[authored.PayloadIndex];
                if (payload == null)
                {
                    continue;
                }

                poseProvider.TryResolvePreviewOrigin(
                    authored.SocketId,
                    out Vector3 origin,
                    out Vector3 forward);
                if (forward.sqrMagnitude <= 0.000001f)
                {
                    forward = Vector3.right;
                }
                else
                {
                    forward.Normalize();
                }

                Vector3 offset = new Vector3(
                    compiledEvent.Offset.XMillimeters * 0.001f,
                    compiledEvent.Offset.YMillimeters * 0.001f,
                    compiledEvent.Offset.ZMillimeters * 0.001f);
                Vector3 aim = ResolveAimPoint(
                    compiledEvent.TargetSource,
                    origin,
                    forward,
                    offset,
                    targets);
                FpgSkillPreviewEventResult result =
                    CreateEventResult(
                        compiledEvent,
                        trigger,
                        authored,
                        payload);

                switch (payload.PreviewKind)
                {
                    case FpgSkillPreviewPayloadKind.PlayerPelletRay:
                        EvaluatePelletRay(
                            frame,
                            result,
                            payload,
                            origin,
                            aim,
                            targets,
                            currentTick);
                        break;

                    case FpgSkillPreviewPayloadKind.PlayerAreaAtFirstSurface:
                        EvaluateArea(
                            frame,
                            result,
                            payload,
                            origin,
                            aim,
                            targets,
                            currentTick);
                        break;

                    case FpgSkillPreviewPayloadKind.EnemyProjectile:
                        EvaluateProjectiles(
                            frame,
                            result,
                            payload,
                            origin,
                            aim,
                            targets,
                            currentTick);
                        break;

                    case FpgSkillPreviewPayloadKind.EnemyTimedImpact:
                        EvaluateTimedImpact(
                            frame,
                            result,
                            payload,
                            aim,
                            targets,
                            currentTick);
                        break;

                    case FpgSkillPreviewPayloadKind.EnemySummon:
                        EvaluateSummon(
                            frame,
                            result,
                            aim,
                            currentTick);
                        break;

                    case FpgSkillPreviewPayloadKind.PlayerReload:
                        result.ExpectedHitTick = compiledEvent.Tick;
                        result.IsReload = true;
                        break;
                }

                frame.AddEventResult(result);
            }

            AddActiveWarnings(
                frame,
                currentTick,
                authoredEvents,
                targets);
            return frame;
        }

        private static FpgSkillPreviewEventResult CreateEventResult(
            in FpgCompiledSkillEvent compiledEvent,
            FpgSkillCompiledTriggerRecord trigger,
            FpgSkillEventRecord authored,
            FpgSkillPayloadRecord payload)
        {
            return new FpgSkillPreviewEventResult
            {
                CompiledEventId = compiledEvent.EventId,
                AuthoredEventIndex = authored?.Index ?? -1,
                AuthoredOrdinal = trigger?.AuthoredOrdinal
                    ?? compiledEvent.SortOrder,
                LaunchTick = compiledEvent.Tick,
                ExpectedHitTick = compiledEvent.Tick
                    + Mathf.Max(0, payload.ImpactDelayTicks),
                EventName = trigger?.Name ?? authored?.Name,
                PayloadName = payload.Name,
                PayloadKind = payload.PreviewKind
            };
        }

        private static void EvaluatePelletRay(
            FpgSkillPreviewSimulationFrame frame,
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            Vector3 origin,
            Vector3 aim,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            int currentTick)
        {
            result.ExpectedHitTick = result.LaunchTick;
            if (currentTick != result.LaunchTick)
            {
                return;
            }

            Vector3 baseDirection = SafeDirection(origin, aim, Vector3.right);
            float distance = ResolveRayDistance(origin, targets);
            int pelletCount = Mathf.Clamp(
                Mathf.Max(1, payload.PelletCount),
                1,
                MaximumPreviewPellets);
            int penetration = Mathf.Max(
                1,
                payload.AdditionalPenetrationCount + 1);
            for (int pelletIndex = 0;
                pelletIndex < pelletCount;
                pelletIndex++)
            {
                float normalized = pelletCount == 1
                    ? 0f
                    : ((pelletIndex + 0.5f) / pelletCount) * 2f - 1f;
                Vector3 direction = Quaternion.AngleAxis(
                    normalized * PelletSpreadDegrees,
                    Vector3.forward) * baseDirection;
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.Ray,
                    origin,
                    origin + direction * distance,
                    0.025f));
                AddRayHits(
                    result,
                    payload,
                    origin,
                    direction,
                    targets,
                    penetration,
                    result.ExpectedHitTick);
            }
        }

        private static void EvaluateArea(
            FpgSkillPreviewSimulationFrame frame,
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            Vector3 origin,
            Vector3 aim,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            int currentTick)
        {
            result.ExpectedHitTick = result.LaunchTick;
            Vector3 direction = SafeDirection(origin, aim, Vector3.right);
            Vector3 center = aim;
            if (TryFindFirstRayHit(
                    origin,
                    direction,
                    targets,
                    out RayHitCandidate surface))
            {
                center = surface.Position;
            }

            float radius = ResolveAreaRadius(targets);
            if (currentTick == result.LaunchTick)
            {
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.Ray,
                    origin,
                    center,
                    0.035f));
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.Area,
                    center,
                    center,
                    radius));
            }

            int limit = Mathf.Max(1, payload.AreaCombatantLimit);
            AddAreaHits(
                result,
                payload,
                center,
                radius,
                targets,
                limit,
                result.ExpectedHitTick);
        }

        private static void EvaluateProjectiles(
            FpgSkillPreviewSimulationFrame frame,
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            Vector3 origin,
            Vector3 aim,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            int currentTick)
        {
            int flightTicks = Mathf.Max(1, payload.ImpactDelayTicks);
            result.ExpectedHitTick = result.LaunchTick + flightTicks;
            result.IsInFlight = currentTick >= result.LaunchTick
                && currentTick < result.ExpectedHitTick;
            result.IsImpactTick = currentTick == result.ExpectedHitTick;
            int projectileCount = Mathf.Clamp(
                Mathf.Max(1, payload.ProjectileCount),
                1,
                MaximumPreviewProjectiles);
            for (int index = 0; index < projectileCount; index++)
            {
                FpgSkillPreviewTarget? target = targets.Count == 0
                    ? (FpgSkillPreviewTarget?)null
                    : targets[index % targets.Count];
                Vector3 destination = target.HasValue
                    ? (index == 0
                        ? target.Value.WeakpointCenter
                        : target.Value.BodyCenter)
                    : aim;
                float lateral = projectileCount == 1
                    ? 0f
                    : (index - (projectileCount - 1) * 0.5f) * 0.12f;
                destination += Vector3.up * lateral;
                if (currentTick <= result.ExpectedHitTick)
                {
                    frame.AddGeometry(new FpgSkillPreviewGeometry(
                        FpgSkillPreviewGeometryKind.Ray,
                        origin,
                        destination,
                        0.018f,
                        target?.Index ?? -1));
                    float progress = Mathf.Clamp01(
                        (currentTick - result.LaunchTick)
                        / (float)flightTicks);
                    Vector3 projectilePosition = Vector3.Lerp(
                        origin,
                        destination,
                        progress);
                    projectilePosition += Vector3.up
                        * Mathf.Sin(progress * Mathf.PI)
                        * ResolveProjectileArc(targets);
                    frame.AddGeometry(new FpgSkillPreviewGeometry(
                        FpgSkillPreviewGeometryKind.Projectile,
                        projectilePosition,
                        projectilePosition,
                        ResolveMarkerRadius(targets) * 0.32f,
                        target?.Index ?? -1));
                }

                if (target.HasValue)
                {
                    FpgSkillPreviewHitPart part = index == 0
                        ? FpgSkillPreviewHitPart.Weakpoint
                        : FpgSkillPreviewHitPart.Body;
                    AddHit(
                        result,
                        payload,
                        target.Value,
                        part,
                        result.ExpectedHitTick);
                }
            }

            if (result.IsImpactTick)
            {
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.Area,
                    targets.Count > 0 ? targets[0].WeakpointCenter : aim,
                    targets.Count > 0 ? targets[0].WeakpointCenter : aim,
                    ResolveMarkerRadius(targets)));
            }
        }

        private static void EvaluateTimedImpact(
            FpgSkillPreviewSimulationFrame frame,
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            Vector3 aim,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            int currentTick)
        {
            int delay = Mathf.Max(0, payload.ImpactDelayTicks);
            result.ExpectedHitTick = result.LaunchTick + delay;
            result.IsInFlight = currentTick >= result.LaunchTick
                && currentTick < result.ExpectedHitTick;
            result.IsImpactTick = currentTick == result.ExpectedHitTick;
            float radius = ResolveMarkerRadius(targets) * 1.35f;
            if (currentTick <= result.ExpectedHitTick)
            {
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.TimedImpact,
                    aim,
                    aim,
                    radius));
            }

            if (targets.Count > 0)
            {
                FpgSkillPreviewTarget target = FindNearestTarget(
                    targets,
                    aim);
                FpgSkillPreviewHitPart part =
                    Vector3.Distance(aim, target.WeakpointCenter)
                        <= Vector3.Distance(aim, target.BodyCenter)
                            ? FpgSkillPreviewHitPart.Weakpoint
                            : FpgSkillPreviewHitPart.Body;
                AddHit(
                    result,
                    payload,
                    target,
                    part,
                    result.ExpectedHitTick);
            }
        }

        private static void EvaluateSummon(
            FpgSkillPreviewSimulationFrame frame,
            FpgSkillPreviewEventResult result,
            Vector3 aim,
            int currentTick)
        {
            result.ExpectedHitTick = result.LaunchTick;
            result.IsSummon = true;
            if (currentTick >= result.LaunchTick)
            {
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.Summon,
                    aim,
                    aim,
                    0.75f));
            }
        }

        private static void AddActiveWarnings(
            FpgSkillPreviewSimulationFrame frame,
            int currentTick,
            IReadOnlyList<FpgSkillEventRecord> authoredEvents,
            IReadOnlyList<FpgSkillPreviewTarget> targets)
        {
            if (targets.Count == 0)
            {
                return;
            }

            for (int index = 0; index < authoredEvents.Count; index++)
            {
                FpgSkillEventRecord authored = authoredEvents[index];
                if (authored.Track != FpgSkillEventTrackKind.Warning
                    || authored.DurationTicks <= 0
                    || currentTick < authored.Tick
                    || currentTick >= authored.Tick
                        + authored.DurationTicks)
                {
                    continue;
                }

                Vector3 center = targets[0].BodyCenter
                    + authored.TargetOffset;
                frame.AddGeometry(new FpgSkillPreviewGeometry(
                    FpgSkillPreviewGeometryKind.Warning,
                    center,
                    center,
                    ResolveAreaRadius(targets)));
            }
        }

        private static void AddRayHits(
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            Vector3 origin,
            Vector3 direction,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            int maximumHits,
            int expectedHitTick)
        {
            List<RayHitCandidate> hits = new List<RayHitCandidate>();
            CollectRayHits(origin, direction, targets, hits);
            hits.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            HashSet<int> hitTargets = new HashSet<int>();
            for (int index = 0;
                index < hits.Count && hitTargets.Count < maximumHits;
                index++)
            {
                RayHitCandidate candidate = hits[index];
                if (!hitTargets.Add(candidate.Target.Index))
                {
                    continue;
                }

                AddHit(
                    result,
                    payload,
                    candidate.Target,
                    candidate.Part,
                    expectedHitTick,
                    candidate.Position);
            }
        }

        private static void AddAreaHits(
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            Vector3 center,
            float radius,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            int maximumHits,
            int expectedHitTick)
        {
            List<AreaHitCandidate> hits = new List<AreaHitCandidate>();
            for (int index = 0; index < targets.Count; index++)
            {
                FpgSkillPreviewTarget target = targets[index];
                float bodyDistance = Vector3.Distance(
                    center,
                    target.BodyCenter);
                float weakpointDistance = Vector3.Distance(
                    center,
                    target.WeakpointCenter);
                bool bodyInside = bodyDistance <= radius
                    + target.BodyRadius;
                bool weakpointInside = weakpointDistance <= radius
                    + target.WeakpointRadius;
                if (!bodyInside && !weakpointInside)
                {
                    continue;
                }

                bool weakpoint = weakpointInside
                    && weakpointDistance <= bodyDistance;
                hits.Add(new AreaHitCandidate(
                    target,
                    weakpoint
                        ? FpgSkillPreviewHitPart.Weakpoint
                        : FpgSkillPreviewHitPart.Body,
                    weakpoint ? weakpointDistance : bodyDistance));
            }

            hits.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            for (int index = 0;
                index < hits.Count && index < maximumHits;
                index++)
            {
                AddHit(
                    result,
                    payload,
                    hits[index].Target,
                    hits[index].Part,
                    expectedHitTick);
            }
        }

        private static void AddHit(
            FpgSkillPreviewEventResult result,
            FpgSkillPayloadRecord payload,
            in FpgSkillPreviewTarget target,
            FpgSkillPreviewHitPart part,
            int expectedHitTick,
            Vector3? explicitPosition = null)
        {
            bool weakpoint = part == FpgSkillPreviewHitPart.Weakpoint;
            result.AddHit(new FpgSkillPreviewHit(
                result.CompiledEventId,
                target.Index,
                part,
                explicitPosition
                    ?? (weakpoint
                        ? target.WeakpointCenter
                        : target.BodyCenter),
                expectedHitTick,
                weakpoint
                    ? payload.WeakpointDamage
                    : payload.BaseDamage,
                weakpoint
                    ? payload.WeakpointBreakDamage
                    : payload.BreakDamage));
        }

        private static bool TryFindFirstRayHit(
            Vector3 origin,
            Vector3 direction,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            out RayHitCandidate hit)
        {
            List<RayHitCandidate> hits = new List<RayHitCandidate>();
            CollectRayHits(origin, direction, targets, hits);
            if (hits.Count == 0)
            {
                hit = default(RayHitCandidate);
                return false;
            }

            hits.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            hit = hits[0];
            return true;
        }

        private static void CollectRayHits(
            Vector3 origin,
            Vector3 direction,
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            List<RayHitCandidate> results)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                FpgSkillPreviewTarget target = targets[index];
                if (TryRaySphere(
                        origin,
                        direction,
                        target.WeakpointCenter,
                        target.WeakpointRadius,
                        out float weakpointDistance))
                {
                    results.Add(new RayHitCandidate(
                        target,
                        FpgSkillPreviewHitPart.Weakpoint,
                        weakpointDistance,
                        origin + direction * weakpointDistance));
                }

                if (TryRaySphere(
                        origin,
                        direction,
                        target.BodyCenter,
                        target.BodyRadius,
                        out float bodyDistance))
                {
                    results.Add(new RayHitCandidate(
                        target,
                        FpgSkillPreviewHitPart.Body,
                        bodyDistance,
                        origin + direction * bodyDistance));
                }
            }
        }

        private static bool TryRaySphere(
            Vector3 origin,
            Vector3 direction,
            Vector3 center,
            float radius,
            out float distance)
        {
            Vector3 offset = origin - center;
            float b = Vector3.Dot(offset, direction);
            float c = Vector3.Dot(offset, offset) - radius * radius;
            float discriminant = b * b - c;
            if (discriminant < 0f)
            {
                distance = 0f;
                return false;
            }

            float root = Mathf.Sqrt(discriminant);
            float near = -b - root;
            float far = -b + root;
            distance = near >= 0f ? near : far;
            return distance >= 0f;
        }

        private static Vector3 ResolveAimPoint(
            FpgSkillTargetSource source,
            Vector3 origin,
            Vector3 forward,
            Vector3 offset,
            IReadOnlyList<FpgSkillPreviewTarget> targets)
        {
            if (source == FpgSkillTargetSource.Self)
            {
                return origin + offset;
            }

            if (source == FpgSkillTargetSource.SocketForward
                || targets.Count == 0)
            {
                return origin + forward * 6f + offset;
            }

            return (source == FpgSkillTargetSource.CurrentTarget
                    ? targets[0].BodyCenter
                    : targets[0].WeakpointCenter)
                + offset;
        }

        private static List<FpgSkillPreviewTarget> CollectTargets(
            IFpgSkillPreviewPoseProvider poseProvider)
        {
            int count = Mathf.Clamp(
                poseProvider.PreviewTargetCount,
                0,
                4);
            List<FpgSkillPreviewTarget> result =
                new List<FpgSkillPreviewTarget>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(poseProvider.GetPreviewTarget(index));
            }

            return result;
        }

        private static FpgSkillCompiledTriggerRecord FindTrigger(
            IReadOnlyList<FpgSkillCompiledTriggerRecord> triggers,
            int compiledEventId)
        {
            for (int index = 0; index < triggers.Count; index++)
            {
                if (triggers[index].CompiledEventId == compiledEventId)
                {
                    return triggers[index];
                }
            }

            return null;
        }

        private static FpgSkillEventRecord FindAuthoredEvent(
            IReadOnlyList<FpgSkillEventRecord> authoredEvents,
            int authoredEventIndex)
        {
            for (int index = 0; index < authoredEvents.Count; index++)
            {
                if (authoredEvents[index].Index == authoredEventIndex)
                {
                    return authoredEvents[index];
                }
            }

            return null;
        }

        private static FpgSkillPreviewTarget FindNearestTarget(
            IReadOnlyList<FpgSkillPreviewTarget> targets,
            Vector3 point)
        {
            FpgSkillPreviewTarget nearest = targets[0];
            float nearestDistance = Vector3.SqrMagnitude(
                point - nearest.BodyCenter);
            for (int index = 1; index < targets.Count; index++)
            {
                float candidate = Vector3.SqrMagnitude(
                    point - targets[index].BodyCenter);
                if (candidate < nearestDistance)
                {
                    nearest = targets[index];
                    nearestDistance = candidate;
                }
            }

            return nearest;
        }

        private static Vector3 SafeDirection(
            Vector3 origin,
            Vector3 target,
            Vector3 fallback)
        {
            Vector3 direction = target - origin;
            return direction.sqrMagnitude <= 0.000001f
                ? fallback.normalized
                : direction.normalized;
        }

        private static float ResolveRayDistance(
            Vector3 origin,
            IReadOnlyList<FpgSkillPreviewTarget> targets)
        {
            float distance = 8f;
            for (int index = 0; index < targets.Count; index++)
            {
                distance = Mathf.Max(
                    distance,
                    Vector3.Distance(
                        origin,
                        targets[index].BodyCenter) + 2f);
            }

            return distance;
        }

        private static float ResolveMarkerRadius(
            IReadOnlyList<FpgSkillPreviewTarget> targets)
        {
            return targets.Count == 0
                ? 0.65f
                : Mathf.Max(0.35f, targets[0].BodyRadius);
        }

        private static float ResolveAreaRadius(
            IReadOnlyList<FpgSkillPreviewTarget> targets)
        {
            return ResolveMarkerRadius(targets) * 3.2f;
        }

        private static float ResolveProjectileArc(
            IReadOnlyList<FpgSkillPreviewTarget> targets)
        {
            return ResolveMarkerRadius(targets) * 1.4f;
        }

        private readonly struct RayHitCandidate
        {
            public RayHitCandidate(
                FpgSkillPreviewTarget target,
                FpgSkillPreviewHitPart part,
                float distance,
                Vector3 position)
            {
                Target = target;
                Part = part;
                Distance = distance;
                Position = position;
            }

            public FpgSkillPreviewTarget Target { get; }
            public FpgSkillPreviewHitPart Part { get; }
            public float Distance { get; }
            public Vector3 Position { get; }
        }

        private readonly struct AreaHitCandidate
        {
            public AreaHitCandidate(
                FpgSkillPreviewTarget target,
                FpgSkillPreviewHitPart part,
                float distance)
            {
                Target = target;
                Part = part;
                Distance = distance;
            }

            public FpgSkillPreviewTarget Target { get; }
            public FpgSkillPreviewHitPart Part { get; }
            public float Distance { get; }
        }
    }
}
