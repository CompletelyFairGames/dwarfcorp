using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DwarfCorp
{
    [Serializable]
    public class NewOverworldFile
    {
        public OverworldMetaData MetaData;
        public OverworldGenerationSettings Settings;
        public OverworldCell[,] OverworldMap;
        private GraphicsDevice Device {  get { return GameState.Game.GraphicsDevice; } }
        private int Width;
        private int Height;

        public NewOverworldFile()
        {
        }

        public NewOverworldFile(GraphicsDevice device, OverworldGenerationSettings Settings)
        {
            this.Settings = Settings;

            var worldFilePath = Settings.Name + System.IO.Path.DirectorySeparatorChar + "world.png";
            var metaFilePath = Settings.Name + System.IO.Path.DirectorySeparatorChar + "meta.txt";

            OverworldMap = Settings.Overworld.Map;
            MetaData = new OverworldMetaData(device, Settings);
            Width = Settings.Overworld.Map.GetLength(0);
            Height = Settings.Overworld.Map.GetLength(1);
        }
        
        public Texture2D CreateScreenshot(GraphicsDevice device, int width, int height, float seaLevel)
        {
            DwarfGame.LogSentryBreadcrumb("Saving", String.Format("User saving an overworld with size {0} x {1}", width, height), SharpRaven.Data.BreadcrumbLevel.Info);
            Texture2D toReturn = new Texture2D(device, width, height);
            var colorData = new Color[width * height];
            DwarfCorp.OverworldMap.TextureFromHeightMap("Height", OverworldMap, null, 1, colorData, seaLevel);
            DwarfCorp.OverworldMap.ShadeHeight(OverworldMap, 1, colorData);
            toReturn.SetData(colorData);
            return toReturn;
        }

        public Texture2D CreateSaveTexture(GraphicsDevice Device)
        {
            var r = new Texture2D(Device, OverworldMap.GetLength(0), OverworldMap.GetLength(1), false, SurfaceFormat.Color);
            var data = new Color[OverworldMap.GetLength(0) * OverworldMap.GetLength(1)];
            DwarfCorp.OverworldMap.GenerateSaveTexture(OverworldMap, data);
            r.SetData(data);
            return r;
        }

        public void LoadFromTexture(Texture2D Texture)
        {
            OverworldMap = new OverworldCell[Texture.Width, Texture.Height];
            var colorData = new Color[Texture.Width * Texture.Height];
            GameState.Game.GraphicsDevice.BlendState = BlendState.NonPremultiplied;
            Texture.GetData(colorData);
            DwarfCorp.OverworldMap.DecodeSaveTexture(OverworldMap, Texture.Width, Texture.Height, colorData);

            // Remap the saved voxel ids to the ids of the currently loaded voxels.
            if (MetaData.BiomeTypeMap != null)
            {
                // First build a replacement mapping.

                var newBiomeMap = BiomeLibrary.GetBiomeTypeMap();
                var newReverseMap = new Dictionary<String, int>();
                foreach (var mapping in newBiomeMap)
                    newReverseMap.Add(mapping.Value, mapping.Key);

                var replacementMap = new Dictionary<int, int>();
                foreach (var mapping in MetaData.BiomeTypeMap)
                {
                    if (newReverseMap.ContainsKey(mapping.Value))
                    {
                        var newId = newReverseMap[mapping.Value];
                        if (mapping.Key != newId)
                            replacementMap.Add(mapping.Key, newId);
                    }
                }

                // If there are no changes, skip the expensive iteration.
                if (replacementMap.Count != 0)
                {
                    for (var x = 0; x < OverworldMap.GetLength(0); ++x)
                        for (var y = 0; y < OverworldMap.GetLength(1); ++y)
                            if (replacementMap.ContainsKey(OverworldMap[x, y].Biome))
                                OverworldMap[x, y].Biome = (byte)replacementMap[OverworldMap[x, y].Biome];
                }
            }
        }

        public NewOverworldFile(string fileName)
        {
            ReadFile(fileName);
        }

        public static bool CheckCompatibility(string filePath)
        {
            try
            {
                var metaFilePath = filePath + global::System.IO.Path.DirectorySeparatorChar + "meta.txt";
                var metadata = FileUtils.LoadJsonFromAbsolutePath<OverworldMetaData>(metaFilePath);

                return Program.CompatibleVersions.Contains(metadata.Version);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetOverworldName(string filePath)
        {
            try
            {
                var metaFilePath = filePath + Path.DirectorySeparatorChar + "meta.txt";
                return FileUtils.LoadJsonFromAbsolutePath<OverworldMetaData>(metaFilePath).Settings.Name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        public bool ReadFile(string filePath)
        {
            var worldFilePath = filePath + global::System.IO.Path.DirectorySeparatorChar + "world.png";
            var metaFilePath = filePath + global::System.IO.Path.DirectorySeparatorChar + "meta.txt";

            MetaData = FileUtils.LoadJsonFromAbsolutePath<OverworldMetaData>(metaFilePath);

                foreach (var resource in MetaData.Resources)
                    if (!ResourceLibrary.Exists(resource.Name))
                        ResourceLibrary.Add(resource);

            var worldTexture = AssetManager.LoadUnbuiltTextureFromAbsolutePath(worldFilePath);

            if (worldTexture != null)
            {
                LoadFromTexture(worldTexture);
            }
            else
            {
                Console.Out.WriteLine("Failed to load overworld texture.");
                return false;
            }
            return true;
        }
        
        public bool WriteFile(string filePath)
        {
            var worldFilePath = filePath + global::System.IO.Path.DirectorySeparatorChar + "world.png";
            var metaFilePath = filePath + global::System.IO.Path.DirectorySeparatorChar + "meta.txt";

            // Write meta info
            MetaData.Version = Program.Version;
            FileUtils.SaveJSon(MetaData, metaFilePath);

            using (var texture = CreateSaveTexture(Device))
            using (var stream = new System.IO.FileStream(worldFilePath, System.IO.FileMode.Create))
                texture.SaveAsPng(stream, Width, Height);

            using (var texture = CreateScreenshot(Device, OverworldMap.GetLength(0), OverworldMap.GetLength(1), Settings.SeaLevel))
            using (var stream = new System.IO.FileStream(filePath + Path.DirectorySeparatorChar + "screenshot.png", System.IO.FileMode.Create))
                texture.SaveAsPng(stream, Width, Height);

                return true;
        }

        public OverworldGenerationSettings CreateSettings()
        {
            MetaData.Settings.Overworld = new OverworldMap(OverworldMap.GetLength(0), OverworldMap.GetLength(1))
            {
                Map = OverworldMap
            };

            return MetaData.Settings;
        }

        public static NewOverworldFile Load(String Path)
        {
            return new NewOverworldFile(Path);
        }
    }
}
