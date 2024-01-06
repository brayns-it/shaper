using System.Reflection;

namespace Brayns.Shaper.Objects
{
    public abstract class Codeunit : Unit
    {
        internal override void UnitInitialize()
        {
            UnitType = UnitTypes.CODEUNIT;
        }
    }
}
