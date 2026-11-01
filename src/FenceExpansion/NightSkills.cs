using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class NightSkills
    {
        internal const string ServiceId = ModEntry.Id + ".associates";
        private static bool Cleaning;
        private static string PendingMissionFaction;
        private static int PendingMissionGain;
        internal static void Enable()
        {
            ModHook.OnGoingSleepEarly += RunMission;
            ModHook.OnGoingSleepLate += CleanAtNight;
            ModHook.OnWakingUpLate += CleanAtNight;
            ModHook.OnWakingUpLate += NotifyMission;
        }
        internal static void Disable()
        {
            ModHook.OnGoingSleepEarly -= RunMission;
            ModHook.OnGoingSleepLate -= CleanAtNight;
            ModHook.OnWakingUpLate -= CleanAtNight;
            ModHook.OnWakingUpLate -= NotifyMission;
            PendingMissionFaction = null;
            if (PlayerStore.HasInstance && PlayerStore.Instance.storeService != null)
                PlayerStore.Instance.storeService.RemoveAll(s => s.id == ServiceId);
        }
        internal static void Purchased(string id)
        {
            if (id == ModEntry.Id + ".skill_3_3") CleanOwned();
            if (id == ModEntry.Id + ".skill_5") EnsureService();
        }
        internal static void EnsureService()
        {
            if (!PlayerStore.HasInstance || PlayerStore.Instance.storeService == null) return;
            var services = PlayerStore.Instance.storeService;
            var service = services.FirstOrDefault(s => s.id == ServiceId);
            if (service == null)
            {
                service = new StoreService { id = ServiceId, cost = 500, introduced = true };
                services.Add(service);
            }
            service.displayName = ModEntry.Text("指派小弟执行任务", "Send Associates on a Mission");
            service.unlocked = ModEntry.Unlocked("skill_5");
            service.cost = 500;
            if (!service.unlocked) service.isUsing = false;
        }
        internal static int MissionGain(int blackMarketReputation) => Math.Max(0, blackMarketReputation / 10);
        private static void RunMission()
        {
            EnsureService();
            if (!PlayerStore.HasInstance) return;
            var store = PlayerStore.Instance;
            var service = store.storeService?.FirstOrDefault(s => s.id == ServiceId);
            if (service == null || !service.isUsing) return;
            // Native EndNight charges every selected service after this hook. Own the
            // conditional transaction here and clear the flag to prevent a second charge.
            service.isUsing = false;
            if (!ModEntry.Unlocked("skill_5") || store.playerCash < 500) return;
            int gain = MissionGain(StoreReputation.GetBMReputation());
            if (gain <= 0) return;
            var choices = store.storeReputations.Where(r => r.factionId != StoreReputation.FACTION_BLACK_MARKET
                && r.GetReputationExact() < 200).ToList();
            if (choices.Count == 0) return;
            double lowest = choices.Min(r => r.GetReputationExact());
            choices.RemoveAll(r => r.GetReputationExact() != lowest);
            var target = choices[RNG.GetRandomInt(0, choices.Count - 1)];
            double actualGain = Math.Min(gain, 200 - target.GetReputationExact());
            target.ModReputationRaw(actualGain);
            store.playerCash -= 500;
            PendingMissionFaction = target.factionId;
            PendingMissionGain = (int)Math.Ceiling(actualGain);
            ModEntry.Log.Log("Associates mission completed: " + target.factionId + ", +" + actualGain);
        }
        private static void NotifyMission()
        {
            if (PendingMissionFaction == null || ReputationUIManager.Instance == null) return;
            // Wait until the waking transition has finished; an early notification
            // would time out while the screen is covered by the night summary.
            ReputationUIManager.Instance.NotifyReputation(PendingMissionFaction, PendingMissionGain);
            PendingMissionFaction = null;
        }
        private static void CleanAtNight()
        {
            if (ModEntry.Unlocked("skill_3_2") || ModEntry.Unlocked("skill_3_3")) CleanOwned();
        }
        internal static void CleanOwned()
        {
            if (Cleaning || !PlayerStore.HasInstance || EmporiumEntry.Instance == null) return;
            Cleaning = true;
            try
            {
                foreach (var item in PlayerStore.Instance.FindAllItem(false))
                    if (GeneralHelper.IsItemEffectivelyOwned(item)) StolenHelper.RemoveStolenStatus(item);
            }
            finally { Cleaning = false; }
        }
        internal static void Acquired(GameItem item)
        {
            if (Cleaning || SaveManager.IsDecoding || item == null || !ModEntry.Unlocked("skill_3_3")) return;
            // Descendants travel with the acquired container. No contraband tags touched.
            Cleaning = true;
            try
            {
                StolenHelper.RemoveStolenStatus(item);
                foreach (var child in GraphUtils.FindAllChildrenType<GameItem>(item))
                    StolenHelper.RemoveStolenStatus(child);
            }
            finally { Cleaning = false; }
        }
        internal static bool DepartmentCard(GameItem item) => item != null && KeyCards.IsKeyCard(item)
            && item.identifier != "cmd_keycard" && item.unitCount > 0;
        internal static void Exchange(ExchangeManager manager)
        {
            if (!ModEntry.Unlocked("skill_3_1") || !manager.isExchangeUnlocked
                || StoreReputation.IsPerkUnlocked("BM_DISTRUSTED") || EmporiumEntry.Instance == null) return;
            // Only items submitted to the ordinary trashcan exchange are eligible.
            var cards = GraphUtils.FindAllChildrenType<GameItem>(EmporiumEntry.Instance.trashcan)
                .Where(i => DepartmentCard(i) && GeneralHelper.IsItemEffectivelyOwned(i)).ToList();
            while (true)
            {
                // Take from the two largest department piles to avoid stranding a pair.
                var departments = cards.Where(i => i.unitCount > 0).GroupBy(i => i.identifier)
                    .OrderByDescending(g => g.Sum(i => (long)i.unitCount)).Take(2).ToList();
                if (departments.Count < 2) break;
                var first = departments[0].First();
                var second = departments[1].First();
                var output = DirectoryMaster.Item("cmd_keycard");
                KeyCards.NormalizeCard(output);
                manager.itemsBuffer.Add(output);
                ConsumeOne(first, cards);
                ConsumeOne(second, cards);
            }
        }
        private static void ConsumeOne(GameItem item, List<GameItem> cards)
        {
            if (item.unitCount > 1) item.unitCount--;
            else { cards.Remove(item); item.Destroy(); }
        }
    }

    [HarmonyPatch(typeof(EndOfDayUIManager), nameof(EndOfDayUIManager.InitUI))]
    internal static class AssociatesServiceUiPatch
    {
        static void Prefix() => NightSkills.EnsureService();
    }
    [HarmonyPatch(typeof(GeneralHelper), nameof(GeneralHelper.SetItemOwned))]
    internal static class AcquiredStolenGoodsPatch
    {
        static void Postfix(GameItem item, bool own) { if (own) NightSkills.Acquired(item); }
    }
    [HarmonyPatch(typeof(ExchangeManager), nameof(ExchangeManager.HandleExchange))]
    internal static class DepartmentExchangePatch
    {
        static void Prefix(ExchangeManager __instance) => NightSkills.Exchange(__instance);
    }

    // Rewards and crafting may already be owned before they enter the shop, so
    // they do not necessarily call SetItemOwned during the actual acquisition.
    [HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.UncheckedAccept))]
    internal static class AcquiredRewardPlacementPatch
    {
        static void Postfix(GameItem item, bool __result)
        {
            if (!__result || SaveManager.IsDecoding || item == null
                || !ModEntry.Unlocked("skill_3_3") || EmporiumEntry.Instance == null
                || !GeneralHelper.IsItemEffectivelyOwned(item)) return;
            // Ignore temporary factory containers and unpurchased customer stock.
            if (PlayerStore.Instance.FindAllItem(false).Contains(item)) NightSkills.Acquired(item);
        }
    }
}
