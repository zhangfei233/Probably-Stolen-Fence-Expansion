using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class EnderContact
    {
        internal const string VisitorId = "fence_expansion.enderman";
        internal const string CardId = "fence_expansion.ender_card";
        internal const long Number = 58;
        internal static string Name => ModEntry.Text("末影人", "Enderman");
        private static PaperUI cardView;
        internal static int BaseBudget(int reputation) => (int)Math.Max(1000L, Math.Min(int.MaxValue, 2000L + 15L * reputation));
        internal static bool IsCrate(GameItem item) => item != null &&
            (KeyBoxes.IsKeyBox(item) || item.IsTag("SUPPLY_CRATE") || item.identifier == GoldenMarket.CrateId);
        internal static void Enable()
        {
            ModHook.OnModItemDirectoryInit += Register;
            PhoneClientList.DialogFuncs[VisitorId] = Call;
            StoreClientListDict.storeClientDict[VisitorId] = CreateVisitor;
        }
        internal static void Disable()
        {
            ModHook.OnModItemDirectoryInit -= Register;
            PhoneClientList.DialogFuncs.Remove(VisitorId);
            StoreClientListDict.storeClientDict.Remove(VisitorId);
            cardView?.Close(); cardView = null;
        }
        private static void Register(ModItemDirectory directory) => directory.Add(CardId, CreateCard);
        internal static void RefreshRun()
        {
            if (!ModEntry.Unlocked("skill_4_2") || PlayerStore.Instance.PhoneClientDict == null) return;
            var contacts = PlayerStore.Instance.PhoneClientDict;
            if (!contacts.TryGetValue(Number, out var contact))
                contacts[Number] = contact = new StorePhoneClient {
                    phoneState = StorePhoneClient.PhoneState.Regular, dialedBefore = true,
                    dialogFuncId = VisitorId, cooldownDuration = 1, locID = VisitorId
                };
            if (contact.dialogFuncId != VisitorId) throw new InvalidOperationException("Phone number 0058 is already registered by another contact.");
            contact.displayName = Name;
            contact.cooldownDuration = 1;
            if (!contact.alias.Contains(VisitorId)) contact.alias.Add(VisitorId);
        }
        internal static void Purchased(string id)
        {
            if (id != ModEntry.Id + ".skill_4_2") return;
            RefreshRun();
            PlayerStore.Instance.AddDirectToWeightedTable(DirectoryMaster.Item(CardId), isOwend: true);
        }
        private static GameItem CreateCard()
        {
            var item = ItemDirectory.CreateEmptyItem(CardId);
            OtherworldAssets.SetItem(item, "ender_eye", 1, 1);
            item.name = ModEntry.Text("末影人的名片", "Enderman's Business Card");
            item.shortDescription = ModEntry.Text("双击查看电话号码。", "Double-click to view the phone number.");
            item.unitValue = 0;
            item.SetGameItemType("DOCUMENT"); item.EnableTag("IMPORTANT_TAG");
            item.canActivateSlotItemFunc = (_, __, ___) => HealthData.CanRead();
            item.mayActivateSlotItemFunc = (_, __, ___) => HealthData.CanRead();
            item.onActivateSlotItemFunc = (_, __, ___) => {
                cardView?.Close();
                cardView = ModHelper.CreatePaper(ModEntry.Id, "ender_card", ModEntry.Id + ":ender_frame", 720, 480).Show();
            };
            return item;
        }
        private static void HangUp() => PhoneUIManager.Instance.OnPhoneDialogEnd();
        internal static Dialogue Call(StorePhoneClient contact)
        {
            if (!ModEntry.Unlocked("skill_4_2") || contact.currentCooldown > 0)
                return new Dialogue().SetText(Name, ModEntry.Text("已经约好了，明天见！", "We're already booked. See you tomorrow!")).SetEndAction(HangUp);
            var first = new Dialogue().SetText(Name, ModEntry.Text("嘿朋友，你又准备了我最爱的物资箱吗？", "Hey, friend! Have you got another of my favorite supply crates ready?"));
            var choices = first.NextDialogue().SetText("PLAYER", ModEntry.Text("邀请末影人前来收购？", "Invite Enderman to buy a crate?"));
            // The phone UI displays Item2 as the player's choice, then advances
            // straight to Item2.nextDialogue. Its reply must be a separate node.
            var yes = new Dialogue().SetText("PLAYER", ModEntry.Text("是的", "Yes"));
            var no = new Dialogue().SetText("PLAYER", ModEntry.Text("还没", "Not yet"));
            yes.NextDialogue().SetText(Name, ModEntry.Text("好的，那我们明天见", "Great, then I'll see you tomorrow."))
                .SetEndAction(HangUp);
            choices.AddChoice(DirectoryMaster.Item("$"), "", "", yes, () => {
                if (contact.currentCooldown <= 0 && ModEntry.Unlocked("skill_4_2")) {
                    contact.UseService();
                    PlayerStore.Instance.QueueFuturClient(VisitorId, 1);
                }
            });
            choices.AddChoice(DirectoryMaster.Item("$"), "", "", no, HangUp);
            return first;
        }
        internal static StoreClient CreateVisitor()
        {
            var visitor = new StoreClient {
                identifier = VisitorId, displayName = Name, spriteName = OtherworldAssets.EnderPortraitKey,
                clientFaction = StoreClient.FACTION_CRIME, clientIntent = StoreClient.ClientIntent.BUY,
                isMultiBuyDisabled = true, cooldown = 0, CanReturnAsBuyer = false
            };
            ModHelper.SetClientSource(visitor, ModEntry.Id);
            visitor.SetBudget(BaseBudget(StoreReputation.GetBMReputation()));
            bool bought = false;
            visitor.willAcceptItem = item => !bought && IsCrate(item);
            visitor.onItemBought += (item, price) => { bought = true; visitor.OverrideBudget(0); };
            visitor.mainDialogue.SetText(Name, ModEntry.Text(
                "嘿朋友，我来啦！我最爱的物资箱在哪？老规矩，我只拿一个哦。",
                "Hey, friend, I'm here! Where's my favorite supply crate? Same as always—just one, please."));
            visitor.AddBasicDialog();
            visitor.allDoneDialogue = new Dialogue().SetText(Name, ModEntry.Text("那下次再见！", "See you next time!"))
                .SetEndAction(() => PlayerStore.Instance.DismissCurrentClient(callNextWhenPossible: false));
            visitor.CompleteClientCreation();
            return visitor;
        }
    }
    // A normal category-specific buyer rejects an open container when individual
    // contents aren't in its buying category. Enderman buys the whole crate.
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.IsClientRefusingItem), new[] { typeof(GameItem), typeof(bool) })]
    internal static class EnderCrateContentsPatch
    {
        static bool Prefix(StoreClient __instance, GameItem gameItem, ref bool __result)
        {
            if (__instance.identifier != EnderContact.VisitorId || !EnderContact.IsCrate(gameItem)) return true;
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetLocalizedDisplayName))]
    internal static class EnderPhoneNamePatch
    {
        static bool Prefix(StorePhoneClient __instance, ref string __result)
        {
            if (__instance.dialogFuncId != EnderContact.VisitorId) return true;
            __result = EnderContact.Name; return false;
        }
    }
    [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SellItem))]
    internal static class EnderSingleSalePatch
    {
        static bool Prefix(PlayerStore __instance, GameItem item)
        {
            var client = __instance.currentClientInstance?.GetClientBlueprint();
            return client?.identifier != EnderContact.VisitorId ||
                (client.GetBudget() > 0 && GeneralHelper.IsItemOwned(item) && EnderContact.IsCrate(item));
        }
    }
    [HarmonyPatch(typeof(NegociationUIManager), "GetSellableBackInvItems")]
    internal static class EnderSaleSelectionPatch
    {
        static void Postfix(StoreClient storeClient, ref List<GameItem> __result)
        {
            if (storeClient?.identifier != EnderContact.VisitorId || __result.Count <= 1) return;
            var selected = NegociationUIManager.Instance == null ? null :
                AccessTools.Field(typeof(NegociationUIManager), "selectedItem").GetValue(NegociationUIManager.Instance) as GameItem;
            var chosen = selected != null && __result.Contains(selected) ? selected : __result[0];
            __result = new List<GameItem> { chosen };
        }
    }
}
