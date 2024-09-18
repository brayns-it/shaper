using System.Xml;
using System.Text;
using System.Reflection;
using System.Globalization;

namespace Brayns.Shaper.Soap
{
    public delegate void SoapHandler<T, U>(T request, U response) where T : class where U : class;

    [Soap(URI = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class SoapFault
    {
        public string? faultstring { get; set; }

        public static void Send(RawSession raw, Exception ex)
        {
            var fault = new SoapFault();
            fault.faultstring = ex.Message;

            var ser = new SoapSerializer<SoapFault>(fault);

            raw.ResponseType = "text/xml; charset=utf-8";
            raw.Response = ser.Serialize();
        }
    }

    public class SoapSession<T, U> where T : class where U : class
    {
        public RawSession RawSession { get; init; }
        public T Request { get; init; }
        public SoapSettings Settings { get; init; }
        public event SoapHandler<T, U>? Executing;

        public SoapSession(RawSession raw, SoapSettings? settings = null)
        {
            RawSession = raw;
            Settings = (settings != null) ? settings : new();

            var deser = new SoapDeserializer<T>(raw.Request!, settings);
            Request = deser.Deserialize();
        }

        public void Execute()
        {
            var response = Activator.CreateInstance<U>();
            Executing?.Invoke(Request, response);

            var ser = new SoapSerializer<U>(response, Settings);

            RawSession.ResponseType = "text/xml; charset=utf-8";
            RawSession.Response = ser.Serialize();
            string z = Encoding.UTF8.GetString(RawSession.Response);
        }
    }

    public class SoapSettings
    {
        public List<string> Flags { get; } = new();
    }

    public class SoapDeserializer<T> where T : class
    {
        private XmlDocument Document { get; init; }
        private List<SoapObject> SoapObjects = new();
        private XmlNode Body { get; init; }
        private SoapSettings Settings { get; init; }

        public SoapDeserializer(string content, SoapSettings? settings = null)
        {
            Document = new XmlDocument();
            Document.LoadXml(content);

            Body = Document.SelectSingleNode("//*[local-name() = 'Body']")!;

            Settings = (settings != null) ? settings : new();
        }

        public SoapDeserializer(byte[] buf, SoapSettings? settings = null) : this(Encoding.UTF8.GetString(buf), settings)
        {
        }

        public T Deserialize()
        {
            var result = Activator.CreateInstance<T>();

            SoapObjects.Add(new() { Object = result, ParentNode = Body.FirstChild });

            while (SoapObjects.Count > 0)
            {
                var soapObj = SoapObjects[0];
                SoapObjects.RemoveAt(0);

                if (IsArray(soapObj.Object!))
                {
                    Type? arrType = GetArrayType(soapObj.Object!);
                    int index = 0;
                    foreach (XmlNode child in soapObj.ParentNode!.ChildNodes)
                    {
                        AppendToArray(index, soapObj.Object!, DeserializeValue(arrType!, child));
                        index++;
                    }
                }
                else
                {
                    foreach (XmlNode child in soapObj.ParentNode!.ChildNodes)
                    {
                        foreach (var prop in soapObj.Object!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        {
                            var soapType = new SoapType(prop, Settings);
                            if (soapType.Ignore) 
                                continue;

                            if (soapType.PropertyName!.Equals(child.LocalName, StringComparison.OrdinalIgnoreCase))
                            {
                                var val = DeserializeValue(prop.PropertyType, child);
                                if (val != null)
                                    prop.SetValue(soapObj.Object!, val);
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }

        private void AppendToArray(int index, object obj, object? val)
        {
            Type t = obj.GetType();

            if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>)))
            {
                ((System.Collections.IList)obj).Add(val);
                return;
            }

            if (t.IsArray)
            {
                ((Array)obj).SetValue(val, index);
                return;
            }
        }

        private Type? GetArrayType(object obj)
        {
            Type t = obj.GetType();

            if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>)))
                return t.GetGenericArguments()[0];

            if (t.IsArray)
                return t.GetElementType()!;

            return null;
        }

        private bool IsArray(object obj)
        {
            Type t = obj.GetType();

            if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>)))
                return true;

            if (t.IsArray)
                return true;

            return false;
        }

        private object? DeserializeValue(Type type, XmlNode node)
        {
            var nt = Nullable.GetUnderlyingType(type);
            if (nt != null)
                type = nt;

            string value = node.InnerText.Trim();

            if (type == typeof(string))
                return value;

