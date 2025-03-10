using System.Text;

namespace Brayns.Shaper
{
    internal class ClientMessageAuthentication : ClientMessage
    {
        public DateTimeOffset? Expires { get; set; }
        public bool Clear { get; set; }
        public string Token { get; set; } = "";
    }

    internal class ClientDirectMessage : ClientMessage
    {
        public object Value { get; set; } = new();
    }

    internal abstract class ClientMessage
    {
    }

    public static class Client
    {
        public static void ClearAuthenticationToken()
        {
            Send(new ClientMessageAuthentication() { Clear = true });
        }

        public static void SetAuthenticationToken(string token, DateTimeOffset? expires = null)
        {
            Send(new ClientMessageAuthentication()
            {
                Token = token,
                Expires = expires
            });
        }

        public static void SendMessage(object o)
        {
            if (CurrentSession.Type != SessionTypes.WEBCLIENT)
                throw new Error("Current session does not support client messaging");

            Send(new ClientDirectMessage()
            {
                Value = o
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

        private static void Send(ClientMessage msg)
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

        public static void AssertFocus()
        {
            var jo = new JObject();
            jo["action"] = "assertFocus";
            SendMessage(jo);
        }

        public static void PlaySound(string url)
        {
            var jo = new JObject();
            jo["action"] = "playSound";
            jo["url"] = url;
            SendMessage(jo);
        }

        public static void RequestFullscreen()
        {
            var jo = new JObject();
            jo["action"] = "requestFullscreen";
            SendMessage(jo);
        }

        public static void Navigate(string url, bool newTarget = true)
        {
            var jo = new JObject();
            jo["action"] = "navigate";
            jo["newTarget"] = newTarget;
            jo["url"] = url;
            SendMessage(jo);
        }

        public static void Download(FileInfo file, string mimeType = "application/other", string fileName = "")
        {
            using (FileStream fs = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buf = new byte[fs.Length];
                fs.Read(buf);
                fs.Close();

                Download(buf, mimeType, fileName);
            }
        }

        public static void Download(byte[] content, string mimeType="application/other", string fileName = "")
        {
            Download(Convert.ToBase64String(content), mimeType, fileName);
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