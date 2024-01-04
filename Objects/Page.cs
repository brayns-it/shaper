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
        internal bool OpenAsNew { get; set; } = false;
        internal BasePage? SourcePage { get; set; }
        internal Controls.BaseSubpage? Parent { get; set; }
        internal int PageSize { get; set; } = 100;
        internal int Offset { get; set; } = 0;

        public List<Controls.Control> Items { get; private set; } = new();
        public PageTypes PageType { get; protected set; }
        public BaseTable? Rec { get; set; }
        public Type? Card { get; set; }
        public event GenericHandler? Inserting;
        public event GenericHandler? Renaming;
        public event GenericHandler? Loading;
        public event GenericHandler? Closing;
        public event PageQueryCloseHandler? QueryClosing;
        public event GenericHandler? DataReading;
        public event GenericHandler? TableSelecting;
        public event GenericHandler? CaptionSetting;
        public bool AllowInsert { get; set; } = true;
        public bool AllowDelete { get; set; } = true;
        public bool AllowModify { get; set; } = true;
        public bool AutoIncrementKey { get; set; } = false;

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
                    actNew.Triggering += ActNew_Triggering;

                    var actDelete = new Controls.Action(actData, "act-data-delete", Label("Delete"));
                    actDelete.Triggering += ActDelete_Triggering;

                    var actRefresh = new Controls.Action(actData, "act-data-refresh", Label("Refresh"));
                    actRefresh.Triggering += ActRefresh_Triggering;
                }
            }
        }

        private void ActNew_Triggering()
        {
            if (Card != null)
            {
                var c = (BasePage)Activator.CreateInstance(Card)!;
                c.OpenAsNew = true;
                c.SourcePage = this;
                c.Closing += SendDataSet;

                if (Rec != null)
                    c.Rec!.CopyFilters(Rec);

                c.Run();
            }
            else
            {
                InitNewRec();
                SendDataRow();
                SendCaption();
            }
        }

        private void ActDelete_Triggering()
        {
            if (MultipleRows && (Selection.Count == 0)) return;

            string lbl;
            if ((!MultipleRows) || (Selection.Count == 1))
                lbl = Label("Delete {0}?", Rec!.UnitCaption);
            else
                lbl = Label("Delete selected {0} {1}?", Selection.Count, Rec!.UnitCaption);

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

                    if ((SourcePage != null) && (SourcePage.Rec != null) && (SourcePage.Rec!.GetType() == Rec.GetType()))
                        SourcePage.SendDataSet();
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

            if (AllowInsert && MultipleRows && (Card == null))
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

        private void ApplyPermissions()
        {
            List<Controls.Control> toDel = new();
            List<Controls.Control> parents = new();

            foreach (var c in AllItems.Values.OfType<Controls.Action>())
            {
                Type? t = null;
                if (c.Run != null) t = c.Run;
                else if (c.PermissionBy != null) t = c.PermissionBy;
                if (t == null) continue;

                if (!Loader.Permissions.IsAllowed(t, Loader.PermissionType.Execute, false))
                    toDel.Add(c);
            }

            while (true)
            {
                foreach (var c in toDel)
                {
                    c.Detach();
                    if (c.Parent != null)
                        if (!parents.Contains(c.Parent))
                            parents.Add(c.Parent);
                }

                toDel.Clear();
                foreach (var c in parents)
                {
                    if (c.GetType() == typeof(Controls.NavigationPane)) continue;
                    if (c.Items.Count == 0)
                        toDel.Add(c);
                }

                parents.Clear();
                if (toDel.Count == 0)
                    break;
            }
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

            if (Selection.Count > 0)
                result["selectedrow"] = Selection[0];

            Client.SendMessage(result);
        }

        [PublicAccess]
        internal void Search(string text)
        {
            Rec!.Reset(Fields.FilterLevel.Or);
            Rec!.TableFilterLevel = Fields.FilterLevel.Or;

            if (text.Trim().Length > 0)
                foreach (var f in DataFields)
                {
                    if ((f.Type == Fields.FieldTypes.CODE) || (f.Type == Fields.FieldTypes.TEXT))
                        f.SetFilter("*" + text + "*");
                    else
                        f.SetFilter(text);

                    int n = f.Filters.Count - 1;
                    try
                    {
                        List<object> vals = new();
                        f.Filters[n].Tokenize(vals);
                    }
                    catch
                    {
                        f.Filters.RemoveAt(n);
                    }
                }

            SendDataSet();
        }

        [PublicAccess]
        internal void Sort(string sortBy, bool ascending)
        {
            Rec!.TableSort.Clear();
            if (sortBy.Length > 0)
                foreach (var f in DataFields)
                    if (f.CodeName == sortBy)
                        Rec!.TableSort.Add(f);

            Rec!.TableAscending = ascending;
            GetData(0);
        }

        [PublicAccess]
        internal void GetData(int offset)
        {
            Offset = offset;
            SendDataSet();
        }

        internal void SendDataSet()
        {
            DataSet.Clear();
            Selection.Clear();

            JArray jset = new();
            JArray jfSet = new();

            var result = new JObject();
            result["pageSize"] = PageSize;

            if (Rec != null)
            {
                TableSelecting?.Invoke();

                if ((!OpenAsNew) && MultipleRows)
                    result["count"] = Rec.Count();

                if ((!OpenAsNew) && Rec.FindSet(PageSize, Offset))
                {
                    while (Rec.Read())
                    {
                        DataReading?.Invoke();
                        DataSet.Add(Rec.GetDataset());
                        jset.Add(GetDataRow(false));
                        jfSet.Add(GetDataRow(true));

                        if (!MultipleRows)
                            break;
                    }
                }
                else if (!MultipleRows)
                {
                    InitNewRec();
                    DataReading?.Invoke();
                    jset.Add(GetDataRow(false));
                    jfSet.Add(GetDataRow(true));
                }
            }
            else
            {
                DataReading?.Invoke();
                jset.Add(GetDataRow(false));
                jfSet.Add(GetDataRow(true));
            }

            result["action"] = "dataset";
            result["pageid"] = UnitID.ToString();
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
            result["locale"] = Session.CultureInfo.Name.ToLower();

            if (Parent != null)
            {
                result["parentId"] = Parent.ID.ToString();
                result["parentType"] = Parent.Parent!.GetType().Name;

                if (Parent.Caption.Length > 0)
                    Control<Controls.Action>("act-data")!.Caption = Parent.Caption;
            }

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
            if (Rec != null)
                Rec.TableFilterLevel = Fields.FilterLevel.Public;

            Loading?.Invoke();
            ApplyDataActions();
            ApplyPermissions();
            SessionRegister();
            SendPage(modal);
            SendDataSet();
            SendCaption();

            foreach (var c in AllItems.Values)
                if (typeof(Controls.BaseSubpage).IsAssignableFrom(c.GetType()))
                    ((Controls.BaseSubpage)c).Run();
        }

        public void Run()
        {
            Run(false);
        }

        public void RunModal()
        {
            Run(true);
        }

        internal void SendCaption()
        {
            if ((Rec != null) && (!MultipleRows))
            {
                if (Rec.TableVersion == DBNull.Value)
                {
                    UnitCaption = Label("New {0}", Rec.UnitCaption);
                }
                else if (SourcePage != null)
                {
                    var pk = Rec.TablePrimaryKey[Rec.TablePrimaryKey.Count - 1];
                    UnitCaption = pk.Format(pk.Value);
                }
            }


            CaptionSetting?.Invoke();

            var result = new JObject();
            result["action"] = "property";
            result["pageid"] = UnitID.ToString();
            result["target"] = "page";
            result["property"] = "caption";
            result["value"] = UnitCaption;

            Client.SendMessage(result);
        }

        [PublicAccess]
        internal void OpenRecord()
        {
            if (Card == null)
                return;

            var pk = Rec!.PrimaryKeyValues();

            var c = (BasePage)Activator.CreateInstance(Card!)!;
            c.Rec!.FilterByPrimaryKey(pk);
            c.SourcePage = this;
            c.Closing += RefreshRow;
            c.Run();
        }

        internal void RefreshRow()
        {
            if (Rec == null)
                return;

            if (Rec.Refresh())
            {
                if (Selection.Count > 0)
                    DataSet[Selection[0]] = Rec!.GetDataset();

                SendDataRow();
            }
        }

        public void Update()
        {
            SendDataRow();
            Client.Flush();
        }

        public void Close()
        {
            Closing?.Invoke();
            SessionUnregister();

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

        internal void SelectRow(int row)
        {
            SelectRows([row]);
        }

        [PublicAccess]
        internal void SelectRows(int[] rows)
        {
            Selection.Clear();
            Selection.AddRange(rows);

            if (Selection.Count > 0)
            {
                Rec!.SetDataset(DataSet[Selection[0]]);

                foreach (var c in AllItems.Values.OfType<Controls.BaseSubpage>())
                {
                    c.ApplyFilter();
                    c.Instance!.SendDataSet();
                }
            }
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

        public Controls.Control? Control(string name)
        {
            foreach (Controls.Control ctl in AllItems.Values)
                if (ctl.Name == name)
                    return ctl;

            return null;
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

        private void InitNewRec()
        {
            Rec!.Init();

            foreach (var f in Rec!.UnitFields)
            {
                object? val = f.GetFilterValue();
                if (val != null)
                    f.Value = val;
            }
        }

        private void HandleAutoIncrementKey()
        {
            if (!AutoIncrementKey)
                return;

            var lastKey = Rec!.TablePrimaryKey[Rec.TablePrimaryKey.Count - 1];
            if (lastKey.Type != Fields.FieldTypes.INTEGER)
                return;

            int val = (int)lastKey.Value!;
            if (val != 0)
                return;

            var rec2 = (BaseTable)Activator.CreateInstance(Rec.GetType())!;
            rec2.FilterByPrimaryKey(Rec.PrimaryKeyValues());

            var lastKey2 = rec2.TablePrimaryKey[rec2.TablePrimaryKey.Count - 1];
            lastKey2.SetRange();
            if (rec2.FindLast())
                lastKey.Value = 10000 + (int)lastKey2.Value!;
            else
                lastKey.Value = 10000;
        }

        internal void ReapplySubpageFilters()
        {
            foreach (var c in AllItems.Values)
                if (typeof(Controls.BaseSubpage).IsAssignableFrom(c.GetType()))
                    ((Controls.BaseSubpage)c).ApplyFilter();
        }

        internal void AfterValidate(Fields.BaseField field)
        {
            if ((Rec != null) && Rec.UnitFields.Contains(field))
            {
                bool isKey = Rec.TablePrimaryKey.Contains(field);
                bool lastKey = (Rec.TablePrimaryKey.IndexOf(field) == (Rec.TablePrimaryKey.Count - 1));

                if (Rec.TableVersion == DBNull.Value)
                {
                    if ((isKey && lastKey) || (!isKey))
                    {
                        HandleAutoIncrementKey();
                        Rec.Insert(true);
                        Inserting?.Invoke();
                        SendCaption();
                        ReapplySubpageFilters();
                    }
                }
                else
                {
                    if (isKey)
                    {
                        Rec.Rename();
                        Renaming?.Invoke();
                        SendCaption();

                        if ((SourcePage != null) && (SourcePage.Rec != null) && (SourcePage.Rec!.GetType() == Rec.GetType()))
                            SourcePage.SendDataSet();
                    }
                    else
                    {
                        Rec.Modify(true);
                    }
                }
            }

            SendDataRow();
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
            if (Loader.Loader.HasAttribute<VirtualTable>(typeof(R)))
                Rec.Connect(true);
        }

        public void SetSelectionFilter(R table)
        {
            List<Dictionary<string, object>> selDs = new();
            foreach (var n in Selection)
                selDs.Add(DataSet[n]);

            table.SetSelection(selDs);
        }
    }

}
