using System.Collections.Generic;
using System.Text;
using SliceDiceTextMod;

public static class ModSerializer
{
    public static string Export(ModDataContainer modData)
    {
        if (modData == null) return "";

        List<string> blocks = new List<string>();

        // 1. Compile active entities
        if (modData.ActiveHero != null && !string.IsNullOrEmpty(modData.ActiveHero.entityName))
        {
            blocks.Add(HeroData.Export(modData.ActiveHero));
        }

        // 2. Compile structural rules
        foreach (var directive in modData.Directives)
        {
            string syntax = SerializeDirective(directive);
            if (!string.IsNullOrWhiteSpace(syntax))
            {
                blocks.Add(syntax);
            }
        }

        return string.Join("&", blocks);
    }

    private static string SerializeDirective(ModDirectiveData data)
    {
        string core = "";

        if (data is PoolDirectiveData pool)
        {
            string prefix = pool is HeroPoolData hp && hp.ReplaceBaseHeroes ? "replace.heropool" :
                            pool is HeroPoolData ? "heropool" :
                            pool is MonsterPoolData ? "monsterpool" :
                            pool is ItemPoolData ? "itempool" :
                            pool is ForcedFightData ? "fight" :
                            pool is SpawnInjectionData ? "add" : "party";

            if (pool.Elements.Count > 0)
            {
                core = $"{prefix}.{string.Join("+", pool.Elements)}";
            }
        }
        else if (data is ConfigDirectiveData config)
        {
            core = $"{config.Prefix}.{config.Payload}";
        }
        else if (data is ToggleDirectiveData toggle)
        {
            core = $"tog{toggle.ToggleName}";
        }
        else if (data is CommandDirectiveData cmd)
        {
            core = cmd.Command;
        }
        else if (data is RawDirectiveData raw)
        {
            core = raw.RawContent;
        }

        if (string.IsNullOrEmpty(core)) return "";

        // Wrap structurally
        if (!string.IsNullOrEmpty(data.FloorSelectorRaw)) core = $"{data.FloorSelectorRaw}.{core}";
        if (data.IsHidden) core = $"!m({core})";

        return core;
    }
}