using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Loader
{
    public enum PermissionMode
    {
        Allow,
        AllowIndirect,
        Deny
    }

    public enum PermissionType
    {
        Insert,
        Modify,
        Delete,
        Execute
    }

    public class Permission
    {
        public Opt<UnitTypes> UnitType { get; set; }
        public string UnitName { get; set; }
        public PermissionType PermissionType { get; set; }
        public PermissionMode PermissionMode { get; set; }

        public Permission(Opt<UnitTypes> unitType, string unitName, PermissionType permType, PermissionMode permMode)
        {
            UnitType = unitType;
            UnitName = unitName;
            PermissionType = permType;
            PermissionMode = permMode;
        }
    }

    public static class Permissions
    {
        internal static string Key
        {
            get { return "System.Permissions:" + CurrentSession.UserId; }
        }

        public static void Clear()
        {
            Brayns.Shaper.Application.DelValue(Key);
        }

        public static void Set(List<Permission> permissions)
        {
            Brayns.Shaper.Application.SetValue(Key, permissions);
        }

        public static bool Exists()
        {
            return Brayns.Shaper.Application.Values.ContainsKey(Key);
        }

        public static bool IsAllowed(Type unitType, PermissionType permType, bool indirect)
        {
            if (CurrentSession.UserId.Length == 0)
                return false;

            if (CurrentSession.IsSuperuser)
                return true;

            if (!Exists())
                return false;

            var perms = (List<Permission>)Brayns.Shaper.Application.Values[Key]!;

            bool denied = false;
            bool allowed = false;

            foreach (var p in perms)
            {
                if (Functions.UnitTypeFromType(unitType) != p.UnitType) continue;
                if ((p.UnitName.Length > 0) && (p.UnitName != unitType.FullName)) continue;
                if (p.PermissionMode == PermissionMode.Deny)
                {
                    denied = true;
                    break;
                }
                if ((p.PermissionMode == PermissionMode.AllowIndirect) && (!indirect)) continue;
                allowed = true;
            }

            if (denied) return false;
            return allowed;
        }
    }
}
