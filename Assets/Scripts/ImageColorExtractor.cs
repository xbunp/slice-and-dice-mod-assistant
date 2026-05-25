using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
public static class ImageColorExtractor
{
    private const int MaxPalette = 999;
    private const int MaxDim = 999;

    public enum CompressionFactor
    {
        None = 0,
        Mild = 1,
        Some = 2,
        Moderate = 4,
        Extreme = 6,
        Hyper = 8,
        Giga = 10,
        Insane = 15,
    }

    public static string GetFormat()
        => "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ%=";

    public static string ExtractColors(Texture2D texture, CompressionFactor compression, out Texture2D previewTexture)
    {
        previewTexture = null;

        if (texture == null)
        {
            Debug.LogError("[ImageColorExtractor] Texture is null.");
            return null;
        }

        string FORMAT = GetFormat();
        int compressionFactor = (int)compression;

        int srcW = texture.width;
        int srcH = texture.height;
        float ratio = (float)srcW / srcH;

        int canvasW = Mathf.Min(MaxDim, srcW);
        int canvasH = Mathf.Min(MaxDim, srcH);

        if (srcW > MaxDim || srcH > MaxDim)
        {
            if (ratio > 1f) canvasH = Mathf.FloorToInt(canvasH / ratio);
            else canvasW = Mathf.FloorToInt(canvasW * ratio);
        }

        Color32[] srcPixels = texture.GetPixels32();
        Color32[] pixels = SampleNearest(srcPixels, srcW, srcH, canvasW, canvasH);

        var palette = new List<string>();
        var colCnt = new Dictionary<string, int>();

        for (int i = 0; i < pixels.Length; i++)
        {
            string colStr = GetColStr(pixels[i], palette, compressionFactor, FORMAT);
            if (!palette.Contains(colStr))
            {
                palette.Add(colStr);
                colCnt[colStr] = 1;
                if (palette.Count > MaxPalette)
                {
                    Debug.LogError("[ImageColorExtractor] Palette too large (> 999 colours).");
                    return "palette too big";
                }
            }
            else
            {
                colCnt[colStr]++;
            }
        }

        palette.Sort((a, b) => colCnt[b].CompareTo(colCnt[a]));

        int paletteBits = Mathf.CeilToInt(Mathf.Log(palette.Count) / Mathf.Log(2));
        int extraPaletteSlots = (1 << paletteBits) - palette.Count;

        var extraBits = new List<int>();
        int epsc = extraPaletteSlots;

        // --- THE FIX IS HERE ---
        while (epsc > 0)
        {
            // By using integer math (epsc + 1) / 2, we force C# to match JavaScript's Math.round
            // behavior exactly, preventing the infinite loop crash.
            int half = (epsc + 1) / 2;
            extraBits.Add(half);
            epsc -= half;
        }
        // ------------------------

        int posBits = 6 - paletteBits;

        var dataString = new StringBuilder();
        string currentCol = null;
        int sameCol = 0;

        Color32[] previewPixels = new Color32[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            string colStr = GetColStr(pixels[i], palette, compressionFactor, FORMAT);

            int x = i % canvasW;
            int y = i / canvasW;
            int unityY = canvasH - 1 - y;
            int unityIdx = unityY * canvasW + x;

            if (colStr == "000" && pixels[i].a < 12)
            {
                previewPixels[unityIdx] = new Color32(0, 0, 0, 0);
            }
            else
            {
                byte pr = (byte)(FORMAT.IndexOf(colStr[0]) * 4);
                byte pg = (byte)(FORMAT.IndexOf(colStr[1]) * 4);
                byte pb = (byte)(FORMAT.IndexOf(colStr[2]) * 4);
                previewPixels[unityIdx] = new Color32(pr, pg, pb, 255);
            }

            if (currentCol == null) currentCol = colStr;
            else if (currentCol != colStr)
            {
                dataString.Append(GetString(sameCol, palette.IndexOf(currentCol), palette.Count, posBits, paletteBits, extraBits, FORMAT));
                sameCol = 0;
            }

            currentCol = colStr;
            sameCol++;

            if (i == pixels.Length - 1)
            {
                dataString.Append(GetString(sameCol, palette.IndexOf(currentCol), palette.Count, posBits, paletteBits, extraBits, FORMAT));
            }
        }

        previewTexture = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false);
        previewTexture.filterMode = FilterMode.Point;
        previewTexture.SetPixels32(previewPixels);
        previewTexture.Apply();

        var finalData = new StringBuilder();
        finalData.Append('2');
        finalData.Append(FORMAT[canvasW]);
        finalData.Append(FORMAT[palette.Count]);

        foreach (string colour in palette) finalData.Append(colour);
        finalData.Append(dataString);

        string finalStr = finalData.ToString();
        string compressed = CompressV3(finalStr);

