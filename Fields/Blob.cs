﻿using Newtonsoft.Json.Linq;

namespace Brayns.Shaper.Fields
{
    public class Blob : BaseField
    {
        public new byte[]? Value
        {
            get { return (byte[]?)base.Value; }
            set { base.Value = CheckValue(value); }
        }

        public new byte[]? XValue
        {
            get { return (byte[]?)base.XValue!; }
            set { base.XValue = CheckValue(value); }
        }

        public new byte[]? InitValue
        {
            get { return (byte[]?)base.InitValue!; }
            set { base.InitValue = CheckValue(value); }
        }

        public Blob(string name, string caption)
        {
            Type = FieldTypes.BLOB;
            Name = name;
            Caption = caption;
            Value = null;
            XValue = null;
            InitValue = null;
            TestValue = null;

            Create();
        }

        public Blob(string caption) : this("", caption)
        {
        }

        public override void Evaluate(string text, out object? result)
        {
            throw new NotImplementedException();
        }

        internal override object? CheckValue(object? value)
        {
            return (byte[]?)value;
        }

        public override string Format(object? value)
        {
            if ((value == null) || (((byte[])value).Length == 0))
                return "";
            else
                return "*";
        }

        public override JValue Serialize(object? value)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(JValue? value, out object? result)
        {
            throw new NotImplementedException();
        }
    }
}
