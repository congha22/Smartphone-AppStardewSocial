using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace SmartphoneAppStardewSocial
{
    public class ModConfig
    {
        public const string PostPerDayHigh = "High";
        public const string PostPerDayMedium = "Medium";
        public const string PostPerDayLow = "Low";

        public const string OpenAIModel_51 = "gpt-5.1";
        public const string OpenAIModel_5mini = "gpt-5-mini";
        public const string OpenAIModel_5nano = "gpt-5-nano";
        public const string OpenAIModel_54mini = "gpt-5.4-mini";
        public const string OpenAIModel_54nano = "gpt-5.4-nano";
        public const string GeminiModel_35Flash = "gemini-3.5-flash";
        public const string GeminiModel_31FlashLite = "gemini-3.1-flash-lite";
        public const string GeminiModel_3FlashPreview = "gemini-3-flash-preview";

        public static readonly List<string> geminiModels = new()
        {
            GeminiModel_35Flash,
            GeminiModel_31FlashLite,
            GeminiModel_3FlashPreview
        };

        public static readonly List<string> openAIModels = new()
        {
            OpenAIModel_51,
            OpenAIModel_5mini,
            OpenAIModel_5nano,
            OpenAIModel_54mini,
            OpenAIModel_54nano
        };

        public string PostPerDay { get; set; } = PostPerDayLow;
        public string Language { get; set; } = "English";

        private string _key = string.Empty;
        private string _model = OpenAIModel_54mini;
        private string _customApiKey = string.Empty;

        // advance
        public string Key
        {
            get => _key;
            set => _key = (value ?? string.Empty).Trim();
        }

        public string Model
        {
            get => string.IsNullOrWhiteSpace(_model) ? OpenAIModel_54mini : _model;
            set => _model = string.IsNullOrWhiteSpace(value) ? OpenAIModel_54mini : value.Trim();
        }

        public string CustomApiEndpoint { get; set; } = string.Empty;

        public string CustomApiKey
        {
            get => _customApiKey;
            set => _customApiKey = (value ?? string.Empty).Trim();
        }

        public string CustomApiKeyHeader { get; set; } = "Authorization";
        public string CustomApiKeyPrefix { get; set; } = "Bearer";
        public string CustomApiPayloadTemplate { get; set; } = "{\"model\":\"MODEL_HERE\",\"messages\":[{\"role\":\"system\",\"content\":\"SYSTEM_INPUT_HERE\"},{\"role\":\"user\",\"content\":\"USER_INPUT_HERE\"}]}";
        public string CustomApiResponseTextPath { get; set; } = "choices[0].message.content";
        public int CustomApiTimeoutSeconds { get; set; } = 45;

        // Legacy aliases for older config.json files.
        public string OpenAIKey
        {
            get => Key;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Key = value;
                else if (string.IsNullOrWhiteSpace(_key))
                    _key = string.Empty;
            }
        }

        public string OpenAIModel
        {
            get => Model;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Model = value;
                else if (string.IsNullOrWhiteSpace(_model))
                    _model = OpenAIModel_54mini;
            }
        }
        public bool BetterQualityComment { get; set; } = false;





        public int MaxStardewConnectPosts { get; set; } = 100;
        public int MaxPhoto { get; set; } = 200;
        public bool ShowSocialImageTags { get; set; } = false;
        public bool ShowUnreadComment { get; set; } = true;

        public string IgnoredNpc { get; set; } = "Leo, Krobus, Dwarf, Gunther, Birdie, Bouncer, MoonSBV, PanSBV, RaccoonSBV, Leximonster, Dianna, Torts";
    }
}
