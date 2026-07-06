using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatVitals))]
    public sealed class CombatVitalsAuthoring : MonoBehaviour
    {
        [SerializeField] private CombatVitalsSettings settings = new CombatVitalsSettings();
        [SerializeField] private bool applyOnAwake = true;

        public CombatVitalsSettings Settings => settings;
        public bool ApplyOnAwake
        {
            get => applyOnAwake;
            set => applyOnAwake = value;
        }

        private void Awake()
        {
            if (applyOnAwake)
            {
                Apply(true);
            }
        }

        public void Apply(bool resetVitals)
        {
            CombatVitals vitals = GetComponent<CombatVitals>();
            if (vitals != null)
            {
                vitals.ApplySettings(settings, resetVitals);
            }
        }

        private void OnValidate()
        {
            if (settings == null)
            {
                settings = new CombatVitalsSettings();
            }

            settings.Normalize();
        }
    }
}
