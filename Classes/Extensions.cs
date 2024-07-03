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

        public static DateTime StartingWeek(this DateTime value)
        {
            if (value.DayOfWeek != DayOfWeek.Monday)
            {
                int d = (value.DayOfWeek == DayOfWeek.Sunday) ? 6 : Convert.ToInt32(value.DayOfWeek) - 1;
                value = value.AddDays(-d);
            }
            return value;
        }

        public static DateTime EndingWeek(this DateTime value)
        {
            if (value.DayOfWeek != DayOfWeek.Sunday)
            {
                int d = 7 - Convert.ToInt32(value.DayOfWeek);
                value = value.AddDays(d);
            }
            return value;
        }
    }
}
