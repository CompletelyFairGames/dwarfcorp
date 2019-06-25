using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp.GameStates;
using DwarfCorp.Gui.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DwarfCorp
{
    public class DeconstructObjectTool : PlayerTool
    {
        [ToolFactory("DeconstructObjects")] // Todo: Normalize name
        private static PlayerTool _factory(WorldManager World)
        {
            return new DeconstructObjectTool(World);
        }

        private List<GameComponent> selectedBodies = new List<GameComponent>();

        public DeconstructObjectTool(WorldManager World)
        {
            this.World = World;
        }

        public override void OnBegin()
        {

        }

        public override void OnEnd()
        {
            World.UserInterface.VoxSelector.Clear();
        }

        public bool CanDestroy(GameComponent body)
        {
            return body.Tags.Any(tag => tag == "Deconstructable") && !body.IsReserved;
        }

        public override void OnBodiesSelected(List<GameComponent> bodies, InputManager.MouseButton button)
        {
            if (bodies.Count == 0)
                return;

            foreach (var body in bodies)
                if (body.Tags.Any(tag => tag == "Deconstructable"))
                {
                    if (body.IsReserved)
                    {
                        World.UserInterface.ShowToolPopup(string.Format("Can't destroy this {0}. It is being used.", body.Name));
                        continue;
                    }
                    body.Die();
                    SoundManager.PlaySound(ContentPaths.Audio.Oscar.sfx_gui_confirm_selection, body.Position, 0.5f);
                }
        }

        public override void OnMouseOver(IEnumerable<GameComponent> bodies)
        {
            DefaultOnMouseOver(bodies);

            foreach (var body in bodies)
                if (body.Tags.Contains("Deconstructable"))
                {
                    if (body.IsReserved)
                    {
                        World.UserInterface.ShowTooltip("Can't destroy this this " + body.Name + "\nIt is being used.");
                        continue;
                    }
                    World.UserInterface.ShowTooltip("Left click to destroy this " + body.Name);
                    body.SetVertexColorRecursive(Color.Red);
                }

            foreach (var body in selectedBodies)
                if (!bodies.Contains(body))
                    body.SetVertexColorRecursive(Color.White);

            selectedBodies = bodies.ToList();
        }

        public override void OnVoxelsSelected(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
            if (selectedBodies.Count != 0)
                return;

            var v = World.UserInterface.VoxSelector.VoxelUnderMouse;

            if (World.ZoneBuilder.IsBuildDesignation(v))
                World.ZoneBuilder.DestroyBuildDesignation(v);
            else if (World.ZoneBuilder.IsInZone(v))
            {
                var existingRoom = World.ZoneBuilder.GetMostLikelyZone(v);

                if (existingRoom != null)
                    World.UserInterface.Gui.ShowModalPopup(new Gui.Widgets.Confirm
                    {
                        Text = "Do you want to destroy this " + existingRoom.Type.Name + "?",
                        OnClose = (sender) => destroyDialog_OnClosed((sender as Gui.Widgets.Confirm).DialogResult, existingRoom)
                    });
            }
        }

        void destroyDialog_OnClosed(Gui.Widgets.Confirm.Result status, Zone room)
        {
            if (status == Gui.Widgets.Confirm.Result.OKAY)
                World.ZoneBuilder.DestroyZone(room);
        }

        public override void OnVoxelsDragged(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {

        }

        public override void Update(DwarfGame game, DwarfTime time)
        {
            if (World.UserInterface.IsMouseOverGui)
                World.UserInterface.SetMouse(World.UserInterface.MousePointer);
            else
                World.UserInterface.SetMouse(new Gui.MousePointer("mouse", 1, 9));

            World.UserInterface.VoxSelector.Enabled = true;
            World.UserInterface.VoxSelector.SelectionType = VoxelSelectionType.SelectFilled;
            World.UserInterface.VoxSelector.DrawBox = false;
            World.UserInterface.VoxSelector.DrawVoxel = false;
            World.UserInterface.BodySelector.Enabled = true;
            World.UserInterface.BodySelector.AllowRightClickSelection = true;
        }

        public override void Render3D(DwarfGame game, DwarfTime time)
        {
            if (selectedBodies.Count == 0)
            {
                var v = World.UserInterface.VoxSelector.VoxelUnderMouse;
                if (v.IsValid && !v.IsEmpty)
                {
                    var room = World.ZoneBuilder.GetRoomThatContainsVoxel(v);
                    if (room != null)
                        Drawer3D.DrawBox(room.GetBoundingBox(), GameSettings.Default.Colors.GetColor("Positive", Color.Green), 0.2f, true);
                }
            }
        }

        public override void Render2D(DwarfGame game, DwarfTime time)
        {
        }
    }
}
