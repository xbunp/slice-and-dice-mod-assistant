using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class ImageUtility
{
    private static Dictionary<string, string> _imgCache = new Dictionary<string, string>();
    private static int _imgCounter = 0;

    // We only need regex here because it's a strict string replacement pass before lexing
    private static readonly Regex ImgRegex = new Regex(@"img\.(.*?)\.|\[(.*?)\]", RegexOptions.Compiled);

    public static void ClearCache()
    {
        _imgCache.Clear();
        _imgCounter = 0;
    }

    public static string CompressImages(string rawText)
    {
        if (string.IsNullOrEmpty(rawText) || (!rawText.Contains("img.") && !rawText.Contains("[")))
            return rawText;

        StringBuilder sb = new StringBuilder(rawText.Length);
        Match m = ImgRegex.Match(rawText);
        int lastIndex = 0;

        while (m.Success)
        {
            sb.Append(rawText, lastIndex, m.Index - lastIndex);
            bool isBracket = m.Value.StartsWith("[");
            string innerData = isBracket ? m.Groups[2].Value : m.Groups[1].Value;

            if (innerData.StartsWith("IMG_") || innerData.Length <= 25)
            {
                sb.Append(m.Value);
            }
            else
            {
                string newId = $"IMG_{++_imgCounter}";
                _imgCache[newId] = innerData;
                sb.Append(isBracket ? $"[{newId}]" : $"img.{newId}.");
            }

            lastIndex = m.Index + m.Length;
            m = m.NextMatch();
        }

        if (lastIndex < rawText.Length) sb.Append(rawText, lastIndex, rawText.Length - lastIndex);
        return sb.ToString();
    }

    public static string RestoreImages(string compressedText)
    {
        if (string.IsNullOrEmpty(compressedText) || (!compressedText.Contains("img.IMG_") && !compressedText.Contains("[IMG_")))
            return compressedText;

        return Regex.Replace(compressedText, @"img\.(IMG_\d+)\.|\[(IMG_\d+)\]", match => {
            bool isBracket = match.Value.StartsWith("[");
            string id = isBracket ? match.Groups[2].Value : match.Groups[1].Value;

            if (_imgCache.TryGetValue(id, out string data))
            {
                return isBracket ? $"[{data}]" : $"img.{data}.";
            }

            return match.Value;
        });
    }
}