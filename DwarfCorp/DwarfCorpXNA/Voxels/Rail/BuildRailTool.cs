// BuildTool.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using DwarfCorp.Gui.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Input;

namespace DwarfCorp.Rail
{
    public class BuildRailTool : PlayerTool
    {
        public Rail.JunctionPattern Pattern;
        private List<RailEntity> PreviewBodies = new List<RailEntity>();
        private Faction Faction;
        private bool RightPressed = false;
        private bool LeftPressed = false;
        public List<ResourceAmount> SelectedResources;
        public bool GodModeSwitch = false;

        private static CraftItem RailCraftItem = new CraftItem
        {
            Description = "Rail.",
            RequiredResources = new List<Quantitiy<Resource.ResourceTags>>
                        {
                            new Quantitiy<Resource.ResourceTags>(Resource.ResourceTags.Rail, 1)
                        },
            Icon = new Gui.TileReference("resources", 38),
            BaseCraftTime = 10,
            Prerequisites = new List<CraftItem.CraftPrereq>() { CraftItem.CraftPrereq.OnGround },
            CraftLocation = "",
            Name = "Rail",
            Type = CraftItem.CraftType.Object,
            AddToOwnedPool = false,
            Moveable = false            
        };

        public BuildRailTool(GameMaster Player)
        {
            this.Player = Player;
            this.Faction = Player.Faction;
        }

        public override void OnVoxelsSelected(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
            if (button == InputManager.MouseButton.Left)
                if (RailHelper.CanPlace(Player, PreviewBodies))
                {
                    RailHelper.Place(Player, PreviewBodies, GodModeSwitch);
                    PreviewBodies.Clear();
                    CreatePreviewBodies(Player.World.ComponentManager, Player.VoxSelector.VoxelUnderMouse);
                }
        }

        public override void OnBegin()
        {
            System.Diagnostics.Debug.Assert(Pattern != null);
            System.Diagnostics.Debug.Assert(SelectedResources != null);
            GodModeSwitch = false;
            CreatePreviewBodies(Faction.World.ComponentManager, new VoxelHandle(Faction.World.ChunkManager.ChunkData, new GlobalVoxelCoordinate(0, 0, 0)));
        }

        public override void OnEnd()
        {
            foreach (var body in PreviewBodies)
                body.Delete();
            PreviewBodies.Clear();
            Pattern = null;
            SelectedResources = null;
            Player.VoxSelector.DrawVoxel = true;
            Player.VoxSelector.DrawBox = true;
        }

        public override void OnMouseOver(IEnumerable<Body> bodies)
        {

        }

        private void CreatePreviewBodies(ComponentManager ComponentManager, VoxelHandle Location)
        {
            foreach (var body in PreviewBodies)
                body.Delete();

            PreviewBodies.Clear();
            foreach (var piece in Pattern.Pieces)
                PreviewBodies.Add(RailHelper.CreatePreviewBody(ComponentManager, Location, piece));

            // Todo: Add CraftDetails component.
        }

        public override void Update(DwarfGame game, DwarfTime time)
        {
            if (Player.IsCameraRotationModeActive())
            {
                Player.VoxSelector.Enabled = false;
                Player.World.SetMouse(null);
                Player.BodySelector.Enabled = false;
                return;
            }

            Player.VoxSelector.Enabled = true;
            Player.BodySelector.Enabled = false;
            Player.VoxSelector.DrawBox = false;
            Player.VoxSelector.DrawVoxel = false;

            if (Player.World.IsMouseOverGui)
                Player.World.SetMouse(Player.World.MousePointer);
            else
                Player.World.SetMouse(new Gui.MousePointer("mouse", 1, 4));

            KeyboardState state = Keyboard.GetState();
            bool leftKey = state.IsKeyDown(ControlSettings.Mappings.RotateObjectLeft);
            bool rightKey = state.IsKeyDown(ControlSettings.Mappings.RotateObjectRight);
            if (LeftPressed && !leftKey)
                Pattern = Pattern.Rotate(Rail.PieceOrientation.East);
            if (RightPressed && !rightKey)
                Pattern = Pattern.Rotate(Rail.PieceOrientation.West);
            LeftPressed = leftKey;
            RightPressed = rightKey;

            var tint = Color.White;

            for (var i = 0; i < PreviewBodies.Count && i < Pattern.Pieces.Count; ++i)
                PreviewBodies[i].UpdatePiece(Pattern.Pieces[i], Player.VoxSelector.VoxelUnderMouse);

            if (RailHelper.CanPlace(Player, PreviewBodies))
                    tint = Color.Green;
                else
                    tint = Color.Red;
        
            foreach (var body in PreviewBodies)
                body.SetTintRecursive(tint);
        }

        public override void Render(DwarfGame game, GraphicsDevice graphics, DwarfTime time)
        {

        }

        public override void OnBodiesSelected(List<Body> bodies, InputManager.MouseButton button)
        {

        }

        public override void OnVoxelsDragged(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
        }
    }
}