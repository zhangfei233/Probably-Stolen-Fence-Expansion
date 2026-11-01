using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class GoldenMarket
    {
        internal const string VisitorId = "fence_expansion.master_thief";
        internal const string LampId = "fence_expansion.golden_lamp";
        internal const string CrateId = "fence_expansion.luxury_crate";
        private static readonly List<string> LuxuryIds = new List<string>();
        private static Dictionary<string, Action<GameItem, float>> Fillers =>
            (Dictionary<string, Action<GameItem, float>>)AccessTools.Field(typeof(PreBuiltItemHelper), "crateFillers").GetValue(null);

        internal static void Enable()
        {
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHook.OnPlaceInventorInventoryItemLate += OfferLamp;
            ModHook.OnCreateTooltipLate += Tooltip;
            ModHook.OnLootTablesLoaded += ResetPool;
            StoreClientListDict.storeClientDict[VisitorId] = CreateVisitor;
            PreBuiltItemHelper.preBuiltItems[CrateId] = CreateLuxuryCrate;
            Fillers[CrateId] = FillLuxury;
        }
        internal static void Disable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnPlaceInventorInventoryItemLate -= OfferLamp;
            ModHook.OnCreateTooltipLate -= Tooltip;
            ModHook.OnLootTablesLoaded -= ResetPool;
            StoreClientListDict.storeClientDict.Remove(VisitorId);
            PreBuiltItemHelper.preBuiltItems.Remove(CrateId);
            Fillers.Remove(CrateId);
            ResetPool();
        }
        private static void ResetPool() => LuxuryIds.Clear();
        internal static void RefreshExisting()
        {
            if (!PlayerStore.HasInstance || EmporiumEntry.Instance == null) return;
            GoldenAssets.EnsureLamp();
            foreach (var item in PlayerStore.Instance.FindAllItem(false))
            {
                if (item.identifier == LampId)
                {
                    item.SetSpriteAndShapeFromMod(ModEntry.Id, "golden_lamp");
                    item.SetShape(new GridShapeBuilder().SetData(new byte[] {1,1,1,1,1,1},2));
                    item.Validate();
                }
                if (item.identifier == CrateId)
                {
                    SetCrateDescription(item);
                    if (item.GetTagReadonly(LockHelper.LOCK_STATUS).GetBool()) item.SetGameItemType("SUPPLY_CRATE");
                }
            }
        }
        private static void SetCrateDescription(GameItem item) => item.shortDescription = ModEntry.Text(
            "一个上锁的板条箱，标签上写着归上层区某富人所有。内含贵重奢侈品。需要指挥部识别卡才能打开。",
            "A locked crate labeled as the property of a wealthy resident of the upper levels. Contains valuable luxury goods. Requires a Command Department ID card to open.");
        private static void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(LampId, CreateLamp);
            directory.Add(CrateId, CreateLuxuryCrate);
        }
        private static GameItem CreateLamp()
        {
            var item = ItemDirectory.CreateEmptyItem(LampId);
            GoldenAssets.EnsureLamp();
            item.SetSpriteAndShapeFromMod(ModEntry.Id, "golden_lamp");
            item.SetShape(new GridShapeBuilder().SetData(new byte[] { 1, 1, 1, 1, 1, 1 }, 2));
            item.name = ModEntry.Text("金灯", "Golden Lamp");
            item.shortDescription = ModEntry.Text("在展示框中吸引大盗。", "Attracts Master Thieves when displayed.");
            item.unitValue = 888;
            item.EnableTag("IMPORTANT_TAG");
            item.mayStackItemFunc = (_, __) => false;
            item.ModifyTag(BlackLampHelper.LAMP_TARGET_TAG, tag => tag.Enable().SetString(VisitorId));
            return item;
        }
        private static void OfferLamp(List<GameItem> owned)
        {
            // Match the random black-lamp offer; deliberately no AddIfNotOwn guarantee.
            if (ModEntry.Unlocked("skill_4_1") && RNG.Roll(15 + StoreReputation.GetBMReputation() / 2))
                PlayerStore.Instance.AddDirectSellingItemToTable(DirectoryMaster.Item(LampId));
        }
        internal static int LampCount => EmporiumEntry.Instance == null ? 0 :
            EmporiumEntry.Instance.showcaseElement.childItems.Count(i => i.identifier == LampId);
        internal static int Chance => LampCount == 0 ? 0 :
            Math.Max(0, Math.Min(BlackMarket.ChanceCeiling(), LampCount * 30 + StoreReputation.GetBMReputation() / 2));
        private static void Tooltip(RichTextBuilder builder, GameItem item)
        {
            if (item.identifier == LampId)
                builder.AddLine(ModEntry.Text("正在发送信号：大盗", "Signaling: Master Thief") + " (" + Chance + "%)", true, RenderHandler.ColorPalette.LightRed, bold: true);
        }
        internal static StoreClient CreateVisitor()
        {
            var visitor = new StoreClient
            {
                identifier = VisitorId,
                displayName = ModEntry.Text("大盗", "Master Thief"),
                clientFaction = StoreClient.FACTION_CRIME,
                spriteName = GoldenAssets.PortraitKey,
                cooldown = 2,
                clientIntent = StoreClient.ClientIntent.SELL
            };
            ModHelper.SetClientSource(visitor, ModEntry.Id);
            visitor.SetWholesale(-10);
            visitor.mainDialogue.SetText(visitor.displayName, ModEntry.Text("听说这里收好货。", "I hear you have an eye for the finer things."))
                .NextDialogue().SetText(visitor.displayName, ModEntry.Text("看看这批货，来路就别问了。", "Take a look. Best not ask where it came from."))
                .SetEndAction(PlaceStock);
            visitor.AddBasicDialog();
            visitor.CompleteClientCreation();
            BlackMarket.ApplyDiscount(visitor);
            return visitor;
        }
        private static void Sell(GameItem item)
        {
            if (item != null) PlayerStore.Instance.AddDirectSellingItemToTable(item, isOwend: false, isStolen: true);
        }
        private static void PlaceStock()
        {
            MasterStockPolicy.Specials(ModEntry.Unlocked("skill_2_3"), p => RNG.Roll(p),
                () => Sell(CreateLuxuryCrate()),
                capped => Sell(DirectoryMaster.Item(capped ? "system_capped_neural_core" : "system_uncapped_neural_core")));
            HighValueStock.Fill();
        }
        internal static GameItem CreateLuxuryCrate()
        {
            var window = DirectoryUtils.CreateInventoryWindow(7, 5);
            var item = ItemDirectory.CreateEmptyItem(CrateId).SetContentWindow(window.Item1)
                .SetSpriteAndShape("Items/items_container", "eleanor_box");
            item.name = ModEntry.Text("奢侈品物资箱", "Luxury Supply Crate");
            SetCrateDescription(item);
            item.unitValue = 300;
            // DirectoryMaster normally assigns ownership after a factory returns. This
            // factory fills its contents first, so initialize the same default now.
            GeneralHelper.SetItemOwned(item, true);
            LockHelper.InitLockedContainerItem(item, LockHelper.LOCK_ID_COMMAND, window.Item2);
            // Keep the vanilla supply-crate category for the tooltip.
            // A scoped unlock patch below handles its unchanged parcel artwork.
            item.ModifyTag(KeyCards.CrateIdTag, tag => tag.Enable().SetString(CrateId));
            if (!SaveManager.IsDecoding)
            {
                LotteryHelper.StampSpawnLuck(item);
                FillLuxury(item, PreBuiltItemHelper.GetLuckMult(item));
            }
            LockHelper.LockUpContainer(item);
            return item;
        }
        internal static void FillLuxury(GameItem item, float luckMult)
        {
            EnsurePool();
            var pool = OtherworldGoods.WithUnlocked(LuxuryIds);
            var grid = item.contentWindow?.child as GameInventory;
            if (grid == null) return;
            var rule = grid.mayInventoryAddItemFunc;
            try
            {
                grid.mayInventoryAddItemFunc = null;
                LuxuryGuarantee.Ensure(item);
                foreach (string id in pool)
                    if (RNG.Roll(Math.Min(100f, 35f * luckMult))) QualityLoot.TryAdd(item, ItemSpawner.Spawn(id));
                // Small themed pools can miss every independent roll. Supply at least one
                // valid reward, including when repairing a sealed crate from the old build.
                if (!ReferenceEquals(item, QualityLoot.Capturing) && !grid.childItems.Any(i => i.identifier != LuxuryGuarantee.CardId))
                    foreach (string id in pool.OrderBy(_ => RNG.GetRandomDouble(0, 1)))
                    {
                        QualityLoot.TryAdd(item, ItemSpawner.Spawn(id));
                        if (grid.childItems.Any(i => i.identifier != LuxuryGuarantee.CardId)) break;
                    }
                KeyBoxes.FillExpandedSpace(item, luckMult);
            }
            finally { grid.mayInventoryAddItemFunc = rule; }
        }
        private static void EnsurePool()
        {
            if (LuxuryIds.Count != 0) return;
            var ids = new HashSet<string>(NormalSellerIds);
            foreach (string table in NormalSellerTables)
                foreach (var entry in LootRegistry.GetTableEntries(table))
                    if (entry.weight > 0) ids.Add(entry.id);
            foreach (string id in ids.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (id == "luxury_supply_crate" || id == CrateId) continue;
                var item = ItemSpawner.Spawn(id);
                if (item == null) continue;
                try
                {
                    if (item.IsTag("LUXURY_ITEM") && !item.IsTag("IMPORTANT_TAG") && item.contentWindow == null)
                        LuxuryIds.Add(id);
                }
                finally { item.Destroy(); }
            }
        }
        // Build-time whitelist: literal stock IDs/tables used by normal StoreClientList merchants.
        private static readonly string[] NormalSellerIds = new[] { "50ml_cylinder", "advanced_flux_agent", "alarm_module_transmitter", "backpack_large", "backpack_medium", "backpack_small", "bandage_item", "bipod", "birth_control_pill", "black_injector", "blank_keycard", "blood_bag", "bloomberry_seed", "blue_blood_bag", "bottled_water_premium", "box_cutter", "box_tampon", "caffeine_pill", "cassette5", "chem_module", "cmd_keycard", "combat_knife", "common_chemical", "common_electronic", "common_medical", "common_ore", "compensator", "crowbar", "crypto_module_cmd", "crypto_module_eng", "crypto_module_med", "crypto_module_sec", "crypto_module_ser", "current_converter", "desequencer", "dna_manipulator_module_negative_filter", "dna_manipulator_module_positive_filter", "dna_manipulator_module_precision", "dna_manipulator_module_stabilizer", "dream_dust", "energy_credit_breeder", "energy_credit_dis", "eng_keycard", "fanta_pill", "farm_module_dream_cap", "farm_module_fillerweed_production", "farm_module_hydroreed_filter", "farm_module_kotton_loom", "farm_module_nightmare_cap", "farm_module_nutrifruit", "farm_module_smuggler", "fillerweed_seed", "flux_agent", "fridge", "furnace_module_blast", "furnace_module_junk", "galaxy_blend", "gl", "grip", "hatchet", "heavy_pistol_ammo", "heavy_pistol_ammo_nl", "heavy_pistol_ammo_p", "hemostatic_bandage_item", "kitchen_cleaver", "kitchen_knife", "kotton_fabric", "kotton_seed", "laser", "laser2", "lottery_info", "lottery_ticket", "machine_bay", "machine_bay_ext", "mag_10mm", "mag_10mm_ext", "mag_10mm_high", "mag_10mm_smg", "mag_10mm_smg_box", "mag_10mm_smg_high", "med_keycard", "mini_bottle", "module_bay_expansion_kit", "module_extractor", "module_extractor_advanced", "mouse_trap", "neuroactive_perfume", "nudka", "nutrifruit", "nutrifruit_seed", "nuts_metal", "phagimycin_pill", "pheromone_perfume", "pink_injector", "pipe_weapon", "plant_dna_disk", "portable_water_purifier", "powerblock", "printer_plastic", "processed_cheese", "processed_juice", "processed_meat", "processed_milk", "pure_white_injector", "rds", "recharger", "red_beer", "rubbing_alcohol", "salve", "satchel", "scrap_metal", "scratcher", "scratcher_stack", "screwdriver", "sec_keycard", "ser_keycard", "shampoo", "short_stock", "silencer", "silencer2", "skincare_cream", "small_pistol_ammo", "smelling_salt", "smuggler_bay", "stock", "storage_bay", "storage_bay_expansion_kit", "storage_bay_large", "stun_gun", "surgery_tool", "system_module_corrupt", "system_module_ruined", "tazer", "toilet_paper", "toothpaste", "topical_bandage_item", "turbo_booster_adv", "uv_filter", "water_filter_adv", "water_pitcher", "water_ration_small", "water_tank", "welder", "wine_berry", "wine_bottle", "wine_yeast", "wine_yeast_infinite", "wine_yeast_red", "wire", "wire_cutter" };
        private static readonly string[] NormalSellerTables = new[] { "householdTable", "medicalTable", "packedFoodTable" };
    }

    [HarmonyPatch(typeof(ShowcaseHelper), nameof(ShowcaseHelper.GetDisplayCasePrediction))]
    internal static class GoldenLampPredictionPatch
    {
        [HarmonyPriority(Priority.First)]
        static void Postfix(List<ValueTuple<string, int, string>> __result)
        {
            if (ModEntry.Unlocked("skill_4_1") && GoldenMarket.LampCount > 0)
                __result.Insert(0, (GoldenMarket.VisitorId, GoldenMarket.Chance, GoldenMarket.LampId));
        }
    }
}