            if (type == typeof(int))
            {
                if (value.Length == 0) return null;
                return int.Parse(node.InnerText);
            }

            if (type == typeof(double))
            {
                if (value.Length == 0) return null;

                var nfi = new NumberFormatInfo();
                nfi.NumberDecimalSeparator = ".";
                nfi.NumberGroupSeparator = "";

                return double.Parse(node.InnerText, nfi);
            }

            if (type == typeof(DateTime))
            {
                if (value.Length == 0) return null;

                DateTime dt;
                if (DateTime.TryParse(node.InnerText, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    return dt;

                return null;
            }

            if (type == typeof(bool))
            {
                if (value.Length == 0) return null;
                return node.InnerText.Equals("true", StringComparison.OrdinalIgnoreCase) ? true : false;
            }

            object? result = null;

            if (type.IsClass && (!type.IsArray))
                result = Activator.CreateInstance(type);

            var propNode = node;

            var href = GetAttribute(node, "href");
            if (href != null)
                propNode = Document.SelectSingleNode("//*[@id = '" + href.Substring(1) + "']");

            if (type.IsArray)
                result = Array.CreateInstance(type.GetElementType()!, propNode!.ChildNodes.Count);

            SoapObjects.Add(new() { Object = result, ParentNode = propNode });

            return result;
        }

        private string? GetAttribute(XmlNode nod, string name)
        {
            if (nod.Attributes != null)
                foreach (XmlAttribute attr in nod.Attributes)
                    if (attr.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return attr.Value;

            return null;
        }
    }

    public class SoapSerializer<T> where T : class
    {
        private int NsCount { get; set; } = 0;
        private List<object> IdBuffer { get; set; } = new();
        private List<SoapObject> SoapObjects { get; set; } = new();
        private XmlDocument Document { get; init; }
        private T Object { get; init; }
        private XmlNode Envelope { get; init; }
        private XmlNode Body { get; init; }
        private SoapSettings Settings { get; init; }

        public SoapSerializer(T obj, SoapSettings? settings = null)
        {
            Object = obj;
            Settings = (settings != null) ? settings : new();

            Document = new XmlDocument();

            Envelope = Document.CreateElement("soap", "Envelope", SoapType.SOAP_URI);
            Document.AppendChild(Envelope);

            AddAttribute(Envelope, "xmlns:xsi", SoapType.XSI_URI);
            AddAttribute(Envelope, "xmlns:xsd", SoapType.XSD_URI);
            AddAttribute(Envelope, "xmlns:soapenc", SoapType.SOAPENC_URI);

            Body = Document.CreateElement("soap", "Body", SoapType.SOAP_URI);
            Envelope.AppendChild(Body);

            AddAttribute(Body, "encodingStyle", SoapType.SOAP_URI, SoapType.SOAPENC_URI);

            SoapObjects.Add(new() { Object = Object, ParentNode = Body });
        }

        private void AddAttribute(XmlNode node, string localName, string value)
        {
            var attr = Document.CreateAttribute(localName);
            attr.InnerText = value;
            node.Attributes!.Append(attr);
        }

        private void AddAttribute(XmlNode node, string localName, string URI, string value)
        {
            var attr = Document.CreateAttribute(localName, URI);
            attr.InnerText = value;
            node.Attributes!.Append(attr);
        }

        private string? LookupPrefix(XmlNode? node, string URI, bool create = false)
        {
            while (true)
            {
                if (node == null) break;

                if (node.NamespaceURI.Equals(URI, StringComparison.OrdinalIgnoreCase))
                    return node.Prefix;

                if (node.Attributes != null)
                    foreach (XmlAttribute attr in node.Attributes!)
                    {
                        if (attr.Prefix.Equals("xmlns", StringComparison.OrdinalIgnoreCase) &&
                            attr.Value.Equals(URI, StringComparison.OrdinalIgnoreCase))
                        {
                            return attr.LocalName;
                        }
                    }

                node = node.ParentNode;
            }

            if (create)
            {
                NsCount++;
                return "q" + NsCount.ToString();
            }

            return null;
        }

        private string GetXsiType(XmlNode? node, SoapType soapType)
        {
            if (soapType.URI.Length > 0)
                return LookupPrefix(node, soapType.URI)! + ":" + soapType.Name;
            else
                return soapType.Name;
        }

        private List<object> GetArray(object obj)
        {
            List<object> result = new();

            if (obj.GetType().IsGenericType && (obj.GetType().GetGenericTypeDefinition() == typeof(List<>)))
            {
                foreach (var item in (System.Collections.ICollection)obj)
                    result.Add(item);
            }

            return result;
        }

        public string SerializeAsText()
        {
            return Encoding.UTF8.GetString(Serialize());
        }

        public byte[] Serialize()
        {
            while (SoapObjects.Count > 0)
            {
                var soapObj = SoapObjects[0];
                SoapObjects.RemoveAt(0);

                XmlNode? node = null;
                List<object>? array = null;

                var soapType = new SoapType(soapObj.Object!.GetType(), Settings);
                if (soapType.IsArray)
                    array = GetArray(soapObj.Object!);

                if (soapType.URI.Length > 0)
                    node = Document.CreateElement(LookupPrefix(soapObj.ParentNode, soapType.URI, true), soapType.Name, soapType.URI);
                else
                    node = Document.CreateElement(soapType.Name);

                soapObj.ParentNode!.AppendChild(node);

                if (soapObj.ID != null)
                {
                    AddAttribute(node, "id", soapObj.ID);

                    if (soapType.IsArray)
                    {
                        if ((soapType.ItemType!.URI.Length > 0) && (LookupPrefix(node, soapType.ItemType!.URI) == null))
                        {
                            var pfix = LookupPrefix(node, soapType.ItemType!.URI, true);
                            AddAttribute(node, "xmlns:" + pfix, soapType.ItemType!.URI);
                        }

                        var xsiType = GetXsiType(node, soapType.ItemType!);
                        xsiType += "[" + array!.Count.ToString() + "]";
                        AddAttribute(node, "arrayType", SoapType.SOAPENC_URI, xsiType);
                    }
                    else
                        AddAttribute(node, "type", SoapType.XSI_URI, GetXsiType(node, soapType));
                }

                if (soapType.IsArray)
                {
                    foreach (var itemObj in array!)
                        AppendChild(node, soapType.ItemType!, itemObj);
                }
                else
                {
                    foreach (var prop in soapObj.Object!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        var propType = new SoapType(prop, Settings);
                        AppendChild(node, propType, prop.GetValue(soapObj.Object!));
                    }
                }
            }

            var wrtSett = new XmlWriterSettings();
            wrtSett.Indent = true;
            wrtSett.Encoding = new UTF8Encoding(false);
            var ms = new MemoryStream();
            var xmlWrt = XmlWriter.Create(ms, wrtSett);
            Document.WriteTo(xmlWrt);
            xmlWrt.Flush();
            byte[] buf = ms.ToArray();
            ms.Close();
            return buf;
        }

        private void AppendChild(XmlNode node, SoapType soapType, object? propertyValue)
        {
            if (soapType.Ignore)
                return;

            if (propertyValue == null)
            {
                AddAttribute(node, "nil", SoapType.XSD_URI, "true");
                return;
            }

            var propertyNode = Document.CreateElement(soapType.PropertyName!);

            if (soapType.IsValue)
            {
                SerializeValue(propertyNode, soapType, propertyValue);
                node.AppendChild(propertyNode);
                return;
            }

            if (propertyValue!.GetType().IsClass)
            {
                var propID = "id";

                if (IdBuffer.Contains(propertyValue))
                {
                    propID += (IdBuffer.IndexOf(propertyValue) + 1).ToString();
                }
                else
                {
                    IdBuffer.Add(propertyValue);
                    propID += IdBuffer.Count().ToString();

                    SoapObjects.Add(new() { Object = propertyValue, ParentNode = Body, ID = propID });
                }

                AddAttribute(propertyNode, "href", "#" + propID);
                node.AppendChild(propertyNode);
            }
        }

        private void SerializeValue(XmlNode node, SoapType soapType, object? propertyValue)
        {
            AddAttribute(node, "type", SoapType.XSI_URI, "xsd:" + soapType.Name);

            if (soapType.Type == typeof(string))
                node.InnerText = propertyValue!.ToString()!;

            if (soapType.Type == typeof(bool))
                node.InnerText = ((bool)propertyValue!) ? "true" : "false";

            if (soapType.Type == typeof(int))
                node.InnerText = ((int)propertyValue!).ToString();

            if (soapType.Type == typeof(double))
            {
                var nfi = new NumberFormatInfo();
                nfi.NumberDecimalSeparator = ".";
                nfi.NumberGroupSeparator = "";

                node.InnerText = ((double)propertyValue!).ToString("0.000000", nfi);
            }

            if (soapType.Type == typeof(DateTime))
                node.InnerText = ((DateTime)propertyValue!).ToString("o");
        }
    }

    internal class SoapObject
    {
        public object? Object { get; set; }
        public XmlNode? ParentNode { get; set; }
        public string? ID { get; set; }
    }

    internal class SoapType
    {
        public const string XSD_URI = "http://www.w3.org/2001/XMLSchema";
        public const string SOAPENC_URI = "http://schemas.xmlsoap.org/soap/encoding/";
        public const string SOAP_URI = "http://schemas.xmlsoap.org/soap/envelope/";
        public const string XSI_URI = "http://www.w3.org/2001/XMLSchema-instance";

        public SoapSettings Settings { get; private set; }
        public string Name { get; private set; } = "";
        public string URI { get; private set; } = "";
        public bool IsArray { get; private set; } = false;
        public bool IsValue { get; private set; } = false;
        public bool Ignore { get; private set; } = false;
        public Type Type { get; private set; }

        public SoapType? ItemType { get; private set; }
        public string? PropertyName { get; private set; }

        public SoapType(PropertyInfo propertyInfo, SoapSettings? settings = null)
        {
            Type = propertyInfo.PropertyType;
            Settings = (settings != null) ? settings : new();
            Initialize();

            var propAttr = GetAttribute(propertyInfo.GetCustomAttributes<SoapAttribute>());
            if (propAttr != null)
            {
                if (propAttr.Name != null) PropertyName = propAttr.Name;
                Ignore = propAttr.Ignore;
            }

            if (PropertyName == null)
                PropertyName = propertyInfo.Name;
        }

        public SoapType(Type type, SoapSettings? settings = null)
        {
            Type = type;
            Settings = (settings != null) ? settings : new();
            Initialize();
        }

        private SoapAttribute? GetAttribute(IEnumerable<SoapAttribute> attributes)
        {
            foreach (SoapAttribute attr in attributes)
            {
                if (attr.If != null)
                {
                    bool ok = false;
                    foreach (var f in Settings.Flags)
                        if (attr.If.Contains(f, StringComparison.OrdinalIgnoreCase))
                        {
                            ok = true;
                            break;
                        }
                    if (!ok) break;
                }

                if (attr.IfNot != null)
                {
                    bool ok = true;
                    foreach (var f in Settings.Flags)
                        if (attr.IfNot.Contains(f, StringComparison.OrdinalIgnoreCase))
                        {
                            ok = false;
                            break;
                        }
                    if (!ok) break;
                }

                return attr;
            }

            return null;
        }

        private void Initialize()
        {
            var nt = Nullable.GetUnderlyingType(Type);
            if (nt != null)
                Type = nt;

            bool ok = false;
            if (!ok) ok = InitAsArray();
            if (!ok) ok = InitAsValue();

            var attr = GetAttribute(Type.GetCustomAttributes<SoapAttribute>());
            if (attr != null)
            {
                if (attr.Name != null) Name = attr.Name;
                if (attr.URI != null) URI = attr.URI;
            }

            if (Name.Length == 0)
                Name = Type.Name;
        }

        private bool InitAsValue()
        {
            if (Type == typeof(string))
            {
                Name = "string";
                URI = XSD_URI;
                IsValue = true;
                return true;
            }

            if (Type == typeof(bool))
            {
                Name = "boolean";
                URI = XSD_URI;
                IsValue = true;
                return true;
            }

            if (Type == typeof(int))
            {
                Name = "int";
                URI = XSD_URI;
                IsValue = true;
                return true;
            }

            if (Type == typeof(double))
            {
                Name = "double";
                URI = XSD_URI;
                IsValue = true;
                return true;
            }

            if (Type == typeof(DateTime))
            {
                Name = "dateTime";
                URI = XSD_URI;
                IsValue = true;
                return true;
            }

            return false;
        }

        private bool InitAsArray()
        {
            if (Type.IsGenericType && (Type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                Name = "Array";
                URI = SOAPENC_URI;
                IsArray = true;

                var args = Type.GetGenericArguments();
                ItemType = new SoapType(args[0], Settings);
                ItemType.PropertyName = "Item";
                return true;
            }

            return false;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class SoapAttribute : Attribute
    {
        public string? Name { get; set; }
        public string? URI { get; set; }
        public bool Ignore { get; set; }
        public string? If { get; set; }
        public string? IfNot { get; set; }
    }
}

