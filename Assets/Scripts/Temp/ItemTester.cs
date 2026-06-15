using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemTester : MonoBehaviour
{
    public string syntax;
    public ItemData item;

    [ContextMenu("Parse Item")]
    public void ParseItem()
    {
        item = new ItemData(syntax);
        item.DebugContentsToConsole();
    }
}