        return compressed.Length < finalStr.Length ? compressed : finalStr;
    }

    public static string CompressV3(string data)
    {
        string FORMAT = GetFormat();
        var encodingParts = new List<string>();

        for (int repLen = 2; repLen <= 6; repLen++)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var map = new Dictionary<string, int>();
                for (int si = 0; si < data.Length - repLen; si++)
                {
                    string part = data.Substring(si, repLen);
                    map.TryGetValue(part, out int cnt);
                    map[part] = cnt + 1;
                }

                if (map.Count == 0) continue;

                string bestKey = null;
                int bestVal = 0;
                foreach (var kv in map)
                {
                    if (kv.Value > bestVal)
                    {
                        bestVal = kv.Value;
                        bestKey = kv.Key;
                    }
                }

                int charsRemoved = bestVal * (repLen - 1) - 2 - repLen;
                if (charsRemoved <= 0) break;

                string unusedChar = FindUnusedChar(data);
                if (unusedChar == null) break;

                data = data.Replace(bestKey, unusedChar);
                encodingParts.Add(unusedChar + bestKey);
            }
        }

        var result = new StringBuilder();
        result.Append('3');
        result.Append(FORMAT[encodingParts.Count]);

        foreach (string entry in encodingParts)
        {
            result.Append(entry.Length - 1);
            result.Append(entry);
        }

        result.Append(data);
        return result.ToString();
    }

    private static Color32[] SampleNearest(Color32[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new Color32[dstW * dstH];
        for (int dstY = 0; dstY < dstH; dstY++)
        {
            int srcYCanvas = Mathf.FloorToInt((float)dstY / dstH * srcH);
            int srcYUnity = srcH - 1 - srcYCanvas;
            for (int dstX = 0; dstX < dstW; dstX++)
            {
                int srcX = Mathf.FloorToInt((float)dstX / dstW * srcW);
                dst[dstY * dstW + dstX] = src[srcYUnity * srcW + srcX];
            }
        }
        return dst;
    }

    private static string GetColStr(Color32 pixel, List<string> palette, int compressionFactor, string FORMAT)
    {
        const float Ratio = 4f;
        float r = pixel.r / Ratio;
        float g = pixel.g / Ratio;
        float b = pixel.b / Ratio;

        if (pixel.a < 12) return "000";

        string colStr = $"{FORMAT[(int)r]}{FORMAT[(int)g]}{FORMAT[(int)b]}";
        foreach (string palEntry in palette)
        {
            if (CloseEnough(palEntry, colStr, compressionFactor, FORMAT)) return palEntry;
        }
        return colStr;
    }

    private static bool CloseEnough(string a, string b, int compressionFactor, string FORMAT)
    {
        if (a == b) return true;
        if (a == "000" || b == "000") return false;

        float sumSq = 0f;
        for (int ch = 0; ch < 3; ch++)
        {
            float delta = FORMAT.IndexOf(a[ch]) - FORMAT.IndexOf(b[ch]);
            sumSq += delta * delta;
        }
        return Mathf.Sqrt(sumSq) <= compressionFactor * 4f;
    }

    private static string GetString(int sameCol, int paletteIndex, int paletteSize, int posBits, int paletteBits, List<int> extraVals, string FORMAT)
    {
        int baseMax = 1 << posBits;
        int ev = paletteIndex < extraVals.Count ? extraVals[paletteIndex] : 0;
        int extraBitValue = 1 << posBits;
        int extraBitsMax = extraBitValue * ev;
        int actualMax = baseMax + extraBitsMax;

        string result = "!";
        int advance = Math.Min(sameCol, actualMax);

        if (advance <= baseMax)
        {
            int charIndex = (paletteIndex << posBits) + (advance - 1);
            sameCol -= advance;
            result = FORMAT[charIndex].ToString();
        }
        else
        {
            int actualAdvance = Math.Min(advance, actualMax);
            sameCol -= actualAdvance;

            int ebv = actualAdvance / extraBitValue;
            int ebvn = ebv * extraBitValue;
            int rightBits = actualAdvance - ebvn - 1;
            int leftBits = paletteIndex << posBits;

            if (ebvn > 0)
            {
                int extraOffset = 0;
                for (int i = 0; i < paletteIndex; i++) extraOffset += (i < extraVals.Count ? extraVals[i] : 0);
                leftBits = (paletteSize - 1 + ebv + extraOffset) << posBits;
            }

            int charIndex = leftBits + rightBits;
            result = FORMAT[charIndex].ToString();
        }

        return sameCol > 0 ? result + GetString(sameCol, paletteIndex, paletteSize, posBits, paletteBits, extraVals, FORMAT) : result;
    }

    private static string FindUnusedChar(string data)
    {
        string FORMAT = GetFormat();
        foreach (char ch in FORMAT)
        {
            if (!data.Contains(ch)) return ch.ToString();
        }
        return null;
    }
}