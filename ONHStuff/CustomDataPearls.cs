using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using System.Text.RegularExpressions;

namespace ONHStuff
{
    public static class CustomDataPearls
    {
        public static void Apply()
        {
            FindCustomPearlData();
            On.DataPearl.ApplyPalette += DataPearl_ApplyPalette;
            On.DataPearl.UniquePearlMainColor += DataPearl_UniquePearlMainColor;
            On.DataPearl.UniquePearlHighLightColor += DataPearl_UniquePearlHighLightColor;
            On.Conversation.DataPearlToConversation += Conversation_DataPearlToConversation;
            On.SLOracleBehaviorHasMark.MoonConversation.AddEvents += MoonConversation_AddEvents;
        }
        #region conversation
        private static void MoonConversation_AddEvents(On.SLOracleBehaviorHasMark.MoonConversation.orig_AddEvents orig, SLOracleBehaviorHasMark.MoonConversation self)
        {
            orig(self);

            foreach (KeyValuePair<DataPearl.AbstractDataPearl.DataPearlType, CustomDataPearlData> customPearl in CustomDataPearlsList)
            {
                if (self.id == customPearl.Value.conversationID)
                {
                    self.PearlIntro();
                    return;
                }
            }
        }

        private static Conversation.ID Conversation_DataPearlToConversation(On.Conversation.orig_DataPearlToConversation orig, DataPearl.AbstractDataPearl.DataPearlType type)
        {
            if (CustomDataPearlsList.TryGetValue(type, out CustomDataPearlData customPearl))
            { return customPearl.conversationID; }
            else { return orig(type); }
        }
        #endregion conversation

        #region color
        private static Color DataPearl_UniquePearlMainColor(On.DataPearl.orig_UniquePearlMainColor orig, DataPearl.AbstractDataPearl.DataPearlType pearlType)
        {
            if (CustomDataPearlsList.TryGetValue(pearlType, out CustomDataPearlData customPearl))
            { return customPearl.color; }
            else
            { return orig(pearlType); }
            
        }

        private static Color? DataPearl_UniquePearlHighLightColor(On.DataPearl.orig_UniquePearlHighLightColor orig, DataPearl.AbstractDataPearl.DataPearlType pearlType)
        {
            if (CustomDataPearlsList.TryGetValue(pearlType, out CustomDataPearlData customPearl))
            { return customPearl.highlightColor; }
            else
            { return orig(pearlType); }

        }

        private static void DataPearl_ApplyPalette(On.DataPearl.orig_ApplyPalette orig, DataPearl self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {

            DataPearl.AbstractDataPearl.DataPearlType pearlType = (self.abstractPhysicalObject as DataPearl.AbstractDataPearl).dataPearlType;
            orig(self, sLeaser, rCam, palette);
            
                if (CustomDataPearlsList.TryGetValue(pearlType, out CustomDataPearlData customPearl))
                {
                self.color = customPearl.color;
                self.highlightColor = customPearl.highlightColor;
                return;
            }
            
        }
        #endregion color

        #region customData
        public static void FindCustomPearlData()
        {
            CustomDataPearlsList = new Dictionary<DataPearl.AbstractDataPearl.DataPearlType, CustomDataPearlData>();
            foreach (string str in AssetManager.ListDirectory("CustomPearls"))
            {
                string fileName = Path.GetFileName(str);
                Debug.Log("Pearl text name is " + fileName);
                if (ExtEnumBase.TryParse(typeof(DataPearl.AbstractDataPearl.DataPearlType), Path.GetFileNameWithoutExtension(str), false, out ExtEnumBase result))
                {
                    DataPearl.AbstractDataPearl.DataPearlType type = (DataPearl.AbstractDataPearl.DataPearlType)result;
                    string[] array = Regex.Split(File.ReadAllText(AssetManager.ResolveFilePath("CustomPearls"+Path.DirectorySeparatorChar+fileName))," : ");
                    Color color = RWCustom.Custom.hexToColor(array[0]);
                    Color colorHighlight = RWCustom.Custom.hexToColor(array[1]);
                    string filePath = array[2];

                    CustomDataPearlData pearl = new CustomDataPearlData((DataPearl.AbstractDataPearl.DataPearlType)result, color, colorHighlight, filePath, ONHStuff.ONHStuffEnums.RegisterConversations(Path.GetFileNameWithoutExtension(str)));
                    CustomDataPearlsList.Add((DataPearl.AbstractDataPearl.DataPearlType)result, pearl);

                }
            }
        }



        public static Dictionary<DataPearl.AbstractDataPearl.DataPearlType, CustomDataPearlData> CustomDataPearlsList;

        public struct CustomDataPearlData
        {
            public Conversation.ID conversationID;
            public DataPearl.AbstractDataPearl.DataPearlType type;
            public Color color;
            public Color highlightColor;
            public string filePath;

            public CustomDataPearlData(DataPearl.AbstractDataPearl.DataPearlType type, Color color, Color highlightColor, string filePath, Conversation.ID conversationID)
            {
                this.type = type;
                this.color = color;
                this.highlightColor = highlightColor;
                this.filePath = filePath;
                this.conversationID = conversationID;
            }
        }
        #endregion customData
    }
}
