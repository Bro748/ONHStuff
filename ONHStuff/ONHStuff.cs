using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using UnityEngine;
using System.Linq;
using System.Threading;

using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]



namespace ONHStuff
{
	[BepInPlugin("bro.onhstuff", "ONHStuff", "0.9.0")]    // (GUID, mod name, mod version)
	public class ONHStuff : BaseUnityPlugin
	{
		public BepInEx.Logging.ManualLogSource _Logger => Logger;

		public static ONHStuff plugin;

        public static readonly string MOD_ID = "onh";
        public void OnEnable()
        {
			try
			{
				plugin = this;
				On.ModManager.RefreshModsLists += ForcePriority.ModManager_RefreshModsLists;
				On.RainWorld.OnModsInit += RainWorld_OnModsInit;
				ReverseCat.Enable();
                RevSupport.Apply();
				InvJunk.ApplyHooks();
                On.Deer.Act += Deer_Act;
                IL.Player.Update += Player_Update;
                /* This is called when the mod is loaded. */
                CreatureBehaviors.ApplyHooks();
                ONHObjects.Apply();
                SuperSlopeHooks.Apply();
                //CustomDataPearls.Apply();
                //save progression, don't show ONH images if player's been to FN gate
                //On.Room.Loaded += ONHProgressionSave;
                On.ActiveTriggerChecker.FireEvent += StopProjectedImageHook;

                On.OverseerHolograms.OverseerHologram.ForcedDirectionPointer.ctor += PointerHook;

                //On.Overseer.TryAddHologram += HoloHook;
                On.ReliableIggyDirection.Update += StopReliableDirection;

                //please don't stop the music
                On.ActiveTriggerChecker.FireEvent += FireMusicHook;
                On.Music.MusicPlayer.RainRequestStopSong += RainStopSongHook;
                On.RainCycle.GetDesiredCycleLength += RainCycle_GetDesiredCycleLength;

                //On.Overseer.Update += UpdateHook;


                /*new Hook(
				typeof(OverseerGraphics).GetProperty("MainColor", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),

				typeof(CustomProjections).GetMethod("GetMainColor", BindingFlags.Static | BindingFlags.Public));*/

                //spawn Iggy in Subterranean no matter what
                //On.WorldLoader.GeneratePopulation += WorldLoader_GeneratePopulation;

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                new Hook(typeof(Water).GetProperty(nameof(Water.waveAmplitude), flags).GetGetMethod(true), WaveAmplitudeHook);
                new Hook(typeof(Water).GetProperty(nameof(Water.waveLength), flags).GetGetMethod(true), WaveLengthHook);
                new Hook(typeof(Water).GetProperty(nameof(Water.waveSpeed), flags).GetGetMethod(true), WaveSpeedHook);
            }
            catch (Exception e) { Logger.LogError(e); }
			}

        private void Deer_Act(On.Deer.orig_Act orig, Deer self, bool eu, float support, float forwardPower)
        {
			orig(self, eu, support, forwardPower);
			if (self.eatCounter == 50)
			{
                if (self.eatObject == null || self.room?.abstractRoom.name.ToLower() != "depot" || !self.room.game.IsArenaSession)
                { return; }

                if (self.State.socialMemory == null) self.State.socialMemory = new SocialMemory();

                self.State.socialMemory.GetOrInitiateRelationship(self.room.game.GetArenaGameSession.Players[0].ID).like += 0.9f;
                Debug.Log($"deer happiness score: " + self.State.socialMemory.GetOrInitiateRelationship(self.room.game.Players[0].ID).like);
            }
        }

