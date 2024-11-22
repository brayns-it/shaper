using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    public static partial class Functions
    {
        public static string ToSqlName(string name)
        {
            var sqlname = "";
            foreach (char c in name)
            {
                if ((c >= 'A') && (c <= 'Z'))
                    sqlname += c;
                else if ((c >= 'a') && (c <= 'z'))
                    sqlname += c;
                else if ((c >= '0') && (c <= '9'))
                    sqlname += c;
                else if (new[] { '(', ')', '-', ' ' }.Contains(c)) 
                    sqlname += c;
                else
                    sqlname += '_';
            }
            return sqlname;
        }

        public static string Format(decimal val)
        {
            NumberFormatInfo nfi = new();
            nfi.NumberDecimalSeparator = ".";
            nfi.NumberGroupSeparator = "";
            return val.ToString("G29", nfi);
        }

        public static string Format(TimeSpan ts, int? stepCount = null)
        {
            List<string> steps = new();

            int days = Convert.ToInt32(Math.Floor(ts.TotalDays));
            if (days > 0) steps.Add(Label("{0} days", days));

            int hours = Convert.ToInt32(Math.Floor(ts.TotalHours - (days * 24)));
            if (hours > 0) steps.Add(Label("{0} hours", hours));

            int minutes = Convert.ToInt32(Math.Floor(ts.TotalMinutes - (days * 24 * 60) - (hours * 60)));
            if (minutes > 0) steps.Add(Label("{0} minutes", minutes));

            int seconds = Convert.ToInt32(Math.Floor(ts.TotalSeconds - (days * 24 * 60 * 60) - (hours * 60 * 60) - (minutes * 60)));
            if (seconds > 0) steps.Add(Label("{0} seconds", seconds));

            if (steps.Count == 0)
                return Label("Now");
            else
            {
                string str = "";
                for (int i = 0; i < steps.Count; i++)
                {
                    if (stepCount.HasValue && (stepCount.Value > 0) && (i >= stepCount.Value))
                        break;

                    str += steps[i] + " ";
                }
                return str.Trim();
            }
        }
    }
}
