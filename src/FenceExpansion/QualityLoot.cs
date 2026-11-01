using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class QualityLoot
    {
        [ThreadStatic] internal static GameItem Capturing;
        [ThreadStatic] internal static List<GameItem> Candidates;

        internal static bool TryGetFiller(GameItem box, out Action<GameItem, float> fill)
        {
            fill = null;
            var fillers = AccessTools.Field(typeof(PreBuiltItemHelper), "crateFillers").GetValue(null)
                as Dictionary<string, Action<GameItem, float>>;
            return fillers != null && fillers.TryGetValue(box.GetTagReadonly(KeyCards.CrateIdTag).GetString() ?? "", out fill);
        }
        internal static void TryAdd(GameItem box, GameItem item)
        {
            if (item == null) return;
            if (ReferenceEquals(box, Capturing)) { Candidates.Add(item); return; }
            if (GraphUtils.TryAcceptAll(box, item) > 0) item.Destroy();
        }
        private static List<GameItem> Capture(GameItem box, Action<GameItem, float> fill)
        {
            var pool = new List<GameItem>();
            var oldBox = Capturing;
            var oldPool = Candidates;
            try { Capturing = box; Candidates = pool; fill(box, 10000f); }
            catch { foreach (var item in pool) item.Destroy(); throw; }
            finally { Capturing = oldBox; Candidates = oldPool; }
            // A return key is not a luxury roll or a quality-tier reward. Do not
            // duplicate it when expansion repeatedly captures the loot factory.
            foreach (var card in pool.Where(i => LuxuryGuarantee.IsGuaranteedCard(box, i)).ToList())
            {
                card.Destroy();
                pool.Remove(card);
            }
            pool.Sort((a,b) => a.unitValue.CompareTo(b.unitValue));
            return pool;
        }
        private static int Choose(List<GameItem> pool, int quality)
        {
            double total = 0;
            for (int i = 0; i < pool.Count; i++) total += Weight(i, pool.Count, quality);
            double roll = UnityEngine.Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Weight(i, pool.Count, quality);
                if (roll <= 0) return i;
            }
            return pool.Count - 1;
        }
        private static bool Place(GameItem box, List<GameItem> pool, int index)
        {
            var item = pool[index];
            pool.RemoveAt(index);
            if (GraphUtils.TryAcceptAll(box, item) == 0) return true;
            item.Destroy();
            return false;
        }
        // Exhaust eligible shapes, rather than giving up after a few failed probability rolls.
        // Finite defensive cap protects against foreign infinite-stack inventories.
        internal static void FillRemaining(GameItem box, int quality)
        {
            if (!(box.contentWindow?.child is GameGridInventory grid) || !TryGetFiller(box, out var fill)) return;
            for (int pass = 0; pass < 512 && grid.HasFreeCell(); pass++)
            {
                var pool = Capture(box, fill);
                bool added = false;
                try
                {
                    while (pool.Count > 0 && grid.HasFreeCell())
                    {
                        int index = Choose(pool, quality);
                        if (!GraphUtils.CanAccept(box, pool[index]))
                        { pool[index].Destroy(); pool.RemoveAt(index); continue; }
                        added |= Place(box, pool, index);
                    }
                }
                finally { foreach (var item in pool) item.Destroy(); }
                if (!added) return;
            }
        }
        internal static bool Generate(GameItem box, int quality)
        {
            if (!(box.contentWindow?.child is GameGridInventory grid) || !TryGetFiller(box, out var fill)) return false;
            var pool = Capture(box, fill);
            var rule = grid.mayInventoryAddItemFunc;
            try
            {
                if (pool.Count == 0) return false;
                foreach (var old in grid.childItems.ToList()) old.Destroy();
                grid.mayInventoryAddItemFunc = null;
                LuxuryGuarantee.Ensure(box);
                int threshold = (int)((pool.Count - 1) * (quality == 1 ? .50 : quality == 2 ? .75 : 1.0));
                for (int i = pool.Count - 1; i >= threshold; i--)
                {
                    int chosen = quality == 3 ? i : RNG.GetRandomInt(threshold, i);
                    if (Place(box, pool, chosen)) break;
                }
                float chance = Math.Max(0f, Math.Min(100f, 55f * (1f + Math.Max(HealthData.GetLuck(), LotteryHelper.GetSpawnLuck(box)) / 100f)));
                int attempts = Math.Max(1, grid.inventoryShape.width * grid.inventoryShape.height / 2);
                for (int n = 0; n < attempts && pool.Count > 0 && grid.CanTakeAnything(); n++)
                    if (RNG.Roll(chance)) Place(box, pool, Choose(pool, quality));
                if (ModEntry.Unlocked("skill_1_1")) FillRemaining(box, quality);
                LockHelper.SetChildBehindLock(box);
                return true;
            }
            finally
            {
                grid.mayInventoryAddItemFunc = rule;
                foreach (var item in pool) item.Destroy();
            }
        }
        internal static double Weight(int rank, int count, int quality) =>
            Math.Pow(1.0 + 3.0 * rank / Math.Max(1, count - 1), quality);
    }
    [HarmonyPatch(typeof(PreBuiltItemHelper), "TryAdd")]
    internal static class QualityLootCapturePatch
    {
        static bool Prefix(GameItem crate, GameItem item)
        {
            if (!ReferenceEquals(crate, QualityLoot.Capturing)) return true;
            if (item != null) QualityLoot.Candidates.Add(item);
            return false;
        }
    }
}
