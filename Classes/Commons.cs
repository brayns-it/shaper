using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Brayns.Shaper.Classes
{
    public static partial class Commons
    {
        public static void Commit()
        {
            Session.Database?.Commit();
        }

        public static void Rollback()
        {
            Session.Database?.Rollback();
        }

        public static string Label(string text, params object[] args)
        {
            var trace = new StackTrace(1, false);
            if (trace.FrameCount > 0)
            {
                var type = trace.GetFrame(0)!.GetMethod()!.ReflectedType;
                if (type != null)
                    return String.Format(Language.TranslateText(text, type!), args);
            }
            return String.Format(text, args);
        }
    }
}

