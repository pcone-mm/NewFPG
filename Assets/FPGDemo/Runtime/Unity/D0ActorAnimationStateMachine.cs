namespace FPG.Demo.Unity
{
    /// <summary>
    /// Durable presentation state owned by one D0 actor. This deliberately
    /// does not model the duration of individual Spine clips: transient clips
    /// are queued by the presenter, while this type protects the gameplay
    /// relevant presentation modes (charge, groggy and terminal outcomes).
    /// </summary>
    public enum D0ActorAnimationState
    {
        Uninitialized = 0,
        Idle,
        SecondaryCharging,
        EnemyGroggy,
        Victory,
        Defeat,
        EnemyDead,
        Reloading
    }

    internal enum D0ActorAnimationCommand
    {
        Initialize = 0,
        Reset,
        ReturnToIdle,
        PrimaryAttack,
        BeginReload,
        CompleteReload,
        BeginSecondaryCharge,
        CancelSecondaryCharge,
        ReleaseSecondary,
        Hit,
        PlayerGroggy,
        EnemyGroggyStarted,
        EnemyGroggyEnded,
        EnemyEnter,
        EnemyFastThreat,
        EnemyVolleyThreat,
        PlayerVictory,
        PlayerDefeat,
        EnemyDeath
    }

    /// <summary>
    /// Presentation-only transition policy for Actor2DPresenter. It contains
    /// no combat state and no Spine dependency, so battle outcomes cannot be
    /// decided by an animation callback or an animation name.
    /// </summary>
    internal sealed class D0ActorAnimationStateMachine
    {
        private readonly bool playerActor;

        public D0ActorAnimationStateMachine(bool playerActor)
        {
            this.playerActor = playerActor;
            State = D0ActorAnimationState.Uninitialized;
        }

        public D0ActorAnimationState State { get; private set; }

        public bool IsChargingSecondary
        {
            get { return State == D0ActorAnimationState.SecondaryCharging; }
        }

        public bool IsReloading
        {
            get { return State == D0ActorAnimationState.Reloading; }
        }

        public bool IsTerminal
        {
            get
            {
                return State == D0ActorAnimationState.Victory
                    || State == D0ActorAnimationState.Defeat
                    || State == D0ActorAnimationState.EnemyDead;
            }
        }

        public bool TryApply(D0ActorAnimationCommand command)
        {
            if (command == D0ActorAnimationCommand.Initialize
                || command == D0ActorAnimationCommand.Reset)
            {
                State = D0ActorAnimationState.Idle;
                return true;
            }

            if (State == D0ActorAnimationState.Uninitialized || IsTerminal)
            {
                return false;
            }

            switch (command)
            {
                case D0ActorAnimationCommand.ReturnToIdle:
                    State = D0ActorAnimationState.Idle;
                    return true;

                case D0ActorAnimationCommand.PrimaryAttack:
                    if (!playerActor)
                    {
                        return false;
                    }

                    // A committed primary shot interrupts a local-only charge
                    // pose; the battle runtime remains the source of truth for
                    // weapon permission and damage.
                    State = D0ActorAnimationState.Idle;
                    return true;

                case D0ActorAnimationCommand.BeginReload:
                    if (!playerActor || State != D0ActorAnimationState.Idle)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.Reloading;
                    return true;

                case D0ActorAnimationCommand.CompleteReload:
                    if (!playerActor || State != D0ActorAnimationState.Reloading)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.Idle;
                    return true;

                case D0ActorAnimationCommand.BeginSecondaryCharge:
                    if (!playerActor || State != D0ActorAnimationState.Idle)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.SecondaryCharging;
                    return true;

                case D0ActorAnimationCommand.CancelSecondaryCharge:
                    if (!playerActor || State != D0ActorAnimationState.SecondaryCharging)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.Idle;
                    return true;

                case D0ActorAnimationCommand.ReleaseSecondary:
                    if (!playerActor)
                    {
                        return false;
                    }

                    // The committed shot feed can arrive before the compact
                    // actor trace that started the local charge pose. Preserve
                    // the release presentation in that case rather than
                    // making visual ordering affect an already-valid shot.
                    State = D0ActorAnimationState.Idle;
                    return true;

                case D0ActorAnimationCommand.Hit:
                    // A one-shot hit reaction is allowed for both actors, but
                    // cannot interrupt the enemy's durable Break presentation.
                    if (!playerActor && State == D0ActorAnimationState.EnemyGroggy)
                    {
                        return false;
                    }

                    if (!playerActor)
                    {
                        State = D0ActorAnimationState.Idle;
                    }
                    else if (State == D0ActorAnimationState.Reloading)
                    {
                        // Only a committed Life-channel hit reaches this
                        // command. BattleSession has already canceled the domain
                        // reload, so presentation mirrors that interruption.
                        State = D0ActorAnimationState.Idle;
                    }

                    return true;

                case D0ActorAnimationCommand.PlayerGroggy:
                    return playerActor;

                case D0ActorAnimationCommand.EnemyGroggyStarted:
                    if (playerActor)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.EnemyGroggy;
                    return true;

                case D0ActorAnimationCommand.EnemyGroggyEnded:
                    if (playerActor || State != D0ActorAnimationState.EnemyGroggy)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.Idle;
                    return true;

                case D0ActorAnimationCommand.EnemyEnter:
                case D0ActorAnimationCommand.EnemyFastThreat:
                case D0ActorAnimationCommand.EnemyVolleyThreat:
                    return !playerActor && State != D0ActorAnimationState.EnemyGroggy;

                case D0ActorAnimationCommand.PlayerVictory:
                    if (!playerActor)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.Victory;
                    return true;

                case D0ActorAnimationCommand.PlayerDefeat:
                    if (!playerActor)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.Defeat;
                    return true;

                case D0ActorAnimationCommand.EnemyDeath:
                    if (playerActor)
                    {
                        return false;
                    }

                    State = D0ActorAnimationState.EnemyDead;
                    return true;

                default:
                    return false;
            }
        }
    }
}
