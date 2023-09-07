using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brayns.Shaper.Fields
{
    public interface INumeric
    {
        public bool BlankZero { get; set; }
    }

    public interface IInteger
    {
        public bool AutoIncrement { get; set; }
    }

    public interface IDecimal
    {
        public int Decimals { get; set; }
    }
}
