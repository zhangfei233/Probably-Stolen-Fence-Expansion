using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class KeyBoxes
    {
        private const string BaseWidth = "fence_expansion.box_base_width";
        private const string BaseHeight = "fence_expansion.box_base_height";
        private const string SavedWidth = "fence_expansion.box_width";
        private const string SavedHeight = "fence_expansion.box_height";
        private const string FilledExpanded = "fence_expansion.expanded_loot_v2";
        private static readonly HashSet<GameItem> Filling = new HashSet<GameItem>();

        private static bool IsExpanded(GameItem item, GameGridInventory grid) =>
            grid.inventoryShape.width > Read(item, BaseWidth, 7)
            || grid.inventoryShape.height > Read(item, BaseHeight, 5);

        // Called only from original loot fillers, never from UI refresh or opening an unlocked box.
        internal static void FillExpandedSpace(GameItem item, float luckMult)
        {
            if (ReferenceEquals(item, QualityLoot.Capturing) || SaveManager.IsDecoding || !IsKeyBox(item)
                || !(item.contentWindow?.child is GameGridInventory grid)
                || !IsExpanded(item, grid) || !Filling.Add(item)) return;
            try
            {
                var fillers = AccessTools.Field(typeof(PreBuiltItemHelper), "crateFillers").GetValue(null)
                    as Dictionary<string, Action<GameItem, float>>;
                string id = item.GetTagReadonly(PreBuiltItemHelper.LOOT_CRATE_ID_STRING).GetString();
                if (fillers == null || !fillers.TryGetValue(id ?? "", out var fill)) return;
                LootFillPolicy.Supplement(() => grid.CanTakeAnything(),
                    () => grid.childItems.Sum(x => (long)Math.Max(1, x.unitCount)),
                    () => fill(item, luckMult));
                QualityLoot.FillRemaining(item, 0);
                item.EnableTag(FilledExpanded);
            }
            finally { Filling.Remove(item); }
        }

        private const string PendingRefill = "fence_expansion.box_pending_refill";
        internal static void RefreshExisting()
        {
            if (EmporiumEntry.Instance == null) return;
            foreach (var item in EmporiumEntry.Instance.GetAllItems())
                if (ApplySize(item) && item.GetTagReadonly(LockHelper.LOCK_STATUS).GetBool()) item.EnableTag(PendingRefill);
        }
        internal static bool IsKeyBox(GameItem item)
        {
            return item != null && item.IsTag(LockHelper.LOCKED_CONTAINER_TAG)
                && item.IsTag(LockHelper.LOCK_ID);
        }

        private static int Read(GameItem item, string key, int fallback)
        {
            return item.IsTag(key) ? Math.Max(1, item.GetTagReadonly(key).GetInt()) : fallback;
        }

        private static void Write(GameItem item, string key, int value)
        {
            item.ModifyTag(key, state => state.Enable().SetInt(value));
        }

        internal static bool ApplySize(GameItem item, bool restoreOnly = false)
        {
            if (!IsKeyBox(item) || !(item.contentWindow?.child is GameGridInventory grid)) return false;
            int baseWidth = Read(item, BaseWidth, grid.inventoryShape.width);
            int baseHeight = Read(item, BaseHeight, grid.inventoryShape.height);
            int extra = restoreOnly ? 0 : ModEntry.Unlocked("skill_1_2") ? 4 : ModEntry.Unlocked("skill_1_1") ? 2 : 0;
            // Never shrink an existing crate: a reset upgrade or another mod must not strand contents.
            int width = Math.Max(grid.inventoryShape.width, Math.Max(Read(item, SavedWidth, baseWidth), baseWidth + extra));
            int height = Math.Max(grid.inventoryShape.height, Math.Max(Read(item, SavedHeight, baseHeight), baseHeight + extra));
            bool changed = width != grid.inventoryShape.width || height != grid.inventoryShape.height;
            if (changed)
            {
                grid.SetShape(width, height);
                item.contentWindow.Validate();
            }
            Write(item, BaseWidth, baseWidth);
            Write(item, BaseHeight, baseHeight);
            Write(item, SavedWidth, width);
            Write(item, SavedHeight, height);
            return changed;
        }

        internal static void ExpandBeforeUnlock(GameItem item, bool force)
        {
            if (force || !IsKeyBox(item) || !item.GetTagReadonly(LockHelper.LOCK_STATUS).GetBool()) return;
            if (!(item.contentWindow?.child is GameGridInventory grid)) return;
            // Resolve the original filler before changing contents. No replacement loot tables are shipped.
            var fillers = AccessTools.Field(typeof(PreBuiltItemHelper), "crateFillers").GetValue(null)
                as Dictionary<string, Action<GameItem, float>>;
            string crateId = item.GetTagReadonly(PreBuiltItemHelper.LOOT_CRATE_ID_STRING).GetString();
            bool changed = ApplySize(item);
            bool needsMigration = IsExpanded(item, grid) && !item.IsTag(FilledExpanded);
            if ((!changed && !item.IsTag(PendingRefill) && !needsMigration) || fillers == null || !fillers.TryGetValue(crateId ?? "", out var fill)) return;
            var originalRule = grid.mayInventoryAddItemFunc;
            try
            {
                grid.mayInventoryAddItemFunc = null;
                foreach (var child in grid.childItems.ToList()) child.Destroy();
                fill(item, PreBuiltItemHelper.GetLuckMult(item));
                LockHelper.SetChildBehindLock(item);
                item.DisableTag(PendingRefill);
            }
            finally { grid.mayInventoryAddItemFunc = originalRule; }
        }
    }

    [HarmonyPatch(typeof(LockHelper), nameof(LockHelper.InitLockedContainerItem))]
    internal static class KeyBoxInitialSizePatch
    {
        private static void Postfix(GameItem gameItem)
        {
            if (!SaveManager.IsDecoding) KeyBoxes.ApplySize(gameItem);
        }
    }

    // The decoder calls RestoreTabs after restoring tags and before attaching saved child items.
    // Restore here, not in OnGameLoaded, so outlying saved cells already exist during attachment.
    [HarmonyPatch(typeof(StorageExpansionHelper), nameof(StorageExpansionHelper.RestoreTabs))]
    internal static class KeyBoxRestoreSizePatch
    {
        private static void Postfix(GameItem __0) { KeyBoxes.ApplySize(__0, restoreOnly: true); }
    }

    [HarmonyPatch(typeof(LockHelper), nameof(LockHelper.UnlockContainer))]
    internal static class KeyBoxUnlockSizePatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(GameItem item, bool force) { KeyBoxes.ExpandBeforeUnlock(item, force); }
    }

}

