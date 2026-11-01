using System;
using System.Collections.Generic;

namespace FenceExpansion
{
    internal static class MasterStockPolicy
    {
        internal static void Specials(bool extra, Func<int, bool> roll, Action crate, Action<bool> core)
        {
            if (extra) crate();
            if (roll(70)) crate();
            if (roll(10)) core(roll(80));
        }

        // A fresh uniformly shuffled pool for each placement: equal weight, with
        // replacement between placements, exhaustive rejection of non-fitting IDs.
        internal static void Fill(int count, int maxPlacements, Func<bool> hasSpace,
            Func<int, int> randomIndex, Func<int, bool> place)
        {
            for (int n = 0; n < maxPlacements && hasSpace(); n++)
            {
                var remaining = new List<int>();
                for (int i = 0; i < count; i++) remaining.Add(i);
                bool placed = false;
                while (remaining.Count > 0)
                {
                    int j = randomIndex(remaining.Count);
                    int candidate = remaining[j];
                    remaining.RemoveAt(j);
                    if (place(candidate)) { placed = true; break; }
                }
                if (!placed) break;
            }
        }
    }
}
