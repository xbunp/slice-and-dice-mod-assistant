using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ModPackageHelper
{
    public static string[] GetHeroNames()
    {
        if (ModPackage.Instance == null || ModPackage.Instance.Heroes == null || ModPackage.Instance.Heroes.Count == 0)
            return new[] { "Default Hero (Builder Fallback)" };
        return ModPackage.Instance.Heroes.Select(h => h.entityName).ToArray();
    }

    public static string[] GetMonsterNames()
    {
        if (ModPackage.Instance == null || ModPackage.Instance.Monsters == null || ModPackage.Instance.Monsters.Count == 0)
            return new[] { "rat", "goblin", "slime" }; // Standard S&D built-in fallbacks
        return ModPackage.Instance.Monsters.Select(m => m.entityName).ToArray();
    }

    public static string[] GetItemNames()
    {
        if (ModPackage.Instance == null || ModPackage.Instance.CustomItems == null || ModPackage.Instance.CustomItems.Count == 0)
            return new[] { "Default Item (Builder Fallback)" };
        return ModPackage.Instance.CustomItems.Select(i => i.entityName).ToArray();
    }

    public static string[] GetAllEntityNames()
    {
        List<string> all = new List<string>();
        all.AddRange(GetHeroNames());
        all.AddRange(GetMonsterNames());
        all.AddRange(GetItemNames());
        return all.ToArray();
    }
}
