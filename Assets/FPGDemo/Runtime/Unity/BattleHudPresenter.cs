using FPG.Demo.Run;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Unity
{
    [DisallowMultipleComponent]
    public sealed class BattleHudPresenter : MonoBehaviour
    {
        public const string PlaytestPrompt = "RMB 瞄准、LMB 主射、E 蓄力/释放、R 换弹、Esc 暂停、F5 重开；快弹缩回、慢弹转火、重预警打弱点";

        [SerializeField]
        private Image playerLifeFill;

        [SerializeField]
        private Image playerBarrierFill;

        [SerializeField]
        private Image enemyLifeFill;

        [SerializeField]
        private Image enemyBreakFill;

        [SerializeField]
        private Text playerLifeText;

        [SerializeField]
        private Text playerBarrierText;

        [SerializeField]
        private Text ammoText;

        [SerializeField]
        private Text enemyLifeText;

        [SerializeField]
        private Text enemyBreakText;

        [SerializeField]
        private Text stateText;

        [SerializeField]
        private Text promptText;

        private int lastPlayerLife = int.MinValue;
        private int lastPlayerBarrier = int.MinValue;
        private int lastPlayerAmmo = int.MinValue;
        private int lastEnemyLife = int.MinValue;
        private int lastEnemyBreak = int.MinValue;
        private BattleSessionState lastState = (BattleSessionState)(-1);
        private BattleCompletionReason lastCompletionReason = (BattleCompletionReason)(-1);

        public bool TryValidate(out string error)
        {
            if (playerLifeFill == null || playerBarrierFill == null
                || enemyLifeFill == null || enemyBreakFill == null
                || playerLifeText == null || playerBarrierText == null || ammoText == null
                || enemyLifeText == null || enemyBreakText == null || stateText == null
                || promptText == null)
            {
                error = "BattleHudPresenter requires all bar and text references.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Refresh(in FinalSnapshot snapshot, ScenarioDefinition definition)
        {
            if (definition == null)
            {
                Clear();
                return;
            }

            if (snapshot.PlayerLife != lastPlayerLife)
            {
                SetFill(playerLifeFill, snapshot.PlayerLife, definition.PlayerLife);
                SetText(playerLifeText, $"LIFE {snapshot.PlayerLife} / {definition.PlayerLife}");
                lastPlayerLife = snapshot.PlayerLife;
            }

            if (snapshot.PlayerBarrier != lastPlayerBarrier)
            {
                SetFill(playerBarrierFill, snapshot.PlayerBarrier, definition.PlayerBarrier);
                SetText(playerBarrierText, $"BARRIER {snapshot.PlayerBarrier} / {definition.PlayerBarrier}");
                lastPlayerBarrier = snapshot.PlayerBarrier;
            }

            if (snapshot.PlayerAmmo != lastPlayerAmmo)
            {
                SetText(ammoText, $"AMMO {snapshot.PlayerAmmo} / {definition.PlayerWeapon.MagazineCapacity}");
                lastPlayerAmmo = snapshot.PlayerAmmo;
            }

            int enemyMaxLife = snapshot.EnemyMaxLife > 0
                ? snapshot.EnemyMaxLife
                : definition.EnemyLife;
            int enemyMaxBreak = snapshot.EnemyMaxBreak > 0
                ? snapshot.EnemyMaxBreak
                : definition.EnemyBreak;
            if (snapshot.EnemyLife != lastEnemyLife)
            {
                SetFill(enemyLifeFill, snapshot.EnemyLife, enemyMaxLife);
                SetText(enemyLifeText, $"ENEMY {snapshot.EnemyLife} / {enemyMaxLife}");
                lastEnemyLife = snapshot.EnemyLife;
            }

            if (snapshot.EnemyBreak != lastEnemyBreak)
            {
                SetFill(enemyBreakFill, snapshot.EnemyBreak, enemyMaxBreak);
                SetText(enemyBreakText, $"BREAK {snapshot.EnemyBreak} / {enemyMaxBreak}");
                lastEnemyBreak = snapshot.EnemyBreak;
            }

            if (snapshot.State != lastState || snapshot.CompletionReason != lastCompletionReason)
            {
                SetText(stateText, FormatState(snapshot.State, snapshot.CompletionReason));
                lastState = snapshot.State;
                lastCompletionReason = snapshot.CompletionReason;
            }

            SetText(promptText, PlaytestPrompt);
        }

        public void Clear()
        {
            SetFill(playerLifeFill, 0, 1);
            SetFill(playerBarrierFill, 0, 1);
            SetFill(enemyLifeFill, 0, 1);
            SetFill(enemyBreakFill, 0, 1);
            SetText(playerLifeText, "LIFE --");
            SetText(playerBarrierText, "BARRIER --");
            SetText(ammoText, "AMMO --");
            SetText(enemyLifeText, "ENEMY --");
            SetText(enemyBreakText, "BREAK --");
            SetText(stateText, "BATTLE UNAVAILABLE");
            SetText(promptText, "");
            lastPlayerLife = int.MinValue;
            lastPlayerBarrier = int.MinValue;
            lastPlayerAmmo = int.MinValue;
            lastEnemyLife = int.MinValue;
            lastEnemyBreak = int.MinValue;
            lastState = (BattleSessionState)(-1);
            lastCompletionReason = (BattleCompletionReason)(-1);
        }

        private static void SetFill(Image image, int value, int maximum)
        {
            if (image != null)
            {
                image.fillAmount = maximum <= 0 ? 0f : Mathf.Clamp01(value / (float)maximum);
            }
        }

        private static void SetText(Text text, string value)
        {
            if (text != null && text.text != value)
            {
                text.text = value;
            }
        }

        private static string FormatState(BattleSessionState state, BattleCompletionReason reason)
        {
            switch (state)
            {
                case BattleSessionState.Running:
                    return "RUNNING";
                case BattleSessionState.Paused:
                    return "PAUSED";
                case BattleSessionState.Completed:
                    return reason == BattleCompletionReason.Victory ? "VICTORY" : "DEFEAT";
                case BattleSessionState.Faulted:
                    return "FAULTED";
                default:
                    return state.ToString().ToUpperInvariant();
            }
        }
    }
}
