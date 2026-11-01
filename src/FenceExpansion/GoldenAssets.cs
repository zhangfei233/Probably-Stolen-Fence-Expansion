using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FenceExpansion
{
    internal static class GoldenAssets
    {
        internal const string PortraitKey = "fence_expansion.master_thief_art";
        private static Sprite portrait;
        internal static void EnsureLamp()
        {
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModEntry.Id);
            if (mod == null || mod.Sprites.ContainsKey("golden_lamp")) return;
            if (!mod.Sprites.TryGetValue("skill_4_1", out var source)) return;
            int cell = UISettings.current.gridSize;
            mod.Sprites["golden_lamp"] = Fit(source.texture, source.rect, 2 * cell, 3 * cell);
        }
        internal static Sprite Portrait(SpriteDict dict)
        {
            if (portrait != null) return portrait;
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModEntry.Id);
            if (mod == null || !mod.Textures.TryGetValue("master_thief", out var texture)) return null;
            var reference = dict.GetSprite(SpriteDict.GetRandomShadySpriteName());
            portrait = Fit(texture, new Rect(0, 0, texture.width, texture.height),
                (int)reference.rect.width, (int)reference.rect.height);
            return portrait;
        }
        // Crop transparent margins, fit inside a native canvas, center horizontally,
        // and align its feet to the bottom. Pixel data and grid share the same origin.
        internal static Sprite Fit(Texture2D source, Rect rect, int width, int height)
        {
            int left=(int)rect.xMax, right=(int)rect.xMin-1, bottom=(int)rect.yMax, top=(int)rect.yMin-1;
            for(int y=(int)rect.yMin;y<(int)rect.yMax;y++)
                for(int x=(int)rect.xMin;x<(int)rect.xMax;x++)
                    if(source.GetPixel(x,y).a > .01f) {left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
            if(right<left || top<bottom) throw new InvalidOperationException("Empty golden-market artwork");
            float scale=Math.Min((float)width/(right-left+1),(float)height/(top-bottom+1));
            int w=Math.Max(1,(int)Math.Round((right-left+1)*scale));
            int h=Math.Max(1,(int)Math.Round((top-bottom+1)*scale));
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(new Color[width*height]);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                texture.SetPixel((width-w)/2+x,y,source.GetPixel(left+Math.Min(right-left,(int)(x/scale)),bottom+Math.Min(top-bottom,(int)(y/scale))));
            texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,width,height),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
        }
    }
    [HarmonyPatch(typeof(SpriteDict), nameof(SpriteDict.GetSprite))]
    internal static class MasterThiefPortraitPatch
    {
        static bool Prefix(SpriteDict __instance, string key, ref Sprite __result)
        {
            if (key != GoldenAssets.PortraitKey) return true;
            __result=GoldenAssets.Portrait(__instance);
            return __result==null;
        }
    }
    [HarmonyPatch(typeof(LockHelper), nameof(LockHelper.UnlockContainer))]
    internal static class LuxuryUnlockArtworkPatch
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(GameItem item, bool force)
        {
            if(item.identifier!=GoldenMarket.CrateId) return;
            // Vanilla switches SUPPLY_CRATE to a '_u' sprite that the parcel lacks.
            item.RemoveGameItemType("SUPPLY_CRATE");
            if(!force && !SaveManager.IsDecoding && item.GetTagReadonly(LockHelper.LOCK_STATUS).GetBool()
                && item.contentWindow?.child is GameInventory grid)
            {
                if (grid.childItems.Count == 0) GoldenMarket.FillLuxury(item,PreBuiltItemHelper.GetLuckMult(item));
                LuxuryGuarantee.Ensure(item);
            }
        }
    }
}
