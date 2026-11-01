using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FenceExpansion
{
    internal static class SkillConfig
    {
        private static readonly Dictionary<string, int> Costs = new Dictionary<string, int>(StringComparer.Ordinal);
        internal static void Enable()
        {
            ModHook.OnModAssetsLoaded += AssetsLoaded;
            // Initial OnEnable runs before ModLoader adds its LoadedMod record.
            if (ModLoader.Mods.Exists(m => m.Manifest.ID == ModEntry.Id)) Load();
        }
        internal static void Disable() => ModHook.OnModAssetsLoaded -= AssetsLoaded;
        private static void AssetsLoaded(LoadedMod mod)
        {
            if (mod.Manifest.ID != ModEntry.Id) return;
            Load();
            ApplyRuntimeCosts();
        }
        internal static void Load()
        {
            Costs.Clear();
            try
            {
                string json = ModHelper.ReadModFileText(ModEntry.Id, "skill.json");
                if (string.IsNullOrWhiteSpace(json)) return;
                LoadJson(json);
            }
            catch (Exception ex)
            {
                Costs.Clear();
                ModEntry.Log.Warning("skill.json could not be read; using default upgrade prices. " + ex.Message);
            }
        }
        internal static void LoadJson(string json)
        {
            Costs.Clear();
            var root = JObject.Parse(json);
            foreach (var property in root.Properties())
            {
                JToken value = property.Value is JObject obj ? obj["cost"] : null;
                if (value == null || value.Type != JTokenType.Integer) continue;
                // Decimal avoids overflow for an unusually large JSON integer.
                decimal number;
                if (!decimal.TryParse(value.ToString(), out number) || number < 0 || number > int.MaxValue) continue;
                Costs[property.Name] = (int)number;
            }
        }
        internal static int Cost(string id, int fallback) => Costs.TryGetValue(id, out int value) ? value : fallback;
        internal static void ApplyRuntimeCosts()
        {
            if (!PlayerStore.HasInstance || PlayerStore.Instance.networkUpgrade == null) return;
            foreach (var entry in SkillDefinitions.DefaultCosts)
                if (PlayerStore.Instance.networkUpgrade.TryGetValue(ModEntry.Id + "." + entry.Key, out var upgrade))
                {
                    upgrade.cost = Cost(entry.Key, entry.Value);
                    var definition = NetworkUpgrade.GetDefinition(ModEntry.Id + "." + entry.Key);
                    if (definition != null)
                    {
                        upgrade.prerequisite.Clear();
                        upgrade.prerequisite.AddRange(definition.prerequisite);
                    }
                }
        }
    }
}
