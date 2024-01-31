using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Mono.Cecil.Cil;
using System.Threading;

namespace ONHStuff
{
    public static class Data
    {
        public class ReverseData
        {
            public bool reversePossible = true;
            public bool reverseGravity = false; 
            public int forceStanding = 0;
            public List<PhysicalObject> objects = new();
        }

        private static ConditionalWeakTable<Player, ReverseData> _ReverseData = new();

        public static ReverseData Reverse(this Player p) => _ReverseData.GetValue(p, _ => new());

        private static ConditionalWeakTable<PlayerGraphics, StrongBox<Vector2>> _PreviousDraw = new();

        public static StrongBox<Vector2> PreviousDraw(this PlayerGraphics p) => _PreviousDraw.GetValue(p, _ => new(defFake));

        public static readonly Vector2 defFake = new(float.MaxValue, float.MaxValue);
    }

    /// <summary>
    /// contains horrors beyond comprehension
    /// </summary>
    internal class ReverseCat
    {
        private static bool _reversedProcessing = false;

        private static Thread main;
        public static bool reversedProcessing
        {
            get => _reversedProcessing && Thread.CurrentThread == main;
            set => _reversedProcessing = value;
        }

        // Todo maaaaaybe just maybe make it easier to get into ceiling pipes
        // that and spawn a grapple or two in outskirts

        protected static Hook lookerDetour;
        protected static Hook chunkDetour;

        public static void Enable()
        {
            try
            {
                // Basic swithched behavior
                // Switch behavior, start inverted processing
                On.Player.Update += Player_Update;
                // proper jump detection
                //On.Player.Jump += Player_Jump;
                // jump behavior changes
                //On.Player.UpdateAnimation += Player_UpdateAnimation1;
                // no op
                //On.Player.WallJump += Player_WallJump;
                // used to reverse, now just catch exceptions to avoid invalid state

                
                On.PlayerGraphics.Update += PlayerGraphics_Update;
                // end of inverted processing
                On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdated;
                
                // Inverted drawing
                // reset previousDraw coordinates
                On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
                // draw things in the mirrored room!!!
                On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
                
                // Edge cases
                // reset called from outside of update, apply reversed coordinates
                On.PlayerGraphics.Reset += PlayerGraphics_Reset;
                // deverse on room leave mid-update, fix wrong tile data during room activation
                On.Creature.SuckedIntoShortCut += Creature_SuckedIntoShortCut;
                // look slugcat over there
                On.ClimbableVinesSystem.VineOverlap += ClimbableVinesSystem_VineOverlap;
                On.ClimbableVinesSystem.OnVinePos += ClimbableVinesSystem_OnVinePos;
                On.ClimbableVinesSystem.VineSwitch += ClimbableVinesSystem_VineSwitch;
                On.ClimbableVinesSystem.ConnectChunkToVine += ClimbableVinesSystem_ConnectChunkToVine;
                
                
                // Items
                // player picks up things considering its real position
                On.Player.PickupCandidate += Player_PickupCandidate;
                // Picked up things move to inverted space
                On.Player.SlugcatGrab += Player_SlugcatGrab;
                // player colides with flies considering its real position
                IL.Player.Update += Player_Update1;
                // fix grapple dir
                On.TubeWorm.Tongue.ProperAutoAim += Tongue_ProperAutoAim;
                
                // Water fixes
                // fix clinging to surface of water while surfaceswim
                IL.Player.UpdateAnimation += Player_UpdateAnimation;
                // Determine deep-swim vs surface swim
                IL.Player.MovementUpdate += Player_MovementUpdate;
                // Bodychunk float when submerged logic
                //IL.BodyChunk.Update += BodyChunk_Update;
                
                // Inverted processing
                On.Room.GetTile_int_int += Flipped_GetTile;
                On.Room.shortcutData_IntVector2 += Flipped_shortcutData;
                //On.Room.FloatWaterLevel += Flipped_FloatWaterLevel;
                On.Room.AddObject += Room_AddObject;
                On.AImap.getAItile_int_int += AImap_getAItile_int_int;
                //On.Rope.GetPosition += Rope_GetPosition;

                On.FNode.ScreenToLocal += FNode_ScreenToLocal;
                
                lookerDetour = new Hook(typeof(PlayerGraphics.PlayerObjectLooker).GetProperty("mostInterestingLookPoint").GetGetMethod(),
                    typeof(ReverseCat).GetMethod("LookPoint_Fix"), null);
                // Chunk 'submerged' inverted (water on top), hook applied during player update, reflection done here.
                //chunkDetour = new Hook(typeof(BodyChunk).GetProperty("submersion").GetGetMethod(), typeof(ReverseCat).GetMethod("Flipped_submersion"), null);
                
            }
            catch (Exception e) { Debug.LogException(e); }
        }
        public static bool debugScreen = false;
        private static Vector2 FNode_ScreenToLocal(On.FNode.orig_ScreenToLocal orig, FNode self, Vector2 screenVector)
        {
            self._container?.UpdateMatrix();
            self._isMatrixDirty = true;
            self.UpdateMatrix();
            float num = -Futile.screen.originX * Futile.screen.pixelWidth;
            float num2 = -Futile.screen.originY * Futile.screen.pixelHeight;
            screenVector = new Vector2((screenVector.x + num) * Futile.displayScaleInverse, (screenVector.y + num2) * Futile.displayScaleInverse);
            if (debugScreen)
            {
                Debug.Log($"screenVector {screenVector}");
                FMatrix matrix = self.screenInverseConcatenatedMatrix;
                Debug.Log($"a: {matrix.a}, b: {matrix.b}, c: {matrix.c}, d: {matrix.d}, tx: {matrix.tx}, ty: {matrix.ty}");
            }
            return self.screenInverseConcatenatedMatrix.GetNewTransformedVector(screenVector);
        }

