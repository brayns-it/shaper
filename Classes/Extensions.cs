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

        public static bool IsNumeric(this string value)
        {
            string num = value.Trim();
            foreach (char c in num)
                if ((c < '0') || (c > '9'))
                    return false;

            return true;
        }
    }
}
