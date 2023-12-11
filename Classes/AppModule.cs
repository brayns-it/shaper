using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Classes
{
    public abstract class AppModule
    {
        public abstract string Name { get; }
        public abstract string Author { get; }
        public abstract Guid Id { get; }
        public abstract Version Version { get; }

        public abstract void Install();
    }
}