        /// <summary>
        /// allows sheltering in crawl hole shelters
        /// </summary>
        private void Player_Update(ILContext il)
        {
			var c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCall<ShortcutData>("get_StartTile"),
                x => x.MatchCall(typeof(Custom), nameof(Custom.ManhattanDistance)),
                x => x.MatchLdcI4(6)
                ))
            {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate((int orig, Player self) =>
                {
					int reg = Custom.ManhattanDistance(self.room.GetTilePosition(self.mainBodyChunk.pos), self.room.shortcuts[0].StartTile);

                    for (int i = 0; i < self.bodyChunks.Length; i++)
					{
						if (self.bodyChunks[i] == self.mainBodyChunk) continue;
						if (Custom.ManhattanDistance(self.room.GetTilePosition(self.mainBodyChunk.pos), self.room.shortcuts[0].StartTile) < reg)
							return orig;
					}
					return orig - 1;
                });
            }
            else
            {
                Logger.LogError("failed to ilhook player.update");
            }
            if (c.TryGotoNext(MoveType.After,

                x => x.MatchBle(out _),
				x => x.MatchLdsfld<ModManager>(nameof(ModManager.MMF))
				))
			{
				c.Emit(OpCodes.Ldarg_0);
				c.EmitDelegate((bool orig, Player self) =>
				{
					return orig && !CrawlSpaceOnly(self.room, self.abstractCreature.pos.Tile, new());
				});
			}
			else
			{
				Logger.LogError("failed to ilhook player.update");
			}
        }

        public static bool CrawlSpaceOnly(Room room, IntVector2 pos, HashSet<IntVector2> visited)
        {
            if (pos.x < 0 || pos.y < 0 || pos.x > room.Tiles.GetLength(0) || pos.y > room.Tiles.GetLength(1)) return true;
            if (room.GetTile(pos).Solid || visited.Contains(pos)) return true;

            if (!room.GetTile(pos + new IntVector2(-1, 0)).Solid &&
                !room.GetTile(pos + new IntVector2(-1, -1)).Solid &&
                !room.GetTile(pos + new IntVector2(0, -1)).Solid)
            { return false; } //returns false if non-crawlspace is detected

            visited.Add(pos);

            if (!CrawlSpaceOnly(room, pos + new IntVector2(1, 0), visited)) return false;
            if (!CrawlSpaceOnly(room, pos + new IntVector2(-1, 0), visited)) return false;
            if (!CrawlSpaceOnly(room, pos + new IntVector2(0, 1), visited)) return false;
            if (!CrawlSpaceOnly(room, pos + new IntVector2(0, -1), visited)) return false;

            return true;
        }

        static float WaveAmplitudeHook(Func<Water, float> orig, Water self)
        {
			if(self.room?.abstractRoom.name.ToLower() == "shoreside rig")
				return Mathf.LerpUnclamped(1f, 40f, self.room.roomSettings.WaveAmplitude);
            return orig(self);
        }
        static float WaveLengthHook(Func<Water, float> orig, Water self)
        {
            if (self.room?.abstractRoom.name.ToLower() == "shoreside rig")
                return Mathf.LerpUnclamped(50f, 750f, self.room.roomSettings.WaveLength);
            return orig(self);
        }
        static float WaveSpeedHook(Func<Water, float> orig, Water self)
        {
            if (self.room?.abstractRoom.name.ToLower() == "shoreside rig")
                return Mathf.LerpUnclamped(-0.033333335f, 0.033333335f, self.room.roomSettings.WaveSpeed);
            return orig(self);
        }

        static bool init = false;

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
			orig(self);
			try { 
            ForcePriority.CheckIfFileForceOverrideIsNecessary();
            //ForcePriority.CheckIfMapsCopyShouldBeDoneAndDoIt();
            }
            catch (Exception e) { Logger.LogError(e); }
            try
            {
                ONHStuffEnums.UnregisterValues();
                ONHStuffEnums.RegisterValues();
                if (init) return;
				init = true;
                RevSupport.LoadBundle(self);
            }
			catch (Exception e) { Logger.LogError(e); }
		}

        private int RainCycle_GetDesiredCycleLength(On.RainCycle.orig_GetDesiredCycleLength orig, RainCycle self)
        {
			if (!self.world.singleRoomWorld && self.world.game.IsStorySession)
			{
				SlugcatStats.Name slugcat = (self.world.game.session as StoryGameSession).saveState.saveStateNumber;

				if (slugcat == InvJunk.Inv && self.world.region.name == "FN")
				{
				return 3 * 40 * 60;
                }

				if (slugcat == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Rivulet && !self.world.game.GetStorySession.saveState.miscWorldSaveData.pebblesEnergyTaken)
				{
					bool useRegularCycle = false;
					foreach (string region in ONHRegions)
					{
						if (self.world.region.name == region && region != "AY")
						{ useRegularCycle = true; break; }
					}

					if (useRegularCycle)
					{
						return orig(self) * 2;
					}
				}
			}
            return orig(self);
        }

        private void StopReliableDirection(On.ReliableIggyDirection.orig_Update orig, ReliableIggyDirection self, bool eu)
        {
			if (self.data.symbol == ONHStuffEnums.Grapple && self.room.game.Players.Any(ply => ply.realizedCreature?.grasps.Any(grasp => grasp?.grabbed is TubeWorm) ?? false))
				
			{
                Debug.Log("Grapple already carried");
				self.Destroy();
				return;
			}
			else
			{
				orig(self, eu);
			}

		}

		private void WorldLoader_GeneratePopulation(On.WorldLoader.orig_GeneratePopulation orig, WorldLoader self, bool fresh)
		{
			if (self is WorldLoader worldloader && worldloader.playerCharacter != SlugcatStats.Name.Red &&
				!(worldloader.game.session as StoryGameSession).saveState.guideOverseerDead &&
				!(worldloader.game.session as StoryGameSession).saveState.miscWorldSaveData.playerGuideState.angryWithPlayer &&
				(worldloader.world.region.name == "SB" || worldloader.world.region.name == "LF" ||
				(worldloader.world.region.name == "CC" && (worldloader.game.session as StoryGameSession).saveState.miscWorldSaveData.SSaiConversationsHad < 1)) /*&&
				onh save data*/)

			{
				Debug.Log("Spawning Iggy regardless of property");
				/*WorldCoordinate worldCoordinate = new WorldCoordinate(wl_self.world.offScreenDen.index, -1, -1, 0);
                AbstractCreature abstractCreature = new AbstractCreature(wl_self.world,
                    StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Overseer), null, worldCoordinate, new EntityID(-1, 5));
                if (wl_self.world.GetAbstractRoom(worldCoordinate).offScreenDen)
                {
                    wl_self.world.GetAbstractRoom(worldCoordinate).entitiesInDens.Add(abstractCreature);
                }
                else
                {
                    wl_self.world.GetAbstractRoom(worldCoordinate).AddEntity(abstractCreature);
                }
				(abstractCreature.abstractAI as OverseerAbstractAI).SetAsPlayerGuide();*/
				worldloader.world.region.regionParams.playerGuideOverseerSpawnChance = 1;

			}
			orig(self, fresh);

		}

		public void StopProjectedImageHook(On.ActiveTriggerChecker.orig_FireEvent orig, ActiveTriggerChecker self)
		{
			if (self.eventTrigger.tEvent != null && 
				(self.eventTrigger.fireChance == 1f || UnityEngine.Random.value < self.eventTrigger.fireChance) &&
				self.eventTrigger.tEvent.type == TriggeredEvent.EventType.ShowProjectedImageEvent /*&&
				onh room */
                )
            {
				Debug.Log("ONH has already been visited!");
				self.Destroy();
				return;
            }
			orig(self);
		}

		public void PointerHook(On.OverseerHolograms.OverseerHologram.ForcedDirectionPointer.orig_ctor orig,
			OverseerHolograms.OverseerHologram.ForcedDirectionPointer Self,
			Overseer overseer, OverseerHolograms.OverseerHologram.Message message, Creature communicateWith, float importance)
			
        { 
			//this adds a new sprite when ReliableIggyDirection is set to the new enum
			//and also remove the default case sprite (which is loaded by the vanilla code)

			orig(Self, overseer, message, communicateWith, importance);

			if (Self.direction.data.symbol == ONHStuffEnums.Grapple)
			{
                string elementName = "GuidanceSlugcat";

                //remove the default
                Self.symbol = new OverseerHolograms.OverseerHologram.Symbol(Self, Self.totalSprites, elementName);
				(Self as OverseerHolograms.OverseerHologram)?.parts.Remove(Self.symbol);
				Self.totalSprites -= Self.symbol.totalSprites;

				//and load the new sprite
				elementName = "Kill_Tubeworm";
				Self.symbol = new OverseerHolograms.OverseerHologram.Symbol(Self, Self.totalSprites, elementName);
				(Self as OverseerHolograms.OverseerHologram)?.AddPart(Self.symbol);

			}
					
			
		}

		public void FireMusicHook(On.ActiveTriggerChecker.orig_FireEvent orig, ActiveTriggerChecker self)
		{
			if (self.eventTrigger.tEvent != null && !self.room.game.world.rainCycle.MusicAllowed &&
				((self.eventTrigger.fireChance == 1f || UnityEngine.Random.value < self.eventTrigger.fireChance) &&
				self.eventTrigger.tEvent.type == TriggeredEvent.EventType.MusicEvent &&
				self.room.game.manager.musicPlayer != null &&
				(!self.room.game.IsStorySession || !self.room.game.GetStorySession.RedIsOutOfCycles) &&
				(self.eventTrigger.tEvent as MusicEvent)?.songName == "Sweet Null"
				))
			{
				Debug.Log("Sweet Null playing despite Rain");
				self.room.game.manager.musicPlayer.GameRequestsSong(self.eventTrigger.tEvent as MusicEvent);
			}
			orig(self);
			return;

		}

		public void RainStopSongHook(On.Music.MusicPlayer.orig_RainRequestStopSong orig, Music.MusicPlayer self)
		{
			if (self.song != null && self.song.name != "Sweet Null")
			{
				orig(self);
			}
		}


		public static List<string> ONHRegions = new List<string>() {
			"FN",
			"CA",
			"CF",
			"VI",
			"MA",
			"OS",
			"ME",
			"AY",
			"VU"
		};



		public static class ONHStuffEnums
		{
			public static ReliableIggyDirection.ReliableIggyDirectionData.Symbol Grapple;
			public static void RegisterValues()
			{
				Grapple = new ReliableIggyDirection.ReliableIggyDirectionData.Symbol("Grapple", true);
            }

			public static void UnregisterValues()
			{
				if (Grapple != null) { Grapple.Unregister(); Grapple = null; }
			}
		}
	}

}
