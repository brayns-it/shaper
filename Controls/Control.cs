﻿using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public enum FontSize
    {
        ExtraSmall,
        Small,
        Medium,
        Large,
        ExtraLarge
    }

    public abstract class Control
    {
        public System.Guid ID { get; private set; }
        public BasePage? Page { get; protected set; }
        public Control? Parent { get; protected set; }
        public List<Control> Items { get; private set; } = new();
        public String Name { get; protected set; }
        public bool Visible { get; set; } = true;
        public Object? Tag { get; set; }

        public Control()
        {
            ID = System.Guid.NewGuid();
            Name = "";
        }

        internal bool HasParentOfType(Type t)
        {
            Control? p = Parent;
            while (p != null)
            {
                if (p.GetType() == t)
                    return true;

                p = p.Parent;
            }
            return false;
        }

        protected Control? ItemByName(string name)
        {
            foreach (Control ctl in Items)
                if (ctl.Name == name)
                    return ctl;
            return null;
        }

        public void MoveFirst()
        {
            if (Parent == null) return;
            if (!Parent.Items.Contains(this)) return;

            Parent.Items.Remove(this);
            Parent.Items.Insert(0, this);
        }

        public void MoveLast()
        {
            if (Parent == null) return;
            if (!Parent.Items.Contains(this)) return;

            Parent.Items.Remove(this);
            Parent.Items.Add(this);
        }

        public void MoveAfter(string anchor)
        {
            if (Parent == null) return;
            if (!Parent.Items.Contains(this)) return;

            var ctlAnchor = Parent.ItemByName(anchor);
            if (ctlAnchor == null) return;

            Parent.Items.Remove(this);

            int n = Parent.Items.IndexOf(ctlAnchor);
            if (n == (Parent.Items.Count - 1))
                Parent.Items.Add(this);
            else
                Parent.Items.Insert(n, this);
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

        public void Redraw()
        {
            var result = Render();
            result["action"] = "ui";
            result["command"] = "redrawControl";
            result["pageid"] = Page!.UnitID.ToString();
            Client.SendMessage(result);
        }

        internal virtual JObject Render()
        {
            var result = new JObject();
            result["id"] = ID.ToString();
            result["controlType"] = GetType().Name;

            var controls = new JArray();
            foreach (var c in Items)
                if (c.Visible)
                    controls.Add(c.Render());
            result["controls"] = controls;

            return result;
        }
    }
}
