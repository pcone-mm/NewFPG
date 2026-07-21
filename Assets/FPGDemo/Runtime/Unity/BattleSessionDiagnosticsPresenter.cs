using System.Text;
using FPG.Demo.Combat;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class BattleSessionDiagnosticsPresenter : MonoBehaviour
    {
        private readonly StringBuilder builder = new StringBuilder(256);

        [SerializeField]
        private BattleSessionHost sessionHost;

        [SerializeField]
        private Rect screenRect = new Rect(12f, 12f, 460f, 132f);

        [SerializeField]
        private bool showOnGui = true;

        public BattleSessionHost SessionHost => sessionHost;

        /// <summary>
        /// Keeps diagnostics data available to automation while allowing the
        /// legacy OnGUI overlay to be hidden when the playable HUD is active.
        /// </summary>
        public bool ShowOnGui
        {
            get => showOnGui;
            set => showOnGui = value;
        }

        public string CurrentText { get; private set; } = "BattleSession unavailable";

        public bool TryValidate(out string error)
        {
            if (sessionHost == null)
            {
                error = "BattleSessionDiagnosticsPresenter requires a BattleSessionHost reference.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public string RefreshText()
        {
            BattleSession session = sessionHost == null ? null : sessionHost.Session;
            if (session == null)
            {
                CurrentText = "BattleSession unavailable";
                return CurrentText;
            }

            FinalSnapshot snapshot = session.GetFinalSnapshot();
            builder.Clear();
            builder.Append("State: ").Append(session.State)
                .Append("  Tick: ").Append(session.CurrentTick.Value)
                .Append("  Executed: ").Append(session.ExecutedTickCount)
                .AppendLine();
            builder.Append("Ammo: ").Append(snapshot.PlayerAmmo)
                .Append("  Pending impacts: ").Append(session.PendingImpactCount)
                .Append("  Active projectiles: ").Append(session.ActiveProjectileCount)
                .AppendLine();
            builder.Append("Trace: ").Append(session.Trace.TotalEventCount)
                .Append(" events");

            if (session.Trace.Count > 0)
            {
                CombatEvent latest = session.Trace.GetOldest(session.Trace.Count - 1);
                builder.Append("  Last #").Append(latest.Sequence)
                    .Append(' ')
                    .Append(latest.EventType)
                    .Append(" @ tick ")
                    .Append(latest.Tick.Value);
            }

            CurrentText = builder.ToString();
            return CurrentText;
        }

        private void LateUpdate()
        {
            // The formal D0 HUD owns its own F3 development overlay.  When
            // this legacy IMGUI surface is hidden, rebuilding its diagnostic
            // string every rendered frame produces avoidable managed garbage
            // without any player-visible consumer.  CombatHud2DPresenter
            // explicitly refreshes the text while its overlay is open.
            if (showOnGui)
            {
                RefreshText();
            }
        }

        private void OnGUI()
        {
            if (showOnGui)
            {
                GUI.Box(screenRect, CurrentText);
            }
        }
    }
}
