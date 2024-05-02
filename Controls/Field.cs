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
        Html,
        TextArea
    }

    public enum InputMode
    {
        None,
        Text,
        Decimal,
        Numeric,
        Telephone,
        EMail
    }

    public class Field : Control
    {
        public string Caption { get; set; } = "";
        public Fields.BaseField SourceField { get; protected set; }
        public InputType InputType { get; set; }
        public event Fields.ValidatingHandler? Validating;
        public bool ReadOnly { get; set; } = false;
        public bool ShowCaption { get; set; } = true;
        public event ActionTriggerHandler? Triggering;
        public bool OpenRecord { get; set; } = false;
        public InputMode InputMode { get; set; } = InputMode.Text;
        public FontSize FontSize { get; set; } = FontSize.Small;
        public bool FontFixed { get; set; } = false;

#pragma warning disable CS8618
        public Field(Group group, string name, Shaper.Fields.BaseField baseField)
#pragma warning restore CS8618
        {
            Init(group, name, baseField);
        }

        public Field(Group group, Shaper.Fields.BaseField baseField) : this(group, "", baseField)
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
            SourceField = baseField;
            Caption = SourceField.Caption;
            InputType = InputType.Text;
            Name = name;

            if (Page != null)
                Page.DataFields.Add(baseField);
        }

        internal override JObject Render()
        {
            var jo = base.Render();
            jo["caption"] = Caption;
            jo["codename"] = SourceField.CodeName;
            jo["fieldType"] = SourceField.Type.ToString();
            jo["inputType"] = InputType.ToString();
            jo["readOnly"] = ReadOnly;
            jo["fontSize"] = FontSize.ToString();
            jo["fontFixed"] = FontFixed;
            jo["showCaption"] = ShowCaption;

            if (Parent != null)
                if (typeof(Group).IsAssignableFrom(Parent.GetType()))
                    if (((Group)Parent!).LabelStyle == LabelStyle.Placeholder)
                        jo["placeholder"] = Caption;

            if (Triggering?.GetInvocationList().Length > 0)
                jo["isLink"] = true;

            return jo;
        }

        public void Focus()
        {
            var jo = new JObject();
            jo["action"] = "focusControl";
            jo["controlId"] = ID.ToString();
            Client.SendMessage(jo);
        }

        internal void Trigger(int row)
        {
            Page!.SelectRow(row);
            Triggering?.Invoke();
        }

        internal JArray GetValues(string text)
        {
            JArray result = new();

            Fields.BaseField? bf = SourceField.TableRelations.Get()?.GetFieldTo();
            if (bf != null)
            {
                if (bf.Table!.TableLookup.Count == 0)
                    bf.Table!.TableLookup.Add(bf);

                bf.Table!.SetTextFilter(bf.Table!.TableLookup, text);

                if (bf.Table!.FindSet(100))
                    while (bf.Table!.Read())
                    {
                        JObject item = new();
                        item["value"] = bf.Serialize();
                        item["hasFormat"] = bf.HasFormat;
                        if (bf.HasFormat)
                            item["fvalue"] = bf.Format();

                        JArray display = new();
                        foreach (var df in bf.Table!.TableLookup)
                            display.Add(df.Format());

                        item["display"] = display;
                        result.Add(item);
                    }
            }

            return result;
        }

        internal void Validate(object? value, bool parseValue = true)
        {
            if (parseValue)
                SourceField.Evaluate(value!.ToString()!, out value);

            SourceField.Validate(value);
            Validating?.Invoke();
            Page!.AfterValidate(SourceField);
        }
    }
}
