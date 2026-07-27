using System;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    public enum FpgRunFlowState
    {
        Running = 0,
        AwaitingExit = 1,
        Transitioning = 2,
        Faulted = 3,
        RecoverableFault = 4
    }

    public sealed class FpgRunFlowController : IDisposable
    {
        private GameBootstrap owner;
        private FpgFormalEncounterHost formalHost;
        private FpgRoomEncounterDirector director;

        public FpgRunFlowState State { get; private set; } =
            FpgRunFlowState.Running;

        public string LastError { get; private set; } = string.Empty;

        public bool TryBind(
            GameBootstrap bootstrap,
            FpgFormalEncounterHost host,
            out string error)
        {
            Unsubscribe();
            if (bootstrap == null || host == null || host.EncounterDirector == null)
            {
                error = "Run flow requires an explicit Bootstrap and formal encounter host.";
                SetFault(error);
                return false;
            }

            owner = bootstrap;
            formalHost = host;
            director = host.EncounterDirector;
            formalHost.RoomCleared += HandleRoomCleared;
            director.ExitOfferSelected += HandleExitOfferSelected;
            director.Failed += HandleDirectorFailed;
            director.RestartSucceeded += HandleDirectorRestartSucceeded;
            State = FpgRunFlowState.Running;
            LastError = string.Empty;
            error = string.Empty;
            return true;
        }

        public bool TryMarkAwaitingExit(out string error)
        {
            if (State != FpgRunFlowState.Running)
            {
                error = "Run flow is not running.";
                return false;
            }

            State = FpgRunFlowState.AwaitingExit;
            error = string.Empty;
            return true;
        }

        public bool TryBeginTransition(out string error)
        {
            if (State != FpgRunFlowState.AwaitingExit)
            {
                error = "Run flow is not awaiting an exit.";
                return false;
            }

            State = FpgRunFlowState.Transitioning;
            Unsubscribe();
            error = string.Empty;
            return true;
        }

        public void SetFault(string error)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Run flow failed."
                : error;
            State = FpgRunFlowState.Faulted;
            Unsubscribe();
        }

        public void SetRecoverableFault(string error)
        {
            LastError = string.IsNullOrWhiteSpace(error)
                ? "Run flow encountered a recoverable runtime fault."
                : error;
            State = FpgRunFlowState.RecoverableFault;
        }

        public void Dispose()
        {
            Unsubscribe();
            owner = null;
            State = FpgRunFlowState.Faulted;
        }

        private void HandleRoomCleared(FpgRoomClearedEvent clearedEvent)
        {
            owner?.HandleRunFlowRoomCleared(this, clearedEvent);
        }

        private void HandleDirectorRestartSucceeded()
        {
            State = FpgRunFlowState.Running;
            LastError = string.Empty;
            owner?.HandleRunFlowRestarted(this);
        }

        private void HandleExitOfferSelected(FpgExitSelectionEvent selectionEvent)
        {
            owner?.HandleRunFlowExitSelected(this, selectionEvent);
        }

        private void HandleDirectorFailed(
            FpgEncounterFailureReason reason,
            string message)
        {
            owner?.HandleRunFlowFailed(this, reason, message);
        }

        private void Unsubscribe()
        {
            if (formalHost != null)
            {
                formalHost.RoomCleared -= HandleRoomCleared;
            }

            if (director != null)
            {
                director.ExitOfferSelected -= HandleExitOfferSelected;
                director.Failed -= HandleDirectorFailed;
                director.RestartSucceeded -= HandleDirectorRestartSucceeded;
            }

            formalHost = null;
            director = null;
        }
    }
}
