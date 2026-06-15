using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModifierData : SDData
{
    public ModifierData() { }
    public ModifierData(string data)
    {
        Parse(data);
    }
}
