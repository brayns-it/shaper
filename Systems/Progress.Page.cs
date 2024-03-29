﻿namespace Brayns.Shaper.Systems
{
    public class Progress : Page<Progress>
    {
        private Dictionary<string, Controls.Html> Lines { get; } = new();
        private bool _cancelRequest = false;

        public Progress()
        {
            UnitCaption = Label("Progress");
            PageType = PageTypes.Normal;

            var actionArea = Controls.ActionArea.Create(this);
            {
                var actionCancel = new Controls.Action(actionArea, Label("Cancel"));
                actionCancel.Triggering += Cancel_Triggering;
                actionCancel.Shortcut = "Escape";
            }
        }

        public void InitLine(string key, string value)
        {
            var contentArea = Controls.ContentArea.Create(this);
            var html = new Controls.Html(contentArea);
            html.Content = value;
            Lines.Add(key, html);
        }

        public void UpdateLine(string key, string value)
        {
            if (!Client.IsAllowed())
                return;

            if (Lines[key].Content != value)
            {
                Lines[key].Content = value;
                Control<Controls.ContentArea>()!.Redraw();
                Client.Flush();
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
            Client.Flush();
        }

        private void Cancel_Triggering()
        {
            CurrentSession.Instance.StopRequested = true;
            Close();
        }
    }
}
