namespace Brayns.Shaper.Extensions
{
    public static class Extensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (value.Length <= maxLength)
                return value;
            else
                return value.Substring(0, maxLength);
        }
    }
}
