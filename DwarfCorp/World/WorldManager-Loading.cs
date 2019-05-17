using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using BloomPostprocess;
using DwarfCorp.Gui.Widgets;
using DwarfCorp.Tutorial;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using DwarfCorp.GameStates;
using System.Text;

namespace DwarfCorp
{
    public partial class WorldManager
    {
        public enum LoadingStatus
        {
            Loading,
            Success,
            Failure
        }

        public LoadingStatus LoadStatus = LoadingStatus.Loading;

        public Exception LoadingException = null;

        public Thread LoadingThread { get; set; }
        public Action<String> OnSetLoadingMessage = null;

        public void SetLoadingMessage(String Message)
        {
            if (OnSetLoadingMessage != null)
                OnSetLoadingMessage(Message);
        }

        public void StartLoad()
        {
            Renderer.Screenshots = new List<WorldRenderer.Screenshot>(); // Todo: ?? Why is this updated every single frame?
            Game.Graphics.PreferMultiSampling = GameSettings.Default.AntiAliasing > 1;
          
            try
            {
                Game.Graphics.ApplyChanges();
            }
            catch (NoSuitableGraphicsDeviceException exception)
            {
                Console.Error.WriteLine(exception.Message);
            }
            // This can occur when the user is plugging in a secondary monitor just as we enter this state.
            // ugh.
            catch (ArgumentException exception)
            {
                Console.Error.WriteLine(exception.Message);
            }

            LoadingThread = new Thread(LoadThreaded) { IsBackground = true };
            LoadingThread.Name = "Load";
            LoadingThread.Start();
        }

