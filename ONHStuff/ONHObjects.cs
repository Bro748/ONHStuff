using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RWCustom;
using static Pom.Pom;

namespace ONHStuff
{
    internal class ONHObjects
    {

        public static void Apply()
        {
            RegisterManagedObject<FireFieldObj, FireFieldData, ManagedRepresentation>("FireField", "ONHStuff");
            RegisterManagedObject<IncineratorSpawnerObj, IncineratorSpawnerData, ManagedRepresentation>("ConstantSpearSpawner", "ONHStuff");
            RegisterFullyManagedObjectType(new ManagedField[] { }, typeof(ElecGateBatteryPosObj), "ElecGateBatteryPos", "ONHStuff");
            On.RegionGate.Update += RegionGate_Update;
        }

        private static void RegionGate_Update(On.RegionGate.orig_Update orig, RegionGate self, bool eu)
        {
            orig(self, eu);
            foreach (PlacedObject pObj in self.room.roomSettings.placedObjects)
            {
                if (pObj.type.ToString() == "ElecGateBatteryPos" && self is ElectricGate eGate)
                { eGate.meterHeight = pObj.pos.y; }
            }
        }

        public static bool check = false;

        internal class ElecGateBatteryPosObj : UpdatableAndDeletable
        {
            public ElecGateBatteryPosObj(PlacedObject pObj, Room room)
            {
            }
        }

        internal class IncineratorSpawnerObj : UpdatableAndDeletable
        {
            public PlacedObject pObj;
            private IncineratorSpawnerData Data => (pObj?.data as IncineratorSpawnerData);
            public IncineratorSpawnerObj(PlacedObject pObj, Room room)
            {
                this.pObj = pObj;
                scraps = new List<PhysicalObject>();
                PickNewTimeTillNextSpawn();
            }

            public void PickNewTimeTillNextSpawn() => timeTillNextSpawn = UnityEngine.Random.Range(Math.Min(Data.MinSpawn, Data.MaxSpawn), Math.Max(Data.MinSpawn, Data.MaxSpawn) +1);


            public override void Update(bool eu)
            {
                if (countDown < timeTillNextSpawn && scraps.Count < Data.MaxQuant)
                { countDown++; }

                if (countDown >= timeTillNextSpawn && scraps.Count < Data.MaxQuant)
                {
                    AbstractPhysicalObject APO;

                    if (UnityEngine.Random.value * 100 > Data.MiscObj)
                    {
                        APO = new AbstractSpear(room.world, null, room.GetWorldCoordinate(Data.PointAlongLine(UnityEngine.Random.value) + new Vector2(0, 100)), room.game.GetNewID(), false);
                    }
                    else
                    {
                        APO = new AbstractPhysicalObject(room.world, AbstractPhysicalObject.AbstractObjectType.Rock, null, room.GetWorldCoordinate(Data.PointAlongLine(UnityEngine.Random.value) + new Vector2(0, 100)), room.game.GetNewID());
                    }

                    room.abstractRoom.entities.Add(APO);
                    APO.RealizeInRoom();
                    if (APO.realizedObject != null)
                    { 
                        scraps.Add(APO.realizedObject);
                        APO.realizedObject.firstChunk.vel.x = Mathf.Lerp(-3f,3f,UnityEngine.Random.value);
                        APO.realizedObject.firstChunk.vel.y = Mathf.Lerp(-3f, -5f, UnityEngine.Random.value);

                        if (APO.realizedObject is Spear spear)
                        {
                            spear.SetRandomSpin();
                            spear.rotationSpeed *= Mathf.Lerp(0.05f, 0.5f, UnityEngine.Random.value);
                        }
                    }

                    countDown = 0;
                    PickNewTimeTillNextSpawn();
                }

                foreach (PhysicalObject obj in scraps.ToList())
                {
                    if (obj.room != room)
                    { 
                        scraps.Remove(obj);
                        continue;
                    }


                    if (obj.bodyChunks[0].pos.y < -obj.bodyChunks[0].restrictInRoomRange + 1f)
                    {
                        scraps.Remove(obj);
                        obj.Destroy(); 
                    }
                }

            }

            private int countDown;

            private int timeTillNextSpawn;

            private List<PhysicalObject> scraps;

        }

        internal class IncineratorSpawnerData : ManagedData
        {
            //this is a mess, lol
            internal PlacedObject pObj;

            internal Vector2 pos2 => GetValue<Vector2>("Line");

