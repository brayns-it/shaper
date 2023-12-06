using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Objects
{
    public enum PageTypes
    {
        Normal,
        Login,
        Start
    }

    public abstract class BasePage : Unit
    {
        internal Dictionary<string, Controls.Control> AllItems { get; set; } = new();
        internal JArray DataSet { get; set; } = new();
        internal JArray FDataSet { get; set; } = new();

        public List<Controls.Control> Items { get; private set; } = new();
        public PageTypes PageType { get; protected set; }
        public BaseTable? Rec { get; set; }

        public BasePage()
        {
            UnitType = UnitTypes.PAGE;
            PageType = PageTypes.Normal;
        }

        internal JArray GetSchema()
        {
            var result = new JArray();
            foreach (var field in UnitFields)
            {
                var item = new JObject();
                item["caption"] = field.Caption;
                item["codename"] = field.CodeName;
                item["fieldType"] = field.Type.ToString();
                item["hasFormat"] = field.HasFormat;

                if (field.Type == Fields.FieldTypes.OPTION)
                {
                    var options = new JArray();
                    var opt = (Opt)field.Value!;
                    foreach (var v in opt.Captions.Keys)
                    {
                        var line = new JObject();
                        line["value"] = v;
                        line["caption"] = opt.Captions[v];
                        options.Add(line);
                    }
                    item["options"] = options;
                }

                result.Add(item);
            }
            return result;
        }

        internal JArray GetDataRow(bool format = false)
        {
            var line = new JArray();
            foreach (var field in UnitFields)
            {
                if (format)
                {
                    if (field.HasFormat)
                        line.Add(field.Format(field.Value));
                    else
                        line.Add("");
                }
                else
                    line.Add(field.Serialize(field.Value));
            }
            return line;
        }

        internal void SendDataRow()
        {
            JArray data = new();
            data.Add(GetDataRow(false));

            JArray fdata = new();
            fdata.Add(GetDataRow(true));

            var result = new JObject();
            result["action"] = "datarow";
            result["pageid"] = UnitID.ToString();
            result["data"] = data;
            result["fdata"] = fdata;

            Client.SendMessage(result);
        }

        internal void SendDataSet()
        {
            DataSet.Clear();
            FDataSet.Clear();
            int count = 0;

            if (Rec != null)
            {

            }
            else
            {
                DataSet.Add(GetDataRow(false));
                FDataSet.Add(GetDataRow(true));
                count = 1;
            }

            var result = new JObject();
            result["action"] = "dataset";
            result["pageid"] = UnitID.ToString();
            result["count"] = count;
            result["data"] = DataSet;
            result["fdata"] = FDataSet;

            Client.SendMessage(result);
        }

        internal void SendPage(bool modal)
        {
            var result = new JObject();
            result["id"] = UnitID.ToString();
            result["name"] = UnitName;
            result["applicationName"] = CurrentSession.ApplicationName;
            result["unitType"] = GetType().ToString();
            result["pageType"] = PageType.ToString();
            result["caption"] = UnitCaption;
            result["action"] = "page";

            if (modal)
                result["display"] = "modal";
            else
                result["display"] = "content";

            var controls = new JArray();
            foreach (var c in Items)
                controls.Add(c.Render());
            result["controls"] = controls;

            result["schema"] = GetSchema();

            var js = new JObject();
            foreach (var c in AllItems.Values.OfType<Controls.Action>())
                if (c.Shortcut.Length > 0)
                    js[c.Shortcut] = c.ID.ToString();
            result["shortcuts"] = js;

            Client.SendMessage(result);
        }

        private void Run(bool modal)
        {
            OnLoad();
            SessionRegister();
            SendPage(modal);
            SendDataSet();
        }

        public void Run()
        {
            Run(false);
        }

        public void RunModal()
        {
            Run(true);
        }

        public void Close()
        {
            OnClose();

            var result = new JObject();
            result["pageid"] = UnitID.ToString();
            result["action"] = "closepage";

            Client.SendMessage(result);
        }

        [PublicAccess]
        internal void QueryClose()
        {
            if (OnQueryClose())
                Close();
        }

        [PublicAccess]
        internal void ControlInvoke(string controlid, string method, JObject? args = null)
        {
            var ctl = AllItems[controlid];
            var prx = Loader.Proxy.CreateFromObject(ctl);
            prx.SkipMethodSecurity = true;
            prx.Invoke(method, args);
        }

        protected virtual void OnLoad()
        {
        }

        protected virtual bool OnQueryClose()
        {
            return true;
        }

        protected void OnClose()
        {
        }
    }

    public abstract class Page<T> : BasePage where T : BasePage
    {

    }

    public abstract class Page<T, R> : Page<T> where T : BasePage where R : BaseTable
    {
        public new R? Rec
        {
            get { return base.Rec as R; }
            set
            {
                base.Rec = value;

                List<Fields.BaseField> toDel = new();
                foreach (var field in UnitFields)
                    if (field.Table != null)
                        toDel.Add(field);

                foreach (var field in toDel)
                    UnitFields.Remove(field);

                if (value != null)
                    foreach (var field in value.UnitFields)
                        UnitFields.Add(field);
            }
        }
    }

}
