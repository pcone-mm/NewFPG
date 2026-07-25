using FPG.Demo.Core;
using FPG.Demo.Skills;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class FpgSkillClockConfigurationTests
    {
        [Test]
        public void ProjectAndSkillClocksUseSixtyHertz()
        {
            Assert.That(GameplayClock.DefaultTickRate, Is.EqualTo(60));
            Assert.That(FpgSkillRuntimeConstants.TickRate, Is.EqualTo(60));
            Assert.That(
                Time.fixedDeltaTime * FpgSkillRuntimeConstants.TickRate,
                Is.EqualTo(1f).Within(0.00001f));
        }
    }
}
