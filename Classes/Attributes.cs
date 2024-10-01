using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    /// <summary>
    /// Assembly contains units
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AppCollectionAttribute : Attribute
    {
    }

    /// <summary>
    /// Type is callable by name
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PublishedAttribute : Attribute
    {
    }

    /// <summary>
    /// Method is callable without authentication
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class PublicAccessAttribute : Attribute
    {
    }

    /// <summary>
    /// Method is callable by any logged user
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class LoggedAccessAttribute : Attribute
    {
    }

    /// <summary>
    /// Is a batch codeunit
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class BatchableAttribute : Attribute
    {
    }

    /// <summary>
    /// Method is part of a batch codeunit
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class BatchMethodAttribute : Attribute
    {
    }

    /// <summary>
    /// Table is optional 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OptionalTableAttribute : Attribute
    {
    }

    /// <summary>
    /// Table is virtual (on memory)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class VirtualTableAttribute : Attribute
    {
        public bool DataPerSession { get; init; } = false;
    }

    /// <summary>
    /// Field value is stored in session state
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ValuePerSessionAttribute : Attribute
    {
    }

    [Flags]
    public enum RequestMethod
    {
        Get = 1,
        Post = 2,
        Put = 4,
        Delete = 8
    }

    /// <summary>
    /// Method is callable via REST
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ApiMethodAttribute : Attribute
    {
        public RequestMethod Method { get; init; } = RequestMethod.Get;
        public string Route { get; init; } = "";
        public string BodyParam { get; init; } = "";
    }

    /// <summary>
    /// Raw HTTP Method callable
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RawMethodAttribute : Attribute
    {
        public string RouteName { get; init; } = "";
        public string Route { get; init; } = "";
        public RequestMethod Method { get; init; } = RequestMethod.Get;
    }

    /// <summary>
    /// Label target
    /// </summary>
    [AttributeUsage(AttributeTargets.All)]
    public class LabelAttribute : Attribute
    {
        public string Label { get; private set; }

        public LabelAttribute(string label)
        {
            Label = label;
        }
    }

    /// <summary>
    /// Method will be called during unit initialization
    /// (cannot define more than one constructor in partial classes)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ExtendedAttribute : Attribute
    {
        public int Priority { get; set; } = 0;
    }
}