        private static Vector2 Rope_GetPosition(On.Rope.orig_GetPosition orig, Rope self, int index)
        {
            Vector2 o = orig(self, index);
            if (reversedProcessing) o.y = self.room.PixelHeight - o.y;
            return o;
        }
        #region basics
        public static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            // trying to se this before ctors was giving me all sorts of headache with the tail sprote
            if (self.Reverse().reverseGravity && self.room != null)
            {
                Room room = self.room;

                main = Thread.CurrentThread;
                ReversePlayer(self, room);
                try
                {
                    orig(self, eu);
                }
                catch (Exception e) { Debug.LogException(e); }

                if (room.game.devToolsActive)
                {
                    bool flag4 = room.game.cameras[0].room == room || !ModManager.CoopAvailable;
                    if (Input.GetKey("v") && flag4)
                    {
                        for (int num11 = 0; num11 < 2; num11++)
                        {
                            self.bodyChunks[num11].pos.y = room.PixelHeight - self.bodyChunks[num11].pos.y;
                            self.bodyChunks[num11].lastPos = self.bodyChunks[num11].pos;
                        }
                    }
                }
                    // die if too far oob upwards too
                    // normally rooms with water would ignore this check (water bottom) but we still need to
                    // coordinates still reversed here
                    if (self.room != null && (self.bodyChunks[0].pos.y < -self.bodyChunks[0].restrictInRoomRange + 1f || self.bodyChunks[1].pos.y < -self.bodyChunks[1].restrictInRoomRange + 1f))
                {
                    self.Die();
                    self.Destroy();
                }

                if (self.slatedForDeletetion || self.room != room)
                    DeversePlayer(self, room);
                // else un-needed because graphics will be updated and deverse happens on graphicsupdated

            }
            else
            {
                orig(self, eu);
            }
        }


