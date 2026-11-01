using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class OtherworldEffects
    {
        internal const string BlessingId = "fence_expansion.golden_apple_blessing";
        private const string DaysKey = "golden_apple_days";
        internal static string Name => ModEntry.Text("金苹果的祝福", "Golden Apple's Blessing");
        internal static bool Active => PlayerStore.HasInstance && ModHelper.GetModDataInt(ModEntry.Id, DaysKey, 0) > 0;
        internal static void Enable() { }
        internal static void Disable()
        {
            if (PlayerStore.HasInstance) PlayerStore.Instance.startingPerks?.RemoveAll(p => p.id == BlessingId);
        }
        internal static void Bind(GameItem item)
        {
            bool bottle = item.identifier == OtherworldGoods.BottleId;
            if (!bottle && item.identifier != OtherworldGoods.AppleId) return;
            if (bottle) { item.RemoveGameItemType("ALCOHOL"); item.SetGameItemType("SUBSTANCE"); }
            item.shortDescription = bottle
                ? ModEntry.Text("摄入后永久增加5点幸运。", "Consume to permanently gain 5 Luck.")
                : ModEntry.Text("摄入后获得金苹果的祝福，持续1天：出售物品的实际收入增加25%。不可叠加。", "Consume to gain Golden Apple's Blessing for 1 day: earn 25% more from item sales. Does not stack.");
            item.canActivateSlotItemFunc = (i, _, __) => CanConsume(i);
            item.mayActivateSlotItemFunc = (i, _, __) => CanConsume(i);
            item.onActivateSlotItemFunc = (i, _, __) => Consume(i);
        }
        internal static bool CanConsume(GameItem item) => PlayerStore.HasInstance && PlayerStore.Instance.healthData != null
            && GeneralHelper.IsItemOwned(item) && !item.IsTag("ITEM_BEHIND_LOCK_TAG");
        internal static void Consume(GameItem item)
        {
            if (!CanConsume(item)) return;
            if (item.identifier == OtherworldGoods.BottleId)
            {
                var health = PlayerStore.Instance.healthData;
                health.luck = (int)Math.Min(int.MaxValue, (long)health.luck + 5);
                StoreUIManager.Instance?.Notify(ModEntry.Text("幸运永久+5", "Permanently gained 5 Luck"));
            }
            else if (item.identifier == OtherworldGoods.AppleId)
            {
                // Native one-day health effects expire at the next sleep. Repeated
                // consumption refreshes one day, never adds strength or duration.
                ModHelper.SetModDataInt(ModEntry.Id, DaysKey, 1);
                RefreshStatus();
                StoreUIManager.Instance?.Notify(Name + ModEntry.Text("：销售额+25%（1天）", ": +25% sale proceeds (1 day)"));
            }
            else return;
            item.Destroy();
        }
        internal static void OnSleep()
        {
            if (!Active) return;
            ModHelper.SetModDataInt(ModEntry.Id, DaysKey, 0);
            RefreshStatus();
            PlayerStore.Instance.AddNightLog(ModEntry.Text("金苹果的祝福已消退。", "Golden Apple's Blessing has worn off."), "cc7755");
        }
        internal static void RefreshStatus()
        {
            if (!PlayerStore.HasInstance || PlayerStore.Instance.startingPerks == null) return;
            var perks = PlayerStore.Instance.startingPerks;
            perks.RemoveAll(p => p.id == BlessingId);
            if (!Active) return;
            // Display alongside the game's active character effects, but do not
            // register in the selectable new-game or mind-reprogramming catalogue.
            perks.Add(new StartingPerk {
                id = BlessingId, type = StartingPerk.StartingPerkType.POSITIVE, cost = 0, maxSlot = 0,
                mindEditable = false, source = ModEntry.Id, iconKey = ModEntry.Id + ":apple",
                nameProvider = () => Name,
                descriptionProvider = () => ModEntry.Text("出售物品的实际收入增加25%，下次睡觉时到期。不可叠加。", "Earn 25% more from item sales until your next sleep. Does not stack.")
            });
        }
        internal static void RefreshRun()
        {
            if (!PlayerStore.HasInstance) return;
            RefreshStatus();
            foreach (var item in PlayerStore.Instance.FindAllItem(false)) Bind(item);
        }
        internal static int SalePayout(int proceeds) => !Active || proceeds <= 0 ? proceeds :
            (int)Math.Min(int.MaxValue, (long)proceeds + proceeds / 4);
    }

    [HarmonyPatch(typeof(HealthData), nameof(HealthData.OnSleep))]
    internal static class AppleBlessingSleepPatch
    {
        static void Postfix() => OtherworldEffects.OnSleep();
    }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class AppleBlessingSalePatch
    {
        // Apply to the actual cash credit after the native negotiation/budget cap.
        // No change to buyer prices, item value, or the customer's budget debit.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList(); int matches = 0;
            for (int i = 1; i < code.Count; i++)
                if (code[i].opcode == OpCodes.Stfld && Equals(code[i].operand, AccessTools.Field(typeof(PlayerStore), "playerCash"))
                    && code[i - 1].opcode == OpCodes.Add)
                {
                    code.Insert(i - 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OtherworldEffects), nameof(OtherworldEffects.SalePayout))));
                    matches++; i++;
                }
            if (matches != 1) throw new InvalidOperationException("Expected exactly one native sale cash-credit anchor.");
            return code;
        }
    }
}
