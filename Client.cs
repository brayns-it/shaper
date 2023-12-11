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

        private static void Send(ClientMessage msg)
        {
            if (CurrentSession.WebTask == null)
                throw new Error(Label("No client connected"));

            if ((CurrentSession.Type != SessionTypes.WEBCLIENT) && (msg.Type == ClientMessageType.Message))
                throw new Error("Current session does not support client messaging");

            CurrentSession.WebTask.Send(msg);
        }

        public static void Reload()
        {
            var jo = new JObject();
            jo["action"] = "reload";
            SendMessage(jo);
        }
    }
}