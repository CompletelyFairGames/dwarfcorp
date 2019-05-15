using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;

namespace DwarfCorp
{
    public class GameMaster
    {
        public OrbitCamera CameraController { get; set; }

        [JsonIgnore]
        public VoxelSelector VoxSelector { get; set; }

        [JsonIgnore]
        public BodySelector BodySelector { get; set; }

        public Faction Faction { get; set; } // Todo: Move to WorldManager

        #region  Player tool management

        [JsonIgnore]
        public Dictionary<String, PlayerTool> Tools { get; set; }

        [JsonIgnore]
        public PlayerTool CurrentTool { get { return Tools[CurrentToolMode]; } }

        public String CurrentToolMode = "SelectUnits";

        public void ChangeTool(String NewTool)
        {
            if (NewTool != "SelectUnits")
            {
                SelectedObjects = new List<GameComponent>();
            }

            // Todo: Should probably clean up existing tool even if they are the same tool.
            Tools[NewTool].OnBegin();
            if (CurrentToolMode != NewTool)
                CurrentTool.OnEnd();
            CurrentToolMode = NewTool;
        }

        #endregion


        [JsonIgnore]
        public List<CreatureAI> SelectedMinions { get { return Faction.SelectedMinions; } set { Faction.SelectedMinions = value; } }

        [JsonIgnore]
        public List<GameComponent> SelectedObjects = new List<GameComponent>();

        [JsonIgnore]
        public WorldManager World { get; set; }

        public TaskManager TaskManager { get; set; }

        private bool sliceDownheld = false;
        private bool sliceUpheld = false;
        private Timer sliceDownTimer = new Timer(0.5f, true, Timer.TimerMode.Real);
        private Timer sliceUpTimer = new Timer(0.5f, true, Timer.TimerMode.Real);

        public Scripting.Gambling GamblingState = new Scripting.Gambling(); // Todo: Belongs in WorldManager?

        [OnDeserialized]
        protected void OnDeserialized(StreamingContext context)
        {
            World = (WorldManager)(context.Context);
            Initialize(GameState.Game, World.ComponentManager, World.ChunkManager, World.Renderer.Camera, GameState.Game.GraphicsDevice);
            World.Master = this;
            TaskManager.Faction = Faction;
        }

        public GameMaster()
        {
        }

        public GameMaster(Faction faction, DwarfGame game, ComponentManager components, ChunkManager chunks, OrbitCamera camera, GraphicsDevice graphics)
        {
            TaskManager = new TaskManager();
            TaskManager.Faction = faction;

            World = components.World;
            Faction = faction;
            Initialize(game, components, chunks, camera, graphics);
            VoxSelector.Selected += OnSelected;
            VoxSelector.Dragged += OnDrag;
            BodySelector.Selected += OnBodiesSelected;
            BodySelector.MouseOver += OnMouseOver;
            World.Master = this;
            World.Time.NewDay += Time_NewDay;
            rememberedViewValue = World.WorldSizeInVoxels.Y;
        }

        public void Initialize(DwarfGame game, ComponentManager components, ChunkManager chunks, OrbitCamera camera, GraphicsDevice graphics)
        {
            CameraController = camera;
            VoxSelector = new VoxelSelector(World);
            BodySelector = new BodySelector(CameraController, GameState.Game.GraphicsDevice, components);
            SelectedMinions = new List<CreatureAI>();

            CreateTools();
        }

        public void Destroy()
        {
            VoxSelector.Selected -= OnSelected;
            VoxSelector.Dragged -= OnDrag;
            BodySelector.Selected -= OnBodiesSelected;
            BodySelector.MouseOver -= OnMouseOver;
            World.Time.NewDay -= Time_NewDay;
            Tools["God"].Destroy();
            Tools["SelectUnits"].Destroy();
            Tools.Clear();
            Faction = null;
            VoxSelector = null;
            BodySelector = null;
        }

        // Todo: Give these the mod hook treatment.
        private void CreateTools()
        {
            Tools = new Dictionary<String, PlayerTool>();

            foreach (var method in AssetManager.EnumerateModHooks(typeof(ToolFactoryAttribute), typeof(PlayerTool), new Type[]
            {
                typeof(GameMaster)
            }))
            {
                var attribute = method.GetCustomAttributes(false).FirstOrDefault(a => a is ToolFactoryAttribute) as ToolFactoryAttribute;
                if (attribute == null) continue;
                Tools[attribute.Name] = method.Invoke(null, new Object[] { this }) as PlayerTool;
            }
        }

