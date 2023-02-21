using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace ONHStuff
{
    internal static class CreatureBehaviors
    {
        public static void ApplyHooks()
        {
            Debug.Log("ONHCreatureBehaviors");
            On.Vulture.ctor += Vulture_ctor;
            On.VultureAbstractAI.AddRandomCheckRoom += VultureAbstractAI_AddRandomCheckRoom;
            On.AbstractCreatureAI.WantToStayInDenUntilEndOfCycle += AbstractCreatureAI_WantToStayInDenUntilEndOfCycle;
            On.AbstractCreature.WantToStayInDenUntilEndOfCycle += AbstractCreature_WantToStayInDenUntilEndOfCycle;
            On.Region.ctor += Region_ctor1;
            Debug.Log("ONHCreatureBehaviors Is Finished");

        }

        private static void Region_ctor1(On.Region.orig_ctor orig, Region self, string name, int firstRoomIndex, int regionNumber, SlugcatStats.Name storyIndex)
        {
            orig(self, name, firstRoomIndex, regionNumber, storyIndex);
            HasRain = name != "FN";
        }

        private static bool AbstractCreature_WantToStayInDenUntilEndOfCycle(On.AbstractCreature.orig_WantToStayInDenUntilEndOfCycle orig, AbstractCreature self)
        {
			if (HasRain)
			{ return orig(self); }

			else
			{ return self.state.dead || (self.state is HealthState && (self.state as HealthState).health < 0.85f); }
		}

		private static bool AbstractCreatureAI_WantToStayInDenUntilEndOfCycle(On.AbstractCreatureAI.orig_WantToStayInDenUntilEndOfCycle orig, AbstractCreatureAI self)
        {
			if (HasRain)
			{ return orig(self); }

			else
			{ return self.parent.state.dead || (self.parent.state is HealthState && (self.parent.state as HealthState).health < 0.85f); }
        }


        private static void VultureAbstractAI_AddRandomCheckRoom(On.VultureAbstractAI.orig_AddRandomCheckRoom orig, VultureAbstractAI self)
        {
            if (!Perception.TryGet(self.parent.realizedCreature as Vulture, out float perception))
            {
                orig(self);
                return;
            }

            foreach (WorldCoordinate skyNode in self.world.skyAccessNodes)
            {
                foreach (AbstractCreature critter in self.world.GetAbstractRoom(skyNode).creatures)
                {
                    if (!(critter.realizedCreature is Player))
                    { continue; }

                    if (self.RoomViableRoamDestination(skyNode.room))
                    {
                        Debug.Log("Vulture found player, targetting directly");
                        self.AddRoomClusterToCheckList(self.world.GetAbstractRoom(skyNode));
                        return;
                    }

                    else
                    {
                        AbstractRoom originalRoom = self.world.GetAbstractRoom(skyNode);

                        foreach (int connection in originalRoom.connections)
                        {
                            if (connection > -1 && self.RoomViableRoamDestination(connection) &&
                              UnityEngine.Random.value < self.world.GetAbstractRoom(connection).AttractionValueForCreature(self.parent.creatureTemplate.type) * 1.5f)
                            {
                                Debug.Log("Vulture found player in unviable room, targetting indirectly");
                                self.AddRoomClusterToCheckList(self.world.GetAbstractRoom(connection));
                                return;
                            }
                        }
                    }


                }

            }

            orig(self);
        }

        private static void Vulture_ctor(On.Vulture.orig_ctor orig, Vulture self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            Perception.Set(self, 0f);
            string spawnData = self.abstractCreature.spawnData;
            if (!string.IsNullOrEmpty(spawnData) && spawnData[0] == '{')
            {
                string[] array = spawnData.Substring(1, spawnData.Length - 2).Split(new char[]
                {
                    ','
                });
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i].Length > 0)
                    {
                        string[] array2 = array[i].Split(new char[] { ':' });
                        string text = array2[0].Trim().ToLowerInvariant();

                        if (text == "perception")
                        {
                            Perception.Set(self,float.Parse(array2[1]));
                        }
                    }
                }
            }
        }



		public static bool HasRain = true;
    public static Utils.AttachedField<Vulture, float> Perception = new Utils.AttachedField<Vulture, float>();
    }
}
