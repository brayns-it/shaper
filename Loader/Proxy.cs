using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Brayns.Shaper.Loader
{
    public class Proxy
    {
        private static List<Type> _typesMaintAllowed = new();

        private object _obj;
        private Type _typ;
        private MethodInfo? _met;
        private Dictionary<string, string> _par = new();
        private string _bodyParam = "";

        public string ResultName { get; init; } = "value";
        public bool SkipMethodSecurity { get; set; }

        static Proxy()
        {
            _typesMaintAllowed.Add(typeof(Systems.ClientManagement));
            _typesMaintAllowed.Add(typeof(Systems.Confirm));
            _typesMaintAllowed.Add(typeof(Systems.Setup));
            _typesMaintAllowed.Add(typeof(Systems.Admin));
        }

        private Proxy(object obj)
        {
            _obj = obj;
            _typ = obj.GetType();
        }

        private Proxy(MethodInfo met, Dictionary<string, string> par)
        {
            _obj = Activator.CreateInstance(met.DeclaringType!)!;
            _typ = _obj.GetType();
            _met = met;
            _par = par;

            var rm = _met.GetCustomAttribute<ApiMethod>(true);
            if (rm != null)
            {
                _bodyParam = rm.BodyParam;
                if (rm.ResultName.Length > 0)
                    ResultName = rm.ResultName;
            }
        }

        public static Proxy CreateFromRoute(string route, ApiAction action)
        {
            Regex re = new Regex("{(.*?)}");
            MethodInfo? mi = null;
            Match? routeMatch = null;
            MatchCollection? patternMatch = null;

            if (Application.Routes.ContainsKey(action))
            {
                foreach (string pattern in Application.Routes[action].Keys)
                {
                    Regex pat = new Regex("^" + re.Replace(pattern, "(.*?)") + "$");
                    routeMatch = pat.Match(route);
                    if (routeMatch.Success)
                    {
                        patternMatch = re.Matches(pattern);
                        mi = Application.Routes[action][pattern];
                        break;
                    }
                }
            }

            if (mi == null)
                throw new Error(Error.E_INVALID_ROUTE, Label("Invalid {0} route '{1}'", action, route));

            AssertMethodSecurity(mi);

            Dictionary<string, string> param = new Dictionary<string, string>();
            for (int i = 0; i < patternMatch!.Count; i++)
                param[patternMatch![i].Groups[1].Value] = routeMatch!.Groups[i + 1].Value;

            return new Proxy(mi, param);
        }

        public static Proxy CreateFromObject(object obj)
        {
            return new Proxy(obj);
        }

        public static Proxy CreateFromId(string id)
        {
            if (!CurrentSession.Units.ContainsKey(id))
                throw new Error(Label("Invalid object ID '{0}'", id));

            return new Proxy(CurrentSession.Units[id]);
        }

        public static Proxy CreateFromName(string fullName)
        {
            object? o = null;

            fullName = fullName.Trim();
            if (fullName.Length == 0)
                throw new Error(Label("Invalid object name '{0}'", fullName));

            if (Loader.UnitTypes.ContainsKey(fullName))
            {
                var t = Loader.UnitTypes[fullName];
                AssertUnitSecurity(t);
                o = Activator.CreateInstance(t)!;
            }

            if (o == null)
                throw new Error(Label("Invalid object name '{0}'", fullName));

            return new Proxy(o);
        }

        private static void AssertUnitSecurity(Type t)
        {
            if (t.GetCustomAttributes(typeof(Published), true).Length > 0)
                return;

            throw new Error(Error.E_UNAUTHORIZED, Label("Unauthorized access to unit '{0}'", t.Name));
        }

        private static void AssertMethodSecurity(MethodInfo mi)
        {
            if (Application.InMaintenance && (!_typesMaintAllowed.Contains(mi.ReflectedType!)))
                throw new Error(Error.E_SYSTEM_IN_MAINTENANCE, Label("Application is in maintenance, try again later"));

            if (mi.GetCustomAttributes(typeof(PublicAccess), true).Length > 0)
                return;

            if (Session.UserId.Length > 0)
            {
                if (mi.GetCustomAttributes(typeof(LoggedAccess), true).Length > 0)
                    return;

                if (Permissions.IsAllowed(mi.DeclaringType!, PermissionType.Execute, false))
                    return;
            }

            throw new Error(Error.E_UNAUTHORIZED, Label("Unauthorized access to method '{0}'", mi.Name));
        }

        private MethodInfo GetMethodByName(string methodName)
        {
            MethodInfo? mi = _typ.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
                throw new Error(Label("Invalid method name '{0}'", methodName));

            if (!SkipMethodSecurity)
                AssertMethodSecurity(mi);

            return mi;
        }

        public object? Invoke(string methodName, object[] pars)
        {
            MethodInfo mi = GetMethodByName(methodName);
            return mi.Invoke(_obj, pars);
        }

        public object? Invoke(string methodName, JObject? parameters)
        {
            MethodInfo mi = GetMethodByName(methodName);
            return Invoke(mi, parameters);
        }

        public object? Invoke(JObject? parameters)
        {
            if (_met == null)
                throw new Error(Label("Unknown method to invoke"));

            if (parameters == null)
                parameters = new();

            if (_bodyParam.Length > 0)
            {
                var xp = parameters;
                parameters = new JObject();
                parameters[_bodyParam] = xp;
            }

            foreach (string k in _par.Keys)
                parameters[k] = _par[k];

            return Invoke(_met, parameters);
        }

        private object? Invoke(MethodInfo method, JObject? parameters)
        {
            List<object?> pars = new List<object?>();
            foreach (var p in method.GetParameters())
            {
                if ((parameters != null) && (parameters.ContainsKey(p.Name!)))
                    pars.Add(parameters[p.Name!]!.ToObject(p.ParameterType));
                else if (p.HasDefaultValue)
                    pars.Add(p.DefaultValue);
                else if (Nullable.GetUnderlyingType(p.ParameterType) != null)
                    pars.Add(null);
                else
                    throw new Error(Label("Missing parameter '{0}' in method '{1}'", p.Name!, method.Name));
            }
            return method.Invoke(_obj, pars.ToArray());
        }
    }
}
