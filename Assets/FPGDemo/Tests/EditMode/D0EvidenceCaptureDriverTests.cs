using NUnit.Framework;

namespace FPG.Demo.Unity.Tests.EditMode
{
    public sealed class D0EvidenceCaptureDriverTests
    {
        [Test]
        public void EvidenceCaptureArgumentIsExplicitAndCaseInsensitive()
        {
            Assert.That(D0EvidenceCaptureDriver.IsEvidenceCaptureRequested(null), Is.False);
            Assert.That(
                D0EvidenceCaptureDriver.IsEvidenceCaptureRequested(new[] { "FPGDemo_D0.exe" }),
                Is.False);
            Assert.That(
                D0EvidenceCaptureDriver.IsEvidenceCaptureRequested(
                    new[] { "FPGDemo_D0.exe", "-D0-G6-EVIDENCE" }),
                Is.True);
        }

        [TestCase(1, "initial")]
        [TestCase(2, "primary_hit")]
        [TestCase(3, "weakpoint_hit")]
        [TestCase(4, "interceptable_volley")]
        [TestCase(6, "heavy_warning")]
        [TestCase(7, "break")]
        [TestCase(8, "victory")]
        [TestCase(9, "defeat")]
        public void StillCaptureKeysHaveStableAcceptanceLabels(
            int functionKey,
            string expectedLabel)
        {
            Assert.That(
                D0EvidenceCaptureDriver.TryGetStillLabelForFunctionKey(
                    functionKey,
                    out string label),
                Is.True);
            Assert.That(label, Is.EqualTo(expectedLabel));
        }

        [TestCase(5)]
        [TestCase(10)]
        [TestCase(11)]
        public void GameplayAndVideoControlKeysAreNotStillLabels(int functionKey)
        {
            Assert.That(
                D0EvidenceCaptureDriver.TryGetStillLabelForFunctionKey(
                    functionKey,
                    out string label),
                Is.False);
            Assert.That(label, Is.Empty);
        }

        [TestCase("initial")]
        [TestCase("primary_hit")]
        [TestCase("weakpoint_hit")]
        [TestCase("interceptable_volley")]
        [TestCase("heavy_warning")]
        [TestCase("break")]
        [TestCase("victory")]
        [TestCase("defeat")]
        public void AcceptanceStillLabelsAreSafeForPlayerSideFileNames(string label)
        {
            Assert.That(label, Does.Not.Contain("/"));
            Assert.That(label, Does.Not.Contain("\\"));
            Assert.That(label, Does.Not.Contain("."));
        }
    }
}
