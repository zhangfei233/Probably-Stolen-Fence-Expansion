using HarmonyLib;

namespace FenceExpansion
{
    internal static class UpperVisitor
    {
        internal const string ClientId = "ulDiscerningShopper";
        internal static bool Guaranteed => ModEntry.Unlocked("skill_7")
            && EmporiumEntry.Instance != null
            && ShowcaseHelper.GetTypeCount("LUXURY_ITEM") > 0;
    }

    // Reuse the single existing HandlePerkClient roll. Adding a visitor in a
    // second callback would duplicate successful vanilla invitations.
    [HarmonyPatch(typeof(ShowcaseHelper), nameof(ShowcaseHelper.GetDiscerningShopperChance))]
    internal static class UpperVisitorChancePatch
    {
        internal static void Postfix(ref int __result)
        {
            if (UpperVisitor.Guaranteed) __result = 100;
        }
    }

    // The vanilla roll has a second cooldown gate outside the chance helper.
    // Only this exact showcase visitor bypasses it while the skill is active.
    [HarmonyPatch(typeof(StoreClientManager), nameof(StoreClientManager.IsClientOnCooldown), new[] { typeof(string) })]
    internal static class UpperVisitorCooldownPatch
    {
        internal static void Postfix(string clientId, ref bool __result)
        {
            if (clientId == UpperVisitor.ClientId && UpperVisitor.Guaranteed) __result = false;
        }
    }

    // Keep the showcase UI's cooldown readout consistent without altering the
    // saved vanilla cooldown; removing the displayed luxury restores its rules.
    [HarmonyPatch(typeof(ShowcaseHelper), nameof(ShowcaseHelper.GetDiscerningShopperCooldownOpenings))]
    internal static class UpperVisitorCooldownDisplayPatch
    {
        internal static void Postfix(ref int __result)
        {
            if (UpperVisitor.Guaranteed) __result = 0;
        }
    }
}
