using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public abstract class Control
    {
        public System.Guid ID { get; private set; }
        public Page? Page { get; protected set; }
        public Control? Parent { get; protected set; }
        public List<Controls.Control> Controls { get; private set; } = new();

        public Control()
        {
            ID = System.Guid.NewGuid();
        }

        protected void SetParent(Page page)
        {
            Parent = null;
            Page = page;
            Page.Controls.Add(this);
            Page.AllControls.Add(ID.ToString(), this);
        }

        protected void SetParent(Control parent)
        {
            Parent = parent;
            Page = parent.Page;
            Parent.Controls.Add(this);
            if (Page != null)
                Page.AllControls.Add(ID.ToString(), this);
        }

        internal virtual JObject Render()
        {
            var result = new JObject();
            result["id"] = ID.ToString();
            result["controlType"] = GetType().Name;

            var controls = new JArray();
            foreach (var c in Controls)
                controls.Add(c.Render());
            result["controls"] = controls;

            return result;
        }
    }
}
