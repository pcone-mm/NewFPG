using NUnit.Framework;

namespace FPG.Demo.Unity.Tests.EditMode
{
    public sealed class D0G6EvidenceAutoplayDriverTests
    {
        [Test]
        public void AutoplayArgumentIsExplicitAndCaseInsensitive()
        {
            Assert.That(D0G6EvidenceAutoplayDriver.IsRequested(null), Is.False);
            Assert.That(
                D0G6EvidenceAutoplayDriver.IsRequested(new[] { "FPGDemo_D0.exe" }),
                Is.False);
            Assert.That(
                D0G6EvidenceAutoplayDriver.IsRequested(
                    new[] { "FPGDemo_D0.exe", "-D0-G6-AUTOPLAY" }),
                Is.True);
            Assert.That(D0G6EvidenceAutoplayDriver.RequiredLoopCount, Is.EqualTo(10));
        }
    }
}
