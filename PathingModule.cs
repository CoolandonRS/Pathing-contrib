﻿using Blish_HUD;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BhModule.Community.Pathing.Entity;
using BhModule.Community.Pathing.LocalHttp;
using BhModule.Community.Pathing.UI.Views;
using BhModule.Community.Pathing.Utility;
using Blish_HUD.Controls;
using Blish_HUD.Entities;
using Blish_HUD.Settings.UI.Views;
using Microsoft.Xna.Framework.Input;
using Blish_HUD.Graphics.UI;

namespace BhModule.Community.Pathing {
    [Export(typeof(Module))]
    public class PathingModule : Module {

        private static readonly Logger Logger = Logger.GetLogger<PathingModule>();

        #region Service Managers
        internal SettingsManager    SettingsManager    => this.ModuleParameters.SettingsManager;
        internal ContentsManager    ContentsManager    => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager      Gw2ApiManager      => this.ModuleParameters.Gw2ApiManager;
        #endregion

        internal static PathingModule Instance { get; private set; }

        public ModuleSettings ModuleSettings { get; private set; }

        private CornerIcon    _pathingIcon;
        private TabbedWindow2 _settingsWindow;

        private bool _packsLoading = false;

        private Tab _packSettingsTab;
        private Tab _mapSettingsTab;
        private Tab _keybindSettingsTab;
        private Tab _markerRepoTab;

        private HttpHost _apiHost;

        [ImportingConstructor]
        public PathingModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {
            ModuleSettings = new ModuleSettings(settings);
        }

        private IEnumerable<ContextMenuStripItem> GetPathingMenuItems() {
            if (this.PackInitiator != null) {
                foreach (var menuItem in this.PackInitiator.GetPackMenuItems()) {
                    menuItem.Enabled = menuItem.Enabled && !_packsLoading;

                    yield return menuItem;
                }
            }

            // Download Marker Packs
            var downloadMarkers = new ContextMenuStripItem() {
                Text = "Download Marker Packs", // TODO: Localize "Download Marker Packs"
            };

            downloadMarkers.Click += (_, _) => {
                _settingsWindow.SelectedTab = _markerRepoTab;
                _settingsWindow.Show();
            };

            // Open Settings
            var openSettings = new ContextMenuStripItem() {
                Text = "Pathing Module Settings" // TODO: Localize "Pathing Module Settings"
            };

            openSettings.Click += (_, _) => {
                if (_settingsWindow.SelectedTab == _markerRepoTab) {
                    _settingsWindow.SelectedTab = _packSettingsTab;

                    if (_settingsWindow.Visible) return;
                }

                _settingsWindow.ToggleWindow();
            };

            yield return downloadMarkers;
            yield return openSettings;
        }

