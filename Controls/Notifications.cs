using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public class NotificationItem
    {
        public string ID { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "";
        public DateTime DateTime { get; set; } = DateTime.Now; 
    }

    public delegate void NotificationsGettingHandler(List<NotificationItem> items);
    public delegate void NotificationTriggeringHandler(string notificationID);

    public class Notifications : Control
    {
        private List<string>? _cache;
        public event NotificationsGettingHandler? Getting;
        public event NotificationTriggeringHandler? Triggering;

        private Notifications(AppCenter center)
        {
            Attach(center);
            Page!.UnitPolling += Page_UnitPolling;
        }

        public static Notifications Create(AppCenter center)
        {
            Notifications? ret = center.Page!.Control<Notifications>();
            if (ret == null)
                ret = new Notifications(center);
            return ret;
        }

        private void Page_UnitPolling()
        {
            GetNotifications();
        }

        internal void Trigger(string notificationID)
        {
            Triggering?.Invoke(notificationID);
            GetNotifications();
        }

        private void GetNotifications()
        {
            List<NotificationItem> items = new();
            Getting?.Invoke(items);

            if (_cache != null)
            {
                bool diff = false;
                if (items.Count == _cache.Count)
                {
                    for (int i = 0; i < items.Count; i++)
                        if (_cache[i] != items[i].ID)
                            diff = true;
                }
                else
                    diff = true;

                if (!diff)
                    return;

                _cache.Clear();
            }
            else
                _cache = new List<string>();

            var jo = new JObject();
            var notifs = new JArray();

            foreach (var itm in items)
            {
                var jn = new JObject();
                jn["notificationID"] = itm.ID;
                jn["title"] = itm.Title;
                jn["description"] = itm.Description;
                jn["icon"] = itm.Icon;
                jn["age"] = Functions.Format(DateTime.Now.Subtract(itm.DateTime), 1);
                notifs.Add(jn);

                _cache.Add(itm.ID);
            }

            if (items.Count == 0)
            {
                var jn = new JObject();
                jn["description"] = Label("No new notifications");
                notifs.Add(jn);
            }

            jo["items"] = notifs;
            jo["action"] = "notifications";
            Client.SendMessage(jo);
        }
    }
}