        private void LoadThreaded()
        {
            DwarfGame.ExitGame = false;
            // Ensure we're using the invariant culture.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            LoadStatus = LoadingStatus.Loading;
            SetLoadingMessage("Initializing ...");

            while (GraphicsDevice == null)
            {
                Thread.Sleep(100);
            }
            Thread.Sleep(1000);

#if !DEBUG
            try
            {
#endif
                bool fileExists = !string.IsNullOrEmpty(Settings.InstanceSettings.ExistingFile);

                SetLoadingMessage("Creating Sky...");

                Renderer.Sky = new SkyRenderer();

                #region Reading game file

                if (fileExists)
                {
                    SetLoadingMessage("Loading " + Settings.InstanceSettings.ExistingFile);

                    gameFile = SaveGame.LoadMetaFromDirectory(Settings.InstanceSettings.ExistingFile);
                    if (gameFile == null) throw new InvalidOperationException("Game File does not exist.");

                    if (gameFile.Metadata.Version != Program.Version && !Program.CompatibleVersions.Contains(gameFile.Metadata.Version))
                    {
                        throw new InvalidOperationException(String.Format("Game file is from version {0}. Compatible versions are {1}.", gameFile.Metadata.Version,
                            TextGenerator.GetListString(Program.CompatibleVersions)));
                    }

                    Renderer.Sky.TimeOfDay = gameFile.Metadata.TimeOfDay;
                Renderer.PersistentSettings = gameFile.Metadata.RendererSettings;
                    Time = gameFile.Metadata.Time;
                    WorldSizeInChunks = gameFile.Metadata.NumChunks;

                    if (gameFile.Metadata.OverworldFile != null && gameFile.Metadata.OverworldFile != "flat")
                    {
                        SetLoadingMessage("Loading Overworld " + gameFile.Metadata.OverworldFile);
                        var worldDirectory = Directory.CreateDirectory(DwarfGame.GetWorldDirectory() + Path.DirectorySeparatorChar + gameFile.Metadata.OverworldFile);
                        var overWorldFile = NewOverworldFile.Load(worldDirectory.FullName);
                    var instanceSettings = Settings.InstanceSettings;
                    Settings = overWorldFile.CreateSettings();
                    Settings.InstanceSettings = instanceSettings;
                    }
                    else
                    {
                        SetLoadingMessage("Generating flat world..");
                        DebugOverworlds.CreateUniformLand();
                    }
                }

                #endregion

                #region Initialize static data

                bool actionComplete = false;

                Game.DoLazyAction(new Action(() =>
                {
                    Renderer.InstanceRenderer = new InstanceRenderer();

                    Renderer.bloom = new BloomComponent(Game)
                    {
                        Settings = BloomSettings.PresetSettings[5]
                    };
                    Renderer.bloom.Initialize();

                    SoundManager.Content = Content;
                    if (PlanService != null)
                        PlanService.Restart();

                    MonsterSpawner = new MonsterSpawner(this);
                    EntityFactory.Initialize(this);
                }), () => { actionComplete = true; return true; });

                while (!actionComplete)
                {
                    Thread.Sleep(10);
                }

                #endregion


                SetLoadingMessage("Creating Planner ...");
                PlanService = new PlanService();

                SetLoadingMessage("Creating Shadows...");

                SetLoadingMessage("Creating Liquids ...");

            #region liquids

            Renderer.WaterRenderer = new WaterRenderer(GraphicsDevice);

                #endregion

                #region Load Components

                // Create updateable systems.
                foreach (var updateSystemFactory in AssetManager.EnumerateModHooks(typeof(UpdateSystemFactoryAttribute), typeof(EngineModule), new Type[] { typeof(WorldManager) }))
                    UpdateSystems.Add(updateSystemFactory.Invoke(null, new Object[] { this }) as EngineModule);

            if (fileExists)
            {
                ChunkManager = new ChunkManager(Content, this, WorldSizeInChunks);
                Splasher = new Splasher(ChunkManager);

                Renderer.ChunkRenderer = new ChunkRenderer(ChunkManager);

                SetLoadingMessage("Loading Terrain...");
                ChunkManager.LoadChunks(gameFile.LoadChunks(), ChunkManager);

                SetLoadingMessage("Loading Entities...");
                gameFile.LoadPlayData(Settings.InstanceSettings.ExistingFile, this);
                Renderer.Camera = gameFile.PlayData.Camera;
                Renderer.DesignationDrawer = gameFile.PlayData.Designations;

                if (gameFile.PlayData.Stats != null)
                    Stats = gameFile.PlayData.Stats;

                if (gameFile.PlayData.Resources != null)
                    foreach (var resource in gameFile.PlayData.Resources)
                        if (!ResourceLibrary.Exists(resource.Name))
                            ResourceLibrary.Add(resource);

                ComponentManager = new ComponentManager(gameFile.PlayData.Components, this);

                foreach (var component in gameFile.PlayData.Components.SaveableComponents)
                {
                    if (!ComponentManager.HasComponent(component.GlobalID) &&
                        ComponentManager.HasComponent(component.Parent.GlobalID))
                    {
                        // Logically impossible.
                        throw new InvalidOperationException("Component exists in save data but not in manager.");
                    }
                }

                ConversationMemory = gameFile.PlayData.ConversationMemory;

                Factions = gameFile.PlayData.Factions;
                ComponentManager.World = this;

                Renderer.Sky.TimeOfDay = gameFile.Metadata.TimeOfDay;
                Time = gameFile.Metadata.Time;

                // Restore native factions from deserialized data.
                Natives = new List<Faction>();

                foreach (var faction in Factions.Factions.Values)
                    if (faction.Race.IsNative && faction.Race.IsIntelligent && !faction.IsRaceFaction)
                        Natives.Add(faction);

                Diplomacy = gameFile.PlayData.Diplomacy;

                EventScheduler = new Events.Scheduler();

                TutorialManager = new Tutorial.TutorialManager();
                TutorialManager.SetFromSaveData(gameFile.PlayData.TutorialSaveData);

            }
            else
            {
                Time = new WorldTime();

                Renderer.Camera = new OrbitCamera(this, // Todo: Is setting the camera position and target redundant here?
                    new Vector3(VoxelConstants.ChunkSizeX,
                        WorldSizeInVoxels.Y - 1.0f,
                        VoxelConstants.ChunkSizeZ),
                    new Vector3(VoxelConstants.ChunkSizeX, WorldSizeInVoxels.Y - 1.0f,
                        VoxelConstants.ChunkSizeZ) +
                    Vector3.Up * 10.0f + Vector3.Backward * 10,
                    MathHelper.PiOver4, GraphicsDevice.Viewport.AspectRatio, 0.1f,
                    GameSettings.Default.VertexCullDistance);

                ChunkManager = new ChunkManager(Content, this, WorldSizeInChunks);
                Splasher = new Splasher(ChunkManager);


                Renderer.ChunkRenderer = new ChunkRenderer(ChunkManager);

                Renderer.Camera.Position = new Vector3(0, 10, 0) + new Vector3(WorldSizeInChunks.X * VoxelConstants.ChunkSizeX, 0, WorldSizeInChunks.Z * VoxelConstants.ChunkSizeZ) * 0.5f;
                Renderer.Camera.Target = new Vector3(0, 10, 1) + new Vector3(WorldSizeInChunks.X * VoxelConstants.ChunkSizeX, 0, WorldSizeInChunks.Z * VoxelConstants.ChunkSizeZ) * 0.5f;


                ComponentManager = new ComponentManager(this);
                ComponentManager.SetRootComponent(new GameComponent(ComponentManager, "root", Matrix.Identity, Vector3.Zero, Vector3.Zero));

                if (Natives == null) // Todo: Always true??
                {
                    FactionLibrary library = new FactionLibrary();
                    library.Initialize(this, Settings.Company);
                    Natives = new List<Faction>();
                    for (int i = 0; i < 10; i++)
                    {
                        Natives.Add(library.GenerateFaction(Settings, i, 10));
                    }

                }

                #region Prepare Factions

                foreach (Faction faction in Natives)
                {
                    faction.World = this;

                    if (faction.RoomBuilder == null)
                        faction.RoomBuilder = new RoomBuilder(faction, this);
                }

                Factions = new FactionLibrary();
                if (Natives != null && Natives.Count > 0)
                {
                    Factions.AddFactions(this, Natives);
                }

                Factions.Initialize(this, Settings.Company);
                Point playerOrigin = new Point((int)(Settings.InstanceSettings.Origin.X), (int)(Settings.InstanceSettings.Origin.Y));

                Factions.Factions["Player"].Center = playerOrigin;
                Factions.Factions["The Motherland"].Center = new Point(playerOrigin.X + 50, playerOrigin.Y + 50);

                #endregion

                Diplomacy = new Diplomacy(this);
                Diplomacy.Initialize(Time.CurrentDate);

                // Initialize goal manager here.
                EventScheduler = new Events.Scheduler();

                TutorialManager = new Tutorial.TutorialManager();
                TutorialManager.TutorialEnabled = !GameSettings.Default.TutorialDisabledGlobally;
                Tutorial("new game start");

                foreach (var item in Library.EnumerateCraftables())
                {
                    if (!String.IsNullOrEmpty(item.Tutorial))
                    {
                        TutorialManager.AddTutorial(item.Name, item.Tutorial, item.Icon);
                    }
                }
            }

                Renderer.Camera.World = this;
                //Drawer3D.Camera = Camera;


                #endregion

                SetLoadingMessage("Creating Particles ...");
                Game.DoLazyAction(new Action(() => ParticleManager = new ParticleManager(ComponentManager)));

                SetLoadingMessage("Creating GameMaster ...");
            PlayerFaction = Factions.Factions["Player"];
            TaskManager = new TaskManager();
            TaskManager.Faction = PlayerFaction;
                Master = new GameMaster(this);

                if (gameFile == null)
                {
                    Game.LogSentryBreadcrumb("Loading", "Started new game without an existing file.");
                    if (Settings.Overworld.Map == null)
                        throw new InvalidProgramException("Tried to start game with an empty overworld. This should not happen.");

                    var generatorSettings = new Generation.GeneratorSettings(MathFunctions.Random.Next(), 0.02f, Settings)
                    {
                        WorldSizeInChunks = WorldSizeInChunks,
                        SetLoadingMessage = SetLoadingMessage,
                        World = this
                    };

                    SetLoadingMessage("Generating Chunks...");
                    Generation.Generator.Generate(Settings.InstanceSettings.SpawnRect, ChunkManager, this, generatorSettings, SetLoadingMessage);
                    CreateInitialEmbarkment(generatorSettings);
                    ChunkManager.NeedsMinimapUpdate = true;
                    ChunkManager.RecalculateBounds();
                }

                if (gameFile != null)
                {
                    Game.LogSentryBreadcrumb("Loading", "Started new game with an existing file.");
                    if (gameFile.PlayData.Tasks != null)
                    {
                        TaskManager = gameFile.PlayData.Tasks;
                        TaskManager.Faction = PlayerFaction;
                    }
                }

            if (PlayerFaction.Economy.Information == null)
                throw new InvalidProgramException();

                if (MathFunctions.RandEvent(0.01f))
                    SetLoadingMessage("Reticulating Splines...");

                ChunkManager.StartThreads();
                SetLoadingMessage("Presimulating ...");
                ShowingWorld = false;
                OnLoadedEvent();
                Thread.Sleep(1000);

                ShowingWorld = true;

                SetLoadingMessage("Complete.");

                // GameFile is no longer needed.
                gameFile = null;
                LoadStatus = LoadingStatus.Success;
#if !DEBUG
            }
            catch (Exception exception)
            {
                Game.CaptureException(exception);
                LoadingException = exception;
                LoadStatus = LoadingStatus.Failure;
                ProgramData.WriteExceptionLog(exception);
            }
#endif
        }

    }
}
