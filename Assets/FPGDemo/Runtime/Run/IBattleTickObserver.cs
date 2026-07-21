using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Optional scene-facing hook invoked immediately before a deterministic
    /// battle tick executes. Implementations may synchronize external spatial
    /// representations, but must never mutate BattleSession state or enqueue
    /// combat commands.
    /// </summary>
    public interface IBattleTickObserver
    {
        void BeforeBattleTick(BattleSession session, TickIndex tick);
    }
}
