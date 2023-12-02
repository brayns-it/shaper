using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class Cell : Control
    {
        public string Caption { get; protected set; } = "";
        public Fields.Field Field { get; protected set; }

        public Cell(Group group, Shaper.Fields.Field field)
        {
            SetParent(group);
            Field = field;
            Caption = Field.Caption;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            return jo;
        }

        internal void Validate(object? value, bool parseValue = true)
        {
            if (parseValue)
                value = Field.Evaluate(value!.ToString()!);

            Field.Validate(value);
        }
    }
}
