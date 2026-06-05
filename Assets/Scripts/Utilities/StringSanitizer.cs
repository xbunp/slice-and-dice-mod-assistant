public static class StringExtensions
{
    public static string SanitizeInput(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value
            .Replace(".", "[dot]")
            .Replace("&", "[272000DGF0x3w1w3x3w0w0w1w1w3x0w1][h]")
            .Replace(",", "[comma]");
    }
}