namespace FenceExpansion
{
    [HarmonyPatch(typeof(PreBuiltItemHelper), "FillEvidence")]
    internal static class EvidenceExpandedLootPatch
    { static void Postfix(GameItem gameItem, float luckMult) => KeyBoxes.FillExpandedSpace(gameItem, luckMult); }
    [HarmonyPatch(typeof(PreBuiltItemHelper), "FillMedical")]
    internal static class MedicalExpandedLootPatch
    { static void Postfix(GameItem gameItem, float luckMult) => KeyBoxes.FillExpandedSpace(gameItem, luckMult); }
    [HarmonyPatch(typeof(PreBuiltItemHelper), "FillSecurity")]
    internal static class SecurityExpandedLootPatch
    { static void Postfix(GameItem gameItem, float luckMult) => KeyBoxes.FillExpandedSpace(gameItem, luckMult); }
    [HarmonyPatch(typeof(PreBuiltItemHelper), "FillService")]
    internal static class ServiceExpandedLootPatch
    { static void Postfix(GameItem gameItem, float luckMult) => KeyBoxes.FillExpandedSpace(gameItem, luckMult); }
    [HarmonyPatch(typeof(PreBuiltItemHelper), "FillEngineering")]
    internal static class EngineeringExpandedLootPatch
    { static void Postfix(GameItem gameItem, float luckMult) => KeyBoxes.FillExpandedSpace(gameItem, luckMult); }
}





