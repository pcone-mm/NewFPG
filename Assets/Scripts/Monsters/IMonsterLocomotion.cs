using UnityEngine;

namespace NewFPG.Monsters
{
    public interface IMonsterLocomotion
    {
        Transform Target { get; set; }
        bool HasArrived { get; }
        void SetMovementEnabled(bool enabled);
        bool TryMoveTo(Vector3 destination);
        void Stop();
    }
}
