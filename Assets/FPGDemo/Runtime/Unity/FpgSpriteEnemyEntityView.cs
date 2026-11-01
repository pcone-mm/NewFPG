using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Skills;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class FpgSpriteEnemyEntityView : MonoBehaviour,
        IFpgFormalEnemyEntityBinder,
        IFpgFormalEnemyPresentationView,
        IFpgFormalEnemyMotionView,
        IFpgFormalEnemyViewValidation,
        IFpgFormalEnemyExternalMotionView,
        IFpgFormalEnemyHitView,
        IFpgEnemyMovementSnapshotReceiver
    {
        [Serializable]
        private sealed class AnimationBinding
        {
            [SerializeField] private string animationName;
            [SerializeField] private AnimationClip clip;
            [Min(0.01f)]
            [Tooltip("Animation playback multiplier. Skill events keep their authored combat ticks.")]
            [SerializeField] private float playbackSpeed = 1f;

            public string AnimationName => animationName ?? string.Empty;
            public AnimationClip Clip => clip;
            public float PlaybackSpeed => playbackSpeed;
        }

        [Serializable]
        private sealed class FrameBinding
        {
            [SerializeField] private string animationName;
            [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();

            public string AnimationName => animationName ?? string.Empty;
            public Sprite[] Frames => frames ?? Array.Empty<Sprite>();
        }

        [Header("Formal Anchors")]
        [SerializeField] private Transform gameplayAnchor;
        [SerializeField] private Transform projectileAnchor;
        [SerializeField] private Transform weakpointAnchor;
        [SerializeField] private Transform overheadHealthBarAnchor;
        [SerializeField] private D0ActorSocketRegistry socketRegistry;
        [SerializeField] private Collider[] hitParts = Array.Empty<Collider>();
        [SerializeField] private HitPart[] hitPartKinds = Array.Empty<HitPart>();

        [Header("Sprite Animation")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private AnimationBinding[] animations =
            Array.Empty<AnimationBinding>();
        [SerializeField] private FrameBinding[] frameBindings =
            Array.Empty<FrameBinding>();
        [SerializeField] private string hitAnimation = "hit";
        [SerializeField] private Color warningTint =
            new Color(1f, 0.35f, 0.15f, 1f);

        [Header("Movement Facing")]
        [SerializeField] private bool faceMovementDirection = true;
        [Tooltip("The direction in the original, unflipped Sprite frames, before Transform rotation.")]
        [SerializeField] private bool sourceArtFacesRight;
        [Min(0f)]
        [SerializeField] private float minimumFacingHorizontalSpeed = 0.05f;

        private RuntimeId runtimeId;
        private int spawnSequence = -1;
        private FpgEnemyDefinition boundDefinition;
        private FpgEnemyBehaviorDefinition boundBehavior;
        private bool gameplayEnabled;
        private bool presentationInitialized;
        private bool pendingHit;
        private bool playingHit;
        private bool hasSkillFrame;
        private bool skillTerminalPending;
        private int activeWarningCount;
        private TickIndex lastMotionTick = TickIndex.Invalid;
        private TickIndex lastMovementSnapshotTick = TickIndex.Invalid;
        private TickIndex animationStartTick = TickIndex.Invalid;
        private TickIndex hitStartTick = TickIndex.Invalid;
        private TickIndex skillTerminalTick = TickIndex.Invalid;
        private string activeAnimationName = string.Empty;
        private FpgFormalEnemySkillSequenceFrame lastSkillFrame;
        private Sprite authoredSprite;
        private Color authoredColor = Color.white;
        private bool authoredFlipX;
        private bool authoredRendererEnabled;
        private Vector3 authoredVisualLocalPosition;
        private Quaternion authoredVisualLocalRotation = Quaternion.identity;
        private Vector3 authoredVisualLocalScale = Vector3.one;
        private bool facingMirrored;
        private Transform[] facingPoints = Array.Empty<Transform>();
        private Vector3[] authoredFacingPointPositions = Array.Empty<Vector3>();

        public Transform GameplayAnchor => gameplayAnchor;
        public Transform ProjectileAnchor => projectileAnchor;
        public Transform WeakpointAnchor => weakpointAnchor;
        public D0ActorSocketRegistry SocketRegistry => socketRegistry;
        public Transform OverheadHealthBarAnchor => overheadHealthBarAnchor;
        public int HitPartCount => hitParts == null ? 0 : hitParts.Length;
        public bool GameplayEnabled => gameplayEnabled;
        public string RuntimeId => runtimeId.IsValid ? runtimeId.ToString() : string.Empty;
        public int SpawnSequence => spawnSequence;

        private void Awake()
        {
            CaptureAuthoredSprite();
            SetFormalGameplayEnabled(false);
        }

        private void OnDisable()
        {
            SetFormalGameplayEnabled(false);
            ResetPresentation();
        }

        public bool TryGetHitPart(
            int hitPartOrdinal,
            out Collider collider,
            out HitPart hitPart)
        {
            collider = null;
            hitPart = HitPart.Body;
            if (hitParts == null
                || hitPartOrdinal < 0
                || hitPartOrdinal >= hitParts.Length
                || hitParts[hitPartOrdinal] == null)
            {
                return false;
            }

            collider = hitParts[hitPartOrdinal];
            if (hitPartKinds != null && hitPartOrdinal < hitPartKinds.Length)
            {
                hitPart = hitPartKinds[hitPartOrdinal];
            }

            return true;
        }

        public bool TryValidateFormalView(
            FpgEnemyDefinition definition,
            out string error)
        {
            if (definition == null || definition.Behavior == null)
            {
                error = "Sprite enemy view requires a formal definition and behavior.";
                return false;
            }

            if (gameplayAnchor == null
                || projectileAnchor == null
                || weakpointAnchor == null
                || overheadHealthBarAnchor == null
                || !IsOwnedTransform(gameplayAnchor)
                || !IsOwnedTransform(projectileAnchor)
                || !IsOwnedTransform(weakpointAnchor)
                || !IsOwnedTransform(overheadHealthBarAnchor)
                || socketRegistry == null
                || spriteRenderer == null
                || !IsOwnedTransform(spriteRenderer.transform))
            {
                error = "Sprite enemy view requires authored anchors, a socket registry, and a child SpriteRenderer.";
                return false;
            }

            if (spriteRenderer.sharedMaterial == null
                || spriteRenderer.sharedMaterial.shader == null)
            {
                error = "Sprite enemy view requires a valid Sprite material and shader.";
                return false;
            }

            if (float.IsNaN(minimumFacingHorizontalSpeed)
                || float.IsInfinity(minimumFacingHorizontalSpeed)
                || minimumFacingHorizontalSpeed < 0f)
            {
                error = "Sprite enemy movement-facing speed threshold must be finite and non-negative.";
                return false;
            }

            if (!socketRegistry.TryValidate(out error))
            {
                return false;
            }

            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            if (colliders.Length == 0 || kinds.Length != colliders.Length)
            {
                error = "Sprite enemy view hit parts and kinds must be non-empty and parallel.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null
                    || !IsOwnedTransform(colliders[index].transform)
                    || !Enum.IsDefined(typeof(HitPart), kinds[index])
                    || kinds[index] == HitPart.Projectile)
                {
                    error = $"Sprite enemy hit part {index} is missing or invalid.";
                    return false;
                }
            }

            AnimationBinding[] bindings = animations ?? Array.Empty<AnimationBinding>();
            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index] == null
                    || string.IsNullOrWhiteSpace(bindings[index].AnimationName)
                    || bindings[index].Clip == null
                    || !IsValidPlaybackSpeed(bindings[index].PlaybackSpeed)
                    || FindAnimation(bindings[index].AnimationName) != bindings[index])
                {
                    error = $"Sprite enemy animation binding {index} is missing, duplicated, or has an invalid playback speed.";
                    return false;
                }
            }

            FpgEnemyBehaviorDefinition behavior = definition.Behavior;
            if (!HasAnimation(behavior.EntryAnimation)
                || !HasAnimation(behavior.IdleAnimation)
                || !HasAnimation(behavior.DeathAnimation)
                || (!string.IsNullOrWhiteSpace(behavior.MovementAnimation)
                    && !HasAnimation(behavior.MovementAnimation))
                || !HasAnimation(hitAnimation))
            {
                error = "Sprite enemy behavior or hit key has no AnimationClip binding.";
                return false;
            }

            for (int attackIndex = 0; attackIndex < definition.AttackPatternCount; attackIndex++)
            {
                FpgEnemyAttackDefinition attack = definition.GetAttackPattern(attackIndex);
                for (int sequenceIndex = 0; sequenceIndex < attack.Sequences.Count; sequenceIndex++)
                {
                    FpgSkillSequenceDefinition sequence = attack.Sequences[sequenceIndex];
                    if (!HasAnimation(sequence.MainAnimation))
                    {
                        error = $"Sprite enemy skill '{attack.SkillId}' references an unbound animation.";
                        return false;
                    }

                    for (int variantIndex = 0; variantIndex < sequence.AlternateAnimations.Count; variantIndex++)
                    {
                        if (!HasAnimation(sequence.AlternateAnimations[variantIndex]))
                        {
                            error = $"Sprite enemy skill '{attack.SkillId}' references an unbound animation variant.";
                            return false;
                        }
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryBindFormalRuntime(
            RuntimeId nextRuntimeId,
            int nextSpawnSequence,
            FpgEnemyDefinition definition,
            out string error)
        {
            error = string.Empty;
            if (!nextRuntimeId.IsValid
                || nextSpawnSequence < 0
                || definition == null
                || presentationInitialized
                || !TryValidateFormalView(definition, out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Sprite enemy binding requires a valid runtime, sequence, and definition.";
                }

                return false;
            }

            CaptureAuthoredSprite();
            boundDefinition = definition;
            boundBehavior = definition.Behavior;
            runtimeId = nextRuntimeId;
            spawnSequence = nextSpawnSequence;
            presentationInitialized = true;
            pendingHit = false;
            playingHit = false;
            hasSkillFrame = false;
            skillTerminalPending = false;
            activeWarningCount = 0;
            activeAnimationName = boundBehavior.EntryAnimation;
            animationStartTick = TickIndex.Invalid;
            hitStartTick = TickIndex.Invalid;
            lastMotionTick = TickIndex.Invalid;
            lastMovementSnapshotTick = TickIndex.Invalid;
            spriteRenderer.enabled = authoredRendererEnabled;
            SetFormalGameplayEnabled(false);
            // Put the authored first Walk frame on the renderer immediately.
            // The formal encounter advances from fixed ticks, so this also
            // prevents a newly acquired pooled instance from showing a stale
            // preview frame during the preparation/activation boundary.
            SampleAnimation(activeAnimationName, 0f);
            error = string.Empty;
            return true;
        }

        public void SetFormalGameplayEnabled(bool enabled)
        {
            gameplayEnabled = enabled;
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = enabled;
                }
            }
        }

        public void UnbindFormalRuntime()
        {
            SetFormalGameplayEnabled(false);
            ResetPresentation();
            runtimeId = FPG.Demo.Core.RuntimeId.Invalid;
            spawnSequence = -1;
        }

        /// <summary>
        /// Applies a pose produced by the AI movement executor.
        /// </summary>
        public DomainResult ApplyMovementSnapshot(
            in FpgEnemyMovementSnapshot snapshot)
        {
            if (!presentationInitialized || !snapshot.Tick.IsValid)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (lastMovementSnapshotTick.IsValid
                && snapshot.Tick.Value < lastMovementSnapshotTick.Value)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            lastMovementSnapshotTick = snapshot.Tick;
            // The executor reports gameplay-anchor coordinates. Preserve any
            // authored root-to-anchor offset when applying the pose instead
            // of assuming the anchor is the root transform.
            Vector3 currentAnchorPosition = gameplayAnchor != null
                ? gameplayAnchor.position
                : transform.position;
            transform.position += snapshot.Position - currentAnchorPosition;
            if (snapshot.IsMoving)
            {
                UpdateMovementFacing(
                    snapshot.Delta,
                    snapshot.FacingReferenceRight);
            }

            return DomainResult.Success;
        }

        /// <summary>
        /// Lets an AI runtime drive ambient movement/idle presentation without
        /// granting the view any attack or movement decision authority.
        /// </summary>
        public DomainResult ApplyMovementAnimation(
            string animationName,
            float elapsedSeconds)
        {
            if (!presentationInitialized
                || playingHit
                || hasSkillFrame
                || string.IsNullOrWhiteSpace(animationName)
                || !HasAnimation(animationName))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (!SampleAnimation(animationName, elapsedSeconds))
            {
                return DomainResult.Rejected(RejectReason.InvariantFault);
            }

            activeAnimationName = animationName;
            int elapsedTicks = Mathf.Max(
                0,
                Mathf.RoundToInt(
                    Mathf.Max(0f, elapsedSeconds)
                        * FpgSkillRuntimeConstants.TickRate));
            animationStartTick = lastMotionTick.IsValid
                ? new TickIndex(System.Math.Max(
                    0L,
                    lastMotionTick.Value - elapsedTicks))
                : TickIndex.Invalid;
            return DomainResult.Success;
        }

        /// <summary>
        /// Updates facing from an externally computed movement vector. This is
        /// intentionally separate from <see cref="ApplyMovementSnapshot"/>
        /// for AI runtimes that use a kinematic movement backend.
        /// </summary>
        public void SetMovementFacing(
            Vector3 movement,
            Vector3 facingReferenceRight)
        {
            UpdateMovementFacing(movement, facingReferenceRight);
        }

        /// <summary>
        /// Advances hit/skill/idle presentation while leaving gameplay-anchor
        /// movement to an external AI movement executor.
        /// </summary>
        public DomainResult AdvanceFormalPresentation(TickIndex tick)
        {
            return AdvanceFormalPresentationInternal(tick);
        }

        private DomainResult AdvanceFormalPresentationInternal(TickIndex tick)
        {
            if (!tick.IsValid || !presentationInitialized)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            if (lastMotionTick.IsValid && tick.Value < lastMotionTick.Value)
            {
                return DomainResult.Rejected(RejectReason.WrongTick);
            }

            if (tick == lastMotionTick)
            {
                return DomainResult.Success;
            }

            lastMotionTick = tick;
            if (pendingHit)
            {
                pendingHit = false;
                playingHit = true;
                activeAnimationName = hitAnimation;
                hitStartTick = tick;
                ApplyWarningTint();
            }

            if (playingHit)
            {
                AnimationBinding hitBinding = FindAnimation(hitAnimation);
                long elapsed = hitStartTick.IsValid
                    ? tick.Value - hitStartTick.Value
                    : 0L;
                if (elapsed >= DurationTicks(hitBinding))
                {
                    playingHit = false;
                    hitStartTick = TickIndex.Invalid;
                    ApplyWarningTint();
                    activeAnimationName = boundBehavior.IdleAnimation;
                    animationStartTick = tick;
                }
                else
                {
                    return SampleAnimation(
                        hitAnimation,
                        (float)(elapsed / (double)FpgSkillRuntimeConstants.TickRate))
                        ? DomainResult.Success
                        : DomainResult.Rejected(RejectReason.InvariantFault);
                }
            }

            if (skillTerminalPending
                && tick.Value > skillTerminalTick.Value)
            {
                skillTerminalPending = false;
                skillTerminalTick = TickIndex.Invalid;
                activeAnimationName = boundBehavior.IdleAnimation;
                animationStartTick = tick;
                return SampleAnimation(activeAnimationName, 0f)
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.InvariantFault);
            }

            if (hasSkillFrame && !lastSkillFrame.IsTerminal)
            {
                if (!TrySampleSkillFrame(lastSkillFrame))
                {
                    return DomainResult.Rejected(RejectReason.InvariantFault);
                }

                return DomainResult.Success;
            }

            if (!animationStartTick.IsValid)
            {
                animationStartTick = tick;
            }

            bool entryIsCurrent = string.Equals(
                activeAnimationName,
                boundBehavior.EntryAnimation,
                StringComparison.Ordinal);
            if (entryIsCurrent
                && tick.Value - animationStartTick.Value
                    < DurationTicks(FindAnimation(boundBehavior.EntryAnimation)))
            {
                string entryName = boundBehavior.EntryAnimation;
                return SampleAnimation(
                        entryName,
                        (float)((tick.Value - animationStartTick.Value)
                            / (double)FpgSkillRuntimeConstants.TickRate))
                    ? DomainResult.Success
                    : DomainResult.Rejected(RejectReason.InvariantFault);
            }

            activeAnimationName = boundBehavior.IdleAnimation;
            float idleSeconds = animationStartTick.IsValid
                ? (float)((tick.Value - animationStartTick.Value)
                    / (double)FpgSkillRuntimeConstants.TickRate)
                : 0f;
            return SampleAnimation(activeAnimationName, idleSeconds)
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.InvariantFault);
        }

        public DomainResult StartFormalSkillMotion(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!IsValidSkillFrame(frame)
                || frame.State != FpgSkillExecutionState.Running
                || frame.RelativeTick != 0
                || frame.Tick != frame.StartTick
                || !FpgEnemySkillPresentationResolver.TryResolveAnimationName(
                    frame.Definition,
                    frame.CompiledSequence.Kind,
                    frame.ResolvedAnimationId,
                    out string animationName)
                || !HasAnimation(animationName))
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            lastSkillFrame = frame;
            hasSkillFrame = true;
            skillTerminalPending = false;
            skillTerminalTick = TickIndex.Invalid;
            activeWarningCount = 0;
            activeAnimationName = animationName;
            animationStartTick = frame.Tick;
            return pendingHit || playingHit || TrySampleSkillFrame(frame)
                ? DomainResult.Success
                : DomainResult.Rejected(RejectReason.InvariantFault);
        }

        public DomainResult ApplyFormalSkillMotionFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!IsValidSkillFrame(frame)
                || !frame.IsTerminal
                || !hasSkillFrame
                || lastSkillFrame.ExecutionId != frame.ExecutionId)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            lastSkillFrame = frame;
            hasSkillFrame = false;
            skillTerminalPending = true;
            skillTerminalTick = frame.Tick;
            return DomainResult.Success;
        }

        public bool TrySetSkillSequenceFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!IsValidSkillFrame(frame))
            {
                return false;
            }

            if (frame.IsTerminal)
            {
                return skillTerminalPending
                    && frame.Tick == skillTerminalTick
                    && lastSkillFrame.ExecutionId == frame.ExecutionId
                    && (pendingHit || playingHit || TrySampleSkillFrame(frame));
            }

            if (!hasSkillFrame || lastSkillFrame.ExecutionId != frame.ExecutionId)
            {
                return false;
            }

            lastSkillFrame = frame;
            return pendingHit || playingHit || TrySampleSkillFrame(frame);
        }

        public bool TrySetSkillWarning(
            in FpgFormalEnemySkillWarningPresentationEvent warningEvent)
        {
            FpgFormalEnemySkillTimelineEvent timelineEvent =
                warningEvent.TimelineEvent;
            if (!presentationInitialized
                || !gameplayEnabled
                || !timelineEvent.OwnerRuntimeId.IsValid
                || timelineEvent.OwnerRuntimeId != runtimeId
                || timelineEvent.SpawnSequence != spawnSequence
                || timelineEvent.Definition == null
                || !OwnsAttack(timelineEvent.Definition)
                || (!string.IsNullOrEmpty(warningEvent.Resolved.SocketName)
                    && (socketRegistry == null
                        || !socketRegistry.TryResolve(
                            warningEvent.Resolved.SocketName,
                            out _))))
            {
                return false;
            }

            activeWarningCount = warningEvent.IsActive
                ? activeWarningCount + 1
                : Mathf.Max(0, activeWarningCount - 1);
            ApplyWarningTint();
            return true;
        }

        public void ClearSkillWarnings()
        {
            activeWarningCount = 0;
            ApplyWarningTint();
        }

        public void NotifyFormalHit()
        {
            if (gameplayEnabled && presentationInitialized)
            {
                pendingHit = true;
            }
        }

        private void UpdateMovementFacing(
            Vector3 movement,
            Vector3 screenRight)
        {
            if (!faceMovementDirection || spriteRenderer == null)
            {
                return;
            }

            if (screenRight.sqrMagnitude <= 0.0000001f)
            {
                screenRight = Vector3.right;
            }

            screenRight.Normalize();
            float horizontalSpeed = Vector3.Dot(movement, screenRight)
                * FpgSkillRuntimeConstants.TickRate;
            if (Mathf.Abs(horizontalSpeed)
                <= Mathf.Max(0.0001f, minimumFacingHorizontalSpeed))
            {
                return;
            }

            Vector3 authoredDirection = spriteRenderer.transform.TransformVector(
                sourceArtFacesRight ? Vector3.right : Vector3.left);
            if (authoredFlipX)
            {
                authoredDirection = -authoredDirection;
            }

            float authoredScreenDirection = Vector3.Dot(authoredDirection, screenRight);
            if (Mathf.Abs(authoredScreenDirection) <= 0.0001f)
            {
                return;
            }

            bool mirror = Mathf.Sign(horizontalSpeed) != Mathf.Sign(authoredScreenDirection);
            if (mirror != facingMirrored)
            {
                facingMirrored = mirror;
                ApplyFacingPoints();
            }

            ApplySpriteFacing();
        }

        private void ApplySpriteFacing()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = authoredFlipX ^ facingMirrored;
            }
        }

        private void ApplyFacingPoints()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Transform visual = spriteRenderer.transform;
            for (int index = 0; index < facingPoints.Length; index++)
            {
                if (facingPoints[index] == null)
                {
                    continue;
                }

                Vector3 local = authoredFacingPointPositions[index];
                local.x *= facingMirrored ? -1f : 1f;
                facingPoints[index].position = visual.TransformPoint(local);
            }
        }

        private void CaptureFacingPoints()
        {
            var points = new List<Transform>();
            foreach (Collider collider in hitParts ?? Array.Empty<Collider>())
            {
                AddFacingPoint(points, collider == null ? null : collider.transform);
            }

            AddFacingPoint(points, weakpointAnchor);
            AddFacingPoint(points, projectileAnchor);
            AddFacingPoint(points, overheadHealthBarAnchor);
            if (socketRegistry != null && socketRegistry.Bindings != null)
            {
                foreach (D0ActorSocketBinding binding in socketRegistry.Bindings)
                {
                    AddFacingPoint(points, binding == null ? null : binding.Anchor);
                }
            }

            // Apply parents before children when anchors share a hierarchy.
            points.Sort((left, right) => FacingPointDepth(left).CompareTo(FacingPointDepth(right)));
            facingPoints = points.ToArray();
            authoredFacingPointPositions = new Vector3[facingPoints.Length];
            for (int index = 0; index < facingPoints.Length; index++)
            {
                authoredFacingPointPositions[index] = spriteRenderer.transform
                    .InverseTransformPoint(facingPoints[index].position);
            }
        }

        private void AddFacingPoint(List<Transform> points, Transform point)
        {
            if (point != null && point != transform && point != gameplayAnchor
                && !spriteRenderer.transform.IsChildOf(point) && !points.Contains(point))
            {
                points.Add(point);
            }
        }

        private int FacingPointDepth(Transform point)
        {
            int depth = 0;
            while (point != null && point != transform)
            {
                depth++;
                point = point.parent;
            }

            return depth;
        }

        private bool TrySampleSkillFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            if (!FpgEnemySkillPresentationResolver.TryResolveAnimationName(
                    frame.Definition,
                    frame.CompiledSequence.Kind,
                    frame.ResolvedAnimationId,
                    out string animationName))
            {
                return false;
            }

            AnimationBinding binding = FindAnimation(animationName);
            if (binding == null)
            {
                return false;
            }

            if (binding.Clip == null)
            {
                activeAnimationName = animationName;
                return true;
            }

            double durationSeconds = GetAnimationDurationSeconds(binding);
            double seconds = FpgSkillAnimationTime.EvaluateSeconds(
                frame.CompiledSequence,
                frame.RelativeTick,
                0d,
                durationSeconds);
            float sampleTime = Mathf.Clamp(
                (float)seconds,
                0f,
                (float)durationSeconds);
            bool sampled = SampleAnimation(animationName, sampleTime);
            if (sampled)
            {
                activeAnimationName = animationName;
            }

            return sampled;
        }

        private bool SampleAnimation(string animationName, float seconds)
        {
            AnimationBinding binding = FindAnimation(animationName);
            if (binding == null || spriteRenderer == null)
            {
                return false;
            }

            if (binding.Clip == null)
            {
                RestoreAuthoredVisualPose();
                return true;
            }

            // Walk is currently used for both entry and idle by the Sprite
            // test enemy. Some Unity import paths do not expose the authored
            // loop flag consistently at runtime, so ambient entry/idle
            // playback must remain looping even when the clip reports false.
            bool shouldLoop = binding.Clip.isLooping
                || IsAmbientLoopAnimation(animationName);
            float clipDuration = GetAnimationDurationSeconds(binding);
            float sourceSeconds = Mathf.Max(0f, seconds) * binding.PlaybackSpeed;
            float time = shouldLoop
                ? Mathf.Repeat(sourceSeconds, clipDuration)
                : Mathf.Clamp(sourceSeconds, 0f, clipDuration);

            FrameBinding frameBinding = FindFrameBinding(animationName);
            Sprite[] frames = frameBinding == null
                ? Array.Empty<Sprite>()
                : frameBinding.Frames;
            if (frames.Length > 0)
            {
                // Drive sprite-sheet playback from the authored frame rate.
                // Unity can expose a stale or zero native clip length while
                // an imported PPtr curve is being rebuilt. Normalizing by
                // that value would then select frame zero forever. The frame
                // rate and fixed-tick seconds remain stable in that state.
                float frameRate = GetAnimationFrameRate(
                    binding,
                    frames.Length);
                int frameIndex = Mathf.FloorToInt(
                    sourceSeconds * frameRate + 0.0001f);
                if (shouldLoop)
                {
                    frameIndex %= frames.Length;
                }
                else
                {
                    frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
                }

                Sprite frame = frames[frameIndex];
                if (frame != null)
                {
                    spriteRenderer.sprite = frame;
                    RestoreAuthoredVisualPose();
                    return true;
                }
            }

            try
            {
                binding.Clip.SampleAnimation(spriteRenderer.gameObject, time);
                RestoreAuthoredVisualPose();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                RestoreAuthoredVisualPose();
                return false;
            }
        }

        private bool IsAmbientLoopAnimation(string animationName)
        {
            if (boundBehavior == null || string.IsNullOrEmpty(animationName))
            {
                return false;
            }

            return string.Equals(
                       animationName,
                       boundBehavior.EntryAnimation,
                       StringComparison.Ordinal)
                || string.Equals(
                       animationName,
                       boundBehavior.IdleAnimation,
                       StringComparison.Ordinal);
        }

        private AnimationBinding FindAnimation(string animationName)
        {
            AnimationBinding[] bindings = animations ?? Array.Empty<AnimationBinding>();
            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index] != null
                    && string.Equals(
                        bindings[index].AnimationName,
                        animationName,
                        StringComparison.Ordinal))
                {
                    return bindings[index];
                }
            }

            return null;
        }

        private FrameBinding FindFrameBinding(string animationName)
        {
            FrameBinding[] bindings = frameBindings ?? Array.Empty<FrameBinding>();
            for (int index = 0; index < bindings.Length; index++)
            {
                if (bindings[index] != null
                    && string.Equals(
                        bindings[index].AnimationName,
                        animationName,
                        StringComparison.Ordinal))
                {
                    return bindings[index];
                }
            }

            return null;
        }

        private bool HasAnimation(string animationName)
        {
            AnimationBinding binding = FindAnimation(animationName);
            return binding != null && binding.Clip != null;
        }

        private int DurationTicks(AnimationBinding binding)
        {
            if (binding == null)
            {
                return 0;
            }

            return Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        GetAnimationDurationSeconds(binding)
                            / binding.PlaybackSpeed
                            * FpgSkillRuntimeConstants.TickRate - 0.0001f));
        }

        private static bool IsValidPlaybackSpeed(float speed)
        {
            return !float.IsNaN(speed) && !float.IsInfinity(speed) && speed > 0f;
        }

