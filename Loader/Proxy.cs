﻿using Newtonsoft.Json.Linq;
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

        private Proxy(MethodInfo met)
        {
            _obj = Activator.CreateInstance(met.DeclaringType!)!;
            _typ = _obj.GetType();
            _met = met;
        }

        private Proxy(MethodInfo met, Dictionary<string, string> par)
        {
            _obj = Activator.CreateInstance(met.DeclaringType!)!;
            _typ = _obj.GetType();
            _met = met;
            _par = par;

            foreach (var rm in _met.GetCustomAttributes<ApiMethod>(true))
                if (rm.BodyParam.Length > 0)
                    _bodyParam = rm.BodyParam;
        }

        public static Proxy CreateFromName(string fullName, params object?[]? args)
        {
            var t = TypeFromName(fullName);
            AssertUnitSecurity(t);
            return new Proxy(Activator.CreateInstance(t, args)!);
        }

        private static MethodInfo GetBestRoute(Dictionary<MethodInfo, Dictionary<string, string>> routes)
        {
            foreach (MethodInfo mi in routes.Keys)
                if (routes[mi].Count == 0)
                    return mi;

            return routes.Keys.First();
        }

        public static Proxy CreateFromRawRoute(string routeName, string route, RawSession session)
        {
            Dictionary<MethodInfo, Dictionary<string, string>> routes = new();

            foreach (var r in Application.RawRoutes.Keys)
            {
                Dictionary<string, string> parts = new();
                if (r.Method.HasFlag((Enum)session.RequestMethod) && (routeName == r.RouteName) && RouteIsMatch(route, r.Route, parts))
                    if (!routes.ContainsKey(Application.RawRoutes[r]))
                        routes.Add(Application.RawRoutes[r], parts);
            }

            if (routes.Count == 0)
                throw new Error(Error.E_INVALID_ROUTE, Label("Invalid raw route '{0}' '{1}'", routeName, route));

            var mi = GetBestRoute(routes);
            session.RouteParts = routes[mi];

            AssertMethodSecurity(mi);

            return new Proxy(mi);
        }

        private static bool RouteIsMatch(string route, string pattern, Dictionary<string, string> parts)
        {
            Regex re = new Regex("{(.*?)}");
            Regex pat = new Regex("^" + re.Replace(pattern, "(.*?)") + "$", RegexOptions.IgnoreCase);
            parts.Clear();

            var rouMatch = pat.Match(route);
            if (!rouMatch.Success)
                return false;

            var patMatch = re.Matches(pattern);
            for (int i = 0; i < patMatch!.Count; i++)
                parts.Add(patMatch![i].Groups[1].Value, rouMatch!.Groups[i + 1].Value);

            return true;
        }

        public static Proxy CreateFromRoute(string route, RequestMethod method)
        {
            Dictionary<string, string> param = new();
            MethodInfo? mi = null;

            foreach (var r in Application.Routes.Keys)
                if (r.Method.HasFlag(method) && RouteIsMatch(route, r.Route, param))
                {
                    mi = Application.Routes[r];
                    break;
                }

            if (mi == null)
                throw new Error(Error.E_INVALID_ROUTE, Label("Invalid {0} route '{1}'", method, route));

            AssertMethodSecurity(mi);

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

        private static void AssertUnitSecurity(Type t)
        {
            if (HasAttribute<Published>(t) || IsType<BasePage>(t))
                return;

            if (Session.IsSuperuser)
                return;

            if (t.GetCustomAttributes(typeof(PublicAccess), true).Length > 0)
                return;

            if (Session.UserId.Length > 0)
            {
                if (Permissions.IsAllowed(t, PermissionType.Execute, false))
                    return;
            }

            throw new Error(Error.E_UNAUTHORIZED, Label("Unauthorized access to unit '{0}'", t.Name));
        }

        private static void AssertMethodSecurity(MethodInfo mi)
        {
            if ((!Application.IsReady) || (!Application.IsLoaded))
            {
                if (!_typesMaintAllowed.Contains(mi.ReflectedType!))
                    throw Application.ErrorInMaintenance();

                if (!Application.IsFromMaintenanceNetwork())
                    throw Application.ErrorInMaintenance();
            }

            if (Session.IsSuperuser)
                return;

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

        public T GetObject<T>()
        {
            return (T)_obj!;
        }

        public object? Invoke(string methodName, object[] pars)
        {
            MethodInfo mi = MethodFromName(_typ, methodName);

            if (!SkipMethodSecurity)
                AssertMethodSecurity(mi);

            return mi.Invoke(_obj, pars);
        }

        public object? Invoke(string methodName, JObject? parameters)
        {
            MethodInfo mi = MethodFromName(_typ, methodName);

            if (!SkipMethodSecurity)
                AssertMethodSecurity(mi);

            return Invoke(mi, parameters);
        }

        public object? Invoke(RawSession rawSession)
        {
            if (_met == null)
                throw new Error(Label("Unknown method to invoke"));

            return _met.Invoke(_obj, new object[] { rawSession });
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

        #region [TOOLS]

        public static bool HasAttribute<T>(string fullName) where T : Attribute
        {
            return HasAttribute<T>(TypeFromName(fullName));
        }

        public static bool HasAttribute<T>(string fullName, string methodName) where T : Attribute
        {
            return HasAttribute<T>(MethodFromName(fullName, methodName));
        }

        public static bool HasAttribute<T>(PropertyInfo pi) where T : Attribute
        {
            return pi.GetCustomAttributes(typeof(T), true).Length > 0;
        }

        public static bool HasAttribute<T>(MethodInfo mi) where T : Attribute
        {
            return mi.GetCustomAttributes(typeof(T), true).Length > 0;
        }

        public static bool HasAttribute<T>(Type typ) where T : Attribute
        {
            return typ.GetCustomAttributes(typeof(T), true).Length > 0;
        }

        public static bool HasAttribute<T>(Assembly asm) where T : Attribute
        {
            return asm.GetCustomAttributes(typeof(T), true).Length > 0;
        }

        public static Type TypeFromName(string fullName)
        {
            fullName = fullName.Trim();

            if (fullName.Length > 0)
                if (Loader.UnitTypes.ContainsKey(fullName))
                    return Loader.UnitTypes[fullName];

            throw new Error(Label("Invalid object name '{0}'", fullName));
        }

        public static bool AssertType<T>(string fullName)
        {
            return AssertType<T>(TypeFromName(fullName));
        }

        public static bool AssertType<T>(Type t)
        {
            if (!IsType<T>(t))
                throw new Error(Label("{0} invalid type", t.FullName!));
            return true;
        }

        public static bool IsType<T>(Type t)
        {
            return typeof(T).IsAssignableFrom(t);
        }

        public static bool IsType<T>(string fullName)
        {
            return typeof(T).IsAssignableFrom(TypeFromName(fullName));
        }

        public static MethodInfo MethodFromName(string typeName, string methodName)
        {
            return MethodFromName(TypeFromName(typeName), methodName);
        }

        public static MethodInfo MethodFromName(Type t, string methodName)
        {
            MethodInfo? mi = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
                throw new Error(Label("Invalid method name '{0}'", methodName));
            return mi;
        }

        #endregion
    }
}
