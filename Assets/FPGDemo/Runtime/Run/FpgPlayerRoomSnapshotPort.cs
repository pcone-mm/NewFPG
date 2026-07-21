using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Optional boundary for player resources that persist across waves but
    /// return to their room-entry values on a room restart. Implementations
    /// own the concrete player snapshot and never expose PlayerRuntime state.
    /// </summary>
    public interface IFpgPlayerRoomSnapshotPort
    {
        DomainResult CaptureEntrySnapshot();

        DomainResult RestoreEntrySnapshot();

        void KeepAcrossWave();
    }

    public sealed class NullFpgPlayerRoomSnapshotPort : IFpgPlayerRoomSnapshotPort
    {
        public static readonly NullFpgPlayerRoomSnapshotPort Instance =
            new NullFpgPlayerRoomSnapshotPort();

        private NullFpgPlayerRoomSnapshotPort()
        {
        }

        public DomainResult CaptureEntrySnapshot()
        {
            return DomainResult.Success;
        }

        public DomainResult RestoreEntrySnapshot()
        {
            return DomainResult.Success;
        }

        public void KeepAcrossWave()
        {
        }
    }
}
