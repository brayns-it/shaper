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

        public Field(Group group, string name, Shaper.Fields.BaseField baseField)
        {
            Init(group, name, baseField);
        }

        public Field(Group group, Shaper.Fields.BaseField baseField) : this (group, "", baseField)
        {
        }

        public Field(Grid grid, string name, Shaper.Fields.BaseField baseField)
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
            jo["inputType"] = InputType.ToString();
            return jo;
        }

        internal void Validate(object? value, bool parseValue = true)
        {
            if (parseValue)
                value = BaseField.Evaluate(value!.ToString()!);

            BaseField.Validate(value);
            Validating?.Invoke();

            if ((Page!.Rec != null) && Page!.Rec.UnitFields.Contains(BaseField))
            {
                bool isKey = Page!.Rec.TablePrimaryKey.Contains(BaseField);
                bool lastKey = (Page!.Rec.TablePrimaryKey.IndexOf(BaseField) == (Page!.Rec.TablePrimaryKey.Count - 1));
                
                if ((isKey && lastKey) || (!isKey))
                {
                    if (Page!.Rec.TableVersion == DBNull.Value)
                        Page!.Rec.Insert(true);
                    else
                        Page!.Rec.Modify(true);
                }
            }

            Page!.SendDataRow();
        }
    }
}
