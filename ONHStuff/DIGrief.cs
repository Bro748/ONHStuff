using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Linq;
using System.Reflection;

namespace ONHStuff
{
    internal static class DIGrief
    {
        public static bool GriefNeeded() => ModManager.ActiveMods.Any(mod => mod.id == "fp.industrialdistrictfix");
        public static void ApplyHooks()
        {
            IL.Room.Loaded += (ILContext il) => DIConditionILHook<Room>(il, "LF");
            IL.Room.Update += (ILContext il) => DIConditionILHook<Room>(il, "DS", "VS");
            IL.RoomCamera.Update += (ILContext il) => DIConditionILHook<RoomCamera>(il, "SB");
            IL.Snail.Click += (ILContext il) => DIConditionILHook<Snail>(il, "DS");
            var painJumpsHook = new Hook(typeof(Player).GetProperty("PainJumps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetGetMethod(), PainJumps_Hook);
        }


        public static bool PainJumps_Hook(Func<Player, bool> orig, Player self)
        {
            return orig(self) || (ModManager.MSC && self.room != null && self.room.game.IsStorySession &&
                !self.room.abstractRoom.gate && !self.room.abstractRoom.shelter && self.room.world.region?.name == "DI");
        }

        private static void DIConditionILHook<T>(ILContext il, params string[] regionNames) where T : class
        {
            var c = new ILCursor(il);
            foreach (string region in regionNames)
            {
                if (MatchSofanthielCheck(c))
                {
                    c.Emit(OpCodes.Ldarg_0);

                    if (typeof(T) == typeof(RoomCamera)) c.Emit<RoomCamera>(OpCodes.Call, "get_room");
                    if (typeof(T).IsSubclassOf(typeof(UpdatableAndDeletable))) c.Emit<UpdatableAndDeletable>(OpCodes.Ldfld, "room");
                    c.EmitDelegate(DICondition);
                }
                else
                { UnityEngine.Debug.Log($"failed to ilhook MatchSofanthiel: {region}"); }

                if (MatchRegionNameCheck(c, region))
                {
                    c.Emit(OpCodes.Ldarg_0);

                    if (typeof(T) == typeof(RoomCamera)) c.Emit<RoomCamera>(OpCodes.Call, "get_room");
                    if (typeof(T).IsSubclassOf(typeof(UpdatableAndDeletable))) c.Emit<UpdatableAndDeletable>(OpCodes.Ldfld, "room");
                    c.EmitDelegate(DICondition);
                }
                else
                { UnityEngine.Debug.Log($"failed to ilhook MatchRegionCheck: {region}"); }
            }
        }

        private static bool MatchSofanthielCheck(ILCursor c)
        {
            return c.TryGotoNext(MoveType.After,
                            x => x.MatchLdsfld<MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName>(nameof(MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel)),
                            x => x.MatchCall(typeof(ExtEnum<SlugcatStats.Name>).GetMethod("op_Equality"))
                            );
        }

        private static bool MatchRegionNameCheck(ILCursor c, string region)
        {
            return c.TryGotoNext(MoveType.After,
                        x => x.MatchLdfld<Room>(nameof(Room.world)),
                        x => x.MatchLdfld<World>(nameof(World.region)),
                        x => x.MatchLdfld<Region>(nameof(Region.name)),
                        x => x.MatchLdstr(region),
                        x => x.MatchCall<string>("op_Equality")
                        );
        }

        public static bool DICondition(bool orig, Room self) => orig || self.world.region?.name == "DI";
    }
}
