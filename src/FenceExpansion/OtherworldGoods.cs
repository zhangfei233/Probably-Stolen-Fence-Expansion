using System.Collections.Generic;
using System.Linq;

namespace FenceExpansion
{
    internal static class OtherworldGoods
    {
        internal const string BottleId = "fence_expansion.enchanting_bottle";
        internal const string AppleId = "fence_expansion.golden_apple";
        private static readonly string[] ExtraIds = { BottleId, AppleId };

        internal static void Enable() => ModHook.OnModItemDirectoryInit += Register;
        internal static void Disable() => ModHook.OnModItemDirectoryInit -= Register;
        private static void Register(ModItemDirectory directory)
        {
            // Keep factories available while decoding saves. Only the four explicitly
            // supported loot producers may offer these IDs, and each is skill-gated.
            directory.Add(BottleId, () => Create(true));
            directory.Add(AppleId, () => Create(false));
        }

        private static GameItem Create(bool bottle)
        {
            var item = ItemDirectory.CreateEmptyItem(bottle ? BottleId : AppleId);
            OtherworldAssets.SetItem(item, bottle ? "bottle" : "apple", 2, bottle ? 3 : 2);
            item.name = bottle ? ModEntry.Text("附魔之瓶", "Bottle o' Enchanting")
                : ModEntry.Text("金苹果", "Golden Apple");
            item.shortDescription = bottle
                ? ModEntry.Text("来自异界的奇妙瓶装珍品。", "An extraordinary bottled delicacy from another world.")
                : ModEntry.Text("来自异界的金色苹果，备受收藏家青睐。", "A golden apple from another world, prized by collectors.");
            item.unitValue = bottle ? 500 : 800;
            item.SetGameItemType(bottle ? "SUBSTANCE" : "FOOD");
            item.SetGameItemType("LUXURY_ITEM");
            item.mayStackItemFunc = (_, __) => false;
            OtherworldEffects.Bind(item);
            return item;
        }

        internal static string[] WithUnlocked(IEnumerable<string> baseIds)
        {
            // Read current run state on every generation, so purchasing the skill
            // immediately affects cached vanilla pools and loading another save.
            return (ModEntry.Unlocked("skill_4_3") ? baseIds.Concat(ExtraIds) : baseIds).Distinct().ToArray();
        }

        internal static float RareChance(string visitorId)
        {
            // Both goods use the uncommon luxury tier, not the 25% ordinary-stock
            // rolls. Preserve the game's black-market reputation stock multiplier.
            return visitorId == "thief" || visitorId == "pettyThief"
                ? 5f * StoreClientList.GetBMRepStockMult() : 0f;
        }

        internal static void PlaceRareStock(string visitorId)
        {
            if (!ModEntry.Unlocked("skill_4_3")) return;
            float chance = RareChance(visitorId);
            if (chance <= 0 || !(EmporiumEntry.Instance?.frontInvinvElement is GameGridInventory grid)) return;
            foreach (string id in ExtraIds)
                if (RNG.Roll(chance)) HighValueStock.SellOnCounter(DirectoryMaster.Item(id), grid);
        }
    }
}
