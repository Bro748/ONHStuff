using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ONHStuff
{
    internal static class RevSupport
    {


        public static void Apply()
        {
            On.Player.Update += Player_Update;
            On.Player.NewRoom += Player_NewRoom;
            On.BackgroundScene.Update += BackgroundScene_Update;
            On.Room.Loaded += Room_Loaded;
            On.BackgroundScene.Simple2DBackgroundIllustration.DrawSprites += Simple2DBackgroundIllustration_DrawSprites;
        }

        private static void Simple2DBackgroundIllustration_DrawSprites(On.BackgroundScene.Simple2DBackgroundIllustration.orig_DrawSprites orig, BackgroundScene.Simple2DBackgroundIllustration self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            if (self.Rotation().Value != float.MaxValue)
            {
                sLeaser.sprites[0].rotation = self.Rotation().Value;
            }
        }

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            orig(self);
            try
            {
                if (self.world.singleRoomWorld || self.world.region.name != "VU") return;
                //if (self.abstractRoom.name != "SI_A07" && !VU) return;
                SetupSplitSky(self, 270f);
            }
            catch (Exception e) { Debug.LogError(e); }
        }

        private static void SetupSplitSky(Room self, float rotation)
        {
            for (int num3 = 0; num3 < self.roomSettings.effects.Count; num3++)
            {
                if (self.roomSettings.effects[num3].type == RoomSettings.RoomEffect.Type.AboveCloudsView)
                {
                    Vector2 centerOfScreen = new(1366f / 2f, 768f / 2f);
                    foreach (UpdatableAndDeletable uad in self.updateList)
                    {
                        if (uad is AboveCloudsView ac)
                        {
                            ac.RotateClouds(rotation);
                            //ac.Rotation().Value = VU ? 270f : 180f;
                            //ac.Position().Value = new(1366f, 768f);
                            //ac.Position().Value = VU ? new(1366f + 150f, -299f) : new(1366f, 768f + 300f);

                            ac.elements.Remove(ac.daySky);
                            ac.elements.Remove(ac.duskSky);
                            ac.elements.Remove(ac.nightSky);
                            break;
                        }
                    }
                    AboveCloudsView acv = new(self, self.roomSettings.effects[num3]);
                    self.AddObject(acv);
                    acv.RotateClouds(rotation - 180f);
                    //acv.Rotation().Value = VU ? 90f : 0f;
                    //acv.Position().Value = VU ? new(-150f, 1067f) : new(0f, -300f);
                    /*if (acv != null)
                    {
                        Debug.Log("double");
                        self.AddObject(acv);
                        acv.Rotation().Value = 0f;
                        acv.Position().Value = new(0f, 0f);
                    }
                    else { Debug.Log("not double"); }*/
                    break;
                }
            }
        }

        private static void BackgroundScene_Update(On.BackgroundScene.orig_Update orig, BackgroundScene self, bool eu)
        {
            bool rotated = self.Rotation().Value != float.MaxValue;

            bool added = !self.elementsAddedToRoom && rotated;
            orig(self, eu);
            if (added)
            {
                self.Container().rotation = self.Rotation().Value;
                self.Container().position = self.Position().Value;
                self.room.AddObject(self.Container());
                foreach (BackgroundScene.BackgroundSceneElement element in self.elements.OrderByDescending(x => x.depth))
                {
                    if (self is AboveCloudsView acv && (element == acv.daySky || element == acv.duskSky || element == acv.nightSky)) continue;
                    if (self is RoofTopView rtv && (element == rtv.daySky || element == rtv.duskSky || element == rtv.nightSky)) continue;
                    self.Container().drawables.Add(element);
                }
            }
            if (rotated && self.room.abstractRoom.name == "VU_TOP")
            {
                self.RotateClouds(self.Rotation().Value + 0.02f);
                self.Container().rotation = self.Rotation().Value;
                self.Container().position = self.Position().Value;
                if (self is AboveCloudsView acv)
                {
                    acv.daySky.Rotation().Value = self.Container().rotation - 90f;
                }
            }
        }
        // if (self is AboveCloudsView acv && (element == acv.daySky || element == acv.duskSky || element == acv.nightSky)) continue;
        // if (self is RoofTopView rtv && (element == rtv.daySky || element == rtv.duskSky || element == rtv.nightSky)) continue;

        public static void RotateClouds(this BackgroundScene p, float rotation)
        {
            Vector2 centerOfScreen = new(1366f / 2f, 768f / 2f);
            p.Rotation().Value = rotation;
            p.Position().Value = centerOfScreen + (RWCustom.Custom.DegToVec(rotation - 180f) * 833f) - (RWCustom.Custom.DegToVec(rotation + 90f) * 683f);
        }

        private static ConditionalWeakTable<BackgroundScene, StrongBox<float>> _Rotation = new();

        public static StrongBox<float> Rotation(this BackgroundScene p) => _Rotation.GetValue(p, _ => new(float.MaxValue));


        private static ConditionalWeakTable<BackgroundScene.Simple2DBackgroundIllustration, StrongBox<float>> _Rotation2 = new();

        public static StrongBox<float> Rotation(this BackgroundScene.Simple2DBackgroundIllustration p) => _Rotation2.GetValue(p, _ => new(float.MaxValue));


        private static ConditionalWeakTable<BackgroundScene, StrongBox<Vector2>> _Position = new();

        public static StrongBox<Vector2> Position(this BackgroundScene p) => _Position.GetValue(p, _ => new(new Vector2()));


        private static ConditionalWeakTable<BackgroundScene, FlipContainer> _Container = new();

        public static FlipContainer Container(this BackgroundScene p) => _Container.GetValue(p, _ => new());

        private static void Player_NewRoom(On.Player.orig_NewRoom orig, Player self, Room newRoom)
        {
            orig(self, newRoom);
            //SetRoomFlip(newRoom, self.Reverse().reverseGravity);
        }

        private static void SetRoomFlip(Room room, bool flip)
        {
            bool exists = false;
            foreach (UpdatableAndDeletable uad in room.updateList)
            {
                if (uad is FlipScreenEffect)
                {
                    exists = true;
                    if (!flip) room.RemoveObject(uad);
                    break;
                }
            }
            if (!exists && flip)
            { room.AddObject(new FlipScreenEffect()); }
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (Input.GetKey(KeyCode.Backspace) && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt) && !button)
            {
                button = true;

                self.Reverse().reverseGravity = !self.Reverse().reverseGravity;
                if (self.room != null)
                {
                    //SetRoomFlip(self.room, self.Reverse().reverseGravity);
                }
            }
            else if (!Input.GetKey(KeyCode.Backspace))
            {
                button = false;
            }

            if (self.slugcatStats.name == InvJunk.Inv)
            {
                if (self.room?.world.name == "CF" && !self.Reverse().reverseGravity)
                {
                    self.Reverse().reverseGravity = true;
                }
                else if (self.Reverse().reverseGravity && self.room?.world.name != "CF")
                {
                    self.Reverse().reverseGravity = false;
                }
            }
            orig(self, eu);
        }

        static bool button = false;


        public static AssetBundle CEAssetBundle;
        public static void LoadBundle(RainWorld self)
        {
            try
            {
                string filePath = AssetManager.ResolveFilePath("AssetBundles/gamer025.rainworldce.assets");
                if (!File.Exists(filePath)) return;
                CEAssetBundle = AssetBundle.LoadFromFile(filePath);
                if (CEAssetBundle == null)
                {
                    ONHStuff.plugin._Logger.LogInfo("Failed to load AssetBundle from " + filePath);
                    //UnityEngine.Object.Destroy(this);
                }
                ONHStuff.plugin._Logger.LogInfo("Assetbundle content: " + string.Join(", ", CEAssetBundle.GetAllAssetNames()));
                self.Shaders.Add("FlipScreenPP", FShader.CreateShader("FlipScreenPP", CEAssetBundle.LoadAsset<Shader>("flipscreen.shader")));
            }
            catch (Exception arg2)
            {
                ONHStuff.plugin._Logger.LogInfo(string.Format("Error loading asset bundle:\n {0}", arg2));
            }
        }
    }


    public class FlipScreenEffect : UpdatableAndDeletable, IDrawable
    {
        // Token: 0x0600005C RID: 92 RVA: 0x00004694 File Offset: 0x00002894
        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            rCam.ReturnFContainer("Bloom").AddChild(sLeaser.sprites[0]);
        }

        // Token: 0x0600005D RID: 93 RVA: 0x000046AE File Offset: 0x000028AE
        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
        }

        // Token: 0x0600005E RID: 94 RVA: 0x000046B0 File Offset: 0x000028B0
        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (!this.done)
            {
                Shader.SetGlobalFloat("Gamer025_YFlip", 1f);
                this.done = true;
            }
        }

        // Token: 0x0600005F RID: 95 RVA: 0x00004718 File Offset: 0x00002918
        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];
            sLeaser.sprites[0] = new FSprite("Futile_White", true)
            {
                shader = rCam.game.rainWorld.Shaders["FlipScreenPP"],
                scaleX = rCam.game.rainWorld.options.ScreenSize.x / 16f,
                scaleY = 48f,
                anchorX = 0f,
                anchorY = 0f
            };
            this.AddToContainer(sLeaser, rCam, null);
        }

        // Token: 0x0400002F RID: 47
        private float yFlip;

        // Token: 0x04000030 RID: 48
        private bool done;
    }


    public class FlipContainer : UpdatableAndDeletable, IDrawable
    {
        // Token: 0x0600005C RID: 92 RVA: 0x00004694 File Offset: 0x00002894
        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            newContatiner ??= rCam.ReturnFContainer("Water");
            newContatiner.AddChild(sLeaser.containers[0]);
        }

        // Token: 0x0600005D RID: 93 RVA: 0x000046AE File Offset: 0x000028AE
        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
        }

        // Token: 0x0600005E RID: 94 RVA: 0x000046B0 File Offset: 0x000028B0
        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            
            sLeaser.containers[0].rotation = rotation;
            sLeaser.containers[0].SetPosition(position);
        }

        // Token: 0x0600005F RID: 95 RVA: 0x00004718 File Offset: 0x00002918
        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[0];
            sLeaser.containers = new FContainer[1];
            sLeaser.containers[0] = new FContainer();
            AddToContainer(sLeaser, rCam, null);

            foreach(IDrawable drawable in drawables)
            {
                foreach (RoomCamera.SpriteLeaser sleaser in rCam.spriteLeasers)
                {
                    if (sleaser.drawableObject == drawable)
                    {
                        sleaser.AddSpritesToContainer(sLeaser.containers[0], rCam);
                        break;
                    }
                }
            }
        }

        public List<IDrawable> drawables = new();

        public float rotation = 0f;

        public Vector2 position = new();

        // Token: 0x0400002F RID: 47
        private float yFlip;

        // Token: 0x04000030 RID: 48
        private bool done;
    }
}
