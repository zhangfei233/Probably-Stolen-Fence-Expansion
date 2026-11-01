using System;

namespace FenceExpansion
{
    public sealed class ModEntry : IMod
    {
        public const string Id = "fence_expansion";
        internal static ModLog Log;
        public static string Text(string zh, string en) =>
            (ModHelper.GetCurrentLocale()?.Identifier.Code ?? "en").StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? zh : en;
        public static bool Unlocked(string suffix) => PlayerStore.HasInstance
            && PlayerStore.Instance.networkUpgrade != null && ModHelper.IsNetworkUpgradeUnlocked(Id + "." + suffix);
        public void Init(ModManifest manifest) { Log = new ModLog(manifest); }
        public void OnEnable()
        {
            SkillConfig.Enable();
            GoldenMarket.Enable();
            OtherworldGoods.Enable();
            OtherworldEffects.Enable();
            EnderContact.Enable();
            BlackMarket.Enable();
            NightSkills.Enable();
            KeyCards.Enable();
            ModHook.OnGameLoadedLate += RefreshRun;
            ModHook.OnNetworkUpgradeUnlocked += OnPurchased;
            ModHook.OnNewGameStarted += OnNewRun;
            Log.Log("Fence Expansion 0.3.2 initialized; waiting for run before registering upgrades.");
        }
        internal static void RegisterUpgrades()
        {
            if (!PlayerStore.HasInstance || PlayerStore.Instance.networkUpgrade == null) return;
            ModHelper.SetNetworkTabLabel(Id, () => Text("销赃人", "Fence"));
            SkillDefinitions.Register();
            SkillConfig.ApplyRuntimeCosts();
        }
        private static void OnNewRun(string scenario) => RefreshRun();
        private static void OnPurchased(string id)
        {
            if (id == Id + ".skill_1_1" || id == Id + ".skill_1_2") KeyBoxes.RefreshExisting();
            NightSkills.Purchased(id);
            EnderContact.Purchased(id);
        }
        internal static void RefreshRun() { RegisterUpgrades(); KeyBoxes.RefreshExisting(); GoldenMarket.RefreshExisting();
            EnderContact.RefreshRun();
            OtherworldEffects.RefreshRun();
#if FENCE_RUNTIME_PROBE
            RuntimeProbe.Run();
#endif
        }
        public void OnDisable()
        {
            ModHook.OnNewGameStarted -= OnNewRun;
            ModHook.OnGameLoadedLate -= RefreshRun;
            ModHook.OnNetworkUpgradeUnlocked -= OnPurchased;
            SkillConfig.Disable();
            BlackMarket.Disable();
            NightSkills.Disable();
            GoldenMarket.Disable();
            OtherworldGoods.Disable();
            OtherworldEffects.Disable();
            EnderContact.Disable();
            KeyCards.Disable();
            ModHelper.RemoveNetworkUpgrades(Id);
        }
    }
    [HarmonyLib.HarmonyPatch(typeof(WildNetworkUI), nameof(WildNetworkUI.Open))]
    internal static class RefreshNetworkDefinitionsPatch
    {
        static void Prefix() => ModEntry.RegisterUpgrades();
    }
}



