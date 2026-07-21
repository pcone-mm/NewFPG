using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Prototype
{
    [CreateAssetMenu(
        fileName = DefaultAssetName,
        menuName = "NewFPG/Prototype/First Person Weapon HUD Layout")]
    public sealed class FirstPersonWeaponLayoutProfile : ScriptableObject
    {
        public const string DefaultAssetName = "FirstPersonWeaponHudLayout";
        public const string DefaultAssetPath = "Assets/Settings/Prototype/" + DefaultAssetName + ".asset";

        [SerializeField] private List<WeaponSlot> weapons = new List<WeaponSlot>
        {
            new WeaponSlot("Left Card", new Vector3(-0.45f, -0.52f, 1.34f), new Vector3(0f, 0f, 9f), 0.34f, 0),
            new WeaponSlot("Left Center Card", new Vector3(-0.15f, -0.47f, 1.24f), new Vector3(0f, 0f, 3f), 0.38f, 2),
            new WeaponSlot("Right Center Card", new Vector3(0.15f, -0.47f, 1.24f), new Vector3(0f, 0f, -3f), 0.38f, 3),
            new WeaponSlot("Right Card", new Vector3(0.45f, -0.52f, 1.34f), new Vector3(0f, 0f, -9f), 0.34f, 1),
        };

        public IReadOnlyList<WeaponSlot> Weapons => weapons;
        public int Count => weapons != null ? weapons.Count : 0;

        public bool TryGetWeapon(int index, out WeaponSlot weapon)
        {
            if (weapons != null && index >= 0 && index < weapons.Count)
            {
                weapon = weapons[index];
                weapon.Normalize(index);
                return true;
            }

            weapon = default;
            return false;
        }

        public void SetWeapon(int index, WeaponSlot weapon)
        {
            if (weapons == null || index < 0 || index >= weapons.Count)
            {
                return;
            }

            weapon.Normalize(index);
            weapons[index] = weapon;
        }

        public void ResetToDefaultLayout()
        {
            weapons = new List<WeaponSlot>
            {
                new WeaponSlot("Left Card", new Vector3(-0.45f, -0.52f, 1.34f), new Vector3(0f, 0f, 9f), 0.34f, 0),
                new WeaponSlot("Left Center Card", new Vector3(-0.15f, -0.47f, 1.24f), new Vector3(0f, 0f, 3f), 0.38f, 2),
                new WeaponSlot("Right Center Card", new Vector3(0.15f, -0.47f, 1.24f), new Vector3(0f, 0f, -3f), 0.38f, 3),
                new WeaponSlot("Right Card", new Vector3(0.45f, -0.52f, 1.34f), new Vector3(0f, 0f, -9f), 0.34f, 1),
            };
        }

        private void OnValidate()
        {
            if (weapons == null)
            {
                weapons = new List<WeaponSlot>();
                return;
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponSlot weapon = weapons[i];
                weapon.Normalize(i);
                weapons[i] = weapon;
            }
        }

        [System.Serializable]
        public struct WeaponSlot
        {
            public string name;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public float width;
            public int sortingOrder;

            public WeaponSlot(
                string name,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                float width,
                int sortingOrder)
            {
                this.name = name;
                this.localPosition = localPosition;
                this.localEulerAngles = localEulerAngles;
                this.width = width;
                this.sortingOrder = sortingOrder;
            }

            public void Normalize(int index)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Weapon " + (index + 1).ToString();
                }

                width = Mathf.Max(0.01f, width);
            }
        }
    }
}
