using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RWCustom;
using static Pom.Pom;

namespace ONHStuff
{

    internal static class SuperSlopeHooks
    {
		public static void Apply()
		{
			RegisterManagedObject<SuperSlopeObj, SuperSlopeData, ManagedRepresentation>("SuperSlope", "ONHStuff");
            //On.Lizard.Act += Lizard_Act;
            On.PlayerGraphics.Update += PlayerGraphics_Update;
            //On.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically;
            On.BodyChunk.checkAgainstSlopesVertically += BodyChunk_checkAgainstSlopesVertically1;
			//On.SharedPhysics.SlopesVertically += SharedPhysics_SlopesVertically;
			On.BodyChunk.Update += BodyChunk_Update;
			//On.Limb.FindGrip += Limb_FindGrip;
			On.AImap.SetVisibilityMapFromCompressedArray += AImap_SetVisibilityMapFromCompressedArray;
		}

        private static void BodyChunk_checkAgainstSlopesVertically1(On.BodyChunk.orig_checkAgainstSlopesVertically orig, BodyChunk self)
        {
			orig(self);
			if (self.owner is Creature && self.owner is not Player)
			{ return; }

			onSlopeAngle.Set(self, 0f);

			foreach (PlacedObject pObj in self.owner.room.roomSettings.placedObjects)
			{
				if (pObj.data is SuperSlopeData SSD)
				{

					float angle = SSD.angle;
					float steepness = Math.Abs(Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(0, 180, angle)));

					Vector2 minPos = SSD.minRect - new Vector2(20, 20);
					Vector2 maxPos = SSD.maxRect + new Vector2(20, 20);

					if (!(minPos.x <= self.pos.x && self.pos.x <= maxPos.x && minPos.y <= self.pos.y && self.pos.y <= maxPos.y))
					{ continue; }


					int num = 0;
					float yValueAtPoint = SSD.HeightAtPoint(self.pos.x);
					//yValueAtPoint = self.pos.x - (vector.x - 10f) + (vector.y - 10f);
					int num3 = 0;

					switch (SSD.direction.ToString())
					{
						case "UpLeft":
							num = -1;
							num3 = -1;
							break;

						case "DownLeft":
							num3 = 1;
							break;

						case "DownRight":
							num3 = 1;
							break;

						case "UpRight":
							num = 1;
							num3 = -1;
							break;
					}

					if (num3 == -1 && self.pos.y <= yValueAtPoint + self.slopeRad + self.slopeRad)
					{
						self.pos.y = yValueAtPoint + self.slopeRad + self.slopeRad;
						self.contactPoint.y = -1;

						if (steepness > 0.5)
						{ self.vel.x = self.vel.x * Mathf.Lerp((1f - self.owner.surfaceFriction), 1f, (steepness - 0.5f) * 2f); }

						else
						{ self.vel.x = self.vel.x * Mathf.Pow(Mathf.Lerp(0f, (1f - self.owner.surfaceFriction), (steepness) * 2f), 0.5f); }

						self.vel.x = self.vel.x + Mathf.Abs(self.vel.y) * Mathf.Clamp(0.5f - self.owner.surfaceFriction, 0f, 0.5f) * (float)num * 0.2f;
						self.vel.y = 0f;
						self.onSlope = num;
						onSlopeAngle.Set(self, (float)((angle - 90f) * (Math.PI / 180.0)));
						self.slopeRad = self.TerrainRad - 1f;
					}

					else if (num3 == 1 && self.pos.y >= yValueAtPoint - self.slopeRad - self.slopeRad)
					{
						self.pos.y = yValueAtPoint - self.slopeRad - self.slopeRad;
						self.contactPoint.y = 1;
						self.vel.x = self.vel.x * (1f - self.owner.surfaceFriction);
						self.vel.y = 0f;
						self.onSlope = num;
						self.slopeRad = self.TerrainRad - 1f;
					}
				}
			}

		}

