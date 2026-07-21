namespace FPG.Demo.Core
{
    public enum RandomDomain : ulong
    {
        None = 0UL,
        PelletSpread = 0x50454C4C45545F31UL,
        Encounter = 0x454E434F554E5431UL,
        Reward = 0x5245574152445F31UL,
        Presentation = 0x50524553454E5431UL
    }

    public static class StableHash
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong DomainSeparator = 0xD6E8FEB86659FD93UL;

        public static ulong Mix(ulong value)
        {
            unchecked
            {
                value += GoldenGamma;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public static ulong Combine(ulong seed, ulong domain, ulong owner, ulong ordinal)
        {
            unchecked
            {
                ulong hash = Mix(seed ^ DomainSeparator);
                hash = Mix(hash ^ Mix(domain + 0x100000001B3UL));
                hash = Mix(hash ^ Mix(owner + 0xC2B2AE3D27D4EB4FUL));
                return Mix(hash ^ Mix(ordinal + 0x165667B19E3779F9UL));
            }
        }

        public static ulong Append(ulong hash, ulong value)
        {
            return Mix(hash ^ Mix(value + DomainSeparator));
        }
    }

    public static class DeterministicRandomV1
    {
        public const int Version = 1;

        public static ulong SampleUInt64(ulong scenarioSeed, RandomDomain domain, ulong owner, ulong ordinal)
        {
            return StableHash.Combine(scenarioSeed, (ulong)domain, owner, ordinal);
        }

        public static int SampleUInt24(ulong scenarioSeed, RandomDomain domain, ulong owner, ulong ordinal)
        {
            return (int)(SampleUInt64(scenarioSeed, domain, owner, ordinal) >> 40);
        }
    }
}
