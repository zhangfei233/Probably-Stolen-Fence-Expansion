using System;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class VisitorProtection
    {
        [ThreadStatic] internal static StoreClient ReportSubject;
        internal static bool IsProtected(StoreClient client) => client != null &&
            (client.identifier == GoldenMarket.VisitorId || client.identifier == EnderContact.VisitorId);
        internal static bool CurrentProtected => PlayerStore.HasInstance &&
            IsProtected(PlayerStore.Instance.currentClientInstance?.GetClientBlueprint());
    }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.OnCustomerReported))]
    internal static class InnocentReportScopePatch
    {
        internal sealed class State { internal StoreClient Previous, Client; internal bool Wanted, Aug; }
        static void Prefix(PlayerStore __instance, out State __state)
        {
            var client = __instance.currentClientInstance?.GetClientBlueprint();
            __state = new State { Previous = VisitorProtection.ReportSubject };
            VisitorProtection.ReportSubject = VisitorProtection.IsProtected(client) ? client : null;
            if (VisitorProtection.ReportSubject == null) return;
            __state.Client = client; __state.Wanted = client.isWanted; __state.Aug = client.isAug;
            client.isWanted = false; client.isAug = false;
        }
        static void Finalizer(State __state)
        {
            if (__state == null) return;
            if (__state.Client != null) { __state.Client.isWanted = __state.Wanted; __state.Client.isAug = __state.Aug; }
            VisitorProtection.ReportSubject = __state.Previous;
        }
    }
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.IsSellingContraband))]
    internal static class InnocentReportContrabandPatch
    {
        static void Postfix(StoreClient __instance, ref bool __result)
        { if (ReferenceEquals(__instance, VisitorProtection.ReportSubject)) __result = false; }
    }
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.IsSellingStolen))]
    internal static class InnocentReportStolenPatch
    {
        static void Postfix(StoreClient __instance, ref bool __result)
        { if (ReferenceEquals(__instance, VisitorProtection.ReportSubject)) __result = false; }
    }
    [HarmonyPatch(typeof(Shotgun), nameof(Shotgun.CanPickup))]
    internal static class ProtectedVisitorShotgunPatch
    {
        static void Postfix(ref bool __result) { if (VisitorProtection.CurrentProtected) __result = false; }
    }
    [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.EnterShootingMode))]
    internal static class ProtectedVisitorShootingModePatch
    {
        static bool Prefix() => !VisitorProtection.CurrentProtected;
    }
    [HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.CanShoot))]
    internal static class ProtectedVisitorCanShootPatch
    {
        static void Postfix(ref bool __result) { if (VisitorProtection.CurrentProtected) __result = false; }
    }
}