        void Time_NewDay(DateTime time)
        {
            Faction.PayEmployees();
        }

        public void OnMouseOver(IEnumerable<GameComponent> bodies)
        {
            CurrentTool.OnMouseOver(bodies);
        }

        public void OnBodiesSelected(List<GameComponent> bodies, InputManager.MouseButton button)
        {
            CurrentTool.OnBodiesSelected(bodies, button);
            if (CurrentToolMode == "SelectUnits")
                SelectedObjects = bodies;
        }

        public void OnDrag(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
            CurrentTool.OnVoxelsDragged(voxels, button);
        }

        public void OnSelected(List<VoxelHandle> voxels, InputManager.MouseButton button)
        {
            CurrentTool.OnVoxelsSelected(voxels, button);
        }

        // Todo: Belongs in... uh WorldManager maybe?
        public bool AreAllEmployeesAsleep()
        {
            return (Faction.Minions.Count > 0) && Faction.Minions.All(minion => !minion.Active || ((!minion.Stats.Species.CanSleep || minion.Creature.Stats.IsAsleep) && !minion.IsDead));
        }

        public void Render2D(DwarfGame game, DwarfTime time)
        {
            CurrentTool.Render2D(game, time);
            
            foreach (CreatureAI creature in Faction.SelectedMinions)
            {
                foreach (Task task in creature.Tasks)
                    if (task.IsFeasible(creature.Creature) == Task.Feasibility.Feasible)
                        task.Render(time);

                if (creature.CurrentTask != null)
                    creature.CurrentTask.Render(time);
            }

            DwarfGame.SpriteBatch.Begin();
            BodySelector.Render(DwarfGame.SpriteBatch);
            DwarfGame.SpriteBatch.End();
        }

        public void Render3D(DwarfGame game, DwarfTime time)
        {
            CurrentTool.Render3D(game, time);
            VoxSelector.Render();

            foreach (var obj in SelectedObjects)
                if (obj.IsVisible && !obj.IsDead)
                    Drawer3D.DrawBox(obj.GetBoundingBox(), Color.White, 0.01f, true);
        }

        private Timer orphanedTaskRateLimiter = new Timer(10.0f, false, Timer.TimerMode.Real);
        private Timer checkFoodTimer = new Timer(60.0f, false, Timer.TimerMode.Real);

        // This hack exists to find orphaned tasks not assigned to any dwarf, and to then
        // put them on the task list.
        // Todo: With the new task pool, how often is this used?
        // Todo: Belongs in... WorldManager?
        public void UpdateOrphanedTasks()
        {
            orphanedTaskRateLimiter.Update(DwarfTime.LastTime);
            if (orphanedTaskRateLimiter.HasTriggered)
            {
                List<Task> orphanedTasks = new List<Task>();
                
                foreach (var ent in Faction.Designations.EnumerateEntityDesignations())
                {
                    if (ent.Type == DesignationType.Attack)
                    {
                        var task = new KillEntityTask(ent.Body, KillEntityTask.KillType.Attack);
                        if (!TaskManager.HasTask(task) &&
                            !Faction.Minions.Any(minion => minion.Tasks.Contains(task)))
                        {
                            orphanedTasks.Add(task);
                        }
                    }
                    
                    
                    else if (ent.Type == DesignationType.Craft)
                    {
                        var task = new CraftItemTask(ent.Tag as CraftDesignation);
                        if (!TaskManager.HasTask(task) &&
                            !Faction.Minions.Any(minion => minion.Tasks.Contains(task)))
                        {
                            orphanedTasks.Add(task);
                        }
                    }
                    
                    // TODO ... other entity task types
                }

                if (orphanedTasks.Count > 0)
                    //TaskManager.AssignTasksGreedy(orphanedTasks, Faction.Minions);
                    TaskManager.AddTasks(orphanedTasks);
            }
        }

