using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum FpgEnemyBehaviorMode
    {
        FixedPosition = 0,
        Patrol = 1,
        Chase = 2
    }

    public enum FpgEnemyAttackKind
    {
        Projectile = 0,
        TimedImpact = 1,
        Summon = 2
    }

}
