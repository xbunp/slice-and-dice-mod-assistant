using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Assets/Scripts/Data/ModData/ModPackageHelper.cs

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

        return ModPackage.Instance.Heroes.Select(h => {
            if (!string.IsNullOrEmpty(h.entityName)) return h.entityName;
            string exp = h.Export();
            if (string.IsNullOrEmpty(exp)) return "Unnamed Hero";
            return exp.Length > 45 ? exp.Substring(0, 42) + "..." : exp;
        }).ToArray();
    }

    public static string[] GetMonsterNames()
    {
        if (ModPackage.Instance == null || ModPackage.Instance.Monsters == null || ModPackage.Instance.Monsters.Count == 0)
            return new[] { "wolf", "rat", "goblin" };

        return ModPackage.Instance.Monsters.Select(m => {
            if (!string.IsNullOrEmpty(m.entityName)) return m.entityName;
            string exp = m.Export();
            if (string.IsNullOrEmpty(exp)) return "Unnamed Monster";
            return exp.Length > 45 ? exp.Substring(0, 42) + "..." : exp;
        }).ToArray();
    }

    public static string[] GetItemNames()
    {
        if (ModPackage.Instance == null || ModPackage.Instance.CustomItems == null || ModPackage.Instance.CustomItems.Count == 0)
            return new[] { "Default Item (Builder Fallback)" };

        return ModPackage.Instance.CustomItems.Select(i => {
            if (!string.IsNullOrEmpty(i.entityName)) return i.entityName;

            // Generate a readable fallback name for unnamed items (like complex hat setups)
            string exportStr = i.Export();
            if (string.IsNullOrEmpty(exportStr)) return "Unnamed Item";
            if (exportStr.Length > 45) return exportStr.Substring(0, 42) + "...";
            return exportStr;
        }).ToArray();
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
