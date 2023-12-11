using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AppCollectionAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class AppModuleAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class PublishedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class PublicAccessAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class LoggedAccessAttribute : Attribute
    {
    }

    [Flags]
    public enum ApiAction
    {
        Create = 1,
        Read = 2,
        Update = 4,
        Delete = 8
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ApiMethodAttribute : Attribute
    {
        public ApiAction Action { get; init; } = ApiAction.Read;
        public string Route { get; init; } = "";
        public string BodyParam { get; init; } = "";
        public string ResultName { get; init; } = "";
    }

    [AttributeUsage(AttributeTargets.All)]
    public class LabelAttribute : Attribute
    {
        public string Label { get; private set; }

        public LabelAttribute(string label)
        {
            Label = label;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ExtendedAttribute : Attribute
    {
    }
}
