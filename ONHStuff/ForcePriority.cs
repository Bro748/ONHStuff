using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace ONHStuff
{
    internal static class ForcePriority
    {
        public static void ModManager_RefreshModsLists(On.ModManager.orig_RefreshModsLists orig, RainWorld rainWorld)
        {
            try {
                orig(rainWorld);
                Debug.Log("checking priority");
                // Prioritize this mod's assets so they override MSC / Remix

                // Get the index above MoreSlugcats, then Remix in that priority - inserting our mod at this index ensures the game loads assets from our mod first
                int? targetIndex = null;

                for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
                {
                    if (ModManager.ActiveMods[i].id == ONHStuff.MOD_ID) return;

                    if (ModManager.ActiveMods[i].id != MoreSlugcats.MoreSlugcats.MOD_ID && ModManager.ActiveMods[i].id != MoreSlugcats.MMF.MOD_ID) continue;

                    targetIndex = i;
                    break;
                }

                // If neither Remix nor MoreSlugcats is installed, we don't need to do anything
                if (targetIndex == null) return;

                // Get our mod
                ModManager.Mod thisMod = null;

                foreach (ModManager.Mod mod in ModManager.ActiveMods)
                {
                    if (mod.id != ONHStuff.MOD_ID) continue;

                    thisMod = mod;
                    ModManager.ActiveMods.Remove(mod);
                    break;
                }

                // Just in case getting our mod fails - this should never happen as our mod has to be active for this to run!
                if (thisMod == null) return;

                Debug.Log($"Successfully overrode load order! Placed mod at {targetIndex}, above MSC/Remix");

                ModManager.ActiveMods.Insert((int)targetIndex, thisMod);
            }
            catch (Exception e) { Debug.Log("force priority failed! aborting...\n" + e); }
        }

        public static void CheckIfFileForceOverrideIsNecessary()
        {
            try
            {
                string path = WorldLoader.FindRoomFile("SB_F03", false, ".txt");
                if (!File.Exists(path)) return;

                string name = File.ReadAllLines(path)[0];
                if (name != "SB_F033")
                { ForceHardOverride(); }
            }
            catch (Exception e) { Debug.Log("force override failed! aborting...\n" + e); }
        }

        public static void ForceHardOverride()
        {
            string ONHPath = "";
            string mergedModsPath = (Path.Combine(RWCustom.Custom.RootFolderDirectory(), "mergedmods"));
            for (int i = 0; i < ModManager.ActiveMods.Count; i++)
            {
                if (ModManager.ActiveMods[i].id == ONHStuff.MOD_ID)
                {
                    ONHPath = ModManager.ActiveMods[i].path;
                    break;
                }
            }
            if (ONHPath == "") return;

            List<string> filesToCopy = new()
            {
            $"world{Path.DirectorySeparatorChar}SB-Rooms{Path.DirectorySeparatorChar}SB_F03.txt",
            $"world{Path.DirectorySeparatorChar}SB-Rooms{Path.DirectorySeparatorChar}SB_A11.txt",
            $"world{Path.DirectorySeparatorChar}SH-Rooms{Path.DirectorySeparatorChar}SH_E03.txt",
            $"world{Path.DirectorySeparatorChar}SH-Rooms{Path.DirectorySeparatorChar}SH_E03RIV.txt",
            };

            foreach (string str in filesToCopy)
            {
                string onh = Path.Combine(ONHPath, str);
                string mergedmods = Path.Combine(mergedModsPath, str);
                if (!File.Exists(onh)) continue;
                if (File.Exists(mergedmods)) File.Delete(mergedmods);
                File.Copy(onh, mergedmods);
            }
        }

        public static void CheckIfMapsCopyShouldBeDoneAndDoIt()
        {
            string ONHPath = "";
            string mergedModsPath = (Path.Combine(RWCustom.Custom.RootFolderDirectory(), "mergedmods"));
            for (int i = ModManager.ActiveMods.Count - 1; i >= 0; i--)
            {
                Debug.Log("id: "+ ModManager.ActiveMods[i].id);
                if (ModManager.ActiveMods[i].id == ONHStuff.MOD_ID)
                {
                    ONHPath = ModManager.ActiveMods[i].path;
                    break;
                }
            }
            Debug.Log($"ONHPath [{ONHPath}]");
            if (ONHPath == "") return;

            List<string> filesToCopy = new List<string>()
            {
            $"world{Path.DirectorySeparatorChar}SB{Path.DirectorySeparatorChar}map_sb-rivulet.txt",
            $"world{Path.DirectorySeparatorChar}SB{Path.DirectorySeparatorChar}map_sb-saint.txt",
            $"world{Path.DirectorySeparatorChar}SH{Path.DirectorySeparatorChar}map_sh-rivulet.txt",
            $"world{Path.DirectorySeparatorChar}SH{Path.DirectorySeparatorChar}map_sh-gourmand.txt",
            };

            foreach (string str in filesToCopy)
            {
                string onh = Path.Combine(ONHPath, str);
                string mergedmods = Path.Combine(mergedModsPath, str);
                if (!File.Exists(onh)) continue;
                if (File.Exists(mergedmods)) File.Delete(mergedmods);
                File.Copy(onh, mergedmods);
            }
        }
    }
}
