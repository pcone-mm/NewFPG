using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class GameBootstrapConfigTests
    {
        [Test]
        public void DefaultsMatchTheFormalRoomFlow()
        {
            GameBootstrapConfig config = ScriptableObject.CreateInstance<GameBootstrapConfig>();

            try
            {
                string error;
                Assert.That(config.TryValidate(out error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(config.RoomSceneName, Is.EqualTo("FormalRoom"));
                Assert.That(config.FrameRateMode, Is.EqualTo(FrameRateMode.Locked));
                Assert.That(config.LockedFramesPerSecond, Is.EqualTo(60));
                Assert.That(config.VSyncCount, Is.Zero);
                Assert.That(config.LoadRoomOnStart, Is.True);
                Assert.That(config.RequireCharacterSelection, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
