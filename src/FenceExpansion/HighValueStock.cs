using System.Linq;

namespace FenceExpansion
{
    internal static partial class HighValueStock
    {
        internal static GameItem Create(string id)
        {
            switch (id)
            {
                case "eng_box": return PreBuiltItemHelper.LootCrateEngineering();
                case "evidence_box": return PreBuiltItemHelper.LootCrateEvidence();
                case "med_box": return PreBuiltItemHelper.LootCrateMedical();
                case "sec_box": return PreBuiltItemHelper.LootCrateSecurity();
                case "service_box": return PreBuiltItemHelper.LootCrateService();
                case "nutrifruit_seed_packet": return PreBuiltItemHelper.FullNutrifruitSeedPacket();
                case "security_alarm": return PreBuiltItemHelper.CreateBrokenAlarm();
                case "ras": return MachineRAS.SpawnRASWithEmptyTank();
                case "rat":
                    var rat = PreBuiltItemHelper.CreateRat(RNG.Roll(50) ? "MALE" : "FEMALE", 5);
                    rat.unitValue = 500;
                    GeneticHelper.SetTrait(rat, HusbandryHelper.GetSpecies(rat),
                        RNG.Roll(50) ? "ANIMAL_GROWTH_RATE_TAG" : "ANIMAL_LITTER_SIZE_TAG", 1.25f);
                    return rat;
                case "sturgeon":
                    var fish = AquacultureItems.SpawnFish(FishSpecies.Sturgeon, "FEMALE", FishSpecies.Sturgeon.stageStartDays[3]);
                    fish.unitValue = 500;
                    return fish;
                default: return DirectoryMaster.Item(id);
            }
        }

        internal static void Fill()
        {
            var grid = EmporiumEntry.Instance.frontInvinvElement as GameGridInventory;
            if (grid == null) return;
            var pool = OtherworldGoods.WithUnlocked(Ids);
            MasterStockPolicy.Fill(pool.Length, grid.inventoryShape.width * grid.inventoryShape.height,
                grid.HasFreeCell, n => RNG.GetRandomInt(0, n - 1), index =>
                {
                    var item = Create(pool[index]);
                    if (item == null) return false;
                    if (item.GetBaseValue() <= 150 || item.IsTag("NOT_FOR_RESALE") || item.IsTag("HAZARDOUS_WASTE_TAG"))
                    { item.Destroy(); return false; }
                    return SellOnCounter(item, grid);
                });
        }

        // Preserve native sale ownership, stolen status and price hooks, but prevent
        // automatic insertion into previously offered machines/containers or stacks.
        internal static bool SellOnCounter(GameItem item, GameGridInventory grid)
        {
            var children = grid.childItems.ToList();
            var passes = children.Select(i => i.slotSearchPassthroughForItemFunc).ToList();
            var gridPass = grid.slotSearchPassthroughForItemFunc;
            var stack = item.mayStackItemFunc;
            try
            {
                grid.slotSearchPassthroughForItemFunc = null;
                foreach (var child in children) child.slotSearchPassthroughForItemFunc = null;
                item.mayStackItemFunc = (_, __) => false;
                GeneralHelper.SetItemOwned(item, false);
                var slot = grid.TryFindOneValidInventorySlot(item);
                if (slot == null || !ReferenceEquals(slot.inventory, grid))
                { item.Destroy(); return false; }
                return PlayerStore.Instance.TryAddDirectSellingItemToTable(item, isOwend: false, isStolen: true);
            }
            finally
            {
                grid.slotSearchPassthroughForItemFunc = gridPass;
                for (int i = 0; i < children.Count; i++) children[i].slotSearchPassthroughForItemFunc = passes[i];
                item.mayStackItemFunc = stack;
            }
        }
    }
}
