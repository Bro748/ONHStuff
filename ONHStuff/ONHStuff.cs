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
				//RevSupport.Apply();
				On.ModManager.RefreshModsLists += ForcePriority.ModManager_RefreshModsLists;
				On.RainWorld.OnModsInit += RainWorld_OnModsInit;
				//ReverseCat.Enable();
			}
			catch (Exception e) { Logger.LogError(e); }
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
				/* This is called when the mod is loaded. */
				//RevSupport.LoadBundle(self);
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
				On.Overseer.TryAddHologram += IggyShutUp;

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
			}
			catch (Exception e) { Logger.LogError(e); }
		}

        private int RainCycle_GetDesiredCycleLength(On.RainCycle.orig_GetDesiredCycleLength orig, RainCycle self)
        {
            if (!self.world.singleRoomWorld && self.world.game.IsStorySession && 
				(self.world.game.session as StoryGameSession).saveState.saveStateNumber == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Rivulet && 
				!self.world.game.GetStorySession.saveState.miscWorldSaveData.pebblesEnergyTaken)
            {
                bool useRegularCycle = false;
                foreach (string region in ONHRegions)
                {
                    if (self.world.region.name == region && region != "AY")
                    { useRegularCycle = true; }
                }

                if (useRegularCycle)
                {
                    return orig(self) * 2;
                }
            }
            return orig(self);
        }

        private void IggyShutUp(On.Overseer.orig_TryAddHologram orig, Overseer self, OverseerHolograms.OverseerHologram.Message message, Creature communicateWith, float importance)
        {
			if (Regex.Split(self.room.abstractRoom.name, "_")[0] == "SB" && 
				(message == OverseerHolograms.OverseerHologram.Message.Bats ||
				message == OverseerHolograms.OverseerHologram.Message.DangerousCreature ||
				message == OverseerHolograms.OverseerHologram.Message.Shelter ||
				message == OverseerHolograms.OverseerHologram.Message.FoodObject))
			{
					//Debug.Log("Shut up, Iggy!");
					return;
			}

			if (Regex.Split(self.room.abstractRoom.name, "_")[0] == "CC" &&
				message == OverseerHolograms.OverseerHologram.Message.ProgressionDirection &&
				ONHFolder() != null && File.Exists(LoadProjFile("ONHProgression", "txt"))
				)
				{
				return;
			}

			orig(self, message, communicateWith, importance);
		}

        private void StopReliableDirection(On.ReliableIggyDirection.orig_Update orig, ReliableIggyDirection self, bool eu)
        {
			if (self.data.symbol == ONHStuff.ONHStuffEnums.Grapple &&
				(self.room.game.Players.Any(ply => ply.realizedCreature?.grasps.Any(grasp => grasp?.grabbed is TubeWorm) ?? false ||
                ONHFolder() != null && File.Exists(LoadProjFile("ONHProgression", "txt"))
				)))
				
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
			if (ONHFolder() != null && self is WorldLoader wl_self && wl_self.playerCharacter != SlugcatStats.Name.Red &&
				!(wl_self.game.session as StoryGameSession).saveState.guideOverseerDead &&
				!(wl_self.game.session as StoryGameSession).saveState.miscWorldSaveData.playerGuideState.angryWithPlayer &&
				(wl_self.world.region.name == "SB" || wl_self.world.region.name == "LF" ||
				(wl_self.world.region.name == "CC" && (wl_self.game.session as StoryGameSession).saveState.miscWorldSaveData.SSaiConversationsHad < 1)) &&
				!File.Exists(LoadProjFile("ONHProgression", "txt")))

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
				wl_self.world.region.regionParams.playerGuideOverseerSpawnChance = 1;

			}
			orig(self, fresh);

		}
		public void HoloHook(On.Overseer.orig_TryAddHologram orig, Overseer self, 
		OverseerHolograms.OverseerHologram.Message message, Creature communicateWith, float importance)
		{
			if ((self.hologram.message == OverseerHolograms.OverseerHologram.Message.ForcedDirection))
			{ }
		
		}

		public void ONHProgressionSave(On.Room.orig_Loaded orig, Room self)
		{
			if (self.abstractRoom.name == "FN_A15")
			{
				string save = LoadProjFile("ONHProgression", "txt");
				if (!File.Exists(save))
				{ File.Create(LoadProjFile("ONHProgression", "txt")); }
			}
			orig(self);
		}

		public void StopProjectedImageHook(On.ActiveTriggerChecker.orig_FireEvent orig, ActiveTriggerChecker self)
		{
			if (self.eventTrigger.tEvent != null && 
				(self.eventTrigger.fireChance == 1f || UnityEngine.Random.value < self.eventTrigger.fireChance) &&
				self.eventTrigger.tEvent.type == TriggeredEvent.EventType.ShowProjectedImageEvent &&
				PackFromRoom(self.room.abstractRoom.name) == ONHFolder() &&
				File.Exists(LoadProjFile("ONHProgression","txt"))
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


			//but first, run the original code
			orig(Self, overseer, message, communicateWith, importance);

			//I don't know if these are totally necessary, but hey, safety first
			Self.direction = overseer.AI.communication.forcedDirectionToGive;
			string elementName = "GuidanceSlugcat";


			//If it matches the new enum...
			if (Self.direction.data.symbol == ONHStuff.ONHStuffEnums.Grapple)
			{
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


		/// <summary>
		/// returns the path to any PROJ files, relative to the pack folder of the current room
		/// </summary>

		public static string LoadProjFile(string fileName, string Type)
		{
			string packPath = ONHFolder();
			if (packPath == null)
			{ return null; }
			string	result = string.Concat(new object[]
				{
				Custom.RootFolderDirectory(),
				Path.DirectorySeparatorChar,
				"Projections",
				Path.DirectorySeparatorChar,
				fileName,
				"_PROJ.",
				Type
				});
			
			return result;
		}
		
		

		/// <summary>
		/// returns the pack name, or null if the room is vanilla
		/// </summary>
		public static string PackFromRoom(string roomname)
		{
			
			return null;

		}

		public static string ONHFolder()
		{
			return null;

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
