using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Classes
{
    public class Config
    {
        public bool Ready { get; set; } = false;
        public int DatabaseType { get; set; } = 0;
        public string DatabaseConnection { get; set; } = "";
        public string DatabaseLogin { get; set; } = "";
        public string DatabasePassword { get; set; } = "";
        public string DatabaseServer { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public string EnvironmentName { get; set; } = "Production";
        public string MaintenanceNetwork { get; set; } = "";

        public static Config FromJson(string json)
        {
            JObject jo = JObject.Parse(json);
            HandlePassword(jo, "DatabasePassword", false);
            return jo.ToObject<Config>()!;
        }

        public string ToJson()
        {
            JObject jo = JObject.FromObject(this);
            HandlePassword(jo, "DatabasePassword", true);
            return jo.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static void HandlePassword(JObject jobj, string key, bool encrypt)
        {
            if (!jobj.ContainsKey(key)) return;

            if (encrypt)
            {
                jobj[key] = Functions.EncryptString(jobj[key]!.ToString(), "password");
            }
            else
            {
                if (jobj[key]!.ToString().StartsWith("plain:"))
                    jobj[key] = jobj[key]!.ToString().Substring(6);
                else
                    jobj[key] = Functions.DecryptString(jobj[key]!.ToString(), "password");
            }
        }
    }
}
