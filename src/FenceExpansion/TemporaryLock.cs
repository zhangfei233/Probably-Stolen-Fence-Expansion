using HarmonyLib;

namespace FenceExpansion
{
    // Keep the original upgrade ID so existing purchases acquire the replacement effect.
    internal static class TemporaryLock
    {
        internal static bool Protects(GameItem item)
        {
            return item != null && KeyBoxes.IsKeyBox(item)
                && item.IsTag(LockHelper.LOCK_STATUS)
                && !item.GetTagReadonly(LockHelper.LOCK_STATUS).GetBool()
                && ModEntry.Unlocked("skill_1_3");
        }
    }

    [HarmonyPatch(typeof(ContrabandHelper), nameof(ContrabandHelper.IsBlockSearchContainer))]
    internal static class TemporaryLockInspectionPatch
    {
        static void Postfix(GameItem gameItem, ref bool __result)
        {
            // Let RecursiveInspect use vanilla CollectConcealed and stop descending.
            // No persistent hidden flags: moving goods out immediately removes protection.
            if (!__result && TemporaryLock.Protects(gameItem)) __result = true;
        }
    }
}

