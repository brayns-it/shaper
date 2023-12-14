using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Controls
{
    public delegate void SubpageFilterHandler<R>(R target) where R: BaseTable;

    public abstract class BaseSubpage : Control
    {
        public Type Part { get; init; }
        public BasePage? Instance { get; internal set; }

        public BaseSubpage(ContentArea contentArea, string name, Type pageType)
        {
            Attach(contentArea);
            Name = name;
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
    }

    public class Subpage<T, R> : BaseSubpage where T: Page<T, R> where R: BaseTable
    {
        public SubpageFilterHandler<R>? Filter { get; set; }

        public Subpage(ContentArea contentArea, string name) : base(contentArea, name, typeof(T))
        {
        }

        public Subpage(ContentArea contentArea) : base(contentArea, "", typeof(T))
        {
        }

        internal override void ApplyFilter()
        {
            if (Instance!.Rec != null)
            {
                Instance!.Rec.TableFilterLevel = Fields.FilterLevel.Relations;
                Filter?.Invoke((R)Instance!.Rec);
            }
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["controlType"] = "Subpage";
            return jo;
        }
    }
}