#if UNITY_EDITOR
        private bool previewInitialized;
        public int AnimationCount => animations == null ? 0 : animations.Length;
        public SpriteRenderer AnimationRenderer => spriteRenderer;

        public string GetAnimationName(int index)
        {
            return index >= 0 && index < AnimationCount && animations[index] != null
                ? animations[index].AnimationName : string.Empty;
        }

        public bool TryGetAnimationPlaybackInfo(
            string animationName,
            out float effectiveDurationSeconds,
            out float effectiveFrameRate,
            out int frameCount)
        {
            effectiveDurationSeconds = 0f;
            effectiveFrameRate = 0f;
            frameCount = 0;
            AnimationBinding binding = FindAnimation(animationName);
            if (binding == null || binding.Clip == null
                || !IsValidPlaybackSpeed(binding.PlaybackSpeed))
            {
                return false;
            }

            FrameBinding frames = FindFrameBinding(animationName);
            frameCount = frames == null ? 0 : frames.Frames.Length;
            effectiveDurationSeconds = GetAnimationDurationSeconds(binding) / binding.PlaybackSpeed;
            effectiveFrameRate = GetAnimationFrameRate(binding, frameCount) * binding.PlaybackSpeed;
            if (frameCount == 0)
            {
                frameCount = Mathf.Max(1, Mathf.CeilToInt(
                    effectiveDurationSeconds * effectiveFrameRate - 0.0001f));
            }

            return true;
        }

        public bool TrySampleAnimationPreview(string animationName, float elapsedSeconds)
        {
            // Preview sampling is restricted to a disposable hidden clone.
            if (presentationInitialized || Application.isPlaying
                || (gameObject.hideFlags & HideFlags.HideAndDontSave) != HideFlags.HideAndDontSave
                || !TryGetAnimationPlaybackInfo(animationName, out _, out _, out _))
            {
                return false;
            }

            if (!previewInitialized)
            {
                CaptureAuthoredSprite();
                previewInitialized = true;
            }

            return SampleAnimation(animationName, elapsedSeconds);
        }
