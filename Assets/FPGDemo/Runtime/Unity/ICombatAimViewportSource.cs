using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Unity-presentation boundary for the virtual combat cursor. It intentionally
    /// exposes only a normalized viewport coordinate and never writes gameplay
    /// state into the deterministic battle assemblies.
    /// </summary>
    public interface ICombatAimViewportSource
    {
        bool TryGetViewport(out Vector2 viewport);
    }
}