		private static void AImap_SetVisibilityMapFromCompressedArray(On.AImap.orig_SetVisibilityMapFromCompressedArray orig, AImap self, int[] ca)
		{
			try { orig(self, ca); }

			catch
			{
				int num = 0;

				for (int i = 0; i < self.room.TileWidth; i++)
				{
					for (int j = 0; j < self.room.TileHeight; j++)
					{
						if (!self.room.GetTile(i, j).Solid)
						{
							try { self.getAItile(i, j).visibility = ca[num]; }

							catch { Debug.Log($"Broken tile at X: {i} Y: {j} L: {num}"); }

							num++;
						}
					}
				}
			}
		}


		/// <summary>
		/// orig disables lizard collisions with slopes if they haven't been trying to move long enough.
		/// why???
		/// </summary>
		/// <param name="orig"></param>
		/// <param name="self"></param>
		private static void Lizard_Act(On.Lizard.orig_Act orig, Lizard self)
		{
			orig(self);
			for (int k = 0; k < self.bodyChunks.Length; k++)
			{
				self.bodyChunks[k].collideWithSlopes = true;
			}
		}

		/// <summary>
		/// set the angle of slugcat's feet to match the SuperSlope
		/// </summary>
		/// <param name="orig"></param>
		/// <param name="self"></param>
		private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
		{
			orig(self);

			if ((self.owner.bodyChunks[1].ContactPoint.y == -1 || self.player.animation == Player.AnimationIndex.StandOnBeam) && onSlopeAngle.TryGet(self.owner.bodyChunks[1], out float angle) && angle != 0f)
			{
				//Debug.Log("legsDirection " + angle);
				self.legsDirection = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
				self.legsDirection.Normalize();
			}
		}

		/// <summary>
		/// reset floor angle every tick so it's only on when it's supposed to
		/// </summary>
		/// <param name="orig"></param>
		/// <param name="self"></param>
		private static void BodyChunk_Update(On.BodyChunk.orig_Update orig, BodyChunk self)
		{
			onSlopeAngle.Set(self, 0f);
			
				orig(self);
			
		}

