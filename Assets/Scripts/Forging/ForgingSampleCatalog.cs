using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Forging
{
    public static class ForgingSampleCatalog
    {
        public static List<ForgingWeaponBlueprintDefinition> CreateWeaponBlueprints()
        {
            ForgingCatalog catalog = ForgingCatalogLoader.LoadDefault();
            return catalog.weaponBlueprints != null
                ? new List<ForgingWeaponBlueprintDefinition>(catalog.weaponBlueprints)
                : new List<ForgingWeaponBlueprintDefinition>();
        }

        public static List<ForgingMaterialDefinition> CreateMaterials()
        {
            ForgingCatalog catalog = ForgingCatalogLoader.LoadDefault();
            return catalog.materials != null
                ? new List<ForgingMaterialDefinition>(catalog.materials)
                : new List<ForgingMaterialDefinition>();
        }

        public static ForgingMaterialDefinition CreateStardustGold()
        {
            ForgingMaterialDefinition material = ForgingCatalogLoader.LoadDefault().FindMaterial("stardust_gold");
            return material ?? new ForgingMaterialDefinition();
        }
    }
}
