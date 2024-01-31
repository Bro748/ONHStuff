using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ONHStuff
{
    internal static class InvJunk
    {
        public static SlugcatStats.Name Inv => MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel;

        public static void ApplyHooks()
        {
            On.SaveState.GetStoryDenPosition += SaveState_GetStoryDenPosition;
            On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
            On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;
            On.Menu.SlugcatSelectMenu.SetChecked += SlugcatSelectMenu_SetChecked;
            On.Menu.SlugcatSelectMenu.GetChecked += SlugcatSelectMenu_GetChecked;
            On.Menu.SlugcatSelectMenu.UpdateSelectedSlugcatInMiscProg += SlugcatSelectMenu_UpdateSelectedSlugcatInMiscProg;

            On.RoomSpecificScript.AddRoomSpecificScript += RoomSpecificScript_AddRoomSpecificScript;
        }

        private static void RoomSpecificScript_AddRoomSpecificScript(On.RoomSpecificScript.orig_AddRoomSpecificScript orig, Room room)
        {
            orig(room);

            if (room.abstractRoom.name == "FN_InvC04")
            {
                Debug.Log("Adding Inv start script");
                room.AddObject(new RoomSpecificScript.SU_C04StartUp(room));
            }
        }

        private static bool SlugcatSelectMenu_GetChecked(On.Menu.SlugcatSelectMenu.orig_GetChecked orig, Menu.SlugcatSelectMenu self, Menu.CheckBox box)
        {
            if (_onhmode.TryGetValue(self, out var box2) && box2 == box)
            { return ONHMode; }
            return orig(self, box);
        }

        private static void SlugcatSelectMenu_SetChecked(On.Menu.SlugcatSelectMenu.orig_SetChecked orig, Menu.SlugcatSelectMenu self, Menu.CheckBox box, bool c)
        {
            if (_onhmode.TryGetValue(self, out var box2) && box2 == box)
            { ONHMode = c; return; }
            orig(self, box, c);
        }

        private static void SlugcatSelectMenu_UpdateSelectedSlugcatInMiscProg(On.Menu.SlugcatSelectMenu.orig_UpdateSelectedSlugcatInMiscProg orig, Menu.SlugcatSelectMenu self)
        {
            orig(self);
            if (_onhmode.TryGetValue(self, out var box))
            {
                if (self.slugcatPages[self.slugcatPageIndex].slugcatNumber != Inv)
                    RemoveONHButton(self);
            }
            else if (self.slugcatPages[self.slugcatPageIndex].slugcatNumber == Inv)
            {
                AddONHButton(self);
            }
        }

        public static void AddONHButton(Menu.SlugcatSelectMenu self)
        {
            var onhbutton = new Menu.CheckBox(self, self.pages[0], self, new Vector2(self.startButton.pos.x + 200f + Menu.SlugcatSelectMenu.GetRestartTextOffset(self.CurrLang), ModManager.MMF ? 90 : 60), Menu.SlugcatSelectMenu.GetRestartTextWidth(self.CurrLang), self.Translate("ONH Start"), "ONHMODE", false);
            Menu.MenuLabel label = self.restartCheckbox.label;
            label.pos.x += Menu.SlugcatSelectMenu.GetRestartTextWidth(self.CurrLang) - self.restartCheckbox.label.label.textRect.width - 5f;
            self.pages[0].subObjects.Add(onhbutton);

            self.SetChecked(onhbutton, true);

            _onhmode.GetValue(self, _ => onhbutton);
        }

        public static void RemoveONHButton(Menu.SlugcatSelectMenu self)
        {
            if (_onhmode.TryGetValue(self, out var box))
            { self.pages[0].RemoveSubObject(box); }
        }

        public static bool ONHMode = false;

        private static void SlugcatSelectMenu_StartGame(On.Menu.SlugcatSelectMenu.orig_StartGame orig, Menu.SlugcatSelectMenu self, SlugcatStats.Name storyGameCharacter)
        {
            if (_onhmode.TryGetValue(self, out var box) && box.Checked && storyGameCharacter == Inv)
            {
                ONHMode = true;
            }
            orig(self, storyGameCharacter);
        }

        private static ConditionalWeakTable<Menu.SlugcatSelectMenu, Menu.CheckBox> _onhmode = new();


        private static void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, Menu.SlugcatSelectMenu self, ProcessManager manager)
        {
            orig(self, manager);
            if (!self.slugcatColorOrder.Contains(Inv)) return;
            AddONHButton(self);
        }

        private static string SaveState_GetStoryDenPosition(On.SaveState.orig_GetStoryDenPosition orig, SlugcatStats.Name slugcat, out bool isVanilla)
        {
            if (slugcat == Inv && ONHMode)
            {
                isVanilla = false;
                return "FN_InvC04";
            }

            return orig(slugcat, out isVanilla);
        }
    }
}
