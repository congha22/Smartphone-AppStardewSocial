using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace SmartphoneAppStardewSocial
{
    public enum AppIconType
    {
        Notification,
        AppStore,
        Camera,
        Photo,
        Setting,
        Calendar
    }

    public interface ISmartPhoneApi
    {
        /// ======================================
        /// API to register custom apps or app groups on the smartphone home screen.
        /// ======================================

        /// <summary>
        /// Registers a custom app icon on the smartphone home screen.
        /// </summary>
        bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null
        );

        /// <summary>
        /// Unregisters a previously registered custom app.
        /// </summary>
        bool UnregisterPhoneApp(string ownerModId, string appId);

        /// <summary>
        /// Registers a grouped app on the smartphone home screen.
        /// </summary>
        bool RegisterPhoneAppGroup(
            string ownerModId,
            string groupId,
            string displayName,
            Texture2D iconTexture,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null
        );

        /// <summary>
        /// Unregisters a previously registered app group and all of its items.
        /// </summary>
        bool UnregisterPhoneAppGroup(string ownerModId, string groupId);

        /// <summary>
        /// Registers or updates an item inside a phone app group.
        /// </summary>
        bool RegisterPhoneAppGroupItem(
            string ownerModId,
            string groupId,
            string itemId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null
        );

        /// <summary>
        /// Unregisters a previously registered app group item.
        /// </summary>
        bool UnregisterPhoneAppGroupItem(string ownerModId, string groupId, string itemId);

        /// ======================================
        /// API to control smartphone screen navigation.
        /// ======================================

        /// <summary>
        /// Opens the smartphone home (landing) screen for the current player.
        /// </summary>
        bool OpenPhoneHomeScreen();

        /// <summary>
        /// Opens a registered app-group screen for the current player.
        /// </summary>
        bool OpenPhoneAppGroup(string ownerModId, string groupId);

        /// ======================================
        /// API to register custom quick actions in App Messenger chat menu.
        /// ======================================

        /// <summary>
        /// Registers a custom quick-action icon in the App Messenger chat quick-action menu.
        /// </summary>
        bool RegisterChatQuickActionButton(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch = false,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            List<string>? npcNames = null
        );

        /// <summary>
        /// Unregisters a previously registered App Messenger chat quick-action icon.
        /// </summary>
        bool UnregisterChatQuickActionButton(string ownerModId, string actionId);

        /// ======================================
        /// API for interacting with the smartphone messenger app
        /// ======================================

        List<string> GetPhoneNpcList(string playerId = "");
        void SendSmartphoneMessageFromNPC(string npcName, string message, string playerId = "");
        void SendSmartphoneMessageFromPlayer(string npcName, string message, string playerId = "");
        void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "");

        /// ======================================
        /// API for interacting with the StardewConnect social media app
        /// ======================================

        string? CreateStardewConnectPostFromNpc(string npcName, string postText, string attachedImageFile = "");
        bool AddStardewConnectCommentFromNpc(string postId, string npcName, string commentText);
        bool SetStardewConnectPostLikedFromNpc(string postId, string npcName, bool liked);

        /// ======================================
        /// API for capturing and accessing photos
        /// ======================================

        string CaptureNpcPhoto(GameLocation targetLocation, Vector2 captureCenter, NPC npc = null, bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null, float zoomLevel = 1f, int? captureTimeOfDay = null, string saveLocation = null);
        List<string> GetPlayerPhotoNames();
        Texture2D GetPlayerPhotoTexture(string photoName);
        string GetPlayerPhotoMetadata(string photoName);
        Dictionary<string, Texture2D> GetAllPlayerPhotoTextures();

        /// ======================================
        /// API to get player profile
        /// ======================================

        string GetPlayerProfile();
        string GetPlayerBirthDate();
        string GetPlayerBirthSeason();
        string GetPlayerAge();

        /// ======================================
        /// API to get phone appearance settings (theme and size).
        /// ======================================

        bool IsSmallPhoneSize();
        float GetPhoneUiScale();
        int GetPhoneFrameWidth();
        int GetPhoneFrameHeight();
        (int offsetX, int offsetY) GetPhoneContentOffset();
        Texture2D? GetPhoneFrameTexture();
        Texture2D? GetPhoneBackgroundTexture();
        (int x, int y) GetPhonePosition();
        void SetPhonePosition(int x, int y);
        bool HandlePhoneAppBottomNavClick(int x, int y, int phoneX, int phoneY, Action? onBack = null);

        /// ======================================
        /// API for Unlimited Event Expansion only
        /// ======================================

        bool RegisterUnlimitedEvent(
            string ownerModId,
            string eventType,
            Action<string> triggerEvent,
            int minimumHeartLevel = 0,
            string toolDescription = ""
        );

        bool UnregisterUnlimitedEvent(string ownerModId, string eventType);
        Texture2D? GetAppTexture(AppIconType appIconType);
        void RetrievePhotos(int limit, bool getTexture, bool getMetadata, Action<string> onComplete);
    }

    public class SelectedPhotoResult
    {
        public string AbsolutePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public byte[]? TextureData { get; set; }
    }
}
