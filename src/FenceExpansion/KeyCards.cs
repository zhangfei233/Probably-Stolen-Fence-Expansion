using System;
using HarmonyLib;

namespace FenceExpansion
{
    /// <summary>Per-item quality, using vanilla tags and save serialization.</summary>
    public static class KeyCards
    {
        internal const string QualityTag = "fence_expansion.card_quality";
        internal const string BasePriceTag = "fence_expansion.card_base_price";
        internal const string OpenQualityTag = "fence_expansion.opened_with_quality";
        internal const string ScannedTag = "LOOT_CRATE_SCANNED_TAG";
        internal const string CrateIdTag = "LOOT_CRATE_ID_STRING";

        [ThreadStatic] internal static UnlockScope CurrentUnlock;


        internal sealed class UnlockScope
        {
            internal GameItem Box;
            internal int Quality;
        }

public static void Enable() => ModHook.OnCreateTooltipLate += AddTooltip;
        public static void Disable()
        {
            ModHook.OnCreateTooltipLate -= AddTooltip;
            CurrentUnlock = null;

        }

        public static bool IsKeyCard(GameItem item)
        {
            if (item == null) return false;
            switch (item.identifier)
            {
                case "cmd_keycard": case "sec_keycard": case "sci_keycard":
                case "med_keycard": case "eng_keycard": case "sup_keycard":
                case "ser_keycard": return true;
                default: return false;
            }
        }

        public static int GetQuality(GameItem item)
        {
            if (item == null || !item.IsTag(QualityTag)) return 0;
            return Math.Max(0, Math.Min(3, item.GetTagReadonly(QualityTag).GetInt()));
        }

        public static int LuckBonus(int quality)
        {
            switch (quality) { case 1: return 25; case 2: return 50; case 3: return 100; default: return 0; }
        }

        public static long PriceForQuality(long ordinaryPrice, int quality)
        {
            // Integer credits: round midpoint upwards, without overflowing a long.
            decimal value = ordinaryPrice * (100m + LuckBonus(quality)) / 100m;
            return value >= long.MaxValue ? long.MaxValue : (long)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        internal static void AssignNewQuality(GameItem item)
        {
            if (item == null || SaveManager.IsDecoding || item.IsTag(QualityTag)) return;
            int quality = RNG.GetRandomInt(0, 3); // This game's upper bound is inclusive.
            long basePrice = item.unitValue;
            item.ModifyTag(QualityTag, tag => tag.Enable().SetInt(quality));
            item.ModifyTag(BasePriceTag, tag => tag.Enable().SetLong(basePrice));
            item.unitValue = PriceForQuality(basePrice, quality);
        }

        /// <summary>Explicit uninstall/migration helper; never called during shutdown.</summary>
        public static void NormalizeCard(GameItem item)
        {
            if (!IsKeyCard(item)) return;
            if (item.IsTag(BasePriceTag)) item.unitValue = item.GetTagReadonly(BasePriceTag).GetLong();
            item.ModifyTag(QualityTag, tag => tag.Enable().SetInt(0));
        }

        internal static bool IsVanillaLootCrate(GameItem box)
        {
            if (box == null || !box.IsTag(CrateIdTag)) return false;
            switch (box.GetTagReadonly(CrateIdTag).GetString())
            {
                case "fence_expansion.luxury_crate": case "loot_crate_evidence": case "loot_crate_medical":
                case "loot_crate_security": case "loot_crate_service":
                case "loot_crate_engineering": return true;
                default: return false;
            }
        }

        private static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (!IsKeyCard(item)) return;
            int quality = GetQuality(item);
            string[] names = { ModEntry.Text("普通", "Common"), ModEntry.Text("稀有", "Rare"), ModEntry.Text("史诗", "Epic"), ModEntry.Text("传说", "Legendary") };
            builder.AddLine(ModEntry.Text("钥匙卡品质：", "Keycard quality: ") + names[quality], true, RenderHandler.ColorPalette.LightCyan);
        }
    }

    // Patch the seven concrete factories, so customer stock, loot, crafting and
    // direct factory callers all get one roll. Blank cards are deliberately excluded.
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.CommandKeycard))]
    internal static class CommandCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.SecKeycard))]
    internal static class SecurityCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.SciKeycard))]
    internal static class ScienceCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.MedicalKeycard))]
    internal static class MedicalCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.EngineerKeycard))]
    internal static class EngineeringCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.ServiceKeycard))]
    internal static class ServiceCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }
    [HarmonyPatch(typeof(MiscItemDirectory), nameof(MiscItemDirectory.SupplyKeycard))]
    internal static class SupplyCardFactoryPatch { static void Postfix(GameItem __result) => KeyCards.AssignNewQuality(__result); }

    [HarmonyPatch(typeof(GameItem), nameof(GameItem.MayStack))]
    internal static class KeyCardStackPatch
    {
        static void Postfix(GameItem __instance, GameItem otherItem, ref bool __result)
        {
            if (__result && (KeyCards.IsKeyCard(__instance) || KeyCards.IsKeyCard(otherItem)))
                __result = KeyCards.GetQuality(__instance) == KeyCards.GetQuality(otherItem);
        }
    }

    [HarmonyPatch(typeof(LockHelper), nameof(LockHelper.TryOpenLock))]
    internal static class KeyCardUnlockPatch
    {
        static void Prefix(GameItem key, GameItem box, string keyLockId, out KeyCards.UnlockScope __state)
        {
            __state = KeyCards.CurrentUnlock;
            KeyCards.CurrentUnlock = null;
            if (SaveManager.IsDecoding || !KeyCards.IsKeyCard(key) || !KeyCards.IsVanillaLootCrate(box)
                || box.IsTag(KeyCards.OpenQualityTag) || !LockHelper.IsCurrentlyLocked(box)
                || !LockHelper.CanOpenLock(box, keyLockId)) return;
            int quality = KeyCards.GetQuality(key);
            if (quality > 0) KeyCards.CurrentUnlock = new KeyCards.UnlockScope { Box = box, Quality = quality };
        }

        static void Postfix(GameItem box)
        {
            var scope = KeyCards.CurrentUnlock;
            if (scope != null && ReferenceEquals(scope.Box, box) && !box.GetTagReadonly("LOCK_STATUS").GetBool())
                box.ModifyTag(KeyCards.OpenQualityTag, tag => tag.Enable().SetInt(scope.Quality));
        }

        static void Finalizer(KeyCards.UnlockScope __state) => KeyCards.CurrentUnlock = __state;
    }

    [HarmonyPatch(typeof(PreBuiltItemHelper), nameof(PreBuiltItemHelper.TryRefillForBetterLuck))]
    internal static class KeyCardRefillPatch
    {
        static bool Prefix(GameItem crate)
        {
            var unlock = KeyCards.CurrentUnlock;
            if (unlock == null || !ReferenceEquals(unlock.Box, crate) || SaveManager.IsDecoding
                || crate.IsTag(KeyCards.OpenQualityTag)) return true;
            return !QualityLoot.Generate(crate, unlock.Quality);
        }
    }
}



