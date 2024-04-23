using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Brayns.Shaper.Classes;
using Brayns.Shaper.Fields;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Brayns.Shaper.Loader
{
    internal static class Loader
    {
        private static AssemblyLoadContext? Context { get; set; }
        private static List<Assembly> AppAssemblies { get; set; } = new();
        private static List<Type> TableTypes { get; set; } = new();
        private static List<string> TableNames { get; set; } = new();
        private static List<Type> CodeunitTypes { get; set; } = new();
        private static List<Type> ModuleTypes { get; set; } = new();
        internal static Dictionary<string, Type> UnitTypes { get; private set; } = new();
        internal static Dictionary<Type, Dictionary<string, List<ITableRelation>>> RelationLinks { get; set; } = new();
        internal static Dictionary<string, Dictionary<string, Dictionary<string, string>>> Translations { get; set; } = new();

        private static void LoadTranslations(Assembly asm)
        {
            foreach (var n in asm.GetManifestResourceNames())
            {
                var fn = n.ToLower();
                if (!fn.EndsWith(".po")) continue;
                int p = fn.LastIndexOf("translation.");
                if (p == -1) continue;

                var locale = fn.Substring(p + 12, fn.Length - p - 12 - 3);
                locale = locale.Replace("_", "-");

                Language.LoadTranslation(locale, asm.GetManifestResourceStream(n)!);
            }
        }

        private static void FinalizeLoadApps()
        {
            TableTypes.Clear();
            CodeunitTypes.Clear();
            ModuleTypes.Clear();
            Translations.Clear();
            UnitTypes.Clear();

            foreach (Assembly asm in AppAssemblies)
            {
                LoadTranslations(asm);

                foreach (Type t in asm.GetExportedTypes())
                {
                    if (t.IsAbstract) continue;

                    // units
                    if (typeof(Unit).IsAssignableFrom(t))
                    {
                        UnitTypes.Add(t.FullName!, t);
                        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
                    }

                    // tables
                    if (typeof(BaseTable).IsAssignableFrom(t))
                        TableTypes.Add(t);

                    // codeunits
                    if (typeof(Codeunit).IsAssignableFrom(t))
                        CodeunitTypes.Add(t);

                    // app modules
                    if (typeof(AppModule).IsAssignableFrom(t))
                        ModuleTypes.Add(t);
                }
            }
        }

        internal static void LoadAppsFromDomain()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Proxy.HasAttribute<AppCollectionAttribute>(asm))
                    AppAssemblies.Add(asm);
            }

            FinalizeLoadApps();
        }

        internal static void SaveConfig()
        {
            FileInfo fi = new FileInfo(Application.RootPath + "var/config.json");
            if (fi.Exists) fi.Delete();

            StreamWriter sw = new StreamWriter(fi.FullName);
            sw.Write(Application.Config.ToJson());
            sw.Close();
        }

        internal static void LoadConfig()
        {
            Application.Config = new();

            FileInfo fi = new FileInfo(Application.RootPath + "var/config.json");
            if (fi.Exists)
            {
                try
                {
                    StreamReader sr = new StreamReader(fi.FullName);
                    Application.Config = Config.FromJson(sr.ReadToEnd());
                    sr.Close();

                    SaveConfig();
                }
                catch (Exception ex)
                {
                    Application.LogException("loadconf", ex);
                }
            }
            else
            {
                SaveConfig();
            }
        }

        internal static List<ITableRelation> GetAllTableRelations(BaseField f)
        {
            var res = new List<ITableRelation>();

            var t = f.Table!.GetType();
            if (!RelationLinks.ContainsKey(t))
                return res;

            var n = f.SqlName.ToLower();
            if (!RelationLinks[t].ContainsKey(n))
                return res;

            return RelationLinks[t][n];
        }

        internal static void CollectApiEndpoints()
        {
            Application.Routes.Clear();
            Application.RawRoutes.Clear();

            foreach (Type t in CodeunitTypes)
            {
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    var r = m.GetCustomAttribute<ApiMethod>(true);
                    if (r != null)
                        Application.Routes.Add(r, m);

                    var w = m.GetCustomAttribute<RawMethodAttribute>(true);
                    if (w != null)
                        Application.RawRoutes.Add(w, m);
                }
            }
        }

        internal static void InstallApps()
        {
            Application.Apps.Clear();

            foreach (Type t in ModuleTypes)
            {
                var a = (AppModule)Activator.CreateInstance(t)!;
                Application.Apps.Add(a.Id, a);

                if (Session.Database != null)
                {
                    a.Install();
                    Commit();
                }
            }
        }

        internal static void CollectTableRelations()
        {
            RelationLinks.Clear();

            foreach (Type t in TableTypes)
            {
                var tab = (BaseTable)Activator.CreateInstance(t)!;

                // assert unique names
                TableNames.Add(tab.TableName);

                foreach (ITableRelation tr in tab.TableRelations)
                {
                    var f = tr.GetFieldForCollect();

                    if (!RelationLinks.ContainsKey(f.Item1))
                        RelationLinks.Add(f.Item1, new Dictionary<string, List<ITableRelation>>());
                    if (!RelationLinks[f.Item1].ContainsKey(f.Item2))
                        RelationLinks[f.Item1].Add(f.Item2, new List<ITableRelation>());

                    RelationLinks[f.Item1][f.Item2].Add(tr);
                }
            }
        }

        internal static void CompileTables(Database.DatabaseCompileMode mode)
        {
            if (CurrentSession.Database == null) return;

            CurrentSession.Database.CompileMode = mode;
            CurrentSession.Database.CompileResult.Clear();

            foreach (Type t in TableTypes)
            {
                if (t.GetCustomAttribute<VirtualTable>(true) != null)
                    continue;

                var tab = (BaseTable)Activator.CreateInstance(t)!;
                Session.Database!.Compile(tab);
                Commit();
            }
        }
    }
}
