using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public enum InputType
    {
        Text,
        Password
    }

    public class Field : Control
    {
        public string Caption { get; protected set; } = "";
        public Fields.BaseField BaseField { get; protected set; }
        public InputType InputType { get; set; }
        public event Fields.ValidatingHandler? Validating;

        public Field(Group group, Shaper.Fields.BaseField baseField)
        {
            SetParent(group);
            BaseField = baseField;
            Caption = BaseField.Caption;
            InputType = InputType.Text;
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["codename"] = BaseField.CodeName;
            jo["inputType"] = InputType.ToString();
            return jo;
        }

        internal void Validate(object? value, bool parseValue = true)
        {
            if (parseValue)
                value = BaseField.Evaluate(value!.ToString()!);

            BaseField.Validate(value);
            Validating?.Invoke();

            Page!.SendDataRow();
        }
    }
}
