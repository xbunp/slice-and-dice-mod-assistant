using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class HeroColors
{
    public enum ColorOption
    {
        Orange, Yellow, Grey, Red, Blue, Green,
        Purple, Cyan, DarkBlue, Black, White, Magenta,
        Pink, Violet, Brown, DarkBrown, Lime, DarkGreen,
        StrongOrange, StrongYellow, LightGrey, StrongRed, StrongGreen,
        WeakGreen, WeakBlue
    }

    public static string[] GetFormattedColorNames()
    {
        // We iterate through the enum to ensure the dropdown order 
        // matches the order defined in ColorOption
        return Enum.GetValues(typeof(ColorOption))
                   .Cast<ColorOption>()
                   .Select(option => ColorNames[GetCode(option)])
                   .ToArray();
    }

    // Maps Enum to the "code" letter
    public static string GetCode(ColorOption option)
    {
        return option switch
        {
            ColorOption.Orange => "o",
            ColorOption.Yellow => "y",
            ColorOption.Grey => "g",
            ColorOption.Red => "r",
            ColorOption.Blue => "b",
            ColorOption.Green => "n",
            ColorOption.Purple => "p",
            ColorOption.Cyan => "c",
            ColorOption.DarkBlue => "s",
            ColorOption.Black => "d",       
            ColorOption.White => "w",
            ColorOption.Magenta => "k",
            ColorOption.Pink => "u",
            ColorOption.Violet => "v",
            ColorOption.Brown => "h",
            ColorOption.DarkBrown => "m",
            ColorOption.Lime => "l",
            ColorOption.DarkGreen => "t",
            ColorOption.StrongOrange => "z",
            ColorOption.StrongYellow => "a",
            ColorOption.LightGrey => "i",
            ColorOption.StrongRed => "q",
            ColorOption.StrongGreen => "x",
            ColorOption.WeakGreen => "f",
            ColorOption.WeakBlue => "j",
            _ => "w"
        };
    }

    // Maps Enum to the actual Unity Color
    public static Color GetColor(ColorOption option)
    {
        string code = GetCode(option);
        // Reuse the dictionary logic from the previous step
        return GetColor(code);
    }

    // Dictionary storing "code" as key and "Nicename" as value
    private static readonly Dictionary<string, string> ColorNames = new Dictionary<string, string>
    {
        { "o", "O: Orange" },
        { "y", "Y: Yellow" },
        { "g", "G: Grey" },
        { "r", "R: Red" },
        { "b", "B: Blue" },
        { "n", "N: Green" },
        { "p", "P: Purple" },
        { "c", "C: Cyan" },
        { "s", "S: Dark Blue" },
        { "d", "D: Black" },
        { "w", "W: White" },
        { "k", "K: Magenta" },
        { "u", "U: Pink" },
        { "v", "V: Violet" },
        { "h", "H: Brown" },
        { "m", "M: Dark Brown" },
        { "l", "L: Lime" },
        { "t", "T: Dark Green" },
        { "z", "Z: Strong Orange" },
        { "a", "A: Strong Yellow" },
        { "i", "I: Light Grey" },
        { "q", "Q: Strong Red" },
        { "x", "X: Strong Green" },
        { "f", "F: Weak Green" },
        { "j", "J: Weak Blue" }
    };

    private static readonly Dictionary<string, Color> ColorMap = new Dictionary<string, Color>
    {
        { "o", new Color(1.0f, 0.64f, 0.0f) },    // Orange
        { "y", Color.yellow },                   // Yellow
        { "g", Color.gray },                     // Grey
        { "r", Color.red },                      // Red
        { "b", Color.blue },                     // Blue
        { "n", Color.green },                    // Green
        { "p", new Color(0.5f, 0.0f, 0.5f) },    // Purple
        { "c", Color.cyan },                     // Cyan
        { "s", new Color(0.0f, 0.0f, 0.5f) },    // Dark Blue
        { "d", Color.black },                    // Black
        { "w", Color.white },                    // White
        { "k", Color.magenta },                  // Magenta
        { "u", new Color(1.0f, 0.75f, 0.8f) },   // Pink
        { "v", new Color(0.93f, 0.5f, 0.93f) },  // Violet
        { "h", new Color(0.6f, 0.4f, 0.2f) },    // Brown
        { "m", new Color(0.4f, 0.2f, 0.1f) },    // Dark Brown
        { "l", new Color(0.75f, 1.0f, 0.0f) },   // Lime
        { "t", new Color(0.0f, 0.3f, 0.0f) },    // Dark Green
        { "z", new Color(1.0f, 0.4f, 0.0f) },    // Strong Orange
        { "a", new Color(1.0f, 0.9f, 0.0f) },    // Strong Yellow
        { "i", new Color(0.7f, 0.7f, 0.7f) },    // Light Grey
        { "q", new Color(0.8f, 0.0f, 0.0f) },    // Strong Red
        { "x", new Color(0.0f, 0.8f, 0.0f) },    // Strong Green
        { "f", new Color(0.6f, 0.8f, 0.6f) },    // Weak Green
        { "j", new Color(0.6f, 0.6f, 1.0f) }     // Weak Blue
    };

    public static string GetColorName(string code)
    {
        // Remove the "col." prefix if it exists
        string key = code.Replace("col.", "").ToLower();

        if (ColorNames.TryGetValue(key, out string name))
        {
            return name;
        }
        return "Unknown Color";
    }

    public static Color GetColor(string code)
    {
        string key = code.Replace("col.", "").ToLower();
        if (ColorMap.TryGetValue(key, out Color color))
        {
            return color;
        }
        return Color.white; // Default fallback
    }
}