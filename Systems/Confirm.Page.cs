namespace Brayns.Shaper.Systems
{
    public class Confirm : Page<Confirm>
    {
        public Confirm(string text = "", Controls.ActionTriggerHandler? onYes = null, Controls.ActionTriggerHandler? onNo = null)
        {
            UnitName = "Confirm";
            UnitCaption = Label("Confirm");
            PageType = PageTypes.Normal;

            var contentArea = Controls.ContentArea.Create(this);
            {
                var html = new Controls.Html(contentArea);
                if (text != null) html.Content = text;
            }

            var actionArea = Controls.ActionArea.Create(this);
            {
                var actionYes = new Controls.Action(actionArea, Label("Yes"));
                actionYes.Triggering += Actions_Triggering;
                if (onYes != null) actionYes.Triggering += onYes;

                var actionNo = new Controls.Action(actionArea, Label("No"));
                actionNo.Triggering += Actions_Triggering;
                actionNo.Shortcut = "Escape";
                if (onNo != null) actionNo.Triggering += onNo;
            }
        }

        private void Actions_Triggering()
        {
            Close();
        }
    }
}
