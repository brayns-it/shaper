using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public abstract class Control
    {
        public System.Guid ID { get; private set; }
        public BasePage? Page { get; protected set; }
        public Control? Parent { get; protected set; }
        public List<Control> Items { get; private set; } = new();

        public Control()
        {
            ID = System.Guid.NewGuid();
        }

        protected void SetParent(BasePage page)
        {
            Parent = null;
            Page = page;
            Page.Items.Add(this);
            Page.AllItems.Add(ID.ToString(), this);
        }

        protected void SetParent(Control parent)
        {
            Parent = parent;
            Page = parent.Page;
            Parent.Items.Add(this);
            if (Page != null)
                Page.AllItems.Add(ID.ToString(), this);
        }

        internal virtual JObject Render()
        {
            var result = new JObject();
            result["id"] = ID.ToString();
            result["controlType"] = GetType().Name;

            var controls = new JArray();
            foreach (var c in Items)
                controls.Add(c.Render());
            result["controls"] = controls;

            return result;
        }
    }
}
