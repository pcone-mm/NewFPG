using NewFPG.Prototype;
using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatHudDebugBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera battleCamera;
        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;
        [SerializeField] private PrototypeWeaponCombatHud weaponHud;
        [SerializeField] private CombatVitals playerVitals;
        [SerializeField] private CombatResourcePool resourcePool;
        [SerializeField] private PlayerWeaponCaster weaponCaster;
        [SerializeField] private bool keepResourceFull = true;

        private void Awake()
        {
            Bind();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void Update()
        {
            if (keepResourceFull && resourcePool != null && resourcePool.Current < resourcePool.Max)
            {
                resourcePool.Fill();
            }
        }

        public void Bind()
        {
            if (resourcePool != null)
            {
                resourcePool.Fill();
            }

            if (weaponHud != null)
            {
                weaponHud.Bind(playerVitals, resourcePool, weaponCaster);
                if (battleCamera != null)
                {
                    weaponHud.SetAimCamera(battleCamera);
                }

                weaponHud.SetCombatEnabled(true);
            }

            if (weaponView != null && battleCamera != null)
            {
                weaponView.SetWorldCamera(battleCamera);
                weaponView.RefreshRuntimeView(battleCamera);
            }

            if (weaponCaster != null)
            {
                weaponCaster.SetCombatEnabled(true);
            }
        }
    }
}
