using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TextModPacker
{
    private const string directiveDelimiter = ",";

    public static string PackTextMod(List<SliceDiceTextMod.ModDirectiveData> Directives)
    {
        string textmod = "";

        for (int i = 0; i < Directives.Count; i++)
        {
            //unpack directive
            // add a comma
            // add to textmod
            //unpack next one
            //no comma if its the last one. 
        }

        return textmod;
    }

    public static List<SliceDiceTextMod.ModDirectiveData> UnpackTextMod(string textmod)
    {
        // the opposite of PackTextMod();
        List<SliceDiceTextMod.ModDirectiveData> unpackedtextmod = new List<SliceDiceTextMod.ModDirectiveData> ();
        return unpackedtextmod;
    }

}
