using HarmonyLib;
using UnityEngine;

namespace FenceExpansion
{
    internal static class OtherworldAssets
    {
        internal const string EnderPortraitKey = "fence_expansion.enderman_art";
        private static Sprite portrait;

        internal static void SetItem(GameItem item, string key, int width, int height)
        {
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModEntry.Id);
            string fitted = "fitted_" + key;
            if (mod != null && !mod.Sprites.ContainsKey(fitted) && mod.Sprites.TryGetValue(key, out var source))
            {
                int cell = UISettings.current.gridSize;
                mod.Sprites[fitted] = GoldenAssets.Fit(source.texture, source.rect, width * cell, height * cell);
            }
            item.SetSpriteAndShapeFromMod(ModEntry.Id, fitted);
            var cells = new byte[width * height];
            for (int i = 0; i < cells.Length; i++) cells[i] = 1;
            item.SetShape(new GridShapeBuilder().SetData(cells, width));
        }

        internal static Sprite Portrait(SpriteDict dict)
        {
            if (portrait != null) return portrait;
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModEntry.Id);
            if (mod == null || !mod.Textures.TryGetValue("enderman", out var texture)) return null;
            var reference = dict.GetSprite(SpriteDict.GetRandomShadySpriteName());
            portrait = GoldenAssets.Fit(texture, new Rect(0, 0, texture.width, texture.height),
                (int)reference.rect.width, (int)reference.rect.height);
            return portrait;
        }
    }

    [HarmonyPatch(typeof(SpriteDict), nameof(SpriteDict.GetSprite))]
    internal static class EnderPortraitPatch
    {
        static bool Prefix(SpriteDict __instance, string key, ref Sprite __result)
        {
            if (key != OtherworldAssets.EnderPortraitKey) return true;
            __result = OtherworldAssets.Portrait(__instance);
            return __result == null;
        }
    }
}