#endif

        private float GetAnimationDurationSeconds(AnimationBinding binding)
        {
            if (binding == null)
            {
                return 0.0001f;
            }

            AnimationClip clip = binding.Clip;
            if (clip != null && clip.length > 0.0001f)
            {
                return clip.length;
            }

            FrameBinding frameBinding = FindFrameBinding(binding.AnimationName);
            int frameCount = frameBinding == null
                ? 0
                : frameBinding.Frames.Length;
            float frameRate = GetAnimationFrameRate(binding, frameCount);
            // Frame bindings are the authoritative fallback while Unity is
            // rebuilding a native PPtr curve cache.
            return Mathf.Max(
                0.0001f,
                frameCount > 0
                    ? frameCount / frameRate
                    : 1f / frameRate);
        }

        private float GetAnimationFrameRate(
            AnimationBinding binding,
            int frameCount)
        {
            if (binding == null || binding.Clip == null)
            {
                return FpgSkillRuntimeConstants.TickRate;
            }

            if (binding.Clip.frameRate > 0f)
            {
                return binding.Clip.frameRate;
            }

            if (frameCount > 0 && binding.Clip.length > 0.0001f)
            {
                return frameCount / binding.Clip.length;
            }

            return FpgSkillRuntimeConstants.TickRate;
        }

        private bool IsValidSkillFrame(
            in FpgFormalEnemySkillSequenceFrame frame)
        {
            return presentationInitialized
                && gameplayEnabled
                && frame.OwnerRuntimeId.IsValid
                && frame.OwnerRuntimeId == runtimeId
                && frame.SpawnSequence == spawnSequence
                && frame.Definition != null
                && OwnsAttack(frame.Definition)
                && frame.CompiledSequence.IsValid
                && frame.ExecutionId.IsValid;
        }

        private bool OwnsAttack(FpgEnemyAttackDefinition attack)
        {
            if (attack == null || boundDefinition == null)
            {
                return false;
            }

            for (int index = 0;
                index < boundDefinition.AttackPatternCount;
                index++)
            {
                if (boundDefinition.GetAttackPattern(index) == attack)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOwnedTransform(Transform candidate)
        {
            return candidate != null
                && (candidate == transform || candidate.IsChildOf(transform));
        }

        private void CaptureAuthoredSprite()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            authoredSprite = spriteRenderer.sprite;
            authoredColor = spriteRenderer.color;
            authoredFlipX = spriteRenderer.flipX;
            authoredRendererEnabled = spriteRenderer.enabled;
            authoredVisualLocalPosition = spriteRenderer.transform.localPosition;
            authoredVisualLocalRotation = spriteRenderer.transform.localRotation;
            authoredVisualLocalScale = spriteRenderer.transform.localScale;
            facingMirrored = false;
            CaptureFacingPoints();
        }

        private void ApplyWarningTint()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = activeWarningCount > 0
                    ? warningTint
                    : authoredColor;
            }
        }

        private void RestoreAuthoredVisualPose()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Transform visual = spriteRenderer.transform;
            visual.localPosition = authoredVisualLocalPosition;
            visual.localRotation = authoredVisualLocalRotation;
            visual.localScale = authoredVisualLocalScale;
            ApplySpriteFacing();
        }

        private void ResetPresentation()
        {
            pendingHit = false;
            playingHit = false;
            hasSkillFrame = false;
            skillTerminalPending = false;
            activeWarningCount = 0;
            lastMotionTick = TickIndex.Invalid;
            lastMovementSnapshotTick = TickIndex.Invalid;
            animationStartTick = TickIndex.Invalid;
            hitStartTick = TickIndex.Invalid;
            skillTerminalTick = TickIndex.Invalid;
            lastSkillFrame = default(FpgFormalEnemySkillSequenceFrame);
            activeAnimationName = string.Empty;
            boundDefinition = null;
            boundBehavior = null;
            runtimeId = FPG.Demo.Core.RuntimeId.Invalid;
            spawnSequence = -1;
            presentationInitialized = false;
            facingMirrored = false;
            if (spriteRenderer != null)
            {
                RestoreAuthoredVisualPose();
                ApplyFacingPoints();
                spriteRenderer.sprite = authoredSprite;
                spriteRenderer.color = authoredColor;
                spriteRenderer.flipX = authoredFlipX;
                spriteRenderer.enabled = authoredRendererEnabled;
            }
        }
    }
}
