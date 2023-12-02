using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public abstract class Codeunit : Unit
    {
        public Codeunit()
        {
            UnitType = UnitTypes.CODEUNIT;
        }
    }
}