        public void Update(DwarfGame game, DwarfTime time)
        {
            // Todo: All input handling should be in one spot. PlayState!
            GamblingState.Update(time);
            TaskManager.Update(Faction.Minions);
            CurrentTool.Update(game, time);
            Faction.RoomBuilder.Update();
            UpdateOrphanedTasks();

            if (World.Paused)
                CameraController.LastWheel = Mouse.GetState().ScrollWheelValue;

            UpdateInput(game, time);

            if (Faction.Minions.Any(m => m.IsDead && m.TriggersMourning))
            {
                foreach (CreatureAI minion in Faction.Minions)
                {
                    minion.Creature.AddThought(Thought.ThoughtType.FriendDied);

                    if (!minion.IsDead) continue;

                    World.MakeAnnouncement(String.Format("{0} ({1}) died!", minion.Stats.FullName, minion.Stats.CurrentClass.Name));
                    SoundManager.PlaySound(ContentPaths.Audio.Oscar.sfx_gui_negative_generic);
                    World.Tutorial("death");
                }

            }

            Faction.Minions.RemoveAll(m => m.IsDead);

            HandlePosessedDwarf();

            if (sliceDownheld)
            {
                sliceDownTimer.Update(time);
                if (sliceDownTimer.HasTriggered)
                {
                    World.Renderer.SetMaxViewingLevel(World.Renderer.PersistentSettings.MaxViewingLevel - 1);
                    sliceDownTimer.Reset(sliceDownTimer.TargetTimeSeconds * 0.6f);
                }
            }
            else if (sliceUpheld)
            {
                sliceUpTimer.Update(time);
                if (sliceUpTimer.HasTriggered)
                {
                    World.Renderer.SetMaxViewingLevel(World.Renderer.PersistentSettings.MaxViewingLevel + 1);
                    sliceUpTimer.Reset(sliceUpTimer.TargetTimeSeconds * 0.6f);
                }
            }

            checkFoodTimer.Update(time);
            if (checkFoodTimer.HasTriggered)
            {
                var food = Faction.CountResourcesWithTag(Resource.ResourceTags.Edible);
                if (food == 0)
                {
                    Faction.World.MakeAnnouncement("We're out of food!", null, () => { return Faction.CountResourcesWithTag(Resource.ResourceTags.Edible) == 0; });
                }
            }

            foreach(var minion in Faction.Minions)
            {
                if (minion == null) throw new InvalidProgramException("Null minion?");
                if (minion.Stats == null) throw new InvalidProgramException("Minion has null status?");

                if (minion.Stats.IsAsleep)
                    continue;

                if (minion.CurrentTask == null)
                    continue;

                if (minion.Stats.IsTaskAllowed(Task.TaskCategory.Dig))
                    minion.Movement.SetCan(MoveType.Dig, GameSettings.Default.AllowAutoDigging);

                minion.ResetPositionConstraint();
            }
        }

        public void HandlePosessedDwarf()
        {
            // Don't attempt any control if the user is trying to type intoa focus item.
            if (World.Gui.FocusItem != null && !World.Gui.FocusItem.IsAnyParentTransparent() && !World.Gui.FocusItem.IsAnyParentHidden())
            {
                return;
            }
            KeyboardState keyState = Keyboard.GetState();
            if (SelectedMinions.Count != 1)
            {
                CameraController.FollowAutoTarget = false;
                CameraController.EnableControl = true;
                foreach (var creature in Faction.Minions)
                {
                    creature.IsPosessed = false;
                }
                return;
            }

            var dwarf = SelectedMinions[0];
            if (!dwarf.IsPosessed)
            {
                CameraController.FollowAutoTarget = false;
                CameraController.EnableControl = true;
                return;
            }
            CameraController.EnableControl = false;
            CameraController.AutoTarget = dwarf.Position;
            CameraController.FollowAutoTarget = true;

            if (dwarf.Velocity.Length() > 0.1)
            {
                var above = VoxelHelpers.FindFirstVoxelAbove(new VoxelHandle(World.ChunkManager, GlobalVoxelCoordinate.FromVector3(dwarf.Position)));

                if (above.IsValid)
                    World.Renderer.SetMaxViewingLevel(above.Coordinate.Y);
                else
                    World.Renderer.SetMaxViewingLevel(World.WorldSizeInVoxels.Y);
            }

            Vector3 forward = CameraController.GetForwardVector();
            Vector3 right = CameraController.GetRightVector();
            Vector3 desiredVelocity = Vector3.Zero;
            bool hadCommand = false;
            bool jumpCommand = false;
            if (keyState.IsKeyDown(ControlSettings.Mappings.Forward) || keyState.IsKeyDown(Keys.Up))
            {
                hadCommand = true;
                desiredVelocity += forward * 10;
            }

            if (keyState.IsKeyDown(ControlSettings.Mappings.Back) || keyState.IsKeyDown(Keys.Down))
            {
                hadCommand = true;
                desiredVelocity -= forward * 10;
            }

            if (keyState.IsKeyDown(ControlSettings.Mappings.Right) || keyState.IsKeyDown(Keys.Right))
            {
                hadCommand = true;
                desiredVelocity += right * 10;
            }

            if (keyState.IsKeyDown(ControlSettings.Mappings.Left) || keyState.IsKeyDown(Keys.Left))
            {
                hadCommand = true;
                desiredVelocity -= right * 10;
            }

            if (keyState.IsKeyDown(ControlSettings.Mappings.Jump))
            {
                jumpCommand = true;
                hadCommand = true;
            }

            if (hadCommand)
            {
                dwarf.CancelCurrentTask();
                dwarf.TryMoveVelocity(desiredVelocity, jumpCommand);
            }
            else if (dwarf.CurrentTask == null)
            {
                if (dwarf.Creature.IsOnGround)
                {
                    if (dwarf.Physics.Velocity.LengthSquared() < 1)
                    {
                        dwarf.Creature.CurrentCharacterMode = DwarfCorp.CharacterMode.Idle;
                    }
                    dwarf.Physics.Velocity = new Vector3(dwarf.Physics.Velocity.X * 0.9f, dwarf.Physics.Velocity.Y,
                        dwarf.Physics.Velocity.Z * 0.9f);
                    dwarf.TryMoveVelocity(Vector3.Zero, false);
                }
            }

        }

