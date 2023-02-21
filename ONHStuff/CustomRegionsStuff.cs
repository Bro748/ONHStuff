using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ONHStuff
{
    public static class CustomRegionsStuff
    {
        public static void Apply()
        {
			RegisterNewLandscapes();

            On.SlugcatStats.getSlugcatStoryRegions += SlugcatStats_getSlugcatStoryRegions;
            On.Region.GetRegionLandscapeScene += Region_GetRegionLandscapeScene;
            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;
            On.Music.ProceduralMusic.ProceduralMusicInstruction.ctor += ProceduralMusicInstruction_ctor1;

        }

		private static void ProceduralMusicInstruction_ctor1(On.Music.ProceduralMusic.ProceduralMusicInstruction.orig_ctor orig, Music.ProceduralMusic.ProceduralMusicInstruction self, string name)
        {
			orig(self, name);
			string proceduralFolder = "Music" + Path.DirectorySeparatorChar + "Procedural" + Path.DirectorySeparatorChar;
			string path = AssetManager.ResolveFilePath("Music" + Path.DirectorySeparatorChar.ToString() + "Procedural" + Path.DirectorySeparatorChar.ToString() + name + ".txt");
			
			if (!File.Exists(path))
			{ return; }


			foreach (string line in File.ReadAllLines(path))
			{
				string[] array2 = Regex.Split(line, " : ");
				if (array2.Length != 0 && array2[0].Length > 4 && array2[0] == "Layer")
				{
					self.layers.Add(new Music.ProceduralMusic.ProceduralMusicInstruction.Layer(self.layers.Count));

					foreach (string str in Regex.Split(RWCustom.Custom.ValidateSpacedDelimiter(array2[1], ","), ", "))
					{
						if (str.Length == 0)
						{ continue; }

						foreach (Music.ProceduralMusic.ProceduralMusicInstruction.Track track in self.tracks)
						{
							string text2 = "";
							string a;
							if (str.Length > 3 && str.Substring(0, 1) == "{" && str.Contains("}"))
							{
								text2 = str.Substring(1, str.IndexOf("}") - 1);
								a = str.Substring(str.IndexOf("}") + 1);
							}
							else
							{ a = str; }

							if (a == track.name)
							{
								string[] subRegions = null;
								int dayNight = 0;
								bool mushroom = false;

								switch (text2)
								{
									case "":
											break;
									case "D":
										dayNight = 1;
										break;
									case "N":
										dayNight = 2;
										break;
									case "M":
										mushroom = true;
										break;
									default:
										subRegions = text2.Split(new char[]{'|'});
										break;
								}
								track.subRegions = subRegions;
								track.dayNight = dayNight;
								track.mushroom = mushroom;
								self.layers[self.layers.Count - 1].tracks.Add(track);
								break;
							}
						}
					}
						

				}
				else if (array2.Length != 0 && array2[0].Length > 0 && File.Exists(AssetManager.ResolveFilePath(proceduralFolder + array2[0] + ".ogg")))
				{
					self.tracks.Add(new Music.ProceduralMusic.ProceduralMusicInstruction.Track(array2[0]));
					string[] array4 = Regex.Split(array2[1], ", ");

					foreach (string str in array4)
					{
						if (str.Length == 0)
						{ continue; }

						if (str == "<PA>") 
						{ self.tracks[self.tracks.Count - 1].remainInPanicMode = true; }

						else
						{ self.tracks[self.tracks.Count - 1].tags.Add(str); }
					}
				}
			}
		}


		public static List<Menu.MenuScene.SceneID> customLandscapes;


		public static void RegisterNewLandscapes()
		{
			customLandscapes = new List<Menu.MenuScene.SceneID>();
			string path = AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar.ToString() + "regions.txt");
			if (File.Exists(path))
			{
				foreach (string text in File.ReadAllLines(path))
				{
					Debug.Log("text " + text);
					Menu.MenuScene.SceneID local = RegisterMenuScenes(text);
					if (local != null)
					{
						Debug.Log("Adding");
						customLandscapes.Add(local); }
				}
			}
		}

		public static Menu.MenuScene.SceneID RegisterMenuScenes(string name)
		{
			string sceneName = "Landscape - " + name;
			name = "Landscape_" + name;
			Debug.Log("new enum for " + name);
			if (ExtEnumBase.TryParse(typeof(Menu.MenuScene.SceneID), name, false, out _))
			{
				Debug.Log("already exists");
				return null;
			}
			else if (Directory.Exists(AssetManager.ResolveDirectory("Scenes" + Path.DirectorySeparatorChar.ToString() + sceneName)))
			{
				Debug.Log("success");
				return new Menu.MenuScene.SceneID(name, true);
			}
			else
			{ return null; }
		}


		private static void MenuScene_BuildScene(On.Menu.MenuScene.orig_BuildScene orig, Menu.MenuScene self)
        {
			orig(self);

			if ((self.sceneFolder == "" || self.sceneFolder == null) && customLandscapes.Contains(self.sceneID))
			{ BuildCustomScene2(self); }
        }

		public static void LoadPositions(Menu.MenuScene scene)
		{

			string path2 = AssetManager.ResolveFilePath(scene.sceneFolder + Path.DirectorySeparatorChar.ToString() + "positions_ims.txt");
			if (!File.Exists(path2) || !(scene is Menu.InteractiveMenuScene))
			{
				path2 = AssetManager.ResolveFilePath(scene.sceneFolder + Path.DirectorySeparatorChar.ToString() + "positions.txt");
			}
			if (File.Exists(path2))
			{
				string[] array3 = File.ReadAllLines(path2);
				int num3 = 0;
				while (num3 < array3.Length && num3 < scene.depthIllustrations.Count)
				{
					scene.depthIllustrations[num3].pos.x = float.Parse(Regex.Split(RWCustom.Custom.ValidateSpacedDelimiter(array3[num3], ","), ", ")[0], NumberStyles.Any, CultureInfo.InvariantCulture);
					scene.depthIllustrations[num3].pos.y = float.Parse(Regex.Split(RWCustom.Custom.ValidateSpacedDelimiter(array3[num3], ","), ", ")[1], NumberStyles.Any, CultureInfo.InvariantCulture);
					scene.depthIllustrations[num3].lastPos = scene.depthIllustrations[num3].pos;
					num3++;
				}
			}

			path2 = AssetManager.ResolveFilePath(scene.sceneFolder + Path.DirectorySeparatorChar.ToString() + "depths.txt");

			if (File.Exists(path2))
			{
				string[] array = File.ReadAllLines(path2);
				int num2 = 0;
				while (num2 < array.Length && num2 < scene.depthIllustrations.Count)
				{
					scene.depthIllustrations[num2].depth = float.Parse(array[num2]);
					num2++;
				}
			}

			
		}

		public static void BuildCustomScene2(Menu.MenuScene scene)
		{
			string[] array = scene.sceneID.ToString().Split('_');

			if (array.Length != 2)
				return;

			string fileName = $"{array[0]} - {array[1]}";
			string regionAcronym = array[1];
			scene.blurMin = -0.1f;
			scene.blurMax = 0.5f;

			scene.sceneFolder = "Scenes" + Path.DirectorySeparatorChar.ToString() + fileName;

			if (!Directory.Exists(AssetManager.ResolveDirectory(scene.sceneFolder)) || Directory.GetFiles(AssetManager.ResolveDirectory(scene.sceneFolder)).Length == 0)
			{ goto LandscapeTitle; }


			if (scene.flatMode)
			{
				scene.AddIllustration(new Menu.MenuIllustration(scene.menu, scene, scene.sceneFolder, fileName + " - Flat", new Vector2(683f, 384f), false, true));
				goto LandscapeTitle;
			}

			string path = scene.sceneFolder + Path.DirectorySeparatorChar.ToString() + fileName + ".txt";

			if (!File.Exists(AssetManager.ResolveFilePath(path)))
			{ goto LandscapeTitle; }

			foreach (string line in File.ReadAllLines(AssetManager.ResolveFilePath(path)))
			{
				string[] array2 = Regex.Split(line," : ");

				if (array2.Length == 0 || array2[0].Length == 0)
				{ continue; }

				if (array2[0] == "blurMin" && array2.Length >= 2)
				{ scene.blurMin = float.Parse(array2[1]); }

				else if (array2[0] == "blurMax" && array2.Length >= 2)
				{ scene.blurMax = float.Parse(array2[1]); }

				else if (array2[0] == "idleDepths" && array2.Length >= 2 && float.TryParse(array2[1], out float idleResult))
				{ (scene as Menu.InteractiveMenuScene)?.idleDepths.Add(idleResult); }

				else
				{
					if (File.Exists(AssetManager.ResolveFilePath(scene.sceneFolder + Path.DirectorySeparatorChar.ToString() + array2[0]+".png")))
					{
						scene.AddIllustration(new Menu.MenuDepthIllustration(
							scene.menu, scene, scene.sceneFolder, array2[0], new Vector2(0f, 0f), 
							(array2.Length >= 2 && int.TryParse(array2[1], out int r) ? r : 1),
							(array2.Length >= 3 && ExtEnumBase.TryParse(typeof(Menu.MenuDepthIllustration.MenuShader), array2[2], false, out ExtEnumBase result) ? (Menu.MenuDepthIllustration.MenuShader)result : Menu.MenuDepthIllustration.MenuShader.Normal)
							));
					}
				}
			}

			LoadPositions(scene);


		LandscapeTitle:;
			if (scene.menu.ID == ProcessManager.ProcessID.FastTravelScreen || scene.menu.ID == ProcessManager.ProcessID.RegionsOverviewScreen)
			{
				scene.AddIllustration(new Menu.MenuIllustration(scene.menu, scene, string.Empty, $"Title_{regionAcronym}_Shadow", new Vector2(0.01f, 0.01f), true, false));
				scene.AddIllustration(new Menu.MenuIllustration(scene.menu, scene, string.Empty, $"Title_{regionAcronym}", new Vector2(0.01f, 0.01f), true, false));
				scene.flatIllustrations[scene.flatIllustrations.Count - 1].sprite.shader = scene.menu.manager.rainWorld.Shaders["MenuText"];
			}

		}

		private static Menu.MenuScene.SceneID Region_GetRegionLandscapeScene(On.Region.orig_GetRegionLandscapeScene orig, string regionAcro)
        {

			Debug.Log("trying to load Landscape_" + regionAcro);
			if (ExtEnumBase.TryParse(typeof(Menu.MenuScene.SceneID), "Landscape_" + regionAcro, false, out ExtEnumBase result))
			{
				return (Menu.MenuScene.SceneID)result;
			}
			return orig(regionAcro);
        }

		private static string[] SlugcatStats_getSlugcatStoryRegions(On.SlugcatStats.orig_getSlugcatStoryRegions orig, SlugcatStats.Name i)
		{

			if (ModManager.MSC && i == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Saint)
			{
				return orig(i).Concat(new string[] {
			"FN"
			}).ToArray();
			}

			return orig(i).Concat(new string[] {
			"FN",
			"CA",
			"CF",
			"VI",
			"MA",
			"OS",
			"ME",
			"AY"
			}).ToArray();
		}

	}
}