		/// <summary>
		/// the actual BodyChunk collisions
		/// </summary>
		/// <param name="orig"></param>
		/// <param name="self"></param>
		private static void BodyChunk_checkAgainstSlopesVertically(On.BodyChunk.orig_checkAgainstSlopesVertically orig, BodyChunk self)
		{
			orig(self);

			if (self.owner is Creature && self.owner is not Player)
			{ return; }

			onSlopeAngle.Set(self, 0f);

			foreach (PlacedObject pObj in self.owner.room.roomSettings.placedObjects)
			{
				if (pObj.data is SuperSlopeData SSD)
				{

					float angle = SSD.angle;
					float steepness = Math.Abs(Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(0, 180, angle)));

					Vector2 minPos = SSD.minRect - new Vector2(20, 20);
					Vector2 maxPos = SSD.maxRect + new Vector2(20, 20);

					if (!(minPos.x <= self.pos.x && self.pos.x <= maxPos.x && minPos.y <= self.pos.y && self.pos.y <= maxPos.y))
					{ continue; }


					int num = 0;
					float yValueAtPoint = SSD.HeightAtPoint(self.pos.x);
					//yValueAtPoint = self.pos.x - (vector.x - 10f) + (vector.y - 10f);
					int num3 = 0;

					switch (SSD.direction.ToString())
					{
						case "UpLeft":
							num = -1;
							num3 = -1;
							break;

						case "DownLeft":
							num3 = 1;
							break;

						case "DownRight":
							num3 = 1;
							break;

						case "UpRight":
							num = 1;
							num3 = -1;
							break;
					}

					if (num3 == -1 && self.pos.y <= yValueAtPoint + self.slopeRad + self.slopeRad)
					{
						self.pos.y = yValueAtPoint + self.slopeRad + self.slopeRad;
						self.contactPoint.y = -1;

						if (steepness > 0.5)
						{ self.vel.x = self.vel.x * Mathf.Lerp((1f - self.owner.surfaceFriction), 1f, (steepness - 0.5f) * 2f); }

						else
						{ self.vel.x = self.vel.x * Mathf.Pow(Mathf.Lerp(0f, (1f - self.owner.surfaceFriction), (steepness) * 2f), 0.5f); }

						self.vel.x = self.vel.x + Mathf.Abs(self.vel.y) * Mathf.Clamp(0.5f - self.owner.surfaceFriction, 0f, 0.5f) * (float)num * 0.2f;
						self.vel.y = 0f;
						self.onSlope = num;
						onSlopeAngle.Set(self, (float)((angle - 90f) * (Math.PI / 180.0)));
						self.slopeRad = self.TerrainRad - 1f;
					}

					else if (num3 == 1 && self.pos.y >= yValueAtPoint - self.slopeRad - self.slopeRad)
					{
						self.pos.y = yValueAtPoint - self.slopeRad - self.slopeRad;
						self.contactPoint.y = 1;
						self.vel.x = self.vel.x * (1f - self.owner.surfaceFriction);
						self.vel.y = 0f;
						self.onSlope = num;
						self.slopeRad = self.TerrainRad - 1f;
					}
				}
			}
		}

		/// <summary>
		/// idk what this is for but I hooked it anyways
		/// </summary>
		/// <param name="orig"></param>
		/// <param name="room"></param>
		/// <param name="cd"></param>
		/// <returns></returns>
		private static SharedPhysics.TerrainCollisionData SharedPhysics_SlopesVertically(On.SharedPhysics.orig_SlopesVertically orig, Room room, SharedPhysics.TerrainCollisionData cd)
		{


			bool superSlopeFound = false;

			foreach (PlacedObject pObj in room.roomSettings.placedObjects)
			{
				if (pObj.data is SuperSlopeData SSD)
				{

					float angle = SSD.angle;
					float steepness = Math.Abs(Mathf.Lerp(-1f, 1f, Mathf.InverseLerp(0, 180, angle)));

					Vector2 minPos = SSD.minRect - new Vector2(20, 20);
					Vector2 maxPos = SSD.maxRect + new Vector2(20, 20);

					if (!(minPos.x <= cd.pos.x && cd.pos.x <= maxPos.x && minPos.y <= cd.pos.y && cd.pos.y <= maxPos.y))
					{ continue; }


					int num = 0;
					float yValueAtPoint = SSD.HeightAtPoint(cd.pos.x);
					//yValueAtPoint = self.pos.x - (vector.x - 10f) + (vector.y - 10f);
					int num3 = 0;

					switch (SSD.direction.ToString())
					{
						case "UpLeft":
							num = -1;
							num3 = -1;
							break;

						case "DownLeft":
							num3 = 1;
							break;

						case "DownRight":
							num3 = 1;
							break;

						case "UpRight":
							num = 1;
							num3 = -1;
							break;
					}

					if (num3 == -1 && cd.pos.y <= yValueAtPoint + cd.rad + cd.rad)
					{
						cd.pos.y = num + cd.rad + cd.rad;
						superSlopeFound = true;
						break;
					}

					else if (num3 == 1 && cd.pos.y >= yValueAtPoint - cd.rad - cd.rad)
					{
						cd.pos.y = num - cd.rad - cd.rad;
						superSlopeFound = true;
						break;
					}
				}
			}

			if (superSlopeFound)
			{ return cd; }
			else
			{ return orig(room, cd); }
		}

		/// <summary>
		/// limb collisions, so that creatures can walk
		/// </summary>
		/// <param name="orig"></param>
		/// <param name="self"></param>
		/// <param name="room"></param>
		/// <param name="attachedPos"></param>
		/// <param name="searchFromPos"></param>
		/// <param name="maximumRadiusFromAttachedPos"></param>
		/// <param name="goalPos"></param>
		/// <param name="forbiddenXDirs"></param>
		/// <param name="forbiddenYDirs"></param>
		/// <param name="behindWalls"></param>
		private static void Limb_FindGrip(On.Limb.orig_FindGrip orig, Limb self, Room room, Vector2 attachedPos, Vector2 searchFromPos, float maximumRadiusFromAttachedPos, Vector2 goalPos, int forbiddenXDirs, int forbiddenYDirs, bool behindWalls)
		{
			//doesn't call orig, Bad Code
			if (!Custom.DistLess(attachedPos, searchFromPos, maximumRadiusFromAttachedPos))
			{
				searchFromPos = attachedPos + Custom.DirVec(attachedPos, searchFromPos) * (maximumRadiusFromAttachedPos - 1f);
			}
			if (!Custom.DistLess(attachedPos, goalPos, maximumRadiusFromAttachedPos))
			{
				goalPos = attachedPos + Custom.DirVec(attachedPos, goalPos) * maximumRadiusFromAttachedPos;
			}
			IntVector2 tilePosition = room.GetTilePosition(searchFromPos);
			Vector2 vector = new Vector2(-100000f, -100000f);
			for (int i = 0; i < 9; i++)
			{
				if (Custom.eightDirectionsAndZero[i].x != forbiddenXDirs && Custom.eightDirectionsAndZero[i].y != forbiddenYDirs)
				{
					Vector2 vector2 = room.MiddleOfTile(tilePosition + Custom.eightDirectionsAndZero[i]);
					Vector2 vector3 = new Vector2(Mathf.Clamp(goalPos.x, vector2.x - 10f, vector2.x + 10f), Mathf.Clamp(goalPos.y, vector2.y - 10f, vector2.y + 10f));
					switch (room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i]).Terrain)
					{
						case Room.Tile.TerrainType.Air:
							if (behindWalls && room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i]).wallbehind && vector == new Vector2(-100000f, -100000f) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
							{
								vector = vector3;
							}
							break;
						case Room.Tile.TerrainType.Solid:
							if (Custom.eightDirectionsAndZero[i].x != 0 && room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i] + new IntVector2(-Custom.eightDirectionsAndZero[i].x, 0)).Terrain != Room.Tile.TerrainType.Solid)
							{
								vector3.x = vector2.x - (float)Custom.eightDirectionsAndZero[i].x * 10f;
							}
							if (Custom.eightDirectionsAndZero[i].y != 0 && room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i] + new IntVector2(0, -Custom.eightDirectionsAndZero[i].y)).Terrain != Room.Tile.TerrainType.Solid)
							{
								vector3.y = vector2.y - (float)Custom.eightDirectionsAndZero[i].y * 10f;
							}
							if (Custom.DistNoSqrt(goalPos, vector3) < Custom.DistNoSqrt(goalPos, vector) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
							{
								vector = vector3;
							}
							break;
						case Room.Tile.TerrainType.Slope:
							switch (room.IdentifySlope(tilePosition + Custom.eightDirectionsAndZero[i]).ToString())
							{
								case "UpLeft":
								case "DownRight":
									vector3.y = vector2.y - 10f + (vector3.x - (vector2.x - 10f));
									break;
								case "UpRight":
								case "DownLeft":
									vector3.y = vector2.y + 10f - (vector3.x - (vector2.x - 10f));
									break;
							}
							if (Custom.DistNoSqrt(goalPos, vector3) < Custom.DistNoSqrt(goalPos, vector) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
							{
								vector = vector3;
							}
							break;
						case Room.Tile.TerrainType.Floor:
							vector3.y = vector2.y + 10f;
							if (Custom.DistNoSqrt(goalPos, vector3) < Custom.DistNoSqrt(goalPos, vector) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
							{
								vector = vector3;
							}
							break;
					}

					//this is the new stuff - probably needs to be il hooked to prevent GrabbedTerrain from being called twice
					/*if (room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i]).Terrain == EnumExt_SuperSlopes.SuperSlope)
					{
						switch (FindSuperSlopeDirection(room, tilePosition + Custom.eightDirectionsAndZero[i]))
						{
							case Room.SlopeDirection.UpLeft:
							case Room.SlopeDirection.DownRight:
								vector3.y = vector2.y - 10f + (vector3.x - (vector2.x - 10f));
								break;
							case Room.SlopeDirection.UpRight:
							case Room.SlopeDirection.DownLeft:
								vector3.y = vector2.y + 10f - (vector3.x - (vector2.x - 10f));
								break;
						}
						if (Custom.DistNoSqrt(goalPos, vector3) < Custom.DistNoSqrt(goalPos, vector) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
						{
							vector = vector3;
							Debug.Log("SuperSlopeLimbGrab: " + tilePosition.ToString());
						}
					}*/

					if (room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i]).horizontalBeam)
					{
						vector3 = new Vector2(Mathf.Clamp(goalPos.x, vector2.x - 10f, vector2.x + 10f), vector2.y);
						if (Custom.DistNoSqrt(goalPos, vector3) < Custom.DistNoSqrt(goalPos, vector) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
						{
							vector = vector3;
						}
					}
					if (room.GetTile(tilePosition + Custom.eightDirectionsAndZero[i]).verticalBeam)
					{
						vector3 = new Vector2(vector2.x, Mathf.Clamp(goalPos.y, vector2.y - 10f, vector2.y + 10f));
						if (Custom.DistNoSqrt(goalPos, vector3) < Custom.DistNoSqrt(goalPos, vector) && Custom.DistLess(attachedPos, vector3, maximumRadiusFromAttachedPos))
						{
							vector = vector3;
						}
					}
				}
			}
			if (vector.x != -100000f && vector.y != -100000f)
			{
				self.mode = Limb.Mode.HuntAbsolutePosition;
				self.absoluteHuntPos = vector;
				self.GrabbedTerrain();
			}
		}

		private static Room.SlopeDirection FindSuperSlopeDirection(Room room, IntVector2 tile)
		{
			foreach (PlacedObject pObj in room.roomSettings.placedObjects)
			{
				if (pObj.data is SuperSlopeData SSD)
				{
					if (SSD.SuperSlopeTiles.Contains(tile))
					{
						return SSD.direction;
					}
				}
			}

			return Room.SlopeDirection.Broken;

		}

		public static float steepnessCheck;

		public static Utils.AttachedField<BodyChunk, float> onSlopeAngle = new Utils.AttachedField<BodyChunk, float>();

	}

	internal class SuperSlopeObj : UpdatableAndDeletable, INotifyWhenRoomIsReady
    {
        public void AIMapReady()
        {
        }

        public void ShortcutsReady()
        {
            findIntersectingTiles();
        }

        public PlacedObject pObj;
        private SuperSlopeData Data => (pObj?.data as SuperSlopeData);
        public List<IntVector2> tiles = new List<IntVector2>();
        public SuperSlopeObj(PlacedObject pObj, Room room)
        {
            this.pObj = pObj;
        }

        public void findIntersectingTiles()
        {
            Debug.Log("finding intersections");
            Debug.Log("pObj pos " + pObj.pos + " pos2 " + Data.AbsPos2);

            Data.SuperSlopeTiles = new List<IntVector2>();

            bool ceiling = Data.direction == Room.SlopeDirection.DownLeft || Data.direction == Room.SlopeDirection.DownRight;

            for (int x = 0; x < Math.Abs(Data.pos2.x) + 1; x++)
            {
                for (int y = 0; y < Math.Abs(Data.pos2.y + 1); y++)
                {
                    IntVector2 intTilePos = room.GetTilePosition(Data.minRect + new Vector2(x * 20, y * 20));

                    //don't set out of bounds tiles
                    if (intTilePos.x < 0 || intTilePos.x >= room.Tiles.GetLength(0) || intTilePos.y < 0 || intTilePos.y >= room.Tiles.GetLength(1))
                    { continue; }

                    Vector2 tilePos = room.MiddleOfTile(intTilePos);

                    if (!ceiling)
                    {
                        if (tilePos.y + 10 < Data.HeightAtPoint(tilePos.x))
                        {
                            //Debug.Log("Setting tile position " + intTilePos.ToString());
                            room.Tiles[intTilePos.x, intTilePos.y].Terrain = Room.Tile.TerrainType.Solid;
                        }

                        else if (tilePos.y + 10 >= Data.HeightAtPoint(tilePos.x) && tilePos.y < Data.HeightAtPoint(tilePos.x) && !room.Tiles[intTilePos.x, intTilePos.y].Solid)
                        {
                            //room.Tiles[intTilePos.x, intTilePos.y].Terrain = SuperSlopes.EnumExt_SuperSlopes.SuperSlope;
                            Data.SuperSlopeTiles.Add(intTilePos);
                        }
                    }

                    else if (ceiling)
                    {
                        if (tilePos.y - 10 > Data.HeightAtPoint(tilePos.x))
                        {
                            //Debug.Log("Setting tile position " + intTilePos.ToString());
                            room.Tiles[intTilePos.x, intTilePos.y].Terrain = Room.Tile.TerrainType.Solid;
                        }

                        else if (tilePos.y - 10 <= Data.HeightAtPoint(tilePos.x) && tilePos.y > Data.HeightAtPoint(tilePos.x))
                        {
                            //room.Tiles[intTilePos.x, intTilePos.y].Terrain = SuperSlopes.EnumExt_SuperSlopes.SuperSlope;
                            Data.SuperSlopeTiles.Add(intTilePos);
                        }
                    }


                }
            }
        }
    }

    internal class SuperSlopeData : ManagedData
    {
        //this is a mess, lol
        internal PlacedObject pObj;

        public List<IntVector2> SuperSlopeTiles = new List<IntVector2>();

        internal IntVector2 pos2 => GetValue<IntVector2>("Line");

        internal Vector2 AbsPos2 => (pos2.ToVector2() * 20) + pObj.pos;

        internal Vector2 minRect => new Vector2(Mathf.Min(pObj.pos.x, AbsPos2.x), Mathf.Min(pObj.pos.y, AbsPos2.y));

        internal Vector2 maxRect => new Vector2(Mathf.Max(pObj.pos.x, AbsPos2.x), Mathf.Max(pObj.pos.y, AbsPos2.y));

        internal float length => Vector2.Distance(Vector2.zero, pos2.ToVector2());

        internal float angle
        {
            get
            {
                float result = (float)(Math.Atan2(pos2.y, pos2.x) * 180.0 / Math.PI);
                if (result < 0f)
                { result += 360f; }
                return result;
            }
        }

        public Room.SlopeDirection direction
        {
            get
            {
                //up left
                if (0 <= angle && angle < 90)
                { return Room.SlopeDirection.UpLeft; }
                //down left
                else if (90 <= angle && angle < 180)
                { return Room.SlopeDirection.DownLeft; }

                //down right
                else if (180 <= angle && angle < 270)
                { return Room.SlopeDirection.DownRight; }

                //up right
                else if (270 <= angle && angle < 360)
                {
                    return Room.SlopeDirection.UpRight;
                }
                return Room.SlopeDirection.Broken;

            }
        }

        public float HeightAtPoint(float x) => Vector2.Lerp(pObj.pos, AbsPos2, Mathf.InverseLerp(pObj.pos.x, AbsPos2.x, x)).y;

        public SuperSlopeData(PlacedObject po) : base(po, new ManagedField[] { new IntVector2Field("Line", new IntVector2(2, 3)) })
        {
            pObj = po;
        }
    }
}