        #region input


        public bool IsCameraRotationModeActive()
        {
            return KeyManager.RotationEnabled(World.Renderer.Camera);

        }


        public void UpdateMouse(MouseState mouseState, KeyboardState keyState, DwarfGame game, DwarfTime time)
        {
            if (KeyManager.RotationEnabled(World.Renderer.Camera))
            {
                World.SetMouse(null);
            }

        }

        public void UpdateInput(DwarfGame game, DwarfTime time)
        {
            KeyboardState keyState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();


            if (!World.IsMouseOverGui)
            {
                UpdateMouse(Mouse.GetState(), Keyboard.GetState(), game, time);
                VoxSelector.Update();
                BodySelector.Update();
            }

        }

        public bool OnKeyPressed(Keys key)
        {
            if (key == ControlSettings.Mappings.SliceUp)
            {
                if (!sliceUpheld)
                {
                    sliceUpheld = true;
                    World.Tutorial("unslice");
                    sliceUpTimer.Reset(0.5f);
                    World.Renderer.SetMaxViewingLevel(World.Renderer.PersistentSettings.MaxViewingLevel + 1);
                    return true;
                }
            }
            else if (key == ControlSettings.Mappings.SliceDown)
            {
                if (!sliceDownheld)
                {
                    World.Tutorial("unslice");
                    sliceDownheld = true;
                    sliceDownTimer.Reset(0.5f);
                    World.Renderer.SetMaxViewingLevel(World.Renderer.PersistentSettings.MaxViewingLevel - 1);
                    return true;
                }
            }
            return false;
        }
        private int rememberedViewValue = 0;

        public bool OnKeyReleased(Keys key)
        {
            KeyboardState keys = Keyboard.GetState();
            if (key == ControlSettings.Mappings.SliceUp)
            {
                sliceUpheld = false;
                return true;
            }

            else if (key == ControlSettings.Mappings.SliceDown)
            {
                sliceDownheld = false;
                return true;
            }
            else if (key == ControlSettings.Mappings.SliceSelected)
            {
                if (keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl))
                {
                    World.Renderer.SetMaxViewingLevel(rememberedViewValue);
                    return true;
                }
                else if (VoxSelector.VoxelUnderMouse.IsValid)
                {
                    World.Tutorial("unslice");
                    World.Renderer.SetMaxViewingLevel(VoxSelector.VoxelUnderMouse.Coordinate.Y + 1);
                    Drawer3D.DrawBox(VoxSelector.VoxelUnderMouse.GetBoundingBox(), Color.White, 0.15f, true);
                    return true;
                }
            }
            else if (key == ControlSettings.Mappings.Unslice)
            {
                rememberedViewValue = World.Renderer.PersistentSettings.MaxViewingLevel;
                World.Renderer.SetMaxViewingLevel(World.WorldSizeInVoxels.Y);
                return true;
            }
            return false;
        }

        #endregion
    }
}
