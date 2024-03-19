using System.Text;

namespace Brayns.Shaper
{
    public enum ClientMessageType
    {
        Message,
        SetAuthentication,
        ClearAuthentication,
        Boundary
    }

    public class ClientMessage
    {
        public ClientMessageType Type { get; set; }
        public string? Value { get; set; }
        public DateTimeOffset? Expires { get; set; }

        public ClientMessage(ClientMessageType type, string? value = null)
        {
            Type = type;
            Value = value;
        }
    }

    public static class Client
    {
        public static void ClearAuthenticationToken()
        {
            var msg = new ClientMessage(ClientMessageType.ClearAuthentication);
            Send(msg);
        }

        public static void SetAuthenticationToken(Guid token, DateTimeOffset? expires = null)
        {
            var msg = new ClientMessage(ClientMessageType.SetAuthentication, token.ToString());
            msg.Expires = expires;
            Send(msg);
        }

        public static void Flush()
        {
            var msg = new ClientMessage(ClientMessageType.Boundary);
            Send(msg);
        }

        public static void SendMessage(object o)
        {
            var jo = JObject.FromObject(o);
            jo["type"] = "send";

            var msg = new ClientMessage(ClientMessageType.Message, jo.ToString(Newtonsoft.Json.Formatting.Indented));
            Send(msg);
        }

        public static bool IsAllowed()
        {
            if (CurrentSession.WebTask == null)
                return false;
            if (CurrentSession.Type != SessionTypes.WEBCLIENT)
                return false;
            return true;
        }

        private static void Send(ClientMessage msg)
        {
            if (CurrentSession.WebTask == null)
                throw new Error(Label("No client connected"));

            if ((CurrentSession.Type != SessionTypes.WEBCLIENT) && (msg.Type == ClientMessageType.Message))
                throw new Error("Current session does not support client messaging");

            CurrentSession.WebTask.Send(msg);
        }

        public static void Reload(bool goHomepage = false)
        {
            var jo = new JObject();
            jo["action"] = "reload";
            jo["goHomepage"] = goHomepage;
            SendMessage(jo);
        }

        public static void Navigate(string url)
        {
            var jo = new JObject();
            jo["action"] = "navigate";
            jo["url"] = url;
            SendMessage(jo);
        }

        public static void Download(byte[] content, string mimeType="application/other", string fileName = "")
        {
            var jo = new JObject();
            jo["action"] = "download";
            jo["mimeType"] = mimeType;
            jo["b64content"] = Convert.ToBase64String(content);
            if (fileName.Length > 0)
                jo["fileName"] = fileName;
            SendMessage(jo);
        }

        public static void Download(string b64Content, string mimeType = "application/other", string fileName = "")
        {
            var jo = new JObject();
            jo["action"] = "download";
            jo["mimeType"] = mimeType;
            jo["b64content"] = b64Content;
            if (fileName.Length > 0)
                jo["fileName"] = fileName;
            SendMessage(jo);
        }
    }
}