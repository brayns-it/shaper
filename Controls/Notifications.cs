using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class NotificationItem : Control
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public Icon? Icon { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;
        public event ActionTriggerHandler? Triggering;

        public void Attach(Notifications parent)
        {
            base.Attach(parent);
            parent.Items.Remove(this);

            int i = 0;
            while (i < parent.Items.Count)
            {
                if (parent.Items[i].GetType() != typeof(NotificationItem))
                    break;

                i++;
            }

            parent.Items.Insert(i, this);
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["title"] = Title;
            jo["icon"] = (Icon != null) ? Icon.ToString() : "";
            jo["description"] = Description;
            jo["dateTime"] = DateTime.ToString("G", Session.CultureInfo);
            return jo;
        }

        internal void Trigger()
        {
            Triggering?.Invoke();
        }
    }

    public class Notifications : Control
    {
        private Notifications(AppCenter center)
        {
            Attach(center);
        }

        public static Notifications Create(AppCenter center)
        {
            Notifications? ret = center.Page!.Control<Notifications>();
            if (ret == null)
                ret = new Notifications(center);
            return ret;
        }

        public void Clear()
        {
            List<Control> toDel = new();

            foreach (var item in Items)
                if (item.GetType() == typeof(NotificationItem))
                    toDel.Add(item);

            foreach (var item in toDel)
                this.Items.Remove(item);
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["emptyText"] = Label("No new notifications");
            return jo;
        }
    }
}
