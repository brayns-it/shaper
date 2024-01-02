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
        Password,
        Html
    }

    public class Field : Control
    {
        public string Caption { get; set; } = "";
        public Fields.BaseField BaseField { get; protected set; }
        public InputType InputType { get; set; }
        public event Fields.ValidatingHandler? Validating;
        public bool ReadOnly { get; set; } = false;

#pragma warning disable CS8618
        public Field(Group group, string name, Shaper.Fields.BaseField baseField)
#pragma warning restore CS8618
        {
            Init(group, name, baseField);
        }

        public Field(Group group, Shaper.Fields.BaseField baseField) : this (group, "", baseField)
        {
        }

#pragma warning disable CS8618
        public Field(Grid grid, string name, Shaper.Fields.BaseField baseField)
#pragma warning restore CS8618
        {
            Init(grid, name, baseField);
        }

        public Field(Grid grid, Shaper.Fields.BaseField baseField) : this(grid, "", baseField)
        {
        }

        private void Init(Control parent, string name, Shaper.Fields.BaseField baseField)
        {
            Attach(parent);
            BaseField = baseField;
            Caption = BaseField.Caption;
            InputType = InputType.Text;
            Name = name;

            if (Page != null)
                Page.DataFields.Add(baseField);
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["codename"] = BaseField.CodeName;
            jo["fieldType"] = BaseField.Type.ToString();
            jo["inputType"] = InputType.ToString();
            jo["readOnly"] = ReadOnly;
            return jo;
        }

        internal void Validate(object? value, bool parseValue = true)
        {
            if (parseValue)
                value = BaseField.Evaluate(value!.ToString()!);

            BaseField.Validate(value);
            Validating?.Invoke();
            Page!.AfterValidate(BaseField);
        }
    }
}
