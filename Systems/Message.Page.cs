﻿namespace Brayns.Shaper.Systems
{
    public class Message : Page<Message>
    {
        public Message(string text = "")
        {
            UnitCaption = Label("Message");
            PageType = PageTypes.Normal;

            var contentArea = Controls.ContentArea.Create(this);
            {
                var html = new Controls.Html(contentArea);
                if (text != null) html.Content = text;
            }

            var actionArea = Controls.ActionArea.Create(this);
            {
                var actionOk = new Controls.Action(actionArea, Label("OK"));
                actionOk.Triggering += Actions_Triggering;
                actionOk.Shortcut = "Escape";
            }
        }

        public static void Show(string text)
        {
            if (!Client.IsAllowed())
                return;

            var msg = new Message(text);
            msg.RunModal();
        }

        private void Actions_Triggering()
        {
            Close();
        }
    }
}
