using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Presentation commands derived exclusively from already-committed combat
    /// trace entries. They are intentionally an internal implementation detail
    /// of the Unity bridge, never battle-session data.
    /// </summary>
    internal enum D0ActorPresentationCommand
    {
        None = 0,
        PlayerReloadStarted,
        PlayerReloadCompleted,
        PlayerSecondaryChargeStarted,
        PlayerSecondaryChargeCanceled,
        PlayerSecondaryReleaseCommitted,
        PlayerHit,
        PlayerGroggy,
        EnemyHit,
        EnemyGroggyStarted,
        EnemyGroggyEnded,
        EnemyDeath,
        PlayerVictory,
        PlayerDefeat
    }

    /// <summary>
    /// Pure trace-to-command mapping for the D0 actors. Keeping this separate
    /// from MonoBehaviour lifecycle code makes the combat-event contract
    /// directly testable without allowing presentation to write battle state.
    /// </summary>
    internal static class D0ActorPresentationRouting
    {
        public static D0ActorPresentationCommand ResolveCommand(
            in CombatEvent combatEvent,
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            bool enemyGroggy)
        {
            switch (combatEvent.EventType)
            {
                case CombatEventType.ReloadStarted:
                    return combatEvent.SourceId == playerRuntimeId
                        && combatEvent.ValueAfter == (int)WeaponState.Reloading
                            ? D0ActorPresentationCommand.PlayerReloadStarted
                            : D0ActorPresentationCommand.None;

                case CombatEventType.ReloadCompleted:
                    return combatEvent.SourceId == playerRuntimeId
                        && combatEvent.ValueBefore == (int)WeaponState.Reloading
                        && combatEvent.ValueAfter != (int)WeaponState.Reloading
                            ? D0ActorPresentationCommand.PlayerReloadCompleted
                            : D0ActorPresentationCommand.None;

                case CombatEventType.InputAccepted:
                    if (combatEvent.SourceId != playerRuntimeId)
                    {
                        return D0ActorPresentationCommand.None;
                    }

                    if (combatEvent.ValueBefore == (int)WeaponState.Ready
                        && combatEvent.ValueAfter == (int)WeaponState.AltCharging)
                    {
                        return D0ActorPresentationCommand.PlayerSecondaryChargeStarted;
                    }

                    if (combatEvent.ValueBefore == (int)WeaponState.AltCharging
                        && combatEvent.ValueAfter == (int)WeaponState.Ready)
                    {
                        return D0ActorPresentationCommand.PlayerSecondaryChargeCanceled;
                    }

                    return (combatEvent.ValueBefore == (int)WeaponState.Ready
                            || combatEvent.ValueBefore == (int)WeaponState.AltCharging)
                        && combatEvent.ValueAfter == (int)WeaponState.AltRecovery
                        && combatEvent.AttackId.IsValid
                            ? D0ActorPresentationCommand.PlayerSecondaryReleaseCommitted
                            : D0ActorPresentationCommand.None;

                case CombatEventType.AttackCanceled:
                    return combatEvent.SourceId == playerRuntimeId
                        && combatEvent.ValueBefore == (int)WeaponState.AltCharging
                            ? D0ActorPresentationCommand.PlayerSecondaryChargeCanceled
                            : D0ActorPresentationCommand.None;

                case CombatEventType.DamageApplied:
                    if (combatEvent.ValueBefore <= combatEvent.ValueAfter)
                    {
                        return D0ActorPresentationCommand.None;
                    }

                    if (combatEvent.TargetId == playerRuntimeId)
                    {
                        return combatEvent.DamageChannel == DamageChannel.Life
                            ? D0ActorPresentationCommand.PlayerHit
                            : D0ActorPresentationCommand.None;
                    }

                    return combatEvent.TargetId == enemyRuntimeId && !enemyGroggy
                        ? D0ActorPresentationCommand.EnemyHit
                        : D0ActorPresentationCommand.None;

                // The combat domain has no player Groggy state. A barrier
                // break is the explicit D0 presentation convention for Fei's
                // short loss-of-balance animation; it never feeds back into
                // the player state machine.
                case CombatEventType.BarrierBroken:
                    return combatEvent.TargetId == playerRuntimeId
                        ? D0ActorPresentationCommand.PlayerGroggy
                        : D0ActorPresentationCommand.None;

                case CombatEventType.GroggyStarted:
                    return combatEvent.TargetId == enemyRuntimeId
                        ? D0ActorPresentationCommand.EnemyGroggyStarted
                        : D0ActorPresentationCommand.None;

                case CombatEventType.GroggyEnded:
                    return combatEvent.TargetId == enemyRuntimeId
                        ? D0ActorPresentationCommand.EnemyGroggyEnded
                        : D0ActorPresentationCommand.None;

                case CombatEventType.Death:
                    return combatEvent.TargetId == enemyRuntimeId
                        ? D0ActorPresentationCommand.EnemyDeath
                        : combatEvent.TargetId == playerRuntimeId
                            ? D0ActorPresentationCommand.PlayerDefeat
                            : D0ActorPresentationCommand.None;

                case CombatEventType.BattleCompleted:
                    switch ((BattleCompletionReason)combatEvent.ValueAfter)
                    {
                        case BattleCompletionReason.Victory:
                            return D0ActorPresentationCommand.PlayerVictory;
                        case BattleCompletionReason.Defeat:
                            return D0ActorPresentationCommand.PlayerDefeat;
                        default:
                            return D0ActorPresentationCommand.None;
                    }

                default:
                    return D0ActorPresentationCommand.None;
            }
        }
    }

    /// <summary>
    /// Binds the explicit Fei/Burstbug presenters to a battle session and
    /// applies only commands resolved from its committed trace. It contains no
    /// combat rule, collision query, or mutable simulation data.
    /// </summary>
    internal sealed class D0ActorPresentationRouter
    {
        private Actor2DPresenter playerActor;
        private Actor2DPresenter enemyActor;
        private RuntimeId playerRuntimeId;
        private RuntimeId enemyRuntimeId;
        private bool isBound;
        private bool enemyGroggy;

        public bool IsConfigured => playerActor != null && enemyActor != null;

        public bool TryConfigure(
            Actor2DPresenter nextPlayerActor,
            Actor2DPresenter nextEnemyActor,
            out string error)
        {
            if (nextPlayerActor == null && nextEnemyActor == null)
            {
                ClearTransientState();
                playerActor = null;
                enemyActor = null;
                error = string.Empty;
                return true;
            }

            if (nextPlayerActor == null || nextEnemyActor == null)
            {
                error = "D0 actor presentation requires both the Fei and Burstbug presenters.";
                return false;
            }

            if (!nextPlayerActor.IsPlayerActor || nextEnemyActor.IsPlayerActor)
            {
                error = "D0 actor presentation requires Fei as player and Burstbug as enemy.";
                return false;
            }

            if (!nextPlayerActor.TryValidate(out error)
                || !nextEnemyActor.TryValidate(out error))
            {
                return false;
            }

            if (playerActor != nextPlayerActor || enemyActor != nextEnemyActor)
            {
                ClearTransientState();
            }

            playerActor = nextPlayerActor;
            enemyActor = nextEnemyActor;
            error = string.Empty;
            return true;
        }

        public bool TryBind(
            RuntimeId nextPlayerRuntimeId,
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!IsConfigured)
            {
                // Non-D0 scene contracts intentionally have no actor bridge.
                error = string.Empty;
                return true;
            }

            if (!nextPlayerRuntimeId.IsValid || !nextEnemyRuntimeId.IsValid
                || nextPlayerRuntimeId == nextEnemyRuntimeId)
            {
                error = "D0 actor presentation requires distinct valid player and enemy runtime IDs.";
                return false;
            }

            ClearTransientState();
            if (!playerActor.TryInitialize(out error)
                || !enemyActor.TryInitialize(out error))
            {
                return false;
            }

            playerRuntimeId = nextPlayerRuntimeId;
            enemyRuntimeId = nextEnemyRuntimeId;
            isBound = true;
            enemyActor.PlayEnemyEnter();
            error = string.Empty;
            return true;
        }

        public bool TryReplaceEnemyActor(
            Actor2DPresenter nextEnemyActor,
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!IsConfigured || !isBound)
            {
                error = "D0 actor presentation must be configured and bound before replacing its enemy presenter.";
                return false;
            }

            if (nextEnemyActor == null || nextEnemyActor.IsPlayerActor
                || !nextEnemyRuntimeId.IsValid
                || nextEnemyRuntimeId == playerRuntimeId)
            {
                error = "D0 actor presentation requires a valid enemy presenter and RuntimeId.";
                return false;
            }

            if (nextEnemyActor == enemyActor)
            {
                return TryRebindEnemyRuntimeId(nextEnemyRuntimeId, out error);
            }

            if (!nextEnemyActor.TryValidate(out error)
                || !nextEnemyActor.TryInitialize(out error))
            {
                return false;
            }

            enemyActor = nextEnemyActor;
            enemyRuntimeId = nextEnemyRuntimeId;
            enemyGroggy = false;
            enemyActor.PlayEnemyEnter();
            error = string.Empty;
            return true;
        }

        public bool TryRebindEnemyRuntimeId(
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!IsConfigured)
            {
                error = string.Empty;
                return true;
            }

            if (!isBound)
            {
                error = "D0 actor presentation must be bound before enemy rebinding.";
                return false;
            }

            if (!nextEnemyRuntimeId.IsValid || nextEnemyRuntimeId == playerRuntimeId)
            {
                error = "D0 actor presentation requires a valid enemy RuntimeId distinct from the player.";
                return false;
            }

            enemyRuntimeId = nextEnemyRuntimeId;
            enemyGroggy = false;
            error = string.Empty;
            return true;
        }

        public void Consume(in CombatEvent combatEvent)
        {
            if (!isBound)
            {
                return;
            }

            switch (D0ActorPresentationRouting.ResolveCommand(
                        combatEvent,
                        playerRuntimeId,
                        enemyRuntimeId,
                        enemyGroggy))
            {
                case D0ActorPresentationCommand.PlayerReloadStarted:
                    playerActor.BeginReload();
                    break;

                case D0ActorPresentationCommand.PlayerReloadCompleted:
                    playerActor.CompleteReload();
                    break;

                case D0ActorPresentationCommand.PlayerSecondaryChargeStarted:
                    playerActor.BeginSecondaryCharge();
                    break;

                case D0ActorPresentationCommand.PlayerSecondaryChargeCanceled:
                    playerActor.CancelSecondaryCharge();
                    break;

                case D0ActorPresentationCommand.PlayerSecondaryReleaseCommitted:
                    // The compact actor trace can omit the earlier charge-start
                    // event. The presenter state machine accepts a committed
                    // release from Idle and remains the sole transition guard.
                    playerActor.PlaySecondaryRelease();
                    break;

                case D0ActorPresentationCommand.PlayerHit:
                    playerActor.PlayHit();
                    break;

                case D0ActorPresentationCommand.PlayerGroggy:
                    playerActor.PlayGroggy();
                    break;

                case D0ActorPresentationCommand.EnemyHit:
                    enemyActor.PlayHit();
                    break;

                case D0ActorPresentationCommand.EnemyGroggyStarted:
                    enemyGroggy = true;
                    enemyActor.PlayGroggy();
                    break;

                case D0ActorPresentationCommand.EnemyGroggyEnded:
                    enemyGroggy = false;
                    enemyActor.ReturnToIdle();
                    break;

                case D0ActorPresentationCommand.EnemyDeath:
                    enemyGroggy = false;
                    enemyActor.PlayEnemyDeath();
                    break;

                case D0ActorPresentationCommand.PlayerVictory:
                    playerActor.PlayVictory();
                    break;

                case D0ActorPresentationCommand.PlayerDefeat:
                    playerActor.PlayDefeat();
                    break;
            }
        }

        /// <summary>
        /// A trace gap cannot safely replay short events. Restore only durable
        /// terminal/groggy/charge/reload state from the session snapshot and leave all
        /// other actors in their neutral idle state.
        /// </summary>
        public void Resynchronize(in FinalSnapshot snapshot, WeaponState playerWeaponState)
        {
            if (!isBound)
            {
                return;
            }

            ClearActorPresentation();
            if (snapshot.State == BattleSessionState.Completed)
            {
                switch (snapshot.CompletionReason)
                {
                    case BattleCompletionReason.Victory:
                        enemyActor.PlayEnemyDeath();
                        playerActor.PlayVictory();
                        return;

                    case BattleCompletionReason.Defeat:
                        playerActor.PlayDefeat();
                        return;
                }
            }

            if (snapshot.EnemyControlState == EnemyControlState.Groggy)
            {
                enemyGroggy = true;
                enemyActor.PlayGroggy();
            }

            if (playerWeaponState == WeaponState.Reloading)
            {
                playerActor.BeginReload();
            }
            else if (playerWeaponState == WeaponState.AltCharging)
            {
                playerActor.BeginSecondaryCharge();
            }
            else
            {
                playerActor.ReturnToIdle();
            }
        }

        public void SetPaused(bool paused)
        {
            if (!IsConfigured)
            {
                return;
            }

            playerActor.SetPaused(paused);
            enemyActor.SetPaused(paused);
        }

        public void ClearTransientState()
        {
            isBound = false;
            playerRuntimeId = RuntimeId.Invalid;
            enemyRuntimeId = RuntimeId.Invalid;
            ClearActorPresentation();
        }

        private void ClearActorPresentation()
        {
            enemyGroggy = false;
            playerActor?.ClearAndReturnToIdle();
            enemyActor?.ClearAndReturnToIdle();
        }

        public void Reset()
        {
            ClearTransientState();
            playerActor = null;
            enemyActor = null;
        }
    }
}
