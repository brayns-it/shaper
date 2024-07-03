using System.Xml;

namespace Brayns.Shaper.Tools
{
    public class SimpleXml
    {
        public XmlDocument Document { get; init; }
        public XmlNode CurrentNode { get; set; }

        public SimpleXml(string rootName)
        {
            Document = new();
            Document.LoadXml("<" + rootName + " />");

            CurrentNode = Document.FirstChild!;
        }

        public XmlNode SelectNode(string xPath)
        {
            CurrentNode = Document.SelectSingleNode(xPath)!;
            return CurrentNode;
        }

        public XmlNode SelectLastNode(string xPath)
        {
            var lst = Document.SelectNodes(xPath);
            CurrentNode = lst![lst.Count - 1]!;
            return CurrentNode;
        }

        public XmlNode AddNode(string nodeName)
        {
            var el = Document.CreateElement(nodeName);
            CurrentNode.AppendChild(el);
            return el;
        }

        public XmlNode AddNode(string nodeName, string value)
        {
            var el = AddNode(nodeName);
            el.InnerText = value;
            return el;
        }

        public XmlNode AddNode(string nodeName, int value)
        {
            var el = AddNode(nodeName);
            el.InnerText = value.ToString();
            return el;
        }

        public XmlNode AddNode(string nodeName, decimal value)
        {
            var el = AddNode(nodeName);

            var nfi = new System.Globalization.NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";
            nfi.NumberGroupSeparator = "";

            el.InnerText = value.ToString("G29", nfi);
            return el;
        }

        public override string ToString()
        {
            return Document.OuterXml;
        }
    }
}

