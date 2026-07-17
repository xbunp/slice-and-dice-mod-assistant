public static class StringExtensions
{
    public static string SanitizeRichInput(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace(".", "[dot]")
            .Replace("&", "[amp]/[and]")
            .Replace(",", "[comma]")
            .Replace("+", "[plus]")
            .Replace("-", "[minus]")
            .Replace("#", "[hash]")
            .Replace("pips", "[pips]")
            .Replace("pipsk", "[pipsk]")
            .Replace("hp", "[hp]")
            .Replace("fhp", "[fullHeart]")
            .Replace("fullHeart", "[fullHeart]")
            .Replace("=", "[equals]");
    }

    public static string SanitizePlainInput(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace(".", "")
            .Replace("&", "")
            .Replace(",", "");
    }
}
