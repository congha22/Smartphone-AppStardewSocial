using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
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
        private Dictionary<string, Texture2D> themedIcons = new(StringComparer.OrdinalIgnoreCase);
        private Texture2D? appBackgroundTexture;
        public static bool modReady = false;

        // NPC characteristic data loaded from assets
        public static Dictionary<string, string> NpcCharacteristicsLong = new();
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

            // Register multiplayer transfer handlers
            helper.Events.GameLoop.UpdateTicked += TransferManager.OnUpdateTicked;
            helper.Events.Multiplayer.ModMessageReceived += TransferManager.OnModMessageReceived;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (!modReady)
                return;
            StardewConnectManager.Load();
            StardewConnectManager.EnforcePhotoSharedRetention();
            UpdatePostInteractionLimit();
            UpdateSocialPostLimit();

            IndoorAreasByLocation = SHelper.Data.ReadJsonFile<Dictionary<string, Dictionary<string, AreaData>>>("assets/area_indoor.json")
                        ?? new Dictionary<string, Dictionary<string, AreaData>>();

            OutdoorAreasByLocation = SHelper.Data.ReadJsonFile<Dictionary<string, Dictionary<string, AreaData>>>("assets/area_outdoor.json")
                        ?? new Dictionary<string, Dictionary<string, AreaData>>();

            try
            {
                var long_ = this.Helper.ModContent.Load<Dictionary<string, string>>("assets/npc_characteristics_long.json");
                if (long_ != null) NpcCharacteristicsLong = long_;
            }
            catch { }

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

            areaTags = new Dictionary<string, Dictionary<string, AreaData>>(IndoorAreasByLocation);
            foreach (var kvp in OutdoorAreasByLocation)
            {
                areaTags[kvp.Key] = kvp.Value;
            }

            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                TransferManager.InitiateFarmhandSync();
            }
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            if (!modReady)
                return;
            StardewConnectManager.Reset();
            ClearPendingRandomNpcSocialPost();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (!modReady)
                return;
            UpdatePostInteractionLimit();
            UpdateSocialPostLimit();
            CleanPhotoTempFolder();

            if (ShouldRunSocialSimulation())
                PrepareDailyRandomNpcSocialPosts();
        }

        private void CleanPhotoTempFolder()
        {
            try
            {
                string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                if (string.IsNullOrWhiteSpace(saveFolder)) return;

                string photoTempDir = Path.Combine(SHelper.DirectoryPath, "userdata", saveFolder, "photo_temp");
                if (Directory.Exists(photoTempDir))
                {
                    foreach (string file in Directory.GetFiles(photoTempDir))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to clean photo_temp folder: {ex.Message}", LogLevel.Error);
            }
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (!modReady)
                return;
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

            iSmartphoneApi.ContactableNpcsChanged += UpdateContactableNpcs;

            this.LoadAssets();
            this.RegisterStardewSocialApp();
        }

        private void LoadAssets()
        {
            try
            {
                this.themedIcons.Clear();
                try
                {
                    this.themedIcons["default"] = this.Helper.ModContent.Load<Texture2D>("assets/default/1x1.png");
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Failed to load default theme icon: {ex.Message}", LogLevel.Error);
                }

                try
                {
                    this.themedIcons["v2"] = this.Helper.ModContent.Load<Texture2D>("assets/v2/1x1.png");
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Failed to load v2 theme icon: {ex.Message}", LogLevel.Error);
                }

                string iconPath = Config.AppIconStyle == "v2" ? "assets/v2/1x1.png" : "assets/default/1x1.png";
                this.appIcon = this.Helper.ModContent.Load<Texture2D>(iconPath);
                this.appBackgroundTexture = this.Helper.ModContent.Load<Texture2D>("assets/background.png");
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed to load Stardew Social assets: {ex.Message}", LogLevel.Error);
            }
        }

        private void RegisterStardewSocialApp()
        {
            if (iSmartphoneApi == null || this.themedIcons.Count == 0)
                return;

            string compositeId = $"{this.ModManifest.UniqueID}::{AppId}";

            bool appRegistered = iSmartphoneApi.RegisterPhoneApp(
                ownerModId: this.ModManifest.UniqueID,
                appId: AppId,
                displayName: "Stardew Social",
                onClick: this.OpenStardewSocialApp,
                closePhoneOnLaunch: true,
                sourceRect: null,
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
                },
                supportedSizes: new AppSize[] { AppSize.Size1x1, AppSize.Size2x1, AppSize.Size2x2 },
                onDrawWidget: (b, rect, size) => SocialWidget.Draw(b, rect, size, this.appIcon ?? this.themedIcons["default"], this.appBackgroundTexture, iSmartphoneApi, compositeId),
                themedIconTextures: this.themedIcons
            );

            if (!appRegistered)
            {
                this.Monitor.Log("Failed to register Stardew Social app.", LogLevel.Error);
                return;
            }

            // Register Contact Action Card for Stardew Social
            List<IContactActionCardButton> buttons = new List<IContactActionCardButton>
            {
                new ContactActionCardButton
                {
                    Text = "Profile",
                    BackgroundColor = Color.HotPink,
                    TextColor = Color.White,
                    OnClick = (npcName) =>
                    {
                        if (iSmartphoneApi == null) return;
                        Game1.activeClickableMenu = new StardewSocialScreen(
                            iSmartphoneApi,
                            npcName,
                            () => iSmartphoneApi.OpenPhoneHomeScreen()
                        );
                    }
                }
            };
            iSmartphoneApi.RegisterContactActionCard(this.ModManifest.UniqueID, "Stardew Social", buttons);

            modReady = true;
        }

        private void OpenStardewSocialApp()
        {
            if (!Context.IsWorldReady || iSmartphoneApi == null)
                return;

            Game1.activeClickableMenu = new StardewSocialScreen(
                iSmartphoneApi,
                () => iSmartphoneApi.OpenPhoneHomeScreen());
        }

        /// <summary>Exposes the API for other mods to interact with the Stardew Social app.</summary>
        public override object? GetApi()
        {
            return new StardewSocialApi();
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

    public class StardewSocialApi : IStardewSocialApi
    {
        public void CreateDraftPost(string? text = null, string? taggedNpc = null, string? imagePath = null, string? postTags = null)
        {
            if (!Context.IsWorldReady || ModEntry.iSmartphoneApi == null)
                return;

            Game1.activeClickableMenu = new StardewSocialScreen(
                ModEntry.iSmartphoneApi,
                () => ModEntry.iSmartphoneApi.OpenPhoneHomeScreen(),
                text,
                taggedNpc,
                imagePath,
                postTags
            );
        }

        public void CreateNpcPost(string authorName, string? taggedNpc = null, string? text = null, string? imagePath = null, string? postTags = null)
        {
            // Allowed on host only
            if (!Context.IsWorldReady || !Context.IsMainPlayer)
                return;

            StardewConnectManager.AddNpcPost(authorName, text, imagePath, taggedNpc, postTags);
        }

        public void OpenProfile(string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName))
                return;

            if (!Context.IsWorldReady || ModEntry.iSmartphoneApi == null)
                return;

            Game1.activeClickableMenu = new StardewSocialScreen(
                ModEntry.iSmartphoneApi,
                actorName,
                () => ModEntry.iSmartphoneApi.OpenPhoneHomeScreen()
            );
        }
    }
}
