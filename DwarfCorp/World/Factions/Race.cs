using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using DwarfCorp.GameStates;
using LibNoise;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DwarfCorp
{
    public class Race
    {
        public string Name { get; set; }
        public string Plural { get; set; }
        public List<string> CreatureTypes { get; set; }
        public List<string> NaturalEnemies { get; set; } 
        public bool IsIntelligent { get; set; }
        public bool IsNative { get; set; }
        public string FactionNameFile { get; set; }
        public string NameFile { get; set; }
        public string DiplomacyConversation = "World/default.conv";
        public Language Language  { get; set; }
        [JsonIgnore]
        public List<List<string>> FactionNameTemplates { get; set; }
        [JsonIgnore]
        public List<List<string>> NameTemplates { get; set; }

        public List<String> LikedResources { get; set; }
        public List<String> HatedResources { get; set; }
        public List<String> CommonResources { get; set; }
        public List<String> RareResources { get; set; } 

        public Dictionary<String, int> TradeGoods { get; set; }
        public List<String> Crafts { get; set; }
        public List<String> Encrustings { get; set; }
        public string TradeMusic { get; set; }

        public Dictionary<String, String> Biomes = new Dictionary<string, string>();
        public int Icon { get; set; }
        public string Posessive = "";
        public int NumFurniture = 0;

        public bool EatsPlants { get; set; }
        public bool EatsMeat { get; set; }
        public string BecomeWhenEvil { get; set; }
        public string BecomeWhenNotEvil { get; set; }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            FactionNameTemplates = TextGenerator.GetAtoms(FactionNameFile);
            NameTemplates = TextGenerator.GetAtoms(NameFile);
        }

        public ResourceSet GenerateTradeItems(WorldManager world)
        {
            var toReturn = new ResourceSet();
            String[] blacklistTags = { "Money", "Corpse" };

            foreach (var tags in TradeGoods)
            {
                int num = MathFunctions.RandInt(tags.Value, tags.Value + 4);

                var resources = Library.EnumerateResourceTypesWithTag(tags.Key);

                if (resources.Count() <= 0) continue;

                for (int i = 0; i < num; i++)
                {
                    MaybeNull<ResourceType> randResource = Datastructures.SelectRandom(resources);

                    if (!randResource.HasValue(out var res) || res.Tags.Any(blacklistTags.Contains))
                        continue;

                    if (tags.Key == "Craft")
                    {
                        var craftTag = Datastructures.SelectRandom(Crafts);
                        var availableCrafts = Library.EnumerateResourceTypesWithTag(craftTag);
                        if (Library.CreateTrinketResourceType(Datastructures.SelectRandom(availableCrafts).Name, MathFunctions.Rand(0.1f, 3.0f)).HasValue(out var trinket))
                        {

                            if (MathFunctions.RandEvent(0.3f) && Encrustings.Count > 0)
                                randResource = Library.CreateEncrustedTrinketResourceType(trinket.Name, Datastructures.SelectRandom(Library.EnumerateResourceTypesWithTag(Datastructures.SelectRandom(Encrustings))).Name);
                            else
                                randResource = trinket;
                        }
                    }

                    if (randResource.HasValue(out res))
                        toReturn.Add(new Resource(res.Name));
                }
            }

            for (int i = 0; i < NumFurniture; i++)
            {
                var randomObject = Datastructures.SelectRandom(Library.EnumerateCraftables().Where(type => type.Type == CraftItem.CraftType.Object && type.RequiredResources.All((tags) =>
                    TradeGoods.Any(good => good.Key == tags.Tag))));
                if (randomObject == null)
                    continue;

                toReturn.Add(new Resource(randomObject.ToResource(world, Posessive + " ").Name));
            }

            return toReturn;
        }
       
    }
}
