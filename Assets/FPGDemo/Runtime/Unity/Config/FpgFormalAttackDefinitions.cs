using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    // These shared enums intentionally use a MonoScript GUID distinct from
    // FpgEnemyBehaviorDefinition assets.
    public enum FpgEnemyBehaviorMode
    {
        FixedPosition = 0,
        Patrol = 1,
        Chase = 2
    }

}