        // Initialize variables
        protected static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            if (!self.Reverse().reversePossible) return;
            self.Reverse().reverseGravity = false;
            self.Reverse().forceStanding = 0;
        }

        // Basic swithched behavior ============================================

        // flip player's perspective of room.
        protected static void ReversePlayer(Player self, Room room)
        {
            if (!self.Reverse().reverseGravity || reversedProcessing) throw new Exception();
            List<PhysicalObject> objs;
            self.Reverse().objects = objs = new List<PhysicalObject>();
            float pheight = room.PixelHeight;
            room.defaultWaterLevel = room.Height - 1 - room.defaultWaterLevel;
            foreach (var c in self.bodyChunks)
            {
                c.pos = new Vector2(c.pos.x, pheight - c.pos.y);
                c.lastPos = new Vector2(c.lastPos.x, pheight - c.lastPos.y);
                c.lastLastPos = new Vector2(c.lastLastPos.x, pheight - c.lastLastPos.y);
                c.contactPoint.y *= -1;
                c.vel.y *= -1;
                if (c.setPos != null) c.setPos = new Vector2(c.setPos.Value.x, pheight - c.setPos.Value.y);
            }
            foreach (var g in self.grasps)
            {
                if (g != null && g.grabbed != null)
                {
                    foreach (var c in g.grabbed.bodyChunks)
                    {
                        c.pos = new Vector2(c.pos.x, pheight - c.pos.y);
                        c.lastPos = new Vector2(c.lastPos.x, pheight - c.lastPos.y);
                        c.lastLastPos = new Vector2(c.lastLastPos.x, pheight - c.lastLastPos.y);
                        c.contactPoint.y *= -1;
                        c.vel.y *= -1;
                        if (c.setPos != null) c.setPos = new Vector2(c.setPos.Value.x, pheight - c.setPos.Value.y);
                    }
                    objs.Add(g.grabbed);
                }
            }
            if (self.graphicsModule is PlayerGraphics pg)
            {
                foreach (var bp in pg.bodyParts)
                {
                    bp.pos = new Vector2(bp.pos.x, pheight - bp.pos.y);
                    bp.lastPos = new Vector2(bp.lastPos.x, pheight - bp.lastPos.y);
                    bp.vel.y *= -1;
                }
            }


            if (self.enteringShortCut != null) self.enteringShortCut = new RWCustom.IntVector2(self.enteringShortCut.Value.x, room.Height - 1 - self.enteringShortCut.Value.y);
            reversedProcessing = true;
        }

        // ReversePlayer undo
        protected static void DeversePlayer(Player self, Room room)
        {
            if (!self.Reverse().reverseGravity || !reversedProcessing) throw new Exception();
            List<PhysicalObject> objs = self.Reverse().objects;
            float pheight = room.PixelHeight;
            foreach (var c in self.bodyChunks)
            {
                c.pos = new Vector2(c.pos.x, pheight - c.pos.y);
                c.lastPos = new Vector2(c.lastPos.x, pheight - c.lastPos.y);
                c.lastLastPos = new Vector2(c.lastLastPos.x, pheight - c.lastLastPos.y);
                c.contactPoint.y *= -1;
                c.vel.y *= -1;
                if (c.setPos != null) c.setPos = new Vector2(c.setPos.Value.x, pheight - c.setPos.Value.y);
            }
            foreach (var o in objs)
            {
                foreach (var c in o.bodyChunks)
                {
                    c.pos = new Vector2(c.pos.x, pheight - c.pos.y);
                    c.lastPos = new Vector2(c.lastPos.x, pheight - c.lastPos.y);
                    c.lastLastPos = new Vector2(c.lastLastPos.x, pheight - c.lastLastPos.y);
                    c.contactPoint.y *= -1;
                    c.vel.y *= -1;
                    if (c.setPos != null) c.setPos = new Vector2(c.setPos.Value.x, pheight - c.setPos.Value.y);
                }
                // thrown weapon
                if (o is Weapon w && w.mode == Weapon.Mode.Thrown)
                {
                    w.thrownPos.y = pheight - w.thrownPos.y;
                    w.throwDir.y *= -1;
                    if (w.firstFrameTraceFromPos != null) w.firstFrameTraceFromPos = new Vector2(w.firstFrameTraceFromPos.Value.x, pheight - w.firstFrameTraceFromPos.Value.y);
                    if (w.setRotation != null) w.setRotation = new Vector2(w.setRotation.Value.x, -w.setRotation.Value.y);
                }
            }

            if (self.graphicsModule is PlayerGraphics pg)
            {
                foreach (var bp in pg.bodyParts)
                {
                    bp.pos = new Vector2(bp.pos.x, pheight - bp.pos.y);
                    bp.lastPos = new Vector2(bp.lastPos.x, pheight - bp.lastPos.y);
                    bp.vel.y *= -1;
                }
            }

            if (self.enteringShortCut != null) self.enteringShortCut = new RWCustom.IntVector2(self.enteringShortCut.Value.x, room.Height - 1 - self.enteringShortCut.Value.y);
            room.defaultWaterLevel = room.Height - 1 - room.defaultWaterLevel;

            objs.Clear();
            self.Reverse().objects = null;
            reversedProcessing = false;
        }

        // used to reverse, now just catch exceptions to avoid invalid state
        #endregion
        #region graphics
        protected static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            if (!self.player.Reverse().reversePossible)
            {
                orig(self);
                return;
            }
            // switched behavior
            if (self.player.Reverse().reverseGravity && self.owner.room != null)// && !alreadyReversedPlayer[self.player])
            {
                //Room room = self.owner.room;
                // already reversed by player.update
                //ReversePlayer(self.player, room);
                try
                {
                    orig(self);
                }
                catch (Exception e) { Debug.LogException(e); }
                //GraphicsModuleUpdated deverses it
                //DeversePlayer(self.player, room);
            }
            else
            {
                orig(self);
            }
        }

        // end of reversed update cycle
        protected static void Player_GraphicsModuleUpdated(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
        {
            if (!self.Reverse().reversePossible)
            {
                orig(self, actuallyViewed, eu);
                return;
            }
            // switched behavior
            if (reversedProcessing && self.room != null)
            {
                Room room = self.room;
                //ReversePlayer(self, room);
                try
                {
                    orig(self, actuallyViewed, eu);
                }
                catch (Exception e) { Debug.LogException(e); }
                DeversePlayer(self, room);
            }
            else
            {
                orig(self, actuallyViewed, eu);
            }
        }


        // Inverted drawing ====================================================

        // reset previousDraw coordinates
        protected static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            if (self.player.Reverse().reversePossible)
            {
                self.PreviousDraw().Value = Data.defFake;
            }
            orig(self, sLeaser, rCam);
        }

        // not initialized per instance, tryget,
        // draw things in the mirrored room!!!
        protected static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            var reversed = self.player.Reverse().reverseGravity;

            if (!self.owner.slatedForDeletetion && reversed)
            {

                Vector2 center;
                if (!(self.PreviousDraw().Value == Data.defFake))
                {
                    Vector2 prevCam = self.PreviousDraw().Value;
                    //deverse
                    center = new Vector2(prevCam.x / 2f, rCam.room.PixelHeight / 2 - prevCam.y);
                    foreach (var s in sLeaser.sprites)
                    {
                        if (!s.isVisible) continue;
                        var rot = s.rotation;
                        s.rotation = 0f;
                        s.ScaleAroundPointRelative(s.ScreenToLocal(center), 1, -1);
                        s.rotation -= rot;
                    }


                }

                var pheight = rCam.room.PixelHeight;

                if (reversed)
                {
                    foreach (var bp in self.bodyParts)
                    {
                        bp.pos = new Vector2(bp.pos.x, pheight - bp.pos.y);
                        bp.lastPos = new Vector2(bp.lastPos.x, pheight - bp.lastPos.y);
                        bp.vel.y *= -1;
                    }
                }
                orig(self, sLeaser, rCam, timeStacker, camPos);
                if (reversed)
                {
                    foreach (var bp in self.bodyParts)
                    {
                        bp.pos = new Vector2(bp.pos.x, pheight - bp.pos.y);
                        bp.lastPos = new Vector2(bp.lastPos.x, pheight - bp.lastPos.y);
                        bp.vel.y *= -1;
                    }
                }


                if (reversed)
                {
                    center = new Vector2(camPos.x / 2f, rCam.room.PixelHeight / 2 - camPos.y);
                    foreach (var s in sLeaser.sprites)
                    {
                        if (!s.isVisible) continue;
                        var rot = s.rotation;
                        s.rotation = 0f;
                        s.ScaleAroundPointRelative(s.ScreenToLocal(center), 1, -1);
                        s.rotation -= rot;
                    }
                    self.PreviousDraw().Value = camPos;
                }
                else
                {
                    self.PreviousDraw().Value = Data.defFake;
                }
            }
            else
            {
                orig(self, sLeaser, rCam, timeStacker, camPos);
            }
        }


        // Edge cases ===========================================================

        // reset called from outside of update, apply reversed coordinates if needed
        protected static void PlayerGraphics_Reset(On.PlayerGraphics.orig_Reset orig, PlayerGraphics self)
        {
            // switched behavior
            // reset on not reversed player that should be reversed!
            if (self.player.Reverse().reverseGravity && self.owner.room != null && !reversedProcessing)
            {
                Room room = self.owner.room;
                ReversePlayer(self.player, room);
                try
                {
                    orig(self);
                }
                catch (Exception e) { Debug.LogException(e); }
                DeversePlayer(self.player, room);
            }
            else
            {
                orig(self);
            }
        }
        #endregion

        // fix wrong tile data during room activation from player entering shortcut
        protected static void Creature_SuckedIntoShortCut(On.Creature.orig_SuckedIntoShortCut orig, Creature self, RWCustom.IntVector2 entrancePos, bool carriedByOther)
        {
            if (self is Player p && reversedProcessing && p.room != null)
            {
                Room room = p.room;
                DeversePlayer(p, room);
                try
                {
                    orig(self, p.enteringShortCut.Value, carriedByOther);
                }
                catch (Exception e) { Debug.LogException(e); }
                ReversePlayer(p, room);
            }
            else
            {
                orig(self, entrancePos, carriedByOther);
            }
        }


        public delegate Vector2 LookPoint_orig(PlayerGraphics.PlayerObjectLooker self);
        public Vector2 LookPoint_Fix(LookPoint_orig orig, PlayerGraphics.PlayerObjectLooker self)
        {
            var retval = orig(self);

            if (reversedProcessing)
            {
                if (self.lookAtPoint != null || self.currentMostInteresting != null)
                {
                    retval.y = self.owner.player.room.PixelHeight - retval.y;
                }
            }

            return retval;
        }

        protected static ClimbableVinesSystem.VinePosition ClimbableVinesSystem_VineOverlap(On.ClimbableVinesSystem.orig_VineOverlap orig, ClimbableVinesSystem self, Vector2 pos, float rad)
        {
            bool reversedProcessing = false;
            if (reversedProcessing) pos.y = self.room.PixelHeight - pos.y;
            return orig(self, pos, rad);
        }

        protected static Vector2 ClimbableVinesSystem_OnVinePos(On.ClimbableVinesSystem.orig_OnVinePos orig, ClimbableVinesSystem self, ClimbableVinesSystem.VinePosition vPos)
        {
            bool reversedProcessing = false;
            var retval = orig(self, vPos);
            if (reversedProcessing) retval.y = self.room.PixelHeight - retval.y;
            return retval;
        }

        protected static ClimbableVinesSystem.VinePosition ClimbableVinesSystem_VineSwitch(On.ClimbableVinesSystem.orig_VineSwitch orig, ClimbableVinesSystem self, ClimbableVinesSystem.VinePosition vPos, Vector2 goalPos, float rad)
        {
            bool reversedProcessing = false;
            if (reversedProcessing) goalPos.y = self.room.PixelHeight - goalPos.y;
            return orig(self, vPos, goalPos, rad);
        }

        protected static void ClimbableVinesSystem_ConnectChunkToVine(On.ClimbableVinesSystem.orig_ConnectChunkToVine orig, ClimbableVinesSystem self, BodyChunk chunk, ClimbableVinesSystem.VinePosition vPos, float conRad)
        {
            if (reversedProcessing)
            {
                chunk.pos.y = self.room.PixelHeight - chunk.pos.y;
                chunk.vel.y *= -1;
            }
            orig(self, chunk, vPos, conRad);
            if (reversedProcessing)
            {
                chunk.pos.y = self.room.PixelHeight - chunk.pos.y;
                chunk.vel.y *= -1;
            }
        }


        // Items ================================================================

        // player picks up things considering its real position
        protected static PhysicalObject Player_PickupCandidate(On.Player.orig_PickupCandidate orig, Player self, float favorSpears)
        {

            PhysicalObject retval = null;
            if (reversedProcessing && self.room != null)
            {
                Room room = self.room;
                // simpler switch
                self.bodyChunks[0].pos.y = room.PixelHeight - self.bodyChunks[0].pos.y;
                try
                {
                    retval = orig(self, favorSpears);
                }
                catch (Exception e) { Debug.LogException(e); }
                self.bodyChunks[0].pos.y = room.PixelHeight - self.bodyChunks[0].pos.y;
            }
            else
            {
                retval = orig(self, favorSpears);
            }
            return retval;
        }

        // grabbed goes into reverse space
        protected static void Player_SlugcatGrab(On.Player.orig_SlugcatGrab orig, Player self, PhysicalObject obj, int graspUsed)
        {
            if (self.room != null && self.Reverse().reverseGravity && reversedProcessing)
            {
                var objs = self.Reverse().objects;
                if (!objs.Contains(obj))
                {
                    var pheight = self.room.PixelHeight;
                    foreach (var c in obj.bodyChunks)
                    {
                        c.pos = new Vector2(c.pos.x, pheight - c.pos.y);
                        c.lastPos = new Vector2(c.lastPos.x, pheight - c.lastPos.y);
                        c.lastLastPos = new Vector2(c.lastLastPos.x, pheight - c.lastLastPos.y);
                        c.contactPoint.y *= -1;
                        c.vel.y *= -1;
                        if (c.setPos != null) c.setPos = new Vector2(c.setPos.Value.x, pheight - c.setPos.Value.y);
                    }
                    objs.Add(obj);
                }
            }
            orig(self, obj, graspUsed);
        }

        #region ilhooks
        // player colides with flies considering its real position
        // player lines 1000 through 1012 envelopped in flipping player y
        // player triggers shelter based on distance from room shortcuts entrance
        // player line 1042 changed
        protected static void Player_Update1(ILContext il)
        {
            var c = new ILCursor(il);

            ILLabel dest1 = null;
            if (c.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0),
                i => i.MatchCall<Creature>("get_grasps"),
                i => i.MatchLdcI4(0),
                i => i.MatchLdelemRef(),
                i => i.MatchBrfalse(out _),

                i => i.MatchLdarg(0),
                i => i.MatchCall<Creature>("get_grasps"),
                i => i.MatchLdcI4(1),
                i => i.MatchLdelemRef(),
                i => i.MatchBrtrue(out dest1),

                i => i.MatchLdarg(0),
                i => i.MatchLdfld<UpdatableAndDeletable>("room"),
                i => i.MatchLdfld<Room>("fliesRoomAi"),
                i => i.MatchBrfalse(out _)
                ))
            {
                c.MoveAfterLabels();

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<Player>>((p) =>
                {
                    if (reversedProcessing)
                    {
                        p.bodyChunks[0].pos.y = p.room.PixelHeight - p.bodyChunks[0].pos.y; // upsidown
                    }
                });

                c.GotoLabel(dest1);
                c.Index++; // the game was mysteriously crashing without this

                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<Player>>((p) =>
                {
                    if (reversedProcessing)
                    {
                        p.bodyChunks[0].pos.y = p.room.PixelHeight - p.bodyChunks[0].pos.y; // upsidown
                    }
                });
            }
            else Debug.LogException(new Exception("Couldn't IL-hook Player_Update from VVVVVV cat")); // deffendisve progrmanig


            // shelter pos fix
            if (c.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0),
                i => i.MatchCall<Creature>("get_abstractCreature"),
                i => i.MatchLdflda<AbstractWorldEntity>("pos"),
                i => i.MatchCall<WorldCoordinate>("get_Tile"),

                i => i.MatchLdarg(0),
                i => i.MatchLdfld<UpdatableAndDeletable>("room"),
                i => i.MatchLdfld<Room>("shortcuts"),
                i => i.MatchLdcI4(0),
                i => i.MatchLdelema<ShortcutData>(),
                i => i.MatchCall<ShortcutData>("get_StartTile"),

                i => i.MatchCall(typeof(RWCustom.Custom).GetMethod("ManhattanDistance", new Type[] { typeof(RWCustom.IntVector2), typeof(RWCustom.IntVector2) }))
                ))
            {

                c.Index += 4;
                c.MoveAfterLabels();
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<RWCustom.IntVector2, Player, RWCustom.IntVector2>>((v, p) =>
                {
                    if (reversedProcessing)
                    {
                        v.y = p.room.Height - 1 - v.y;
                    }
                    return v;
                });
            }
            else Debug.LogException(new Exception("Couldn't IL-hook Player_Update from VVVVVV cat 2")); // deffendisve progrmanig


        }

        // tubeworm tongue goes up, silly
        protected static Vector2 Tongue_ProperAutoAim(On.TubeWorm.Tongue.orig_ProperAutoAim orig, TubeWorm.Tongue self, Vector2 originalDir)
        {
            if (self.worm.grabbedBy.Count > 0 && self.worm.grabbedBy[0].grabber is Player p /*&& IsMe(p)*/ && p.Reverse().reverseGravity)
            {
                originalDir.y *= -1f;
            }
            return orig(self, originalDir);
        }


        // Water fixes ===========================================================
        // fix clinging to surface of water while surfaceswim
        // player line 2429
        protected static void Player_UpdateAnimation(ILContext il)
        {
            var c = new ILCursor(il);
            float mulfac = 0f;
            if (c.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchCall<PhysicalObject>("get_bodyChunks"),
                i => i.MatchLdcI4(0),
                i => i.MatchLdelemRef(),
                i => i.MatchLdflda<BodyChunk>("pos"),
                i => i.MatchLdfld<Vector2>("y"),

                i => i.MatchLdarg(0),
                i => i.MatchLdfld<UpdatableAndDeletable>("room"),

                i => i.MatchLdarg(0),
                i => i.MatchCall<PhysicalObject>("get_bodyChunks"),
                i => i.MatchLdcI4(0),
                i => i.MatchLdelemRef(),
                i => i.MatchLdflda<BodyChunk>("pos"),
                i => i.MatchLdfld<Vector2>("x"),

                i => i.MatchCallOrCallvirt<Room>("FloatWaterLevel"),
                i => i.MatchLdcR4(out _), // a value

                i => i.MatchAdd(),
                i => i.MatchSub(),
                i => i.MatchLdcR4(out mulfac), // a value
                i => i.MatchMul()
                ))
            {
                c.Index -= 4;
                c.MoveAfterLabels();

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, float, float, Player, float>>((f1, f2, f3, p) => // distance offset from surface times gain
                {
                    if (p.Reverse().reverseGravity)
                    {
                        return (f2 - f3) - f1; // upsidown
                    }
                    else return f1 - (f2 + f3);
                });
                c.RemoveRange(2);
            }
            else Debug.LogException(new Exception("Couldn't IL-hook Player_UpdateAnimation from VVVVVV cat")); // deffendisve progrmanig

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<Player>(nameof(Player.tubeWorm)),
                x => x.MatchLdfld<TubeWorm>(nameof(TubeWorm.tongues)),
                x => x.MatchLdcI4(0),
                x => x.MatchLdelemRef(),
                x => x.MatchCallvirt<TubeWorm.Tongue>("get_AttachedPos")
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((Vector2 orig, Player self) => 
                {
                    if (self.Reverse().reverseGravity && self.room != null)
                    {
                        orig.y = self.room.PixelHeight - orig.y;
                    }
                    return orig;
                });
            }
            else Debug.LogException(new Exception("Couldn't IL-hook Player_UpdateAnimation from VVVVVV cat 2"));
        }

        // Determine deep-swim vs surface swim
        // player line 5453 patched in 2 spots
        protected static void Player_MovementUpdate(ILContext il)
        {
            var c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                i => i.MatchCallOrCallvirt<Room>("FloatWaterLevel"),
                i => i.MatchLdcR4(80f),
                i => i.MatchSub(),
                i => i.MatchClt()))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((bool flag, Player self) => self.Reverse().reverseGravity ? self.bodyChunks[0].pos.y > self.room.FloatWaterLevel(self.bodyChunks[0].pos.x) + 80f : flag);
            }
            else
            {
                Debug.LogException(new("Couldn't ILHook Player.MovementUpdate from VVVVVV cat! (part 1)"));
                return;
            }
            int loc = 0;
            c.TryGotoNext(MoveType.After,
                i => i.MatchCallOrCallvirt<Room>("FloatWaterLevel"),
                i => i.MatchLdloc(out loc));
            if (c.TryGotoNext(MoveType.After,
                i => i.MatchSub(),
                i => i.MatchClt()))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc_S, il.Body.Variables[loc]);
                c.EmitDelegate((bool flag, Player self, bool flag4) => self.Reverse().reverseGravity ? self.bodyChunks[0].pos.y > self.room.FloatWaterLevel(self.bodyChunks[0].pos.x) + (flag4 ? 10f : 30f) : flag);
            }
            else
                Debug.LogException(new("Couldn't ILHook Player.MovementUpdate from VVVVVV cat! (part 2)"));
        }

        // Bodychunk float when submerged logic
        // BodyChunk line 104
        protected static void BodyChunk_Update(ILContext il)
        {
            var c = new ILCursor(il);
            ILLabel dest1 = null;
            if (c.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdflda<BodyChunk>("pos"),
                i => i.MatchLdfld<Vector2>("y"),

                i => i.MatchLdarg(0),
                i => i.MatchLdfld<BodyChunk>("rad"),

                i => i.MatchSub(),

                i => i.MatchLdarg(0),
                i => i.MatchCall<BodyChunk>("get_owner"),
                i => i.MatchLdfld<UpdatableAndDeletable>("room"),

                i => i.MatchLdarg(0),
                i => i.MatchLdflda<BodyChunk>("pos"),
                i => i.MatchLdfld<Vector2>("x"),

                i => i.MatchCallOrCallvirt<Room>("FloatWaterLevel"),

                i => i.MatchBgtUn(out dest1) // fail out
                ))
            {
                c.Index--;

                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float, float, float, BodyChunk, bool>>((y, r, l, b) => // Test wether should NOT float up (shortcircuit out logic)
                {
                    if (b.owner is Player p && reversedProcessing)
                    {
                        return (y + r) <= l; // upsidown
                    }
                    else return (y - r) >= l;
                });
                c.Emit(OpCodes.Brtrue, dest1);
                c.Remove();
                c.GotoPrev(MoveType.Before, i => i.MatchSub());
                c.Remove();
            }
            else Debug.LogException(new Exception("Couldn't IL-hook BodyChunk_Update from VVVVVV cat")); // deffendisve progrmanig
        }