            internal Vector2 PointAlongLine(float distance) => pObj.pos + (pos2 * distance);

            internal int MinSpawn => GetValue<int>("MinSpawn");

            internal int MaxSpawn => GetValue<int>("MaxSpawn");

            internal int MaxQuant => GetValue<int>("MaxQuant");
            internal int MiscObj => GetValue<int>("MiscObj");

            public IncineratorSpawnerData(PlacedObject po) : base(po, new ManagedField[] {
                new Vector2Field("Line", new Vector2(20, 0)),
                new IntegerField("MinSpawn", 1, 400, 1, ManagedFieldWithPanel.ControlType.slider),
                new IntegerField("MaxSpawn", 1, 400, 1, ManagedFieldWithPanel.ControlType.slider),
                new IntegerField("MaxQuant", 0, 100, 0, ManagedFieldWithPanel.ControlType.slider),
                new IntegerField("MiscObj", 0, 100, 0, ManagedFieldWithPanel.ControlType.slider)
            })
            {
                pObj = po;
            }
        }

        internal class FireFieldObj : UpdatableAndDeletable
        {
            public PlacedObject pObj;
            private FireFieldData Data => (pObj?.data as FireFieldData);
            public FireFieldObj(PlacedObject pObj, Room room)
            {
                this.pObj = pObj;
                lightSources = new LightSource[0];
            }

            public void RefreshLights()
            {
                if(lightSources.Length != 0)
                {
                    foreach (LightSource light in lightSources)
                    { room.RemoveObject(light); }
                }

                lightSources = new LightSource[Data.LightAmount];
                getToPositions = new Vector2[lightSources.Length];
                getToRads = new float[lightSources.Length];

                for (int i = 0; i < lightSources.Length; i++)
                {
                    lightSources[i] = new LightSource(Data.PointAlongLine(i / (float)(lightSources.Length - 1)), false, Custom.HSL2RGB(Mathf.Lerp(0.01f, 0.07f, i / (float)(lightSources.Length - 1)), 1f, 0.5f), this);
                    room.AddObject(lightSources[i]);
                    lightSources[i].setAlpha = new float?(1f);
                }
            }
            
            public override void Update(bool eu)
            {
                if (Data.LightAmount != lightSources.Length)
                { RefreshLights(); }

                for (int i = 0; i < lightSources.Length; i++)
                {
                    if (UnityEngine.Random.value < 0.2f)
                    {
                        getToPositions[i] = Custom.RNV() * 50f * UnityEngine.Random.value;
                    }
                    if (UnityEngine.Random.value < 0.2f)
                    {
                        getToRads[i] = Mathf.Lerp(50f, Mathf.Lerp(400f, 200f, i / (float)(lightSources.Length - 1)), Mathf.Pow(UnityEngine.Random.value, 0.5f));
                    }

                    lightSources[i].setPos = new Vector2?(Vector2.Lerp(lightSources[i].Pos, Data.PointAlongLine(i / (float)(lightSources.Length - 1)) + getToPositions[i], 0.2f));
                    lightSources[i].setRad = new float?(Mathf.Lerp(lightSources[i].Rad, getToRads[i], 0.2f));
                }

                for (int i = 0; i < Data.Amount; i++)
                {
                    room.AddObject(new HolyFire.HolyFireSprite(Data.PointAlongLine(UnityEngine.Random.value)));
                }

            }
            private LightSource[] lightSources;
            private Vector2[] getToPositions;
            private float[] getToRads;

        }

        internal class FireFieldData : ManagedData
        {
            //this is a mess, lol
            internal PlacedObject pObj;

            internal Vector2 pos2 => GetValue<Vector2>("Line");

            internal int Amount => GetValue<int>("Amount");

            internal int LightAmount => GetValue<int>("LightAmount");

            internal float length => Vector2.Distance(Vector2.zero, pos2);

            internal Vector2 PointAlongLine(float distance) => pObj.pos + (pos2 * distance);


            public FireFieldData(PlacedObject po) : base(po, new ManagedField[] { 
                new Vector2Field("Line", new Vector2(20, 0)),
                new IntegerField("Amount", 1, 100, 1, ManagedFieldWithPanel.ControlType.slider),
                new IntegerField("LightAmount", 0, 50, 0, ManagedFieldWithPanel.ControlType.slider)
            
            })
            {
                pObj = po;
            }
        }
    }
}
