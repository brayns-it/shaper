namespace Brayns.Shaper.Objects
{
    public enum PageTypes
    {
        Normal,
        Login,
        Start
    }

    public delegate void PageQueryCloseHandler(ref bool canClose);

    public abstract class BasePage : Unit
    {
        internal List<Fields.BaseField> DataFields { get; set; } = new();
        internal Dictionary<string, Controls.Control> AllItems { get; set; } = new();
        internal List<Dictionary<string, object>> DataSet { get; set; } = new();
        internal List<int> Selection { get; set; } = new();
        internal bool MultipleRows { get; set; } = false;

        public List<Controls.Control> Items { get; private set; } = new();
        public PageTypes PageType { get; protected set; }
        public BaseTable? Rec { get; set; }
        public event GenericHandler? Loading;
        public event GenericHandler? Closing;
        public event PageQueryCloseHandler? QueryClosing;
        public event GenericHandler? DataReading;
        public bool AllowInsert { get; set; } = true;
        public bool AllowDelete { get; set; } = true;
        public bool AllowModify { get; set; } = true;

        public BasePage()
        {
            UnitType = UnitTypes.PAGE;
            PageType = PageTypes.Normal;

            CreateDataActions();
        }

        private void CreateDataActions()
        {
            var actArea = Controls.ActionArea.Create(this);
            {
                var actData = new Controls.Action(actArea, "act-data", Label("Data"));
                {
                    var actNew = new Controls.Action(actData, "act-data-new", Label("New"));

                    var actDelete = new Controls.Action(actData, "act-data-delete", Label("Delete"));
                    actDelete.Triggering += ActDelete_Triggering;

                    var actRefresh = new Controls.Action(actData, "act-data-refresh", Label("Refresh"));
                    actRefresh.Triggering += ActRefresh_Triggering;
                }
            }
        }

        private void ActDelete_Triggering()
        {
            if (MultipleRows && (Selection.Count == 0)) return;

            string lbl;
            if ((!MultipleRows) || (Selection.Count == 1))
                lbl = Label("Delete {0}?", Rec!.UnitCaption);
            else
                lbl = Label("Delete selected {0} {1}?", Selection.Count,  Rec!.UnitCaption);

            new Confirm(lbl, () =>
            {
                if (MultipleRows)
                {
                    foreach (var i in Selection)
                    {
                        Rec!.SetDataset(DataSet[i]);
                        Rec!.Delete(true);
                    }

                    SendDataSet();
                }
                else
                {
                    Rec!.Delete(true);
                    Close();
                }
            }).RunModal();
        }

        private void ActRefresh_Triggering()
        {
            SendDataSet();
        }

        private void ApplyDataActions()
        {
            if ((!AllowInsert) || (Rec == null))
                Control<Controls.Action>("act-data-new")?.Detach();

            if ((!AllowDelete) || (Rec == null))
                Control<Controls.Action>("act-data-delete")?.Detach();

            if (Rec == null)
                Control<Controls.Action>("act-data-refresh")?.Detach();

            var actData = Control<Controls.Action>("act-data");
            if ((actData != null) && (actData.Items.Count == 0))
                actData.Detach();

            var actArea = Control<Controls.ActionArea>();
            if ((actArea != null) && (actArea.Items.Count == 0))
                actArea.Detach();
        }

        internal JArray GetSchema()
        {
            var result = new JArray();
            foreach (var field in DataFields)
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
            foreach (var field in DataFields)
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
            Selection.Clear();

            JArray jset = new();
            JArray jfSet = new();
            int count = 0;
            
            if (Rec != null)
            {
                if (Rec.FindSet())
                {
                    while (Rec.Read())
                    {
                        DataReading?.Invoke();
                        DataSet.Add(Rec.GetDataset());
                        jset.Add(GetDataRow(false));
                        jfSet.Add(GetDataRow(true));
                        count++;

                        if (!MultipleRows)
                            break;
                    }
                }
                else if (!MultipleRows)
                {
                    Rec.Init();
                    DataReading?.Invoke();
                    jset.Add(GetDataRow(false));
                    jfSet.Add(GetDataRow(true));
                    count++;
                }
            }
            else
            {
                DataReading?.Invoke();
                jset.Add(GetDataRow(false));
                jfSet.Add(GetDataRow(true));
                count++;
            }

            var result = new JObject();
            result["action"] = "dataset";
            result["pageid"] = UnitID.ToString();
            result["count"] = count;
            result["data"] = jset;
            result["fdata"] = jfSet;

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
            Loading?.Invoke();
            ApplyDataActions();
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
            Closing?.Invoke();

            var result = new JObject();
            result["pageid"] = UnitID.ToString();
            result["action"] = "closepage";

            Client.SendMessage(result);
        }

        [PublicAccess]
        internal void QueryClose()
        {
            bool canClose = true;
            QueryClosing?.Invoke(ref canClose);
            if (canClose)
                Close();
        }

        [PublicAccess]
        internal void SelectRows(int[] rows)
        {
            Selection.Clear();
            Selection.AddRange(rows);
        }

        [PublicAccess]
        internal void ControlInvoke(string controlid, string method, JObject? args = null)
        {
            var ctl = AllItems[controlid];
            var prx = Loader.Proxy.CreateFromObject(ctl);
            prx.SkipMethodSecurity = true;
            prx.Invoke(method, args);
        }

        public bool ControlExists(string name)
        {
            foreach (Controls.Control ctl in AllItems.Values)
                if (ctl.Name == name)
                    return true;

            return false;
        }

        public bool ControlExists<T>()
        {
            foreach (Controls.Control ctl in AllItems.Values)
                if (ctl.GetType() == typeof(T))
                    return true;

            return false;
        }

        public T? Control<T>(string name) where T : Controls.Control
        {
            foreach (Controls.Control ctl in AllItems.Values)
                if (ctl.Name == name)
                    return (T)ctl;

            return null;
        }

        public T? Control<T>() where T : Controls.Control
        {
            foreach (Controls.Control ctl in AllItems.Values)
                if (ctl.GetType() == typeof(T))
                    return (T)ctl;

            return null;
        }
    }

    public abstract class Page<T> : BasePage where T : BasePage
    {

    }

    public abstract class Page<T, R> : Page<T> where T : BasePage where R : BaseTable
    {
        public new R Rec
        {
            get { return (R)base.Rec!; }
            set { base.Rec = value; }
        }

        public Page()
        {
            Rec = (R)Activator.CreateInstance(typeof(R))!;
        }
    }

}