#endregion

        public delegate float Orig_BodyChunk_submersion(BodyChunk b);
        // Chunk 'submerged' inverted (water on top)
        // reflected over in ctor hook
        public static float Flipped_submersion(Orig_BodyChunk_submersion orig, BodyChunk self)
        {
            if (!reversedProcessing) return orig(self);
            return 1f - orig(self);
        }

        protected static float Flipped_FloatWaterLevel(On.Room.orig_FloatWaterLevel orig, Room self, float horizontalPos)
        {
            if (!reversedProcessing) return orig(self, horizontalPos);
            return self.PixelHeight - orig(self, horizontalPos);
        }

        protected static ShortcutData Flipped_shortcutData(On.Room.orig_shortcutData_IntVector2 orig, Room self, RWCustom.IntVector2 pos)
        {
            if (!reversedProcessing) return orig(self, pos);
            return orig(self, new RWCustom.IntVector2(pos.x, self.Height - 1 - pos.y));
        }

        protected static Room.Tile Flipped_GetTile(On.Room.orig_GetTile_int_int orig, Room self, int x, int y)
        {
            if (!reversedProcessing) return orig(self, x, y);
            return orig(self, x, self.Height - 1 - y);
        }

        // Patchup objects placed by player
        protected static void Room_AddObject(On.Room.orig_AddObject orig, Room self, UpdatableAndDeletable obj)
        {
            if (reversedProcessing)
            {
                if (obj is CosmeticSprite cs)
                {
                    var ph = self.PixelHeight;
                    cs.pos.y = ph - cs.pos.y;
                    cs.lastPos.y = ph - cs.lastPos.y;
                    cs.vel.y *= -1f;

                    if (cs is WaterDrip wd)
                    {
                        wd.lastLastPos = wd.pos;
                        wd.lastLastLastPos = wd.pos;
                    }
                }
            }
            orig(self, obj);
        }

        protected static AItile AImap_getAItile_int_int(On.AImap.orig_getAItile_int_int orig, AImap self, int x, int y)
        {
            if (!reversedProcessing) return orig(self, x, y);
            return orig(self, x, self.height - 1 - y);
        }
    }
}
