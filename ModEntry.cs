using System;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SmartphoneAppStardewSocial
{
    public partial class ModEntry : Mod
    {
        private const string SmartphoneModId = "d5a1lamdtd.Smartphone";
        private const string AppId = "stardew_social";

        public static ModEntry Instance { get; private set; } = null!;
        public static ModConfig Config { get; private set; } = null!;
        public static IMonitor SMonitor = null!;
        public static IModHelper SHelper = null!;

        internal static ISmartPhoneApi? iSmartphoneApi;
        private Texture2D? appIcon;
        private Texture2D? appBackgroundTexture;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            SMonitor = this.Monitor;
            SHelper = helper;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            StardewConnectManager.Load();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            StardewConnectManager.Load();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ConfigMenu(this.ModManifest, this.Helper);

            iSmartphoneApi = this.Helper.ModRegistry.GetApi<ISmartPhoneApi>(SmartphoneModId);
            if (iSmartphoneApi == null)
            {
                this.Monitor.Log("Smartphone API is unavailable; Stardew Social app was not registered.", LogLevel.Warn);
                return;
            }

            this.LoadAssets();
            this.RegisterStardewSocialApp();
        }

        private void LoadAssets()
        {
            try
            {
                this.appIcon = this.Helper.ModContent.Load<Texture2D>("assets/app_social.png");
                this.appBackgroundTexture = this.Helper.ModContent.Load<Texture2D>("assets/background.png");
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to load Stardew Social assets: {ex.Message}", LogLevel.Error);
            }
        }

        private void RegisterStardewSocialApp()
        {
            if (iSmartphoneApi == null || this.appIcon == null)
                return;

            bool appRegistered = iSmartphoneApi.RegisterPhoneApp(
                ownerModId: this.ModManifest.UniqueID,
                appId: AppId,
                displayName: "StardewSocial",
                iconTexture: this.appIcon,
                onClick: this.OpenStardewSocialApp,
                closePhoneOnLaunch: true,
                sortOrder: 2,
                sourceRect: null,
                isVisible: () => Context.IsWorldReady,
                getBadgeCount: () =>
                {
                    try
                    {
                        return StardewConnectManager.GetActiveSocialNotificationCount();
                    }
                    catch
                    {
                        return 0;
                    }
                });

            if (!appRegistered)
            {
                this.Monitor.Log("Failed to register Stardew Social app.", LogLevel.Warn);
            }
        }

        private void OpenStardewSocialApp()
        {
            if (!Context.IsWorldReady || iSmartphoneApi == null)
                return;

            Game1.activeClickableMenu = new StardewSocialScreen(
                iSmartphoneApi,
                () => iSmartphoneApi.OpenPhoneHomeScreen());
        }
    }
}
