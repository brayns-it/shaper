using System.Text.RegularExpressions;

namespace Brayns.Shaper.Classes
{
    public static partial class Functions
    {
        internal static bool AreEquals(object? o1, object? o2)
        {
            if ((o1 == null) && (o2 == null))
                return true;

            if ((o1 != null) && (o2 == null))
                return false;

            if ((o1 == null) && (o2 != null))
                return false;

            return o1!.Equals(o2);
        }

        public static string Increment(string value)
        {
            Regex re = new Regex("\\d+$");
            Match ma = re.Match(value);
            if (ma.Success)
            {
                int no = Convert.ToInt32(ma.Value);
                no++;

                string newNo = no.ToString().PadLeft(ma.Value.Length, '0');
                if (newNo.Length == ma.Value.Length)
                    return re.Replace(value, newNo);
            }
            throw new Error(Label("Cannot increment {0}"), value);
        }
    }
}
