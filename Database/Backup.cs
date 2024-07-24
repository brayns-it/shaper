using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Reflection;
using Newtonsoft.Json;

namespace Brayns.Shaper.Database
{
    public class Backup
    {
        public string FileName { get; set; }

        public Backup()
        {
            FileName = Application.RootPath + "var/backup_" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".json";
        }

        public void DoBackupYN()
        {
            Confirm.Show(Label("Backup database to {0}?", FileName), () => DoBackup());
        }

        public void DoBackup()
        {
            var progr = new Progress();
            progr.InitLine("tab");
            progr.InitLine("prg");
            progr.Show();

            var sw = new StreamWriter(FileName);
            var jw = new JsonTextWriter(sw);
            jw.Formatting = Formatting.Indented;
            jw.WriteStartArray();

            foreach (Type t in Loader.Loader.TableTypes)
            {
                if (t.GetCustomAttribute<VirtualTable>(true) != null)
                    continue;

                var tab = (BaseTable)Activator.CreateInstance(t)!;
                progr.UpdateLine("tab", tab.UnitCaption);

                int c = tab.Count();
                int n = 0;

                if (tab.FindSet())
                {
                    jw.WriteStartObject();
                    jw.WritePropertyName("tableName");
                    jw.WriteValue(tab.TableName);

                    jw.WritePropertyName("fields");
                    jw.WriteStartArray();
                    foreach (var f in tab.UnitFields)
                        if (f.Type != Fields.FieldTypes.TIMESTAMP)
                            jw.WriteValue(f.Name);
                    jw.WriteEndArray();

                    jw.WritePropertyName("rows");
                    jw.WriteStartArray();

                    while (tab.Read())
                    {
                        n++;
                        progr.UpdateLinePercent("prg", n, c);

                        jw.WriteStartArray();
                        foreach (var f in tab.UnitFields)
                            if (f.Type != Fields.FieldTypes.TIMESTAMP)
                                jw.WriteValue(f.Serialize());
                        jw.WriteEndArray();
                    }

                    jw.WriteEndArray();
                    jw.WriteEndObject();
                }
            }

            jw.WriteEndArray();
            jw.Close();
            sw.Close();
            progr.Close();
        }
    }
}
