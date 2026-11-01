using System;
using System.Linq;

namespace FenceExpansion
{
    /// <summary>The guaranteed return key is separate from quality-weighted luxury loot.</summary>
    internal static class LuxuryGuarantee
    {
        internal const string CardId = "cmd_keycard";

        internal static bool IsLuxury(GameItem box) => box != null && box.identifier == GoldenMarket.CrateId;
        internal static bool IsGuaranteedCard(GameItem box, GameItem item) =>
            IsLuxury(box) && item != null && item.identifier == CardId;

        // Invoke only while generating contents or on the first real unlock, never as
        // an inventory/UI refresh. Taking the key out of an open crate must not refill it.
        internal static void Ensure(GameItem box)
        {
            if (!IsLuxury(box) || SaveManager.IsDecoding || ReferenceEquals(box, QualityLoot.Capturing)
                || !(box.contentWindow?.child is GameGridInventory grid)) return;

            var keys = grid.childItems.Where(i => i.identifier == CardId).ToList();
            if (keys.Count > 0)
            {
                keys[0].SetUnitCount(1);
                foreach (var duplicate in keys.Skip(1)) duplicate.Destroy();
                return;
            }

            var card = DirectoryMaster.Item(CardId);
            // The concrete factory normally assigns quality. The guarded fallback
            // also handles callers whose item factory bypassed the Harmony hook.
            KeyCards.AssignNewQuality(card);
            card.SetUnitCount(1);
            GeneralHelper.SetItemOwned(card, GeneralHelper.IsItemOwned(box));
            var rule = grid.mayInventoryAddItemFunc;
            try
            {
                grid.mayInventoryAddItemFunc = null;
                // Existing sealed crates from the previous version may already be
                // full. Reserve the return key before the next loot-generation pass.
                foreach (var existing in grid.childItems.AsEnumerable().Reverse().ToList())
                {
                    if (GraphUtils.CanAccept(box, card)) break;
                    existing.Destroy();
                }
                if (GraphUtils.TryAcceptAll(box, card) != 0)
                    throw new InvalidOperationException("Luxury crate cannot accept its guaranteed Command Keycard.");
                card = null; // Ownership transferred to the inventory.
            }
            finally
            {
                grid.mayInventoryAddItemFunc = rule;
                card?.Destroy();
            }
        }
    }
}
