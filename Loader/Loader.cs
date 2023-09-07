using Newtonsoft.Json;
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
        internal static Assembly? AppAssembly { get; private set; }
        internal static Dictionary<Type, Dictionary<string, List<ITableRelation>>> RelationLinks { get; set; } = new();
        internal static Dictionary<string, Dictionary<string, Dictionary<string, string>>> Translations { get; set; } = new();

        internal static void LoadTranslations()
        {
            Translations.Clear();

            List<Assembly> asms = new();
            if (AppAssembly != null) asms.Add(AppAssembly);
            asms.Add(Assembly.GetExecutingAssembly());

            foreach (var a in asms)
            {
                foreach (var n in a.GetManifestResourceNames())
                {
                    var fn = n.ToLower();
                    if (!fn.EndsWith(".po")) continue;
                    int p = fn.LastIndexOf("translation.");
                    if (p == -1) continue;

                    var locale = fn.Substring(p + 12, fn.Length - p - 12 - 3);
                    locale = locale.Replace("_", "-");

                    Language.LoadTranslation(locale, a.GetManifestResourceStream(n)!);
                }
            }
        }

        private static void FinalizeLoadApps()
        {
            Application.SystemModule = null;

            if (AppAssembly == null)
                return;

            foreach (Type t in AppAssembly.GetExportedTypes())
            {
                if (t.GetCustomAttributes(typeof(SystemModuleAttribute), true).Length > 0)
                {
                    Application.SystemModule = (SystemModule)Activator.CreateInstance(t)!;
                    break;
                }
            }
        }

        internal static void LoadAppsFromDomain()
        {
            AppAssembly = null;

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetCustomAttributes(typeof(AppCollectionAttribute), true).Length > 0)
                {
                    AppAssembly = a;
                    break;
                }
            }

            if (AppAssembly == null)
                throw new Error(Label("No App assembly has been found"));

            FinalizeLoadApps();
        }

        internal static void LoadAppsFromRoot()
        {
            if (Context != null)
            {
                Context.Unload();
                Context = null;
            }

            AppAssembly = null;

            if (Application.Config.CopyAppsFromPath != null)
            {
                DirectoryInfo di = new DirectoryInfo(Application.Config.CopyAppsFromPath);
                foreach (FileInfo fp in di.GetFiles())
                {
                    FileInfo fd = new(Application.RootPath + "apps/" + fp.Name);
                    if ((!fd.Exists) || (fd.LastWriteTimeUtc != fp.LastWriteTimeUtc) || (fd.Length != fp.Length))
                        fp.CopyTo(fd.FullName, true);
                }
            }

            if (Application.Config.AppsAssemblyName != null)
            {
                FileInfo fi = new FileInfo(Application.RootPath + "apps/" + Application.Config.AppsAssemblyName);
                if (fi.Exists)
                {
                    Context = new AssemblyLoadContext("Apps", true);
                    Context.Resolving += Context_Resolving;

                    FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read);
                    AppAssembly = Context.LoadFromStream(fs);
                    fs.Close();
                }
            }

            FinalizeLoadApps();
        }

        private static Assembly? Context_Resolving(AssemblyLoadContext arg1, AssemblyName arg2)
        {
            FileInfo fi = new FileInfo(Application.RootPath + "apps/" + arg2.Name + ".dll");
            if (!fi.Exists)
                fi = new FileInfo(Application.RootPath + arg2.Name + ".dll");
            if (!fi.Exists)
                throw new Error(Label("Cannot load assembly '{0}'"), arg2.Name!);

            FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read);
            var asm = arg1.LoadFromStream(fs);
            fs.Close();
            return asm;
        }

        internal static void SaveConfig()
        {
            FileInfo fi = new FileInfo(Application.RootPath + "var/config.json");
            if (fi.Exists) fi.Delete();

            StreamWriter sw = new StreamWriter(fi.FullName);
            JsonSerializer ser = new JsonSerializer();
            ser.Formatting = Formatting.Indented;
            ser.Serialize(sw, Application.Config);
            sw.Close();
        }

        internal static void LoadConfig()
        {
            FileInfo fi = new FileInfo(Application.RootPath + "var/config.json");
            if (fi.Exists)
            {
                StreamReader sr = new StreamReader(fi.FullName);
                var cfg = JsonConvert.DeserializeObject<Classes.Config>(sr.ReadToEnd());
                sr.Close();

                if (cfg == null)
                    throw new Error(Label("Invalid configuration file"));
                else
                    Application.Config = cfg;

                if (Application.Config.EncryptPlainPasswords())
                    SaveConfig();
            }
            else
            {
                Application.Config = new Classes.Config();
                SaveConfig();
            }
        }

        internal static List<ITableRelation> GetAllTableRelations(Field f)
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

            if (AppAssembly == null)
                return;

            foreach (Type t in AppAssembly.GetExportedTypes())
            {
                if (!typeof(Codeunit).IsAssignableFrom(t)) continue;

                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                {
                    var r = m.GetCustomAttribute<ApiMethodAttribute>(true);
                    if ((r != null) && (r.Route != null) && (r.Route.Length > 0))
                    {
                        if (r.Action.HasFlag(Classes.ApiAction.Create))
                        {
                            if (!Application.Routes.ContainsKey(r.Action)) Application.Routes[r.Action] = new();
                            Application.Routes[Classes.ApiAction.Create][r.Route] = m;
                        }
                        if (r.Action.HasFlag(Classes.ApiAction.Update))
                        {
                            if (!Application.Routes.ContainsKey(r.Action)) Application.Routes[r.Action] = new();
                            Application.Routes[Classes.ApiAction.Update][r.Route] = m;
                        }
                        if (r.Action.HasFlag(Classes.ApiAction.Delete))
                        {
                            if (!Application.Routes.ContainsKey(r.Action)) Application.Routes[r.Action] = new();
                            Application.Routes[Classes.ApiAction.Delete][r.Route] = m;
                        }
                        if (r.Action.HasFlag(Classes.ApiAction.Read))
                        {
                            if (!Application.Routes.ContainsKey(r.Action)) Application.Routes[r.Action] = new();
                            Application.Routes[Classes.ApiAction.Read][r.Route] = m;
                        }
                    }
                }
            }
        }

        internal static void InstallApps()
        {
            Application.Apps.Clear();

            if (AppAssembly == null)
                return;

            foreach (Type t in AppAssembly.GetExportedTypes())
            {
                if (t.GetCustomAttributes(typeof(AppModuleAttribute), true).Length > 0)
                {
                    var a = (AppModule)Activator.CreateInstance(t)!;
                    a.Install();
                    Application.Apps.Add(a.Id, a);

                    Commit();
                }
            }
        }

        internal static void CollectTableRelations()
        {
            RelationLinks.Clear();

            if (AppAssembly == null)
                return;

            foreach (Type t in AppAssembly.GetExportedTypes())
            {
                if (typeof(BaseTable).IsAssignableFrom(t))
                {
                    var tab = (BaseTable)Activator.CreateInstance(t)!;
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
        }

        internal static void SyncSchema(bool onlyCheck)
        {
            if (Application.Config.DatabaseType == Database.DatabaseType.None)
                return;

            if (AppAssembly == null)
                return;

            foreach (Type t in AppAssembly.GetExportedTypes())
            {
                if (typeof(BaseTable).IsAssignableFrom(t))
                {
                    var tab = (BaseTable)Activator.CreateInstance(t)!;
                    Session.Database!.Compile(tab, onlyCheck);
                    Commit();
                }
            }
        }
    }
}
