using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RWCustom;
using UnityEngine;

namespace ONHStuff
{
    internal static class FunnySlug
    {
        public static void OnEnable()
        {
            On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
            On.HUD.KarmaMeter.ctor += KarmaMeter_ctor;
        }

        private static void KarmaMeter_ctor(On.HUD.KarmaMeter.orig_ctor orig, HUD.KarmaMeter self, HUD.HUD hud, FContainer fContainer, RWCustom.IntVector2 displayKarma, bool showAsReinforced)
        {
            orig(self, hud, fContainer, displayKarma, showAsReinforced);

            if (!(hud.owner is Player))
            { return; }

            int count = 0;
            foreach (HUD.HudPart part in hud.parts)
            {
                if (part is HUD.KarmaMeter)
                { count++; }
            }
            Debug.Log("karma count is " + count);
            if (count != 0)
            { return; }


            fContainer.RemoveAllChildren();

            displayKarma.x = Custom.IntClamp(displayKarma.x, 0, displayKarma.y);
            self.pos = new Vector2(95.01f, 70.01f);
            self.lastPos = self.pos;
            self.rad = 10f;
            self.lastRad = self.rad;
            self.darkFade = new FSprite("Futile_White", true);
            self.darkFade.shader = hud.rainWorld.Shaders["FlatLight"];
            self.darkFade.color = new Color(0f, 0f, 0f);
            fContainer.AddChild(self.darkFade);
            self.karmaSprite = new FSprite(HUD.KarmaMeter.KarmaSymbolSprite(true, displayKarma), true);
            self.karmaSprite.color = new Color(1f, 1f, 1f);
            fContainer.AddChild(self.karmaSprite);
            self.glowSprite = new FSprite("Futile_White", true);
            self.glowSprite.shader = hud.rainWorld.Shaders["FlatLight"];
            fContainer.AddChild(self.glowSprite);
        }

        private static void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
        {

            self.AddPart(new HUD.KarmaMeter(self, self.fContainers[1], new IntVector2((self.owner as Player).Karma - 1, (self.owner as Player).KarmaCap), (self.owner as Player).KarmaIsReinforced));
            orig(self, cam);
        }
    }
}
