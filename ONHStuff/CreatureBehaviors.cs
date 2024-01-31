using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Random = UnityEngine.Random;
using RWCustom;
using System.Runtime.CompilerServices;

namespace ONHStuff
{
    internal static class CreatureBehaviors
    {
        public static void ApplyHooks()
        {
            Debug.Log("ONHCreatureBehaviors");
            On.Vulture.ctor += Vulture_ctor;
            On.VultureAbstractAI.AddRandomCheckRoom += VultureAbstractAI_AddRandomCheckRoom;

            On.ScavengerAbstractAI.TryAssembleSquad += ScavengerAbstractAI_TryAssembleSquad;

            //I don't think this actually does anything
            //On.DaddyCorruption.AIMapReady += DaddyCorruption_AIMapReady;

            On.StaticWorld.InitStaticWorld += StaticWorld_InitStaticWorld;
        }

        private static void StaticWorld_InitStaticWorld(On.StaticWorld.orig_InitStaticWorld orig)
        {
            orig();
            try {
                //lol
                StaticWorld.EstablishRelationship(CreatureTemplate.Type.CicadaA, CreatureTemplate.Type.MirosBird, new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 1f));
                StaticWorld.EstablishRelationship(CreatureTemplate.Type.CicadaB, CreatureTemplate.Type.MirosBird, new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 1f));
                StaticWorld.EstablishRelationship(CreatureTemplate.Type.LizardTemplate, CreatureTemplate.Type.MirosBird, new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 0.9f));

            }

            catch (Exception e) { Debug.LogError("Error in ONH StaticWorld, non-fatal\n"+e.ToString()); }
        }

        private static void DaddyCorruption_AIMapReady(On.DaddyCorruption.orig_AIMapReady orig, DaddyCorruption self)
        {
            self.bottomLeft = new IntVector2(int.MaxValue, int.MaxValue);
            self.topRight = new IntVector2(int.MinValue, int.MinValue);
            self.tiles = new List<IntVector2>();
            int seed = UnityEngine.Random.seed;
            UnityEngine.Random.seed = (int)self.places[0].pos.x + (int)self.places[0].pos.y;
            Color color = Custom.HSL2RGB(UnityEngine.Random.value, 1f, 0.5f);
            Random.State state = Random.state;
            Random.InitState((int)self.places[0].pos.x + (int)self.places[0].pos.y);
            for (int i = 0; i < self.places.Count; i++)
            {
                for (int j = self.room.GetTilePosition(self.places[i].pos).x - (int)((self.places[i].data as PlacedObject.ResizableObjectData).Rad / 20f) - 1; j <= self.room.GetTilePosition(self.places[i].pos).x + (int)((self.places[i].data as PlacedObject.ResizableObjectData).Rad / 20f) + 1; j++)
                {
                    for (int k = self.room.GetTilePosition(self.places[i].pos).y - (int)((self.places[i].data as PlacedObject.ResizableObjectData).Rad / 20f) - 1; k <= self.room.GetTilePosition(self.places[i].pos).y + (int)((self.places[i].data as PlacedObject.ResizableObjectData).Rad / 20f) + 1; k++)
                    {
                        if (!self.room.GetTile(j, k).Solid && Custom.DistLess(self.room.MiddleOfTile(j, k), self.places[i].pos, (self.places[i].data as PlacedObject.ResizableObjectData).Rad) && !self.tiles.Contains(new IntVector2(j, k)))
                        {
                            bool flag = false;
                            int num = 0;
                            while (num < 8 && !flag)
                            {
                                if (self.room.GetTile(j + Custom.eightDirections[num].x, k + Custom.eightDirections[num].y).Solid)
                                {
                                    flag = true;
                                }
                                num++;
                            }
                            if (flag)
                            {
                                self.tiles.Add(new IntVector2(j, k));
                                if (j < self.bottomLeft.x)
                                {
                                    self.bottomLeft.x = j;
                                }
                                if (k < self.bottomLeft.y)
                                {
                                    self.bottomLeft.y = k;
                                }
                                if (j > self.topRight.x)
                                {
                                    self.topRight.x = j;
                                }
                                if (k > self.topRight.y)
                                {
                                    self.topRight.y = k;
                                }
                            }
                        }
                    }
                }
            }
            self.directions = new Vector2[self.topRight.x - self.bottomLeft.x + 1, self.topRight.y - self.bottomLeft.y + 1];
            self.bulbs = new List<DaddyCorruption.Bulb>[self.topRight.x - self.bottomLeft.x + 1, self.topRight.y - self.bottomLeft.y + 1];
            for (int l = 0; l < self.tiles.Count; l++)
            {
                Vector2 vector = new Vector2(0f, 0f);
                for (int m = 1; m < 3; m++)
                {
                    for (int n = 0; n < 8; n++)
                    {
                        if (self.room.GetTile(self.tiles[l] + Custom.eightDirections[n] * m).Solid)
                        {
                            vector -= Custom.eightDirections[n].ToVector2().normalized / (float)m;
                        }
                        else
                        {
                            vector += Custom.eightDirections[n].ToVector2().normalized / (float)m;
                        }
                    }
                }
                vector.Normalize();
                self.directions[self.tiles[l].x - self.bottomLeft.x, self.tiles[l].y - self.bottomLeft.y] = vector;
            }
            for (int num2 = 0; num2 < self.tiles.Count; num2++)
            {
                Vector2 a = new Vector2(0f, 0f);
                for (int num3 = 1; num3 < 4; num3++)
                {
                    for (int num4 = 0; num4 < 8; num4++)
                    {
                        if (!self.room.GetTile(self.tiles[num2] + Custom.eightDirections[num4] * num3).Solid)
                        {
                            if (self.Occupied(self.tiles[num2] + Custom.eightDirections[num4] * num3))
                            {
                                a -= Custom.eightDirections[num4].ToVector2().normalized / (float)num3;
                            }
                            else
                            {
                                a += Custom.eightDirections[num4].ToVector2().normalized / (float)num3;
                            }
                        }
                    }
                }
                self.directions[self.tiles[num2].x - self.bottomLeft.x, self.tiles[num2].y - self.bottomLeft.y] = (self.directions[self.tiles[num2].x - self.bottomLeft.x, self.tiles[num2].y - self.bottomLeft.y] + a * 0.15f).normalized;
            }
            self.totalSprites = 0;
            for (int num5 = 0; num5 < self.tiles.Count; num5++)
            {
                self.bulbs[self.tiles[num5].x - self.bottomLeft.x, self.tiles[num5].y - self.bottomLeft.y] = new List<DaddyCorruption.Bulb>();
                //for (int num6 = Random.Range(1, 1 + (int)Mathf.Lerp(1f, 3f, self.CorruptionLevel(self.tiles[num5]))); num6 >= 0; num6--)
                for (int num6 = UnityEngine.Random.Range(1, 1 + (int)Mathf.Lerp(1f, 3f, self.CorruptionLevel(self.tiles[num5]))); num6 >= 0; num6--)
                {
                    self.bulbs[self.tiles[num5].x - self.bottomLeft.x, self.tiles[num5].y - self.bottomLeft.y].Add(new DaddyCorruption.Bulb(self, self.totalSprites, num6 == 0, self.tiles[num5]));
                    self.totalSprites += self.bulbs[self.tiles[num5].x - self.bottomLeft.x, self.tiles[num5].y - self.bottomLeft.y][self.bulbs[self.tiles[num5].x - self.bottomLeft.x, self.tiles[num5].y - self.bottomLeft.y].Count - 1].totalSprites;
                }
            }
            for (int num7 = 0; num7 < self.room.roomSettings.placedObjects.Count; num7++)
            {
                if (self.room.roomSettings.placedObjects[num7].active)
                {
                    if (self.room.roomSettings.placedObjects[num7].type == PlacedObject.Type.CorruptionTube)
                    {
                        DaddyCorruption.ClimbableCorruptionTube climbableCorruptionTube = new DaddyCorruption.ClimbableCorruptionTube(self.room, self, self.totalSprites, self.room.roomSettings.placedObjects[num7]);
                        self.room.AddObject(climbableCorruptionTube);
                        self.climbTubes.Add(climbableCorruptionTube);
                        self.totalSprites += climbableCorruptionTube.graphic.sprites;
                    }
                    else if (self.room.roomSettings.placedObjects[num7].type == PlacedObject.Type.StuckDaddy)
                    {
                        Vector2 pos = self.room.roomSettings.placedObjects[num7].pos;
                        AbstractCreature abstractCreature = new AbstractCreature(self.room.world, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.DaddyLongLegs), null, self.room.GetWorldCoordinate(pos), new EntityID(-1, self.room.abstractRoom.index * 1000 + num7));
                        abstractCreature.destroyOnAbstraction = true;
                        abstractCreature.ignoreCycle = true;
                        if (ModManager.MSC)
                        {
                            abstractCreature.saveCreature = false;
                        }
                        self.room.abstractRoom.AddEntity(abstractCreature);
                        abstractCreature.RealizeInRoom();
                        DaddyLongLegs daddyLongLegs = abstractCreature.realizedCreature as DaddyLongLegs;
                        daddyLongLegs.stuckPos = self.room.roomSettings.placedObjects[num7];
                        DaddyCorruption.DaddyRestraint daddyRestraint = new DaddyCorruption.DaddyRestraint(daddyLongLegs, self, self.totalSprites, self.room.roomSettings.placedObjects[num7]);
                        self.room.AddObject(daddyRestraint);
                        self.restrainedDaddies.Add(daddyRestraint);
                        self.totalSprites += daddyRestraint.graphic.sprites;
                    }
                    else if (ModManager.MSC && self.room.roomSettings.placedObjects[num7].type == MoreSlugcats.MoreSlugcatsEnums.PlacedObjectType.RotFlyPaper)
                    {
                        DaddyCorruption.NeuronFilledLeg neuronFilledLeg = new DaddyCorruption.NeuronFilledLeg(self, self.totalSprites, self.room.roomSettings.placedObjects[num7].pos, self.room.roomSettings.placedObjects[num7].pos + (self.room.roomSettings.placedObjects[num7].data as PlacedObject.ResizableObjectData).handlePos, self.room.roomSettings.placedObjects[num7].data as PlacedObject.ResizableObjectData);
                        self.room.AddObject(neuronFilledLeg);
                        self.neuronLegs.Add(neuronFilledLeg);
                        self.totalSprites += neuronFilledLeg.graphic.sprites;
                    }
                }
            }
            Random.state = state;
        }

        private static void ScavengerAbstractAI_TryAssembleSquad(On.ScavengerAbstractAI.orig_TryAssembleSquad orig, ScavengerAbstractAI self)
        {
            orig(self);
            if (self.squad != null || self.world.region.name != "VI"
                || self.parent.creatureTemplate.type != MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerElite) return;

            if (self.parent.nightCreature && self.world.rainCycle.dayNightCounter < 600)
            { return; }

            if (self.world.rainCycle.TimeUntilRain < 800 && !self.parent.nightCreature && !self.parent.ignoreCycle)
            { return; }

            int num = 0;
            foreach (AbstractCreature creature in self.parent.Room.creatures)
            {
                if (creature.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerElite
                    && creature != self.parent && (creature.abstractAI as ScavengerAbstractAI).ReadyToJoinSquad())
                {
                    if (creature.personality.dominance > self.parent.personality.dominance)
                    { return; } //only the most dominant can create a squad
                    num++;
                }
            }

            if (num < 2)
            { return; }
            Debug.Log("scavenger elite trying to organize squad");

            WorldCoordinate worldCoordinate = self.RandomDestinationRoom();
            if (!self.CanRoamThroughRoom(worldCoordinate.room) || !self.worldAI.floodFiller.IsRoomAccessible(worldCoordinate.room))
            { return; }


            self.squad = new ScavengerAbstractAI.ScavengerSquad(self.parent);
            int maxExclusive = 7;

            int num2 = Math.Min(num, Random.Range(2, maxExclusive));
            int num3 = 0;

            while (num3 < self.parent.Room.creatures.Count && num2 > 0)
            {
                if (self.parent.Room.creatures[num3].creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger && self.parent.Room.creatures[num3] != self.parent && (self.parent.Room.creatures[num3].abstractAI as ScavengerAbstractAI).ReadyToJoinSquad())
                {
                    self.squad.AddMember(self.parent.Room.creatures[num3]);
                    num2--;
                }
                num3++;
            }
            if (!self.squad.StayIn)
            {
                self.squad = null;
            }


            self.SetDestination(worldCoordinate);
            self.longTermMigration = worldCoordinate;
            self.dontMigrate = Random.Range(400, 4800);
            if (self.squad != null && self.squad.leader == self.parent)
            {
                Debug.Log("scavenger elite squad is heading out");
                self.squad.CommonMovement(worldCoordinate.room, self.parent, false);
                for (int l = 0; l < self.squad.members.Count; l++)
                {
                    if (self.squad.members[l] != self.parent)
                    {
                        (self.squad.members[l].abstractAI as ScavengerAbstractAI).freeze = l * 10 + Random.Range(0, 10);
                        (self.squad.members[l].abstractAI as ScavengerAbstractAI).dontMigrate = Random.Range(400, 4800);
                    }
                }
            }
        }

        private static void VultureAbstractAI_AddRandomCheckRoom(On.VultureAbstractAI.orig_AddRandomCheckRoom orig, VultureAbstractAI self)
        {
            if (self.parent.realizedCreature is Vulture vul && vul.Perception().Value != 1)
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
                            self.Perception().Value = float.Parse(array2[1]);
                        }
                    }
                }
            }
        }



		public static bool HasRain = true;
        public static ConditionalWeakTable<Vulture, StrongBox<float>> _Perception = new();
        public static StrongBox<float> Perception(this Vulture p) => _Perception.GetValue(p, _ => new(0f));

    //public static Utils.AttachedField<Vulture, float> Perception = new Utils.AttachedField<Vulture, float>();
    }
}
