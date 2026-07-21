namespace FPG.Demo.Unity
{
    /// <summary>
    /// Composition lifecycle aliases kept outside the tick driver's hot path.
    /// Clear() remains the IFpgFormalPlayerTickDriver restart hook, while these
    /// methods make binding teardown and runtime reset explicit to composers.
    /// </summary>
    public static class FpgFormalPlayerTickDriverLifecycleExtensions
    {
        public static void ClearPlayerBinding(
            this FpgFormalPlayerTickDriver driver)
        {
            driver?.ClearPlayerBinding();
        }

        public static void ResetRuntimeState(
            this FpgFormalPlayerTickDriver driver)
        {
            driver?.Clear();
        }
    }
}


