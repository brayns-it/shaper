namespace Brayns.Shaper.Systems
{
    public class Progress : Page<Progress>
    {
        private decimal _total = 0;
        private decimal _count = 0;
        private DateTime _lastUpdate = DateTime.MinValue;
        private Dictionary<string, Controls.Html> Lines { get; } = new();

        public Progress()
        {
            UnitCaption = Label("Progress");
            PageType = PageTypes.Normal;

            var actionArea = Controls.ActionArea.Create(this);
            {
                var actionCancel = new Controls.Action(actionArea, Label("Cancel"));
                actionCancel.IsCancelation = true;
                actionCancel.Shortcut = "Escape";
            }
        }

        public void InitLine(string key, string value = "")
        {
            var contentArea = Controls.ContentArea.Create(this);
            var html = new Controls.Html(contentArea);
            html.Content = value;
            Lines.Add(key, html);
        }

        public void ResetTotal(int total)
        {
            _total = total;
            _count = 0;
        }

        public void UpdateLinePercent(string key, string value)
        {
            _count++;
            string prc = "";
            if (_total != 0)
                prc = (_count / _total * 100).ToString("0") + "%";
            value = value.Replace("%%", prc);
            UpdateLine(key, value);
        }

        public string UpdateLinePercent(string key, int count, int total)
        {
            string prc = "";
            if (total != 0)
                prc = (Convert.ToDecimal(count) / Convert.ToDecimal(total) * 100).ToString("0") + "%";
            UpdateLine(key, prc);
            return prc;
        }

        public void UpdateLine(string key, string value)
        {
            if (!Client.IsAllowed())
                return;

            if (Lines[key].Content != value)
            {
                Lines[key].Content = value;
                if (DateTime.Now.Subtract(_lastUpdate).TotalSeconds >= 1)
                {
                    Control<Controls.ContentArea>()!.Redraw();
                    _lastUpdate = DateTime.Now;
                }
            }
        }

        public new void Close()
        {
            if (!Client.IsAllowed())
                return;

            base.Close();
        }

        public void Show()
        {
            if (!Client.IsAllowed())
                return;

            RunModal();
        }
    }
}
