using System;
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
                }
            );

            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Quick Setup");

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Language",
                tooltip: () => "Choose prompt and response language.",
                getValue: () => string.IsNullOrWhiteSpace(Config.Language) ? "English" : Config.Language,
                setValue: value => Config.Language = string.IsNullOrWhiteSpace(value) ? "English" : value.Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Post Frequency",
                tooltip: () => "Choose post frequency per day.",
                getValue: () => EnsureAllowedValue(Config.PostPerDay, ModConfig.PostPerDayLow, postPerDayValues),
                setValue: value => Config.PostPerDay = value,
                allowedValues: postPerDayValues
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show Social Image Tags",
                tooltip: () => "Show tags for attached photos in social tooltips.",
                getValue: () => Config.ShowSocialImageTags,
                setValue: value => Config.ShowSocialImageTags = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show Unread Comment",
                tooltip: () => "Show unread comment indicator.",
                getValue: () => Config.ShowUnreadComment,
                setValue: value => Config.ShowUnreadComment = value
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "ai-settings",
                text: () => "AI Settings",
                tooltip: () => "Configure API keys and models."
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "storage-limits",
                text: () => "Limits & Storage",
                tooltip: () => "Configure post retention and photo limits."
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "misc-settings",
                text: () => "Miscellaneous",
                tooltip: () => "Configure ignored NPCs."
            );

            // AI Settings page
            configMenu.AddPage(mod: ModManifest, pageId: "ai-settings", pageTitle: () => "AI Settings");
            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => "Setup your AI provider credentials here."
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "API Key",
                tooltip: () => "Your OpenAI or Gemini API Key.",
                getValue: () => Config.Key,
                setValue: value => Config.Key = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Model",
                tooltip: () => "Select the AI model to use.",
                getValue: () => EnsureAllowedValue(Config.Model, ModConfig.OpenAIModel_54mini, aiModelValues),
                setValue: value => Config.Model = value,
                allowedValues: aiModelValues
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Better Quality Comment",
                tooltip: () => "Generate comments with better quality (might take slightly longer).",
                getValue: () => Config.BetterQualityComment,
                setValue: value => Config.BetterQualityComment = value
            );

            // Custom API settings inside AI page
            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Custom Endpoint Settings (Optional)");
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Custom API Endpoint",
                tooltip: () => "URL endpoint for a custom API (leave empty to use default providers).",
                getValue: () => Config.CustomApiEndpoint,
                setValue: value => Config.CustomApiEndpoint = (value ?? string.Empty).Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Custom API Key",
                tooltip: () => "Key for your custom API endpoint.",
                getValue: () => Config.CustomApiKey,
                setValue: value => Config.CustomApiKey = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Custom API Key Header",
                tooltip: () => "HTTP Header name for the API Key.",
                getValue: () => Config.CustomApiKeyHeader,
                setValue: value => Config.CustomApiKeyHeader = (value ?? "Authorization").Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Custom API Key Prefix",
                tooltip: () => "Prefix for the key (e.g. Bearer).",
                getValue: () => Config.CustomApiKeyPrefix,
                setValue: value => Config.CustomApiKeyPrefix = (value ?? "Bearer").Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Custom API Payload Template",
                tooltip: () => "JSON payload template.",
                getValue: () => Config.CustomApiPayloadTemplate,
                setValue: value => Config.CustomApiPayloadTemplate = value ?? string.Empty
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Custom API Response JSON Path",
                tooltip: () => "JSON path to extract response text.",
                getValue: () => Config.CustomApiResponseTextPath,
                setValue: value => Config.CustomApiResponseTextPath = value ?? string.Empty
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Custom API Timeout Seconds",
                tooltip: () => "Request timeout in seconds.",
                getValue: () => Config.CustomApiTimeoutSeconds,
                setValue: value => Config.CustomApiTimeoutSeconds = Math.Clamp(value, 5, 300),
                min: 5,
                max: 300
            );

            // Limits & Storage page
            configMenu.AddPage(mod: ModManifest, pageId: "storage-limits", pageTitle: () => "Limits & Storage");

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Max StardewConnect Posts",
                tooltip: () => "Max number of posts to keep in StardewConnect.",
                getValue: () => Config.MaxStardewConnectPosts,
                setValue: value => Config.MaxStardewConnectPosts = Math.Clamp(value, 10, 1000),
                min: 10,
                max: 1000
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Max Photos",
                tooltip: () => "Max number of photos to keep in photo_shared folder.",
                getValue: () => Config.MaxPhoto,
                setValue: value => Config.MaxPhoto = Math.Clamp(value, 10, 1000),
                min: 10,
                max: 1000
            );

            // Miscellaneous page
            configMenu.AddPage(mod: ModManifest, pageId: "misc-settings", pageTitle: () => "Miscellaneous");

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Ignored NPCs",
                tooltip: () => "Comma-separated list of NPCs who cannot post or comment.",
                getValue: () => Config.IgnoredNpc ?? string.Empty,
                setValue: value => Config.IgnoredNpc = value ?? string.Empty
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
