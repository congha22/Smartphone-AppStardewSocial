using System;
using System.Collections.Generic;
using System.IO;
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

        // NPC characteristic data loaded from assets
        public static Dictionary<string, string> NpcCharacteristicsShort = new();
        public static Dictionary<string, string> NpcCharacteristicsMinimal = new();

        // Indoor/Outdoor Areas for NPC photo scene setup
        public static Dictionary<string, Dictionary<string, AreaData>> IndoorAreasByLocation = new();
        public static Dictionary<string, Dictionary<string, AreaData>> OutdoorAreasByLocation = new();
        public static Dictionary<string, Dictionary<string, AreaData>> areaTags = new();

        // Image tags from photos captured via the framework API (key = filename, value = tag string)
        private static readonly Dictionary<string, string> NpcPhotoTags = new(StringComparer.OrdinalIgnoreCase);

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            SMonitor = this.Monitor;
            SHelper = helper;
            Config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            StardewConnectManager.Load();
            ResetDailyAiUsageLimit();
            RefreshIgnoredNpcList();
            UpdatePostInteractionLimit();
            UpdateSocialPostLimit();

            IndoorAreasByLocation = SHelper.Data.ReadJsonFile<Dictionary<string, Dictionary<string, AreaData>>>("assets/area_indoor.json")
                        ?? new Dictionary<string, Dictionary<string, AreaData>>();

            OutdoorAreasByLocation = SHelper.Data.ReadJsonFile<Dictionary<string, Dictionary<string, AreaData>>>("assets/area_outdoor.json")
                        ?? new Dictionary<string, Dictionary<string, AreaData>>();

            areaTags = new Dictionary<string, Dictionary<string, AreaData>>(IndoorAreasByLocation);
            foreach (var kvp in OutdoorAreasByLocation)
            {
                areaTags[kvp.Key] = kvp.Value;
            }
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            StardewConnectManager.Load();
            ClearPendingRandomNpcSocialPost();
            ClearQueuedAiActions();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            ResetDailyAiUsageLimit();
            RefreshIgnoredNpcList();
            UpdatePostInteractionLimit();
            UpdateSocialPostLimit();

            if (ShouldRunSocialSimulation())
                PrepareDailyRandomNpcSocialPosts();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            HandleAiUsageTimeChanged(e.NewTime);
            HandleAiModelSettingTimeChanged(e.NewTime);

            if (ShouldRunSocialSimulation())
            {
                HandleScheduledSocialPostsOnTimeChanged(e.NewTime);

                if (GetSocialCommentEngagementIntervalFromConfig().Contains(e.NewTime))
                    QueueRandomNpcCommentEngagement();

                if (GetSocialLikeEngagementIntervalFromConfig().Contains(e.NewTime))
                    QueueRandomNpcLikeEngagement();
            }
        }

        /// <summary>Social simulation only runs for the host (or single-player).</summary>
        private static bool ShouldRunSocialSimulation()
        {
            return !StardewModdingAPI.Context.IsMultiplayer || StardewModdingAPI.Context.IsMainPlayer;
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

            try
            {
                var short_ = this.Helper.ModContent.Load<Dictionary<string, string>>("assets/npc_characteristics_short.json");
                if (short_ != null) NpcCharacteristicsShort = short_;
            }
            catch { }

            try
            {
                var minimal_ = this.Helper.ModContent.Load<Dictionary<string, string>>("assets/npc_characteristics_minimal.json");
                if (minimal_ != null) NpcCharacteristicsMinimal = minimal_;
            }
            catch { }
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

        /// <summary>
        /// Stores a tag for an NPC photo file captured via the Smartphone framework API.
        /// Called after a successful CaptureNpcPhoto API call so the tag can be retrieved later.
        /// </summary>
        public static void RegisterNpcPhotoTag(string filePath, string tag)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string fileName = Path.GetFileName(filePath.Trim());
            if (!string.IsNullOrWhiteSpace(fileName))
                NpcPhotoTags[fileName] = tag ?? string.Empty;
        }

        /// <summary>Retrieves the tag string for an NPC photo file, or empty string if unknown.</summary>
        public static string GetNpcPhotoTag(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return string.Empty;

            string fileName = Path.GetFileName(filePath.Trim());
            if (!string.IsNullOrWhiteSpace(fileName) && NpcPhotoTags.TryGetValue(fileName, out string? tag))
                return tag ?? string.Empty;

            return string.Empty;
        }
    }
}
