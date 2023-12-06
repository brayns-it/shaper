using System.Text;

namespace Brayns.Shaper
{
    public enum ClientMessageType
    {
        Message,
        AuthenticationToken,
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
        private static readonly string _boundary;

        static Client()
        {
            // 8k buffer
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 230; i++)
                sb.Append("d4015c7b-152e-492e-8e8b-2021248db290");
            _boundary = "\"" + sb.ToString() + "\"";
        }

        public static void ClearAuthenticationToken()
        {
            var msg = new ClientMessage(ClientMessageType.AuthenticationToken);
            Send(msg);
        }

        public static void SetAuthenticationToken(Guid token, DateTimeOffset? expires = null)
        {
            var msg = new ClientMessage(ClientMessageType.AuthenticationToken, token.ToString());
            msg.Expires = expires;
            Send(msg);
        }

        public static void SendMessage(object o)
        {
            var jo = JObject.FromObject(o);
            jo["type"] = "send";

            var msg1 = new ClientMessage(ClientMessageType.Message, jo.ToString(Newtonsoft.Json.Formatting.Indented));
            var msg2 = new ClientMessage(ClientMessageType.Boundary, _boundary);

            Send(msg1, msg2);
        }

        private static void Send(params ClientMessage[] msgs)
        {
            if (CurrentSession.WebTask == null)
                throw new Error(Label("No client connected"));

            if (CurrentSession.Type != SessionTypes.WEBCLIENT)
                foreach (ClientMessage msg in msgs)
                    if (msg.Type == ClientMessageType.Message)
                        throw new Error("Current session does not support client messaging");

            CurrentSession.WebTask.Send(msgs);
        }

        public static void Reload()
        {
            var jo = new JObject();
            jo["action"] = "reload";
            SendMessage(jo);
        }
    }
}