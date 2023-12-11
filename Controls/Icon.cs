using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Controls
{
    public class Icon 
    {
        public string Name { get; set; }

        public Icon(string name)
        {
            Name = name;
        }

        public static Icon FromName(string name)
        {
            return new(name);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
