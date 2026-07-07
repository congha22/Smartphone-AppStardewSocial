using System;
using System.IO;
using System.Linq;
using StardewModdingAPI;


namespace SmartphoneAppStardewSocial
{
    public partial class ModEntry
    {
        public static void ConfigMenu(IManifest ModManifest, IModHelper Helper)
        {
            var configMenu = Helper.ModRegistry.GetApi<SmartphoneAppStardewSocial.Data.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            string[] aiModelValues =
            {
                ModConfig.OpenAIModel_51,
                ModConfig.OpenAIModel_5mini,
                ModConfig.OpenAIModel_5nano,
                ModConfig.OpenAIModel_54mini,
                ModConfig.OpenAIModel_54nano,
                ModConfig.GeminiModel_35Flash,
                ModConfig.GeminiModel_31FlashLite,
                ModConfig.GeminiModel_3FlashPreview
            };

            string[] postPerDayValues =
            {
                ModConfig.PostPerDayLow,
                ModConfig.PostPerDayMedium,
                ModConfig.PostPerDayHigh
            };

            string[] characteristicValues =
            {
                ModConfig.CharacteristicModeMinimal,
                ModConfig.CharacteristicModeShort,
                ModConfig.CharacteristicModeLong
            };

            static string EnsureAllowedValue(string? value, string fallback, string[] allowedValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return fallback;
                return Array.IndexOf(allowedValues, value) >= 0 ? value : fallback;
            }

            configMenu.Register(
                mod: ModManifest,
                reset: () =>
                {
                    Config = new ModConfig();
                },
                save: () =>
                {
                    Helper.WriteConfig(Config);
                    try
                    {
                        Instance.LoadAssets();
                        Instance.RegisterStardewSocialApp();
                        if (Context.IsWorldReady)
                        {
                            Instance.LoadNpcCharacteristics();
                            UpdateContactableNpcsFromConfig();
                        }
                    }
                    catch { }
                }
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.quickSetup"));

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.language.name"),
                tooltip: () => Helper.Translation.Get("config.language.tooltip"),
                getValue: () => string.IsNullOrWhiteSpace(Config.Language) ? "English" : Config.Language,
                setValue: value => Config.Language = string.IsNullOrWhiteSpace(value) ? "English" : value.Trim()
            );

            string npcProfilePath = Path.Combine(Helper.DirectoryPath, "npc_profile");
            string[] themeOptions = Directory.Exists(npcProfilePath)
                ? Directory.GetDirectories(npcProfilePath).Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name)).ToArray()!
                : new[] { "vanilla" };
            if (themeOptions.Length == 0)
            {
                themeOptions = new[] { "vanilla" };
            }

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.theme.name"),
                tooltip: () => Helper.Translation.Get("config.theme.tooltip"),
                getValue: () => string.IsNullOrWhiteSpace(Config.NpcProfileTheme) ? "vanilla" : Config.NpcProfileTheme,
                setValue: value => Config.NpcProfileTheme = string.IsNullOrWhiteSpace(value) ? "vanilla" : value.Trim(),
                allowedValues: themeOptions
            );


            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.showSocialImageTags.name"),
                tooltip: () => Helper.Translation.Get("config.showSocialImageTags.tooltip"),
                getValue: () => Config.ShowSocialImageTags,
                setValue: value => Config.ShowSocialImageTags = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.showUnreadComment.name"),
                tooltip: () => Helper.Translation.Get("config.showUnreadComment.tooltip"),
                getValue: () => Config.ShowUnreadComment,
                setValue: value => Config.ShowUnreadComment = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.allowedNpc.name"),
                tooltip: () => Helper.Translation.Get("config.allowedNpc.tooltip"),
                getValue: () => Config.AllowedNpc,
                setValue: value => Config.AllowedNpc = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.friendshipRequirement.name"),
                tooltip: () => Helper.Translation.Get("config.friendshipRequirement.tooltip"),
                getValue: () => Config.FriendshipRequirement,
                setValue: value => Config.FriendshipRequirement = value,
                allowedValues: new[] { "Meet", "Friend" }
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "ai-settings",
                text: () => Helper.Translation.Get("config.aiSettings.name"),
                tooltip: () => Helper.Translation.Get("config.aiSettings.tooltip")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "storage-limits",
                text: () => Helper.Translation.Get("config.storageLimits.name"),
                tooltip: () => Helper.Translation.Get("config.storageLimits.tooltip")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "advance-settings",
                text: () => Helper.Translation.Get("config.advanceSettings.name"),
                tooltip: () => Helper.Translation.Get("config.advanceSettings.tooltip")
            );

            // AI Settings page
            configMenu.AddPage(mod: ModManifest, pageId: "ai-settings", pageTitle: () => Helper.Translation.Get("config.aiSettings.name"));
            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.aiSettings.description")
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.key.name"),
                tooltip: () => Helper.Translation.Get("config.key.tooltip"),
                getValue: () => Config.Key,
                setValue: value => Config.Key = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.model.name"),
                tooltip: () => Helper.Translation.Get("config.model.tooltip"),
                getValue: () => EnsureAllowedValue(Config.Model, ModConfig.OpenAIModel_54mini, aiModelValues),
                setValue: value => Config.Model = value,
                allowedValues: aiModelValues
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.activity.name"),
                tooltip: () => Helper.Translation.Get("config.activity.tooltip"),
                getValue: () => EnsureAllowedValue(Config.PostPerDay, ModConfig.PostPerDayLow, postPerDayValues),
                setValue: value => Config.PostPerDay = value,
                allowedValues: postPerDayValues
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.characteristic.name"),
                tooltip: () => Helper.Translation.Get("config.characteristic.tooltip"),
                getValue: () => EnsureAllowedValue(Config.CharacteristicMode, ModConfig.CharacteristicModeShort, characteristicValues),
                setValue: value => Config.CharacteristicMode = value,
                allowedValues: characteristicValues
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.betterQualityComment.name"),
                tooltip: () => Helper.Translation.Get("config.betterQualityComment.tooltip"),
                getValue: () => Config.BetterQualityComment,
                setValue: value => Config.BetterQualityComment = value
            );

            // Storage and Limits page
            configMenu.AddPage(mod: ModManifest, pageId: "storage-limits", pageTitle: () => Helper.Translation.Get("config.storageLimits.name"));

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.maxPosts.name"),
                tooltip: () => Helper.Translation.Get("config.maxPosts.tooltip"),
                getValue: () => Config.MaxStardewConnectPosts,
                setValue: value => Config.MaxStardewConnectPosts = Math.Clamp(value, 10, 1000),
                min: 10,
                max: 1000
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.maxPhotos.name"),
                tooltip: () => Helper.Translation.Get("config.maxPhotos.tooltip"),
                getValue: () => Config.MaxPhoto,
                setValue: value => Config.MaxPhoto = Math.Clamp(value, 10, 1000),
                min: 10,
                max: 1000
            );

            configMenu.AddPage(mod: ModManifest, pageId: "advance-settings", pageTitle: () => Helper.Translation.Get("config.advanceSettings.name"));
            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.advanceSettings.description")
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customEndpoint.name"),
                tooltip: () => Helper.Translation.Get("config.customEndpoint.tooltip"),
                getValue: () => Config.CustomApiEndpoint,
                setValue: value => Config.CustomApiEndpoint = (value ?? string.Empty).Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customKey.name"),
                tooltip: () => Helper.Translation.Get("config.customKey.tooltip"),
                getValue: () => Config.CustomApiKey,
                setValue: value => Config.CustomApiKey = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customKeyHeader.name"),
                tooltip: () => Helper.Translation.Get("config.customKeyHeader.tooltip"),
                getValue: () => Config.CustomApiKeyHeader,
                setValue: value => Config.CustomApiKeyHeader = (value ?? "Authorization").Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customKeyPrefix.name"),
                tooltip: () => Helper.Translation.Get("config.customKeyPrefix.tooltip"),
                getValue: () => Config.CustomApiKeyPrefix,
                setValue: value => Config.CustomApiKeyPrefix = (value ?? "Bearer").Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customPayload.name"),
                tooltip: () => Helper.Translation.Get("config.customPayload.tooltip"),
                getValue: () => Config.CustomApiPayloadTemplate,
                setValue: value => Config.CustomApiPayloadTemplate = value ?? string.Empty
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customResponsePath.name"),
                tooltip: () => Helper.Translation.Get("config.customResponsePath.tooltip"),
                getValue: () => Config.CustomApiResponseTextPath,
                setValue: value => Config.CustomApiResponseTextPath = value ?? string.Empty
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.customTimeout.name"),
                tooltip: () => Helper.Translation.Get("config.customTimeout.tooltip"),
                getValue: () => Config.CustomApiTimeoutSeconds,
                setValue: value => Config.CustomApiTimeoutSeconds = Math.Clamp(value, 5, 300),
                min: 5,
                max: 300
            );
        }
    }

    namespace Data
    {
        public interface IGenericModConfigMenuApi
        {
            void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
            void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
            void AddParagraph(IManifest mod, Func<string> text);
            void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
            void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);
            void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatValue = null, string fieldId = null);
            void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string> tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string> formatValue = null, string fieldId = null);
            void AddPage(IManifest mod, string pageId, Func<string> pageTitle = null);
            void AddPageLink(IManifest mod, string pageId, Func<string> text, Func<string> tooltip = null);
        }
    }
}
