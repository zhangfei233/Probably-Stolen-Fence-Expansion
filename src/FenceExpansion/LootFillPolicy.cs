using System;

namespace FenceExpansion
{
    public static class LootFillPolicy
    {
        public static int Supplement(Func<bool> hasSpace, Func<long> units, Action roll)
        {
            int stalls = 0, passes = 0;
            while (passes < 12 && stalls < 3 && hasSpace())
            {
                long before = units();
                roll();
                passes++;
                stalls = units() > before ? 0 : stalls + 1;
            }
            return passes;
        }
    }
}
