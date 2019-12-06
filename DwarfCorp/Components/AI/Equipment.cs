using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace DwarfCorp
{
    public class Equipment : GameComponent
    {
        public Equipment() { }
        public Equipment(ComponentManager Manager) : base(Manager) { }

        public Dictionary<String, Resource> EquippedItems = new Dictionary<String, Resource>();

        public MaybeNull<Resource> GetItemInSlot(String Slot)
        {
            if (EquippedItems.ContainsKey(Slot))
                return EquippedItems[Slot];
            return null;
        }

        public void EquipItem(Resource Item)
        {
            if (!Item.Equipable) return;
            if (String.IsNullOrEmpty(Item.Equipment_Slot)) return;

            UnequipItem(Item.Equipment_Slot);

            EquippedItems[Item.Equipment_Slot] = Item;

            if (!String.IsNullOrEmpty(Item.Equipment_LayerName) && GetRoot().GetComponent<DwarfSprites.LayeredCharacterSprite>().HasValue(out var sprite))
                if (DwarfSprites.LayerLibrary.FindLayerWithName(Item.Equipment_LayerType, Item.Equipment_LayerName).HasValue(out var layer))
                {
                    if (DwarfSprites.LayerLibrary.FindPalette(Item.Equipment_Palette).HasValue(out var palette))
                        sprite.AddLayer(layer, palette);
                    else
                        sprite.AddLayer(layer, DwarfSprites.LayerLibrary.BasePalette);
                }
        }

        public void UnequipItem(String Slot)
        {
            if (GetItemInSlot(Slot).HasValue(out var existing) 
                && !String.IsNullOrEmpty(existing.Equipment_LayerName) 
                && GetRoot().GetComponent< DwarfSprites.LayeredCharacterSprite>().HasValue(out var sprite))
                sprite.RemoveLayer(existing.Equipment_LayerType);

            EquippedItems.Remove(Slot);
        }

        public void UnequipItem(Resource Item)
        {
            if (GetItemInSlot(Item.Equipment_Slot).HasValue(out var res) && Object.ReferenceEquals(res, Item))
                UnequipItem(Item.Equipment_Slot);
        }
    }
}
