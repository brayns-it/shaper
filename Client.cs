using System.Text;

namespace Brayns.Shaper
{
    internal class ClientMessageBoundary
    {
    }

    internal class ClientMessageAuthentication
    {
        public DateTimeOffset? Expires { get; set; }
        public bool Clear { get; set; }
        public string Token { get; set; } = "";
    }

    internal class ClientMessage
    {
        public string Value { get; set; } = "";
    }

    public static class Client
    {
        public static void ClearAuthenticationToken()
        {
            Send(new ClientMessageAuthentication() { Clear = true });
        }

        public static void SetAuthenticationToken(Guid token, DateTimeOffset? expires = null)
        {
            Send(new ClientMessageAuthentication()
            {
                Token = token.ToString(),
                Expires = expires
            });
        }

        public static void Flush()
        {
            Send(new ClientMessageBoundary());
        }

        public static void SendMessage(object o)
        {
            if (CurrentSession.Type != SessionTypes.WEBCLIENT)
                throw new Error("Current session does not support client messaging");

            var jo = JObject.FromObject(o);
            jo["type"] = "send";

            Send(new ClientMessage()
            {
                Value = jo.ToString(Newtonsoft.Json.Formatting.Indented)
            });
        }

        public static bool IsAllowed()
        {
            if (CurrentSession.WebTask == null)
                return false;
            if (CurrentSession.Type != SessionTypes.WEBCLIENT)
                return false;
            return true;
        }

        private static void Send(object msg)
        {
            if (CurrentSession.WebTask == null)
                throw new Error(Label("No client connected"));

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