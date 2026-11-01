using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;

namespace FenceExpansion
{
    internal static class BlackMarketMath
    {
        internal static int ClampChance(int raw, bool capUnlocked, bool stockUnlocked)
            => Math.Max(0, Math.Min(raw, stockUnlocked ? int.MaxValue : capUnlocked ? 100 : 95));
        internal static int DiscountPercent(int rawChance) => (int)Math.Max(0L, Math.Min(50L, (long)rawChance - 100));
        internal static int AdjustedCount(int count, int rawChance)
            => count <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)count * Math.Max(100, rawChance) / 100);
        // Retained signature for the arithmetic harness; fractional counts now always floor.
        internal static int ExtraCount(int count, int rawChance, double ignored) => count <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)count * Math.Max(0, rawChance - 100) / 100);
    }

    internal static class BlackMarket
    {
        private static readonly string[] Targets = { "foodThief", "pettyThief", "thief" };
        internal static bool InNormalGeneration;
        public static void Enable() => ModHook.OnGenerateCustomerVeryLate += AddLampVisitors;
        public static void Disable() { ModHook.OnGenerateCustomerVeryLate -= AddLampVisitors; InNormalGeneration=false; }
        internal static bool IsTarget(string id) => Array.IndexOf(Targets,id)>=0;
        internal static int ChanceCeiling() => BlackMarketMath.ClampChance(int.MaxValue,
            ModEntry.Unlocked("skill_2_1"), ModEntry.Unlocked("skill_2_2"));
        internal static int GetAdjustedCount(int count,int rawChance) =>
            count; // Skill 2_2 now changes price only, never inventory quantity.

        private static void AddLampVisitors(StoreClientManager manager)
        {
            if (!ModEntry.Unlocked("skill_6")) return;
            var visitors=new List<StoreClient>();
            foreach(var id in Targets)
            {
                int chance=BlackLampHelper.GetChanceForTarget(id);
                for(int i=0;i<BlackLampHelper.GetLampCountForTarget(id);i++)
                {
                    if(chance<100 && !RNG.Roll(chance))continue;
                    var visitor=StoreClientListDict.CreateStoreClient(id);
                    if(visitor==null)continue;
                    visitor.attractedItemId="black_lamp";
                    visitors.Add(visitor);
                }
            }
            for(int i=0;i<GoldenMarket.LampCount;i++)
            {
                if(GoldenMarket.Chance<100 && !RNG.Roll(GoldenMarket.Chance))continue;
                var visitor=GoldenMarket.CreateVisitor();
                visitor.attractedItemId=GoldenMarket.LampId;
                visitors.Add(visitor);
            }
            // Shuffle only the added invitations, leaving scripted client priorities intact.
            for(int i=visitors.Count-1;i>0;i--)
            {
                int j=RNG.GetRandomInt(0,i);
                var value=visitors[i];visitors[i]=visitors[j];visitors[j]=value;
            }
            foreach(var visitor in visitors) manager.AddClient(visitor);
        }

        internal static void ApplyDiscount(StoreClient client)
        {
            if (client == null) return;
            client.onItemAddedToCounters += item =>
            {
                const string featureId = "fence_expansion.black_market_reputation";
                item.RemoveItemFeatureByID(featureId);
                if (GeneralHelper.IsItemOwned(item) || !ModEntry.Unlocked("skill_2_2")) return;
                int chance = client.identifier == GoldenMarket.VisitorId ? GoldenMarket.Chance
                    : IsTarget(client.identifier) ? BlackLampHelper.GetChanceForTarget(client.identifier) : 0;
                int discount = BlackMarketMath.DiscountPercent(chance);
                if (discount == 0) return;
                var feature = new ItemFeature(ItemFeature.FeatureType.TemporaryBuying)
                {
                    identifier = featureId, category = featureId,
                    valueStage = ItemFeature.ValueStage.Final, isFeatureMatch = true
                };
                string reason = ModEntry.Text("黑市的名声", "Black Market Reputation");
                feature.SetPublicDisplay(reason).SetActualDisplay(reason).SetPreExposedValueMod(-discount);
                item.AddItemFeature(feature);
            };
        }

        internal static void WrapStock(StoreClient client)
        {
            if (client == null || !IsTarget(client.identifier)) return;
            ApplyDiscount(client);
            // Original stock algorithms and their side effects run exactly once.
            var stockDialogue = client.mainDialogue?.nextDialogue;
            if (stockDialogue?.endAction == null) return;
            Action original = stockDialogue.endAction;
            stockDialogue.endAction = () =>
            {
                if (client.identifier == "thief" && ModEntry.Unlocked("skill_2_3"))
                    PlayerStore.Instance.TryAddDirectSellingItemToTable(CreateThiefCrate(), false, true, false, 0);
                OtherworldGoods.PlaceRareStock(client.identifier);
                original();
            };
        }

        internal static GameItem CreateThiefCrate()
        {
            int roll=RNG.GetRandomInt(0,100);
            if(roll<=20)return PreBuiltItemHelper.LootCrateEvidence();
            if(roll<=40)return PreBuiltItemHelper.LootCrateMedical();
            if(roll<=60)return PreBuiltItemHelper.LootCrateEngineering();
            if(roll<=75)return PreBuiltItemHelper.LootCrateSecurity();
            return PreBuiltItemHelper.LootCrateService();
        }

        internal static int SamplePettyCount()
        {
            float p=25f*StoreClientList.GetBMRepStockMult();
            int count=1;
            for(int i=0;i<10+(NetworkUpgrade.IsWinemakerUnlocked()?3:0);i++)if(RNG.Roll(p))count++;
            return count;
        }

    }

    [HarmonyPatch(typeof(BlackLampHelper), nameof(BlackLampHelper.GetChanceForTarget))]
    internal static class LampChancePatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code=instructions.ToList();int matches=0;
            for(int i=0;i+1<code.Count;i++)
                if(code[i].LoadsConstant(95) && code[i+1].opcode==OpCodes.Call)
                {
                    code[i].opcode=OpCodes.Call;
                    code[i].operand=AccessTools.Method(typeof(BlackMarket),nameof(BlackMarket.ChanceCeiling));matches++;
                }
            if(matches!=1)throw new InvalidOperationException("Expected one final black-lamp chance clamp.");
            return code;
        }
    }
    [HarmonyPatch(typeof(StoreClientManager),nameof(StoreClientManager.HandleNormalClient))]
    internal static class NormalGenerationScopePatch
    {
        static void Prefix(out bool __state){__state=BlackMarket.InNormalGeneration;BlackMarket.InNormalGeneration=true;}
        static Exception Finalizer(Exception __exception,bool __state){BlackMarket.InNormalGeneration=__state;return __exception;}
    }
    [HarmonyPatch(typeof(ShowcaseHelper),nameof(ShowcaseHelper.GetDisplayCasePrediction))]
    internal static class LampPredictionPatch
    {
        static void Postfix(List<ValueTuple<string,int,string>> __result)
        {
            if(BlackMarket.InNormalGeneration && ModEntry.Unlocked("skill_6"))
                __result.RemoveAll(p=>(p.Item3=="black_lamp" && BlackMarket.IsTarget(p.Item1)) || p.Item3==GoldenMarket.LampId);
        }
    }
    [HarmonyPatch(typeof(StoreClientList),nameof(StoreClientList.CreateThief))]
    internal static class ThiefStockPatch{static void Postfix(StoreClient __result)=>BlackMarket.WrapStock(__result);}
    [HarmonyPatch(typeof(StoreClientList),nameof(StoreClientList.CreatePettyThief))]
    internal static class PettyThiefStockPatch{static void Postfix(StoreClient __result)=>BlackMarket.WrapStock(__result);}
    [HarmonyPatch(typeof(StoreClientList),nameof(StoreClientList.CreateFoodThief))]
    internal static class FoodThiefStockPatch{static void Postfix(StoreClient __result)=>BlackMarket.WrapStock(__result);}
}

