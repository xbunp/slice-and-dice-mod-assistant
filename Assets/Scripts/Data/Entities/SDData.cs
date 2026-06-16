using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public abstract class SDData
{
    public string entityName = "NewEntity";
    public string imageOverride = "None";

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