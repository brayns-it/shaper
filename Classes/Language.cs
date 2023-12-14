using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Brayns.Shaper.Classes
{
    public static class Language
    {
        public static void CreatePoFile(string appPath)
        {
            appPath = appPath.Replace("\\", "/").ToLower();
            if (!appPath.EndsWith("/")) appPath += "/";

            DirectoryInfo di = new DirectoryInfo(appPath);

            DirectoryInfo di2 = new DirectoryInfo(appPath + "Translation");
            if (!di2.Exists) di2.Create();

            FileInfo fi = new FileInfo(di2.FullName + "/Source.pot");
            if (fi.Exists) fi.Delete();

            StreamWriter sw = new StreamWriter(fi.FullName);
            sw.WriteLine("msgid \"\"");
            sw.WriteLine("msgstr \"\"");
            sw.WriteLine("\"Project-Id-Version: \\n\"");
            sw.WriteLine("\"POT-Creation-Date: \\n\"");
            sw.WriteLine("\"PO-Revision-Date: \\n\"");
            sw.WriteLine("\"Last-Translator: \\n\"");
            sw.WriteLine("\"Language-Team: \\n\"");
            sw.WriteLine("\"Language: \\n\"");
            sw.WriteLine("\"MIME-Version: 1.0\\n\"");
            sw.WriteLine("\"Content-Type: text/plain; charset=UTF-8\\n\"");
            sw.WriteLine("\"Content-Transfer-Encoding: 8bit\\n\"");
            sw.WriteLine("\"X-Generator: Shaper\\n\"");
            sw.WriteLine();

            Regex regLbl = new Regex("Label\\(\"(.*?)\".*\\)");
            Regex regNam = new Regex("^namespace\\s(.*)$");
            Regex regCla = new Regex(".*class\\s(\\w*)");
            List<string> dups = new();

            foreach (FileInfo src in di.GetFiles("*.cs", SearchOption.AllDirectories))
            {
                string dn = src.DirectoryName!.Replace("\\", "/").ToLower();
                if (dn.StartsWith(appPath + "obj/")) continue;
                if (dn.StartsWith(appPath + "bin/")) continue;

                string nSpace = "";
                string clName = "";

                StreamReader sr = new StreamReader(src.FullName);
                while (!sr.EndOfStream)
                {
                    string? line = sr.ReadLine();
                    if (line == null) return;

                    Match m = regNam.Match(line);
                    if (m.Success) nSpace = m.Groups[1].Value;

                    m = regCla.Match(line);
                    if (m.Success) clName = m.Groups[1].Value;

                    if ((nSpace.Length > 0) && (clName.Length > 0))
                    {
                        foreach (Match n in regLbl.Matches(line))
                        {
                            string k = nSpace + "_" + clName + "_" + n.Groups[1].Value;
                            if (dups.Contains(k)) continue;
                            dups.Add(k);

                            sw.WriteLine("msgctxt \"" + nSpace + "." + clName + "\"");
                            sw.WriteLine("msgid \"" + n.Groups[1].Value + "\"");
                            sw.WriteLine("msgstr \"\"");
                            sw.WriteLine();
                        }
                    }
                }
                sr.Close();

            }

            sw.Close();
        }

        public static string TranslateText(string text)
        {
            var m = new StackTrace().GetFrame(2)!.GetMethod();
            return TranslateText(text, m!.ReflectedType!);
        }

        public static string TranslateText(string text, Type reflectedType)
        {
            string res = text;
            var g = Session.CultureInfo.Name.ToLower();
            var fn = reflectedType.Namespace! + "." + reflectedType.Name;

            if (Loader.Loader.Translations.ContainsKey(g) &&
                Loader.Loader.Translations[g].ContainsKey(fn) &&
                Loader.Loader.Translations[g][fn].ContainsKey(text))
            {
                res = Loader.Loader.Translations[g][fn][text];

            }
            else if (g.Contains("-"))
            {
                g = g.Split('-')[0];

                if (Loader.Loader.Translations.ContainsKey(g) &&
                    Loader.Loader.Translations[g].ContainsKey(fn) &&
                    Loader.Loader.Translations[g][fn].ContainsKey(text))
                {
                    res = Loader.Loader.Translations[g][fn][text];
                }
            }

            if (res.Length == 0)
                res = text;

            return res;
        }

        internal static void LoadTranslation(string locale, Stream poStream)
        {
            if (!Loader.Loader.Translations.ContainsKey(locale))
                Loader.Loader.Translations[locale] = new();

            Regex ctx = new Regex("^msgctxt \"(.*)\"$");
            Regex id = new Regex("^msgid \"(.*)\"$");
            Regex str = new Regex("^msgstr \"(.*)\"$");

            string ctxStr = "";
            string idStr = "";

            StreamReader sr = new StreamReader(poStream);
            while (!sr.EndOfStream)
            {
                string? line = sr.ReadLine();
                if (line == null) break;
                if (line.Trim().Length == 0)
                {
                    ctxStr = "";
                    idStr = "";
                }

                Match m = ctx.Match(line);
                if (m.Success) ctxStr = m.Groups[1].Value;

                m = id.Match(line);
                if (m.Success) idStr = m.Groups[1].Value;

                m = str.Match(line);
                if (m.Success && (ctxStr.Length > 0) && (idStr.Length > 0))
                {
                    if (!Loader.Loader.Translations[locale].ContainsKey(ctxStr))
                        Loader.Loader.Translations[locale][ctxStr] = new();

                    if (!Loader.Loader.Translations[locale][ctxStr].ContainsKey(ctxStr))
                        Loader.Loader.Translations[locale][ctxStr][idStr] = m.Groups[1].Value;
                }
            }
            sr.Close();
            poStream.Close();
        }
    }
}

