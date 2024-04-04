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
            throw new Error(Label("Cannot increment {0}", value));
        }

        public static Opt<UnitTypes> UnitTypeFromType(Type t)
        {
            if (typeof(Codeunit).IsAssignableFrom(t))
                return UnitTypes.CODEUNIT;

            if (typeof(BasePage).IsAssignableFrom(t))
                return UnitTypes.PAGE;

            if (typeof(BaseTable).IsAssignableFrom(t))
                return UnitTypes.TABLE;

            return UnitTypes.NONE;
        }

        public static string GetFileResourceAsString(string path)
        {
            string result = "";
            FileInfo fi = new FileInfo(Application.RootPath + "var/resources/" + path);
            if (fi.Exists)
            {
                StreamReader sr = new(fi.FullName);
                result = sr.ReadToEnd();
                sr.Close();
            }
            return result;
        }

        public static string NameForProperty(string name, bool firstLower = true)
        {
            name = name.Trim();
            if (name.Length == 0)
                return "";

            var name2 = "";
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (((c >= 'A') && (c <= 'Z')) ||
                    ((c >= 'a') && (c <= 'z')) ||
                    ((c >= '0') && (c <= '9')) ||
                    (c == ' '))
                    name2 += c;
                else if (c == '.')
                    name2 += "";   // strip dot
                else
                    name2 += "_";
            }

            bool toUpper = !firstLower;
            var result = "";
            for (int i = 0; i < name2.Length; i++)
            {
                var c = name2.Substring(i, 1);
                if (c == " ")
                    toUpper = true;
                else
                {
                    if (toUpper)
                        result += c.ToUpper();
                    else
                        result += c.ToLower();
                    toUpper = false;
                }
            }

            return result;
        }
    }
}
