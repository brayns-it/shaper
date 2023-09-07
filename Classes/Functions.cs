using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