        protected override void Initialize() {
            _pathingIcon = new CornerIcon() {
                IconName = Strings.General_UiName,
                Icon     = ContentsManager.GetTexture(@"png\pathing-icon.png"),
                Priority = Strings.General_UiName.GetHashCode()
            };

            _settingsWindow = new TabbedWindow2(
                                                ContentsManager.GetTexture(@"png\controls\156006.png"),
                                                new Rectangle(35, 36, 900,      640),
                                                new Rectangle(95, 42, 783 + 38, 592)
                                               ) {
                Title         = Strings.General_UiName,
                Parent        = GameService.Graphics.SpriteScreen,
                Location      = new Point(100, 100),
                // Fixes an issue in v0.11.2 where the window is clipped for some reason if window scale is != 100%
                ClipsBounds   = Program.OverlayVersion == new SemVer.Version(0, 11, 2) && GameService.Graphics.GetDpiScaleRatio() != 1f,
                Emblem        = this.ContentsManager.GetTexture(@"png\controls\1615829.png"),
                Id            = $"{this.Namespace}_SettingsWindow",
                SavesPosition = true,
            };

            _packSettingsTab    = new Tab(ContentsManager.GetTexture(@"png\156740+155150.png"), () => new SettingsView(ModuleSettings.PackSettings),    Strings.Window_MainSettingsTab);
            _mapSettingsTab     = new Tab(ContentsManager.GetTexture(@"png\157123+155150.png"), () => new SettingsView(ModuleSettings.MapSettings),     Strings.Window_MapSettingsTab);
            _keybindSettingsTab = new Tab(ContentsManager.GetTexture(@"png\156734+155150.png"), () => new SettingsView(ModuleSettings.KeyBindSettings), Strings.Window_KeyBindSettingsTab);
            _markerRepoTab      = new Tab(ContentsManager.GetTexture(@"png\156909.png"),        () => new PackRepoView(),                                Strings.Window_DownloadMarkerPacks);

            _settingsWindow.Tabs.Add(_packSettingsTab);
            _settingsWindow.Tabs.Add(_mapSettingsTab);
            _settingsWindow.Tabs.Add(_keybindSettingsTab);
            _settingsWindow.Tabs.Add(_markerRepoTab);

            _pathingIcon.Click += delegate {
                if (GameService.Input.Keyboard.ActiveModifiers.HasFlag(ModifierKeys.Ctrl)) {
                    ModuleSettings.GlobalPathablesEnabled.Value = !ModuleSettings.GlobalPathablesEnabled.Value;
                } else {
                    ShowPathingContextMenu();
                }
            };

            _pathingIcon.RightMouseButtonPressed += delegate {
                if (_pathingIcon.Enabled) {
                    ShowPathingContextMenu();
                }
            };
        }

        private void ShowPathingContextMenu() {
            var pathingContextMenuStrip = new ContextMenuStrip();
            pathingContextMenuStrip.AddMenuItems(GetPathingMenuItems());

            pathingContextMenuStrip.Show(_pathingIcon);
        }

        public PackInitiator                 PackInitiator  { get; private set; }
        public MarkerPackRepo.MarkerPackRepo MarkerPackRepo { get; private set; }

        private void UpdateModuleLoading(string loadingMessage) {
            _pathingIcon.LoadingMessage = loadingMessage;
            _packsLoading               = !string.IsNullOrWhiteSpace(loadingMessage);
        }

        public IProgress<string> GetModuleProgressHandler() {
            // TODO: Consider enforcing a source so that multiple items can be shown in the loading tooltip.
            return new Progress<string>(UpdateModuleLoading);
        }

        protected override async Task LoadAsync() {
            var sw = Stopwatch.StartNew();
#if SHOWINDEV
            _apiHost = new HttpHost(19903);
            _apiHost.Start();
#endif
            this.MarkerPackRepo = new MarkerPackRepo.MarkerPackRepo();
            this.MarkerPackRepo.Init();
            this.PackInitiator  = new PackInitiator(DirectoriesManager.GetFullDirectoryPath("markers"), ModuleSettings, GetModuleProgressHandler());
            await this.PackInitiator.Init();
            sw.Stop();
            Logger.Debug($"Took {sw.ElapsedMilliseconds} ms to complete loading Pathing module...");
        }

        public override IView GetSettingsView() {
            return new SettingsHintView((_settingsWindow.Show, this.PackInitiator));
        }

        protected override void Update(GameTime gameTime) {
            this.PackInitiator?.Update(gameTime);
        }

        private void UnloadPathingElements() {
            var entities = GameService.Graphics.World.Entities as IEntity[];

            foreach (var entity in entities) {
                if (entity is IPathingEntity) {
                    GameService.Graphics.World.RemoveEntity(entity);
                }
            }
        }

        protected override void Unload() {
            _apiHost?.Close();
            this.PackInitiator?.Unload();
            _pathingIcon?.Dispose();
            _settingsWindow?.Dispose();

            // Help to ensure that all pathing entities
            // are removed regardless of our current state.
            UnloadPathingElements();

            Instance = null;
        }

    }

}
