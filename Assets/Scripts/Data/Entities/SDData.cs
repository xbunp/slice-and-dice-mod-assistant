using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public abstract class SDData
{
    public string entityName = "NewEntity";
    public string imageOverride = "None";

    [Header("Deep Payloads")]
    public List<CustomPayload> customPayloads = new List<CustomPayload>();
    public List<ItemData> customItems =>
        customPayloads?.Where(p => p.Type == PayloadType.Item).Select(p => p.Data as ItemData).ToList() ?? new List<ItemData>();
    public List<AbilityData> customAbilities =>
        customPayloads?.Where(p => p.Type == PayloadType.Ability).Select(p => p.Data as AbilityData).ToList() ?? new List<AbilityData>();
    public List<HeroData> customHeroes =>
        customPayloads?.Where(p => p.Type == PayloadType.Hero).Select(p => p.Data as HeroData).ToList() ?? new List<HeroData>();
    public List<MonsterData> customMonsters =>
        customPayloads?.Where(p => p.Type == PayloadType.Monster).Select(p => p.Data as MonsterData).ToList() ?? new List<MonsterData>();

    public virtual string Export()
    {
        return $"n.{entityName}.img.{imageOverride}";
    }

    public virtual void Parse(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;

        string[] tokens = data.Split('.');
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            string token = tokens[i].ToLower();
            if (token == "n")
            {
                entityName = tokens[++i];
            }
            else if (token == "img")
            {
                imageOverride = tokens[++i];
            }
        }
    }
}