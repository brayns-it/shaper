using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public abstract class Control
    {
        public System.Guid ID { get; private set; }
        public BasePage? Page { get; protected set; }
        public Control? Parent { get; protected set; }
        public List<Control> Items { get; private set; } = new();
        public String Name { get; protected set; }

        public Control()
        {
            ID = System.Guid.NewGuid();
            Name = "";
        }

        public void MoveFirst()
        {
            if (Parent == null) return;

            int n = Parent.Items.IndexOf(this);
            if (n > 0)
            {
                Parent.Items.Remove(this);
                Parent.Items.Insert(0, this);
            }
        }

        public void Detach()
        {
            if (Parent != null)
                Parent.Items.Remove(this);
            if (Page != null)
            {
                if (Page.Items.Contains(this))
                    Page.Items.Remove(this);
                Page.AllItems.Remove(ID.ToString());
            }
        }

        protected void Attach(BasePage page)
        {
            Parent = null;
            Page = page;
            Page.Items.Add(this);
            Page.AllItems.Add(ID.ToString(), this);
        }

        protected void Attach(Control parent)
        {
            Parent = parent;
            Page = parent.Page;
            Parent.Items.Add(this);
            if (Page != null)
            {
                Page.AllItems.Add(ID.ToString(), this);
            }
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
