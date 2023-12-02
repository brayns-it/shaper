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

        public static string Label(string text)
        {
            return Language.TranslateText(text);
        }

        public static string Label(string text, params object[] args)
        {
            return String.Format(Language.TranslateText(text), args);
        }
    }
}

