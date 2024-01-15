using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public delegate void SubpageFilterHandler<R>(R target) where R: BaseTable;

    public abstract class BaseSubpage : Control
    {
        public Type Part { get; init; }
        public BasePage? Instance { get; internal set; }
        public string Caption { get; set; } = "";

        public BaseSubpage(Control parent, Type pageType)
        {
            Attach(parent);
            Part = pageType;
        }

        internal virtual void ApplyFilter()
        {
        }

        internal void Run()
        {
            Instance = (BasePage)Activator.CreateInstance(Part)!;
            Instance.Parent = this;
            ApplyFilter();
            Instance.Run();
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["controlType"] = "Subpage";
            jo["caption"] = Caption;
            return jo;
        }
    }

    public class Subpage<T> : BaseSubpage where T : Page<T>
    {
        public Subpage(ContentArea contentArea, string name, string caption) : base(contentArea, typeof(T))
        {
            Name = name;
            Caption = caption;
        }

        public Subpage(ContentArea contentArea, string caption = "") : base(contentArea, typeof(T))
        {
            Caption = caption;
        }
    }

    public class Subpage<T, R> : BaseSubpage where T: Page<T, R> where R: BaseTable
    {
        public SubpageFilterHandler<R>? Filter { get; set; }

        public Subpage(ContentArea contentArea, string name, string caption) : base(contentArea, typeof(T))
        {
            Name = name;
            Caption = caption;
        }

        public Subpage(ContentArea contentArea, string caption = "") : base(contentArea, typeof(T))
        {
            Caption = caption;
        }

        public Subpage(DetailArea detailArea, string name, string caption) : base(detailArea, typeof(T))
        {
            Name = name;
            Caption = caption;
        }

        public Subpage(DetailArea detailArea, string caption = "") : base(detailArea, typeof(T))
        {
            Caption = caption;
        }

        internal override void ApplyFilter()
        {
            if (Instance!.Rec != null)
            {
                Instance!.Rec.TableFilterLevel = FilterLevel.Relations;
                Filter?.Invoke((R)Instance!.Rec);
            }
        }
    }
}
