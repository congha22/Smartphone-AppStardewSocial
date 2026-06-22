using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network.NetEvents;

namespace SmartphoneAppStardewSocial
{
    public partial class ModEntry
    {
        private const string AiProviderOpenAi = "openai";
        private const string AiProviderGemini = "gemini";
        private const string AiProviderCustom = "custom";
        private const string GeminiThinkingLevelMinimal = "MINIMAL";
        private const string CustomPayloadTokenModel = "MODEL_HERE";
        private const string CustomPayloadTokenInput = "INPUT_HERE";
        private const string CustomPayloadTokenSystemInput = "SYSTEM_INPUT_HERE";
        private const string CustomPayloadTokenUserInput = "USER_INPUT_HERE";
        private const string CustomPayloadTokenSystemMessage = "SYSTEM_MESSAGE_HERE";
        private const string CustomPayloadTokenUserMessage = "USER_MESSAGE_HERE";
        private const string CustomDefaultPayloadTemplate = "{\"model\":\"MODEL_HERE\",\"messages\":[{\"role\":\"system\",\"content\":\"SYSTEM_INPUT_HERE\"},{\"role\":\"user\",\"content\":\"USER_INPUT_HERE\"}]}";
        private static readonly string[] CustomResponseFallbackPaths =
        {
            "choices[0].message.content",
            "choices[0].text",
            "output_text",
            "output[0].content[0].text",
            "candidates[0].content.parts[0].text",
            "text"
        };

        public static bool IsMaxedLimit = false;
        public static bool IsReducedQuality = false;
        public static int totalFailedCheck = 0;

        // gpt-5.4 variant support reasoning effort: none, low, medium, high, xhigh
        public static string chatModel = "gpt-5.4-mini";
        public static object chatReasoningEffort = new { effort = "none" };
        public static string chatGeminiThinkingLevel = GeminiThinkingLevelMinimal;

        private static bool HasUserProvidedAiKey()
        {
            return !string.IsNullOrWhiteSpace(Config?.Key);
        }

        private static bool HasCustomProviderConfigured()
        {
            return !string.IsNullOrWhiteSpace((Config?.CustomApiEndpoint ?? string.Empty).Trim());
        }

        internal static bool IsBringYourOwnAiProviderMode()
        {
            return HasUserProvidedAiKey() || HasCustomProviderConfigured();
        }

        internal static bool IsSharedAiProviderMode()
        {
            return !IsBringYourOwnAiProviderMode();
        }

        private static string ResolveAiRuntimeKey(string provider)
        {
            if (string.Equals(provider, AiProviderCustom, StringComparison.OrdinalIgnoreCase))
                return (Config?.CustomApiKey ?? string.Empty).Trim();

            if (HasUserProvidedAiKey())
                return (Config.Key ?? string.Empty).Trim();

            return EmbeddedAiSecrets.SharedOpenAiRuntimeKey ?? string.Empty;
        }

        private static string ResolveOpenAiAdminKey()
        {
            return EmbeddedAiSecrets.SharedOpenAiAdminKey ?? string.Empty;
        }

        private static bool IsGeminiModel(string? model)
        {
            return !string.IsNullOrWhiteSpace(model)
                && ModConfig.geminiModels.Contains(model.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private static string GetProviderForModel(string? model)
        {
            if (HasCustomProviderConfigured())
                return AiProviderCustom;

            return IsGeminiModel(model) ? AiProviderGemini : AiProviderOpenAi;
        }

        private static string ResolveAiInstructionLanguage()
        {
            string configuredLanguage = (Config?.Language ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(configuredLanguage) ? "English" : configuredLanguage;
        }

        private static string AppendLanguageInstruction(string instructionText)
        {
            string normalizedInstruction = (instructionText ?? string.Empty).TrimEnd();
            string language = ResolveAiInstructionLanguage();

            if (string.Equals(language, "english", StringComparison.OrdinalIgnoreCase))
                return normalizedInstruction;

            return $"{normalizedInstruction}\nUse {language} language and alphabet";
        }

        private static string ResolveCustomApiEndpoint()
        {
            return (Config?.CustomApiEndpoint ?? string.Empty).Trim();
        }

        private static string ResolveCustomApiPayloadTemplate()
        {
            string configuredTemplate = (Config?.CustomApiPayloadTemplate ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(configuredTemplate)
                ? CustomDefaultPayloadTemplate
                : configuredTemplate;
        }

        private static string ResolveCustomApiResponseTextPath()
        {
            return (Config?.CustomApiResponseTextPath ?? string.Empty).Trim();
        }

        private static int ResolveCustomApiTimeoutSeconds()
        {
            return Math.Clamp(Config?.CustomApiTimeoutSeconds ?? 45, 5, 300);
        }

        private static bool TryResolveCustomApiEndpointUri(out Uri endpointUri, out string errorMessage)
        {
            endpointUri = null!;
            errorMessage = string.Empty;

            string endpoint = ResolveCustomApiEndpoint();
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                errorMessage = "Custom API endpoint is empty.";
                return false;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsedUri) || parsedUri == null)
            {
                errorMessage = $"Custom API endpoint '{endpoint}' is not a valid absolute URL.";
                return false;
            }

            if (string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                endpointUri = parsedUri;
                return true;
            }

            if (!string.Equals(parsedUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Custom API endpoint scheme '{parsedUri.Scheme}' is not supported. Use HTTPS, or HTTP only for localhost.";
                return false;
            }

            endpointUri = parsedUri;
            return true;
        }

        private static bool IsValidHttpHeaderName(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                return false;

            foreach (char character in headerName)
            {
                bool isLetterOrDigit = char.IsLetterOrDigit(character);
                if (!isLetterOrDigit && character != '-')
                    return false;
            }

            return true;
        }

        private static string BuildCustomAuthHeaderValue(string apiKey)
        {
            string normalizedApiKey = (apiKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedApiKey))
                return string.Empty;

            string prefix = (Config?.CustomApiKeyPrefix ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prefix))
                return normalizedApiKey;

            return $"{prefix} {normalizedApiKey}";
        }

        private static string BuildCombinedCustomInputPrompt(string systemMessage, string userMessage)
        {
            string normalizedSystemMessage = (systemMessage ?? string.Empty).Trim();
            string normalizedUserMessage = (userMessage ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedSystemMessage))
                return normalizedUserMessage;

            if (string.IsNullOrWhiteSpace(normalizedUserMessage))
                return normalizedSystemMessage;

            return $"SYSTEM:\n{normalizedSystemMessage}\n\nUSER:\n{normalizedUserMessage}";
        }

        private static bool ContainsCustomPayloadInputToken(string payloadTemplate)
        {
            string template = payloadTemplate ?? string.Empty;

            return template.Contains(CustomPayloadTokenInput, StringComparison.Ordinal)
                || template.Contains(CustomPayloadTokenSystemInput, StringComparison.Ordinal)
                || template.Contains(CustomPayloadTokenUserInput, StringComparison.Ordinal)
                || template.Contains(CustomPayloadTokenSystemMessage, StringComparison.Ordinal)
                || template.Contains(CustomPayloadTokenUserMessage, StringComparison.Ordinal)
                || template.Contains($"{{{{{CustomPayloadTokenInput}}}}}", StringComparison.Ordinal)
                || template.Contains($"{{{{{CustomPayloadTokenSystemInput}}}}}", StringComparison.Ordinal)
                || template.Contains($"{{{{{CustomPayloadTokenUserInput}}}}}", StringComparison.Ordinal)
                || template.Contains($"{{{{{CustomPayloadTokenSystemMessage}}}}}", StringComparison.Ordinal)
                || template.Contains($"{{{{{CustomPayloadTokenUserMessage}}}}}", StringComparison.Ordinal);
        }

        private static string ReplaceTemplateToken(string source, string token, string replacementValue)
        {
            string value = replacementValue ?? string.Empty;
            return (source ?? string.Empty)
                .Replace(token, value, StringComparison.Ordinal)
                .Replace($"{{{{{token}}}}}", value, StringComparison.Ordinal);
        }

        private static string ReplaceCustomPayloadTokens(string source, string model, string systemMessage, string userMessage, string combinedInput)
        {
            string resolvedSource = source ?? string.Empty;
            resolvedSource = ReplaceTemplateToken(resolvedSource, CustomPayloadTokenModel, model ?? string.Empty);
            resolvedSource = ReplaceTemplateToken(resolvedSource, CustomPayloadTokenSystemInput, systemMessage ?? string.Empty);
            resolvedSource = ReplaceTemplateToken(resolvedSource, CustomPayloadTokenUserInput, userMessage ?? string.Empty);
            resolvedSource = ReplaceTemplateToken(resolvedSource, CustomPayloadTokenSystemMessage, systemMessage ?? string.Empty);
            resolvedSource = ReplaceTemplateToken(resolvedSource, CustomPayloadTokenUserMessage, userMessage ?? string.Empty);
            resolvedSource = ReplaceTemplateToken(resolvedSource, CustomPayloadTokenInput, combinedInput ?? string.Empty);
            return resolvedSource;
        }

        private static void ReplaceCustomPayloadTokensInPlace(JToken token, string model, string systemMessage, string userMessage, string combinedInput)
        {
            if (token == null)
                return;

            if (token.Type == JTokenType.Object)
            {
                foreach (JProperty property in ((JObject)token).Properties())
                    ReplaceCustomPayloadTokensInPlace(property.Value, model, systemMessage, userMessage, combinedInput);

                return;
            }

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken child in (JArray)token)
                    ReplaceCustomPayloadTokensInPlace(child, model, systemMessage, userMessage, combinedInput);

                return;
            }

            if (token.Type == JTokenType.String && token is JValue value)
            {
                value.Value = ReplaceCustomPayloadTokens(value.Value?.ToString() ?? string.Empty, model, systemMessage, userMessage, combinedInput);
            }
        }

        private static bool TryBuildCustomApiPayload(string model, string systemMessage, string userMessage, out string payloadJson, out string errorMessage)
        {
            payloadJson = string.Empty;
            errorMessage = string.Empty;

            string payloadTemplate = ResolveCustomApiPayloadTemplate();
            if (!ContainsCustomPayloadInputToken(payloadTemplate))
            {
                errorMessage = "Custom payload template must include INPUT_HERE, SYSTEM_INPUT_HERE, or USER_INPUT_HERE placeholders.";
                return false;
            }

            try
            {
                JToken payloadToken = JToken.Parse(payloadTemplate);
                string combinedInput = BuildCombinedCustomInputPrompt(systemMessage, userMessage);
                ReplaceCustomPayloadTokensInPlace(payloadToken, model, systemMessage, userMessage, combinedInput);

                payloadJson = payloadToken.ToString(Formatting.None);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Custom payload template is invalid JSON: {ex.Message}";
                return false;
            }
        }

        private static async Task<HttpResponseMessage?> SendCustomProviderRequestAsync(HttpClient httpClient, string model, string systemMessage, string userMessage, string runtimeKey, string operationName)
        {
            if (httpClient == null)
                return null;

            if (!TryResolveCustomApiEndpointUri(out Uri endpointUri, out string endpointError))
            {
                SMonitor.Log($"Unable to call custom AI endpoint for {operationName}. {endpointError}", LogLevel.Warn);
                return null;
            }

            if (!TryBuildCustomApiPayload(model, systemMessage, userMessage, out string payloadJson, out string payloadError))
            {
                SMonitor.Log($"Unable to build custom AI payload for {operationName}. {payloadError}", LogLevel.Warn);
                return null;
            }

            string headerName = (Config?.CustomApiKeyHeader ?? string.Empty).Trim();
            string headerValue = BuildCustomAuthHeaderValue(runtimeKey);

            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                if (string.IsNullOrWhiteSpace(headerName))
                    headerName = "Authorization";

                if (!IsValidHttpHeaderName(headerName))
                {
                    SMonitor.Log($"Unable to call custom AI endpoint for {operationName}. Header name '{headerName}' is invalid.", LogLevel.Warn);
                    return null;
                }

                if (!httpClient.DefaultRequestHeaders.TryAddWithoutValidation(headerName, headerValue))
                {
                    SMonitor.Log($"Unable to call custom AI endpoint for {operationName}. Failed to attach header '{headerName}'.", LogLevel.Warn);
                    return null;
                }
            }

            httpClient.Timeout = TimeSpan.FromSeconds(ResolveCustomApiTimeoutSeconds());

            var httpContent = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            return await httpClient.PostAsync(endpointUri, httpContent);
        }

        internal static void HandleAiModelSettingTimeChanged(int newTime)
        {
            if (IsBringYourOwnAiProviderMode())
            {
                chatModel = Config.Model;

                string provider = GetProviderForModel(chatModel);

                if (string.Equals(provider, AiProviderGemini, StringComparison.OrdinalIgnoreCase))
                {
                    chatGeminiThinkingLevel = GeminiThinkingLevelMinimal;
                    chatReasoningEffort = new { effort = "minimal" };
                    return;
                }

                if (string.Equals(provider, AiProviderCustom, StringComparison.OrdinalIgnoreCase))
                {
                    chatGeminiThinkingLevel = GeminiThinkingLevelMinimal;
                    chatReasoningEffort = new { effort = "none" };
                    IsReducedQuality = false;
                    IsMaxedLimit = false;
                    totalFailedCheck = 0;
                    return;
                }

                switch (chatModel)
                {
                    case ModConfig.OpenAIModel_51:
                        chatReasoningEffort = new { effort = "none" };
                        break;
                    case ModConfig.OpenAIModel_5mini:
                        chatReasoningEffort = new { effort = "minimal" };
                        break;
                    case ModConfig.OpenAIModel_5nano:
                        chatReasoningEffort = new { effort = "minimal" };
                        break;
                    case ModConfig.OpenAIModel_54mini:
                        chatReasoningEffort = new { effort = "none" };
                        break;
                    case ModConfig.OpenAIModel_54nano:
                        chatReasoningEffort = new { effort = "none" };
                        break;
                    default:
                        chatReasoningEffort = new { effort = "minimal" };
                        break;
                }
                return;
            }

            // Shared provider: periodically check usage and adjust model quality
            if (IsSharedAiProviderMode() && newTime % 300 == 0)
            {
                Task.Run(async () =>
                {
                    var (premium, regular) = await GetOpenAIUsage();
                    if (regular == -1 || premium == -1)
                    {
                        totalFailedCheck += 1;
                        if (totalFailedCheck >= 3)
                        {
                            IsMaxedLimit = true;
                            return;
                        }
                    }

                    if (regular > 25000000)
                    {
                        IsMaxedLimit = true;
                        return;
                    }

                    IsMaxedLimit = false;
                    if (regular > 15000000 && chatModel != "gpt-5-nano")
                    {
                        chatModel = "gpt-5-nano";
                        chatReasoningEffort = new { effort = "minimal" };
                        IsReducedQuality = true;
                    }
                    else if (regular > 10000000)
                    {
                        IsReducedQuality = false;
                        chatModel = "gpt-5-mini";
                        chatReasoningEffort = new { effort = "minimal" };
                    }
                    else
                    {
                        IsReducedQuality = false;
                        chatModel = "gpt-5.4-mini";
                        chatReasoningEffort = new { effort = "none" };
                    }
                });
            }
        }

        private static string GetNpcCharacteristicForPrompt(NPC npc, bool getMinimal = false)
        {
            if (npc is null)
                return string.Empty;

            string npcAge = npc.Age == 0 ? "adult" : npc.Age == 1 ? "teens" : npc.Age == 2 ? "child" : "adult";
            string npcManner = npc.Manners == 0 ? "a typical neutral manner" : npc.Manners == 1 ? "a polite and courteous manner" : npc.Manners == 2 ? "a distant and reserved manner" : "a typical neutral manner";
            string npcSocial = npc.SocialAnxiety == 0 ? "an outgoing person" : npc.SocialAnxiety == 1 ? "a little shy person" : "neither too outgoing nor shy";

            string npcCharacteristic = $" {npc.Name} is {npcAge}, {npcManner}, and is {npcSocial}";

            if (getMinimal && IsSharedAiProviderMode())
                return npcCharacteristic;

            // CUSTOM CHARACTERISTIC OVERRIDE
            if (IsBringYourOwnAiProviderMode())
            {
                if (Config.CharacteristicMode == ModConfig.CharacteristicModeLong && NpcCharacteristicsLong.TryGetValue(npc.Name, out string? customCharacteristicLong) && !string.IsNullOrWhiteSpace(customCharacteristicLong) && !getMinimal)
                {
                    npcCharacteristic = customCharacteristicLong;
                }
                else if (Config.CharacteristicMode == ModConfig.CharacteristicModeShort && NpcCharacteristicsShort.TryGetValue(npc.Name, out string? customCharacteristic) && !string.IsNullOrWhiteSpace(customCharacteristic) && !getMinimal)
                {
                    npcCharacteristic = customCharacteristic;
                }
                else if (NpcCharacteristicsMinimal.TryGetValue(npc.Name, out string? customCharacteristicMinimal) && !string.IsNullOrWhiteSpace(customCharacteristicMinimal) && (Config.CharacteristicMode == ModConfig.CharacteristicModeMinimal || getMinimal && Config.BetterQualityComment))
                {
                    npcCharacteristic = customCharacteristicMinimal;
                }
                return npcCharacteristic.Trim();
            }
            // DEFAULT CHARACTERISTIC
            else
            {
                if (NpcCharacteristicsShort.TryGetValue(npc.Name, out string? customCharacteristic) && !string.IsNullOrWhiteSpace(customCharacteristic) && !IsReducedQuality)
                {
                    npcCharacteristic = customCharacteristic;
                }
                else if (NpcCharacteristicsMinimal.TryGetValue(npc.Name, out string? customCharacteristicMinimal) && !string.IsNullOrWhiteSpace(customCharacteristicMinimal))
                {
                    npcCharacteristic = customCharacteristicMinimal;
                }

                return npcCharacteristic.Trim();
            }
        }

        private static async Task<Dictionary<string, string>> GenerateNpcSocialPostTextsBatch(IReadOnlyList<DailySocialPostPlan> scheduledPosts)
        {
            var generatedPosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (scheduledPosts == null || scheduledPosts.Count == 0 || IsMaxedLimit)
                return generatedPosts;

            List<DailySocialPostPlan> validPlans = scheduledPosts
                .Where(plan => plan != null
                    && plan.IncludeText
                    && !string.IsNullOrWhiteSpace(plan.PlanId)
                    && !string.IsNullOrWhiteSpace(plan.AuthorName))
                .ToList();

            if (validPlans.Count == 0)
                return generatedPosts;

            try
            {
                string provider = GetProviderForModel(chatModel);
                var key = ResolveAiRuntimeKey(provider);
                if (!string.Equals(provider, AiProviderCustom, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(key))
                {
                    SMonitor.Log("GenerateNpcSocialPostTextsBatch skipped because no AI key is available.", LogLevel.Trace);
                    return generatedPosts;
                }

                var payloadPosts = validPlans.Select(plan => new
                {
                    id = plan.PlanId,
                    npc = plan.AuthorName,
                    characteristicDevelopmentStage = ResolveNpcCharacteristicDevelopmentStage(plan.AuthorName),
                    npcCharacteristic = (plan.NpcCharacteristic ?? string.Empty).Trim(),
                    imageTags = string.Join(", ", (plan.AttachmentTags ?? new List<List<string>>())
                        .SelectMany(tags => tags ?? new List<string>())
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Select(tag => tag.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                    worldContext = BuildWorldContextForSocialPost(plan.ScheduledTime),
                    scheduledTime = plan.ScheduledTime
                }).ToList();

                string developerMessage = @"
                    You are roleplaying as NPCs in Stardew Valley. Your task is to write posts on a social media channel for each NPC.
                    For each post input item, generate one short in-character post from the first-person perspective of the provided NPC.
                    Requirements:
                    - Keep each post under 40 words.
                    - Match the personality and characteristic development stage of the NPC.
                    - If image tags are provided, then the post should be relevant to the photo content and the world context. The tags describing where, when, what and who can be seen in the photo. In most case, the photo is taken by the NPC when they are visiting others, hanging out or just doing something.
                    - Invent random topic such as daily life, something happened, something fun, something strange, some drama, some topic to debate,... Be creative and dynamics. Do not be repetitive!
                    Return exactly one valid JSON object in this format:
                    {""posts"": [{""id"": ""<id>"", ""text"": ""<generated post text>""}]}
                    Every returned id must match an input id.";

                developerMessage = AppendLanguageInstruction(developerMessage);

                var userPayload = new
                {
                    posts = payloadPosts
                };

                using (var httpClient = new HttpClient())
                {
                    HttpResponseMessage httpResponse;

                    if (provider == AiProviderGemini)
                    {
                        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(chatModel)}:generateContent";
                        httpClient.DefaultRequestHeaders.Add("X-goog-api-key", key);

                        var requestBody = new
                        {
                            systemInstruction = new
                            {
                                parts = new object[]
                                {
                                    new { text = developerMessage }
                                }
                            },
                            contents = new object[]
                            {
                                new
                                {
                                    role = "user",
                                    parts = new object[]
                                    {
                                        new { text = JsonConvert.SerializeObject(userPayload, Formatting.Indented) }
                                    }
                                }
                            },
                            generationConfig = new
                            {
                                responseMimeType = "application/json",
                                maxOutputTokens = 2048,
                                thinkingConfig = new
                                {
                                    thinkingLevel = chatGeminiThinkingLevel
                                }
                            }
                        };

                        var jsonRequest = JsonConvert.SerializeObject(requestBody);
                        var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PostAsync(endpoint, httpContent);
                    }
                    else if (provider == AiProviderCustom)
                    {
                        HttpResponseMessage? customResponse = await SendCustomProviderRequestAsync(
                            httpClient,
                            chatModel,
                            developerMessage,
                            JsonConvert.SerializeObject(userPayload, Formatting.Indented),
                            key,
                            "GenerateNpcSocialPostTextsBatch");

                        if (customResponse == null)
                            return generatedPosts;

                        httpResponse = customResponse;
                    }
                    else
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

                        var requestBody = new
                        {
                            model = chatModel,
                            max_output_tokens = 2048,
                            input = new object[]
                            {
                                new
                                {
                                    role = "developer",
                                    content = new object[]
                                    {
                                        new { type = "input_text", text = developerMessage }
                                    }
                                },
                                new
                                {
                                    role = "user",
                                    content = new object[]
                                    {
                                        new { type = "input_text", text = JsonConvert.SerializeObject(userPayload, Formatting.Indented) }
                                    }
                                }
                            },
                            text = new { format = new { type = "json_object" }, verbosity = "low" },
                            reasoning = chatReasoningEffort
                        };

                        var jsonRequest = JsonConvert.SerializeObject(requestBody);
                        var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    }

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        JToken parsedToken;
                        try
                        {
                            parsedToken = JToken.Parse(jsonResponse);
                        }
                        catch (JsonReaderException)
                        {
                            SMonitor.Log($"Unable to parse social posts response payload from {provider}: {jsonResponse}", LogLevel.Trace);
                            return generatedPosts;
                        }


                    // SMonitor.Log(jsonResponse.ToString(), LogLevel.Error);
                    // SMonitor.Log("system-----", LogLevel.Error);
                    // SMonitor.Log(system, LogLevel.Error);
                    // SMonitor.Log("user-----", LogLevel.Error);
                    // SMonitor.Log(user, LogLevel.Error);
                    SMonitor.Log("response-----", LogLevel.Error);
                    SMonitor.Log(jsonResponse, LogLevel.Error);
                    SMonitor.Log("\n\n", LogLevel.Error);

                        string responseText;
                        if (provider == AiProviderGemini)
                        {
                            JObject geminiJson = parsedToken as JObject ?? new JObject();
                            responseText = GetGeminiResponseOutputText(geminiJson).Trim();
                        }
                        else if (provider == AiProviderOpenAi)
                        {
                            JObject openAiJson = parsedToken as JObject ?? new JObject();
                            responseText = GetResponseOutputText(openAiJson).Trim();
                        }
                        else
                        {
                            responseText = GetCustomResponseOutputText(parsedToken).Trim();
                        }

                        string[] expectedPostIds = validPlans
                            .Select(plan => plan.PlanId)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        if (TryParseGeneratedNpcSocialPosts(responseText, expectedPostIds, out Dictionary<string, string> parsedPosts))
                            return parsedPosts;

                        SMonitor.Log($"Unable to parse generated social posts payload: {responseText}", LogLevel.Trace);
                        return generatedPosts;
                    }

                    int statusCode = (int)httpResponse.StatusCode;
                    string errorMessage = "Check for mod update";
                    switch (statusCode)
                    {
                        case 400:
                            errorMessage = "Bad request sent to AI provider.";
                            break;
                        case 403:
                            errorMessage = "Country, region, or territory not supported.";
                            break;
                        case 429:
                            errorMessage = "Please try again in a few minutes. If not work, then total AI usage for all players has passed the limit set by OpenAI. This will be reset the next day in timezone UTC+0";
                            break;
                        case 500:
                            errorMessage = "Server Error: The server had an issue while processing your request. Please try again.";
                            break;
                        case 503:
                            errorMessage = "Server Overload: The server is experiencing high traffic. Please try again later.";
                            break;
                    }

                    SMonitor.Log($"Unable to generate batch social post content from {provider}. {statusCode}, {errorMessage}\n\n", LogLevel.Error);
                    return generatedPosts;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to generate batch social post content: {ex}", LogLevel.Trace);
                return generatedPosts;
            }
        }

        private static string ResolveNpcCharacteristicDevelopmentStage(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)
                || Game1.player?.friendshipData == null
                || !Game1.player.friendshipData.TryGetValue(npcName, out Friendship? friendship)
                || friendship == null)
            {
                return "very early";
            }

            int heartLevel = friendship.Points / 250;
            return heartLevel <= 2
                ? "very early"
                : heartLevel <= 4
                    ? "early"
                    : heartLevel <= 6
                        ? "middle"
                        : "late";
        }

        private static string BuildWorldContextForSocialPost(int scheduledTime)
        {
            string weather = Game1.currentLocation != null
                ? Game1.currentLocation.GetWeather().Weather.ToString()
                : "Unknown";

            string season = Game1.currentLocation != null
                ? Game1.currentLocation.GetSeason().ToString()
                : Game1.currentSeason;

            return $"Weather: {weather}. Time: {ResolveSocialPostPeriod(scheduledTime)}. Day: {Game1.dayOfMonth} {season}.";
        }

        private static string ResolveSocialPostPeriod(int scheduledTime)
        {
            return scheduledTime switch
            {
                >= 600 and < 1200 => "morning",
                >= 1200 and < 1700 => "afternoon",
                >= 1700 and < 2200 => "evening",
                _ => "night"
            };
        }

        public static async Task<Dictionary<string, Dictionary<string, string>>> GenerateNpcSocialPostCommentsBatch(IReadOnlyDictionary<string, IReadOnlyList<string>> commenterNamesByPostId)
        {
            var generatedCommentsByPost = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (commenterNamesByPostId == null || commenterNamesByPostId.Count == 0 || IsMaxedLimit)
                return generatedCommentsByPost;

            var expectedCommentersByPost = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var payloadPosts = new List<object>();

            foreach (KeyValuePair<string, IReadOnlyList<string>> entry in commenterNamesByPostId)
            {
                string postId = (entry.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(postId))
                    continue;

                StardewConnectPost? post = StardewConnectManager.GetPost(postId);
                if (post == null)
                    continue;

                string[] normalizedCommenters = (entry.Value ?? Array.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (normalizedCommenters.Length == 0)
                    continue;

                expectedCommentersByPost[postId] = normalizedCommenters;

                string postAuthor = (post.AuthorName ?? string.Empty).Trim();
                string postText = (post.Text ?? string.Empty).Trim();
                string postTag = string.Join(";", (post.Photo ?? new List<StardewConnectPhoto>())
                    .Select(p => p.Tag ?? string.Empty)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

                var latestComments = (post.Comments ?? Enumerable.Empty<StardewConnectComment>())
                    .Where(comment => comment != null)
                    .TakeLast(3)
                    .Select(comment => new
                    {
                        authorName = (comment!.AuthorName ?? string.Empty).Trim(),
                        text = (comment.Text ?? string.Empty).Trim()
                    })
                    .Where(comment => !string.IsNullOrWhiteSpace(comment.authorName) || !string.IsNullOrWhiteSpace(comment.text))
                    .ToArray();

                var commenterPayload = normalizedCommenters
                    .Select(name =>
                    {
                        int heartLevel = 0;
                        if (Game1.player.friendshipData.ContainsKey(name))
                            heartLevel = (int)Game1.player.friendshipData[name].Points / 250;

                        string npcCharacteristicState = heartLevel <= 2
                            ? "very early"
                            : heartLevel <= 4
                                ? "early"
                                : heartLevel <= 6
                                    ? "middle"
                                    : "late";

                        string npcCharacteristic = GetNpcCharacteristicForPrompt(Game1.getCharacterFromName(name), true);

                        return new
                        {
                            npc = name,
                            characteristicDevelopmentState = npcCharacteristicState,
                            characteristic = npcCharacteristic
                        };
                    })
                    .ToArray();

                payloadPosts.Add(new
                {
                    id = postId,
                    author = postAuthor,
                    postDescription = postText,
                    imageTag = postTag,
                    recentComments = latestComments,
                    commenters = commenterPayload
                });
            }

            if (payloadPosts.Count == 0)
                return generatedCommentsByPost;

            try
            {
                string provider = GetProviderForModel(chatModel);
                var key = ResolveAiRuntimeKey(provider);
                if (!string.Equals(provider, AiProviderCustom, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(key))
                {
                    SMonitor.Log("GenerateNpcSocialPostCommentsBatch skipped because no AI key is available.", LogLevel.Trace);
                    return generatedCommentsByPost;
                }

                string weatherText = Game1.currentLocation?.GetWeather().Weather.ToString() ?? "Unknown";
                string seasonText = Game1.currentLocation?.GetSeason().ToString() ?? "Unknown";
                int time = Game1.timeOfDay;
                string timeFormatted = time switch
                {
                    >= 600 and < 1200 => "morning",
                    >= 1200 and < 1700 => "afternoon",
                    >= 1700 and < 2200 => "evening",
                    _ => "night"
                };

                var developerMessage = $@"
                You are roleplaying as Stardew Valley NPCs writing comments to posts on a social media platform.
                Generate comments for multiple posts in one response.
                For each post:
                - Return exactly one short in-character comment for every requested NPC in commenters.
                - If the NPC is also the post author, then they should respond to other commenters instead of replying to their own post.
                - Use post author, post description, recent comments, and image tags when relevant.
                - Follow each NPC characteristic development state when deciding tone.
                - Keep each comment concise, casual, and under 20 words. Be dynamic and creative. Do not be repetitive.
                You may tag another NPC with @Name to respond to their comment. To tag the PLAYER, use @{Game1.player.Name}.
                Return exactly one valid JSON object with this format:
                {{""posts"": [{{""id"": ""<postId>"", ""comments"": {{""NPC name"": ""Comment""}}}}]}}
                Every returned id must match an input id.
                Include every requested NPC exactly once for each returned id.
                No markdown, no labels, no extra prose.";

                developerMessage = AppendLanguageInstruction(developerMessage);

                var userPayload = new
                {
                    worldContext = $"Weather: {weatherText}. Time: {timeFormatted} of day {Game1.dayOfMonth} {seasonText}",
                    posts = payloadPosts
                };

                using (var httpClient = new HttpClient())
                {
                    HttpResponseMessage httpResponse;

                    if (provider == AiProviderGemini)
                    {
                        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(chatModel)}:generateContent";
                        httpClient.DefaultRequestHeaders.Add("X-goog-api-key", key);

                        var requestBody = new
                        {
                            systemInstruction = new
                            {
                                parts = new object[]
                                {
                                    new { text = developerMessage }
                                }
                            },
                            contents = new object[]
                            {
                                new
                                {
                                    role = "user",
                                    parts = new object[]
                                    {
                                        new { text = JsonConvert.SerializeObject(userPayload, Formatting.Indented) }
                                    }
                                }
                            },
                            generationConfig = new
                            {
                                responseMimeType = "application/json",
                                maxOutputTokens = 2048,
                                thinkingConfig = new
                                {
                                    thinkingLevel = chatGeminiThinkingLevel
                                }
                            }
                        };

                        var jsonRequest = JsonConvert.SerializeObject(requestBody);
                        var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PostAsync(endpoint, httpContent);
                    }
                    else if (provider == AiProviderCustom)
                    {
                        HttpResponseMessage? customResponse = await SendCustomProviderRequestAsync(
                            httpClient,
                            chatModel,
                            developerMessage,
                            JsonConvert.SerializeObject(userPayload, Formatting.Indented),
                            key,
                            "GenerateNpcSocialPostCommentsBatch");

                        if (customResponse == null)
                            return generatedCommentsByPost;

                        httpResponse = customResponse;
                    }
                    else
                    {
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

                        var requestBody = new
                        {
                            model = chatModel,
                            max_output_tokens = 2048,
                            input = new object[]
                            {
                                new
                                {
                                    role = "developer",
                                    content = new object[]
                                    {
                                        new { type = "input_text", text = developerMessage }
                                    }
                                },
                                new
                                {
                                    role = "user",
                                    content = new object[]
                                    {
                                        new { type = "input_text", text = JsonConvert.SerializeObject(userPayload, Formatting.Indented) }
                                    }
                                }
                            },
                            text = new { format = new { type = "json_object" }, verbosity = "low" },
                            reasoning = chatReasoningEffort
                        };

                        var jsonRequest = JsonConvert.SerializeObject(requestBody);
                        var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    }

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        string jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        JToken parsedToken;
                        try
                        {
                            parsedToken = JToken.Parse(jsonResponse);
                        }
                        catch (JsonReaderException)
                        {
                            SMonitor.Log($"Unable to parse social comments response payload from {provider}: {jsonResponse}", LogLevel.Trace);
                            return generatedCommentsByPost;
                        }

                        string responseText;
                        if (provider == AiProviderGemini)
                        {
                            JObject geminiJson = parsedToken as JObject ?? new JObject();
                            responseText = GetGeminiResponseOutputText(geminiJson).Trim();
                        }
                        else if (provider == AiProviderOpenAi)
                        {
                            JObject openAiJson = parsedToken as JObject ?? new JObject();
                            responseText = GetResponseOutputText(openAiJson).Trim();
                        }
                        else
                        {
                            responseText = GetCustomResponseOutputText(parsedToken).Trim();
                        }

                        if (TryParseGeneratedNpcSocialCommentsBatch(responseText, expectedCommentersByPost, out Dictionary<string, Dictionary<string, string>> parsedCommentsByPost))
                            return parsedCommentsByPost;

                        SMonitor.Log($"Unable to parse generated social comments payload: {responseText}", LogLevel.Trace);
                        return generatedCommentsByPost;
                    }

                    int statusCode = (int)httpResponse.StatusCode;
                    string errorMessage = "Check for mod update";
                    switch (statusCode)
                    {
                        case 400:
                            errorMessage = "Bad request sent to AI provider.";
                            break;
                        case 403:
                            errorMessage = "Country, region, or territory not supported.";
                            break;
                        case 429:
                            errorMessage = "Please try again in a few minutes. If not work, then total AI usage for all players has passed the limit set by OpenAI. This will be reset the next day in timezone UTC+0";
                            break;
                        case 500:
                            errorMessage = "Server Error: The server had an issue while processing your request. Please try again.";
                            break;
                        case 503:
                            errorMessage = "Server Overload: The server is experiencing high traffic. Please try again later.";
                            break;
                    }

                    SMonitor.Log($"Unable to generate batched social comments from {provider}. {statusCode}, {errorMessage}\n\n", LogLevel.Error);
                    return generatedCommentsByPost;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to generate batched social comments: {ex}", LogLevel.Trace);
                return generatedCommentsByPost;
            }
        }

        // ===== Response parsing helpers =====

        private static string GetResponseOutputText(JObject responseJson)
        {
            var output = responseJson?["output"] as JArray;
            if (output != null)
            {
                foreach (var item in output.Reverse())
                {
                    if (item?["type"]?.ToString() != "message")
                        continue;

                    var content = item["content"] as JArray;
                    if (content == null)
                        continue;

                    foreach (var part in content)
                    {
                        if (part?["type"]?.ToString() == "output_text")
                        {
                            var text = part["text"]?.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                                return text;
                        }
                    }
                }
            }

            var fallbackText = responseJson?["output_text"]?.ToString()?.Trim();
            return fallbackText ?? string.Empty;
        }

        private static string GetGeminiResponseOutputText(JObject responseJson)
        {
            var candidates = responseJson?["candidates"] as JArray;
            if (candidates != null)
            {
                foreach (var candidate in candidates)
                {
                    var parts = candidate?["content"]?["parts"] as JArray;
                    if (parts == null)
                        continue;

                    var textParts = parts
                        .Select(part => part?["text"]?.ToString()?.Trim() ?? string.Empty)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .ToArray();

                    if (textParts.Length > 0)
                        return string.Join("\n", textParts).Trim();
                }
            }

            return string.Empty;
        }

        private static JToken? TrySelectJsonPathToken(JToken responseToken, string path)
        {
            if (responseToken == null)
                return null;

            string normalizedPath = (path ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedPath))
                return null;

            try
            {
                JToken? selectedToken = responseToken.SelectToken(normalizedPath, errorWhenNoMatch: false);
                if (selectedToken != null)
                    return selectedToken;

                string rootedPath = normalizedPath.StartsWith("$", StringComparison.Ordinal)
                    ? normalizedPath
                    : normalizedPath.StartsWith("[", StringComparison.Ordinal)
                        ? $"${normalizedPath}"
                        : $"$.{normalizedPath}";

                return responseToken.SelectToken(rootedPath, errorWhenNoMatch: false);
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractTextFromCustomResponseToken(JToken? token)
        {
            if (token == null)
                return string.Empty;

            if (token.Type == JTokenType.String)
                return token.ToString().Trim();

            if (token.Type == JTokenType.Array)
            {
                string[] values = ((JArray)token)
                    .Select(ExtractTextFromCustomResponseToken)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();

                return values.Length == 0 ? string.Empty : string.Join("\n", values).Trim();
            }

            if (token.Type == JTokenType.Object)
            {
                JObject tokenObject = (JObject)token;
                string[] preferredKeys = { "text", "content", "message", "output_text", "response", "result" };
                foreach (string preferredKey in preferredKeys)
                {
                    if (!TryGetJsonPropertyValue(tokenObject, preferredKey, out JToken? preferredValue))
                        continue;

                    string extracted = ExtractTextFromCustomResponseToken(preferredValue);
                    if (!string.IsNullOrWhiteSpace(extracted))
                        return extracted;
                }

                string[] fallbackValues = tokenObject.Properties()
                    .Select(property => ExtractTextFromCustomResponseToken(property.Value))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();

                return fallbackValues.Length == 0 ? string.Empty : string.Join("\n", fallbackValues).Trim();
            }

            return token.ToString().Trim();
        }

        private static string GetCustomResponseOutputText(JToken responseToken)
        {
            if (responseToken == null)
                return string.Empty;

            string configuredPath = ResolveCustomApiResponseTextPath();
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                JToken? configuredToken = TrySelectJsonPathToken(responseToken, configuredPath);
                string configuredText = ExtractTextFromCustomResponseToken(configuredToken);
                if (!string.IsNullOrWhiteSpace(configuredText))
                    return configuredText;
            }

            foreach (string fallbackPath in CustomResponseFallbackPaths)
            {
                JToken? fallbackToken = TrySelectJsonPathToken(responseToken, fallbackPath);
                string fallbackText = ExtractTextFromCustomResponseToken(fallbackToken);
                if (!string.IsNullOrWhiteSpace(fallbackText))
                    return fallbackText;
            }

            return ExtractTextFromCustomResponseToken(responseToken);
        }

        // ===== Parse generated post/comment output =====

        private static bool TryParseGeneratedNpcSocialPosts(string responseText, IEnumerable<string> expectedPostIds, out Dictionary<string, string> posts)
        {
            posts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] normalizedPostIds = (expectedPostIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedPostIds.Length == 0)
                return false;

            string jsonPayload = ExtractJsonPayload(responseText);
            if (string.IsNullOrWhiteSpace(jsonPayload))
                return false;

            try
            {
                JToken token = JToken.Parse(jsonPayload);
                return TryPopulateGeneratedNpcSocialPosts(token, normalizedPostIds, posts);
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        private static bool TryPopulateGeneratedNpcSocialPosts(JToken token, IReadOnlyCollection<string> expectedPostIds, Dictionary<string, string> posts)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                if ((TryGetJsonPropertyValue(obj, "posts", out JToken? postsToken)
                        || TryGetJsonPropertyValue(obj, "post", out postsToken)
                        || TryGetJsonPropertyValue(obj, "results", out postsToken))
                    && postsToken != null)
                {
                    return TryPopulateGeneratedNpcSocialPosts(postsToken, expectedPostIds, posts);
                }

                string postId = string.Empty;
                if (TryGetJsonPropertyValue(obj, "id", out JToken? idToken)
                    || TryGetJsonPropertyValue(obj, "postId", out idToken)
                    || TryGetJsonPropertyValue(obj, "post_id", out idToken))
                {
                    postId = (idToken?.ToString() ?? string.Empty).Trim();
                }

                if (!string.IsNullOrWhiteSpace(postId) && expectedPostIds.Contains(postId, StringComparer.OrdinalIgnoreCase))
                {
                    JToken? postTextToken = null;
                    if (!TryGetJsonPropertyValue(obj, "text", out postTextToken)
                        && !TryGetJsonPropertyValue(obj, "message", out postTextToken)
                        && !TryGetJsonPropertyValue(obj, "postText", out postTextToken)
                        && !TryGetJsonPropertyValue(obj, "post_text", out postTextToken))
                    {
                        return posts.Count > 0;
                    }

                    string postText = ExtractGeneratedSocialPostText(postTextToken);
                    if (!string.IsNullOrWhiteSpace(postText))
                        posts[postId] = postText;

                    return posts.Count > 0;
                }

                foreach (string expectedPostId in expectedPostIds)
                {
                    if (!TryGetJsonPropertyValue(obj, expectedPostId, out JToken? postTextToken))
                        continue;

                    string postText = ExtractGeneratedSocialPostText(postTextToken);
                    if (!string.IsNullOrWhiteSpace(postText))
                        posts[expectedPostId] = postText;
                }

                return posts.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                if (item.Type != JTokenType.Object)
                    continue;

                JObject itemObject = (JObject)item;
                string postId = string.Empty;
                if (TryGetJsonPropertyValue(itemObject, "id", out JToken? idToken)
                    || TryGetJsonPropertyValue(itemObject, "postId", out idToken)
                    || TryGetJsonPropertyValue(itemObject, "post_id", out idToken))
                {
                    postId = (idToken?.ToString() ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(postId) || !expectedPostIds.Contains(postId, StringComparer.OrdinalIgnoreCase))
                    continue;

                JToken? postTextToken = null;
                if (!TryGetJsonPropertyValue(itemObject, "text", out postTextToken)
                    && !TryGetJsonPropertyValue(itemObject, "message", out postTextToken)
                    && !TryGetJsonPropertyValue(itemObject, "postText", out postTextToken)
                    && !TryGetJsonPropertyValue(itemObject, "post_text", out postTextToken))
                {
                    continue;
                }

                string postText = ExtractGeneratedSocialPostText(postTextToken);
                if (!string.IsNullOrWhiteSpace(postText))
                    posts[postId] = postText;
            }

            return posts.Count > 0;
        }

        private static string ExtractGeneratedSocialPostText(JToken? textToken)
        {
            if (textToken == null)
                return string.Empty;

            if (textToken.Type == JTokenType.Object)
            {
                JObject textObject = (JObject)textToken;
                if (TryGetJsonPropertyValue(textObject, "text", out JToken? nestedText)
                    || TryGetJsonPropertyValue(textObject, "message", out nestedText)
                    || TryGetJsonPropertyValue(textObject, "post", out nestedText)
                    || TryGetJsonPropertyValue(textObject, "value", out nestedText))
                {
                    return NormalizeGeneratedSocialPostText(nestedText?.ToString());
                }
            }

            if (textToken.Type == JTokenType.Array)
            {
                string merged = string.Join(" ",
                    ((JArray)textToken)
                        .Select(entry => entry?.ToString() ?? string.Empty)
                        .Where(entry => !string.IsNullOrWhiteSpace(entry)));

                return NormalizeGeneratedSocialPostText(merged);
            }

            return NormalizeGeneratedSocialPostText(textToken.ToString());
        }

        private static bool TryGetJsonPropertyValue(JObject obj, string propertyName, out JToken? value)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string NormalizeGeneratedSocialPostText(string? postText)
        {
            string normalizedText = string.Join(" ", (postText ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if ((normalizedText.StartsWith("\"") && normalizedText.EndsWith("\"") && normalizedText.Length > 1)
                || (normalizedText.StartsWith("'") && normalizedText.EndsWith("'") && normalizedText.Length > 1))
            {
                normalizedText = normalizedText.Substring(1, normalizedText.Length - 2).Trim();
            }

            return normalizedText;
        }

        internal static string NormalizeGeneratedSocialCommentText(string npcName, string? commentText)
        {
            string normalizedText = string.Join(" ", (commentText ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if (normalizedText.StartsWith($"{npcName}:", StringComparison.OrdinalIgnoreCase))
                normalizedText = normalizedText.Substring(npcName.Length + 1).TrimStart();

            if ((normalizedText.StartsWith("\"") && normalizedText.EndsWith("\"") && normalizedText.Length > 1)
                || (normalizedText.StartsWith("'") && normalizedText.EndsWith("'") && normalizedText.Length > 1))
            {
                normalizedText = normalizedText.Substring(1, normalizedText.Length - 2).Trim();
            }

            return normalizedText;
        }

        private static bool TryParseGeneratedNpcSocialCommentsBatch(string responseText, IReadOnlyDictionary<string, string[]> expectedCommentersByPost, out Dictionary<string, Dictionary<string, string>> commentsByPost)
        {
            commentsByPost = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (expectedCommentersByPost == null || expectedCommentersByPost.Count == 0)
                return false;

            string jsonPayload = ExtractJsonPayload(responseText);
            if (string.IsNullOrWhiteSpace(jsonPayload))
                return false;

            try
            {
                JToken token = JToken.Parse(jsonPayload);
                return TryPopulateGeneratedNpcSocialCommentsBatch(token, expectedCommentersByPost, commentsByPost);
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        private static bool TryPopulateGeneratedNpcSocialCommentsBatch(JToken token, IReadOnlyDictionary<string, string[]> expectedCommentersByPost, Dictionary<string, Dictionary<string, string>> commentsByPost)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                if ((TryGetJsonPropertyValue(obj, "posts", out JToken? postsToken)
                        || TryGetJsonPropertyValue(obj, "results", out postsToken)
                        || TryGetJsonPropertyValue(obj, "commentsByPost", out postsToken)
                        || TryGetJsonPropertyValue(obj, "comments_by_post", out postsToken))
                    && postsToken != null)
                {
                    TryPopulateGeneratedNpcSocialCommentsBatch(postsToken, expectedCommentersByPost, commentsByPost);
                }

                string postId = string.Empty;
                if (TryGetJsonPropertyValue(obj, "id", out JToken? idToken)
                    || TryGetJsonPropertyValue(obj, "postId", out idToken)
                    || TryGetJsonPropertyValue(obj, "post_id", out idToken))
                {
                    postId = (idToken?.ToString() ?? string.Empty).Trim();
                }

                if (!string.IsNullOrWhiteSpace(postId)
                    && expectedCommentersByPost.TryGetValue(postId, out string[]? expectedCommentersForId)
                    && expectedCommentersForId != null
                    && expectedCommentersForId.Length > 0)
                {
                    var postComments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (TryPopulateGeneratedNpcSocialComments(obj, expectedCommentersForId, postComments))
                        commentsByPost[postId] = postComments;
                }

                foreach (KeyValuePair<string, string[]> entry in expectedCommentersByPost)
                {
                    string expectedPostId = entry.Key;
                    string[] expectedCommenters = entry.Value;
                    if (string.IsNullOrWhiteSpace(expectedPostId)
                        || expectedCommenters == null
                        || expectedCommenters.Length == 0)
                    {
                        continue;
                    }

                    if (!TryGetJsonPropertyValue(obj, expectedPostId, out JToken? postCommentsToken) || postCommentsToken == null)
                        continue;

                    var postComments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (TryPopulateGeneratedNpcSocialComments(postCommentsToken, expectedCommenters, postComments))
                        commentsByPost[expectedPostId] = postComments;
                }

                return commentsByPost.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                TryPopulateGeneratedNpcSocialCommentsBatch(item, expectedCommentersByPost, commentsByPost);
            }

            return commentsByPost.Count > 0;
        }

        private static bool TryPopulateGeneratedNpcSocialComments(JToken token, IReadOnlyCollection<string> expectedNpcNames, Dictionary<string, string> comments)
        {
            if (token == null || expectedNpcNames == null || expectedNpcNames.Count == 0)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                if (TryGetJsonPropertyValue(obj, "comments", out JToken? commentsToken) && commentsToken != null)
                {
                    TryPopulateGeneratedNpcSocialComments(commentsToken, expectedNpcNames, comments);
                }

                foreach (string expectedNpcName in expectedNpcNames)
                {
                    if (!TryGetJsonPropertyValue(obj, expectedNpcName, out JToken? commentTokenValue) || commentTokenValue == null)
                        continue;

                    string commentText = NormalizeGeneratedSocialCommentText(expectedNpcName, commentTokenValue.ToString());
                    if (!string.IsNullOrWhiteSpace(commentText))
                        comments[expectedNpcName] = commentText;
                }

                return comments.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                if (item.Type != JTokenType.Object)
                    continue;

                JObject itemObject = (JObject)item;
                string npcName = string.Empty;
                if (TryGetJsonPropertyValue(itemObject, "npc", out JToken? npcToken)
                    || TryGetJsonPropertyValue(itemObject, "name", out npcToken)
                    || TryGetJsonPropertyValue(itemObject, "author", out npcToken))
                {
                    npcName = (npcToken?.ToString() ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(npcName) || !expectedNpcNames.Contains(npcName, StringComparer.OrdinalIgnoreCase))
                    continue;

                JToken? commentTokenValue = null;
                if (!TryGetJsonPropertyValue(itemObject, "comment", out commentTokenValue)
                    && !TryGetJsonPropertyValue(itemObject, "text", out commentTokenValue)
                    && !TryGetJsonPropertyValue(itemObject, "message", out commentTokenValue))
                {
                    continue;
                }

                string commentText = NormalizeGeneratedSocialCommentText(npcName, commentTokenValue?.ToString());
                if (!string.IsNullOrWhiteSpace(commentText))
                    comments[npcName] = commentText;
            }

            return comments.Count > 0;
        }

        private static string ExtractJsonPayload(string responseText)
        {
            string trimmed = (responseText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLineBreak = trimmed.IndexOf('\n');
                if (firstLineBreak >= 0)
                    trimmed = trimmed.Substring(firstLineBreak + 1).Trim();

                int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (closingFence >= 0)
                    trimmed = trimmed.Substring(0, closingFence).Trim();
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
                return trimmed;

            int objectStart = trimmed.IndexOf('{');
            int objectEnd = trimmed.LastIndexOf('}');
            if (objectStart >= 0 && objectEnd > objectStart)
                return trimmed.Substring(objectStart, objectEnd - objectStart + 1).Trim();

            int arrayStart = trimmed.IndexOf('[');
            int arrayEnd = trimmed.LastIndexOf(']');
            if (arrayStart >= 0 && arrayEnd > arrayStart)
                return trimmed.Substring(arrayStart, arrayEnd - arrayStart + 1).Trim();

            return trimmed;
        }

        public static async Task<(int, int)> GetOpenAIUsage()
        {
            List<string> premiumModels = new List<string> { "gpt-5.4", "gpt-5.2", "gpt-5.1", "gpt-5.1-codex", "gpt-5", "gpt-5-codex", "gpt-5-chat-latest", "gpt-4.1", "gpt-4o", "o1", "o3" };
            List<string> regularModels = new List<string> { "gpt-5.4-mini", "gpt-5.4-nano", "gpt-5.1-codex-mini", "gpt-5-mini", "gpt-5-nano", "gpt-4.1-mini", "gpt-4.1-nano", "gpt-4o-mini",
                                                            "o1-mini", "o3-mini", "o4-mini", "codex-mini-latest" };
            string admin_key = ResolveOpenAiAdminKey();
            if (string.IsNullOrWhiteSpace(admin_key))
                return (-1, -1);

            string usageUrl = "https://api.openai.com/v1/organization/usage/completions";
            using HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin_key);

            DateTime utcNow = DateTime.UtcNow;
            DateTime utcStartOfToday = utcNow.Date;
            long startTime = new DateTimeOffset(utcStartOfToday).ToUnixTimeSeconds();
            long endTime = new DateTimeOffset(utcNow).ToUnixTimeSeconds();

            try
            {
                var perModelTotals = new Dictionary<string, (long Input, long Output)>();
                string? nextPage = null;
                bool hasMore;

                do
                {
                    var query = $"start_time={startTime}&end_time={endTime}&group_by=model";
                    if (!string.IsNullOrWhiteSpace(nextPage))
                        query += $"&page={Uri.EscapeDataString(nextPage)}";

                    string requestUrl = $"{usageUrl}?{query}";
                    HttpResponseMessage response = await client.GetAsync(requestUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        SMonitor.Log($"Error: {response.StatusCode} - {error}", LogLevel.Error);
                        return (-1, -1);
                    }

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(jsonResponse);

                    var data = json["data"] as JArray;
                    if (data != null)
                    {
                        foreach (var bucket in data)
                        {
                            var results = bucket?["results"] as JArray;
                            if (results == null)
                                continue;

                            foreach (var result in results)
                            {
                                string? model = result?["model"]?.ToString();
                                if (string.IsNullOrWhiteSpace(model))
                                    model = "unknown";

                                long inputTokens = result?["input_tokens"]?.Value<long>() ?? 0;
                                long outputTokens = result?["output_tokens"]?.Value<long>() ?? 0;

                                if (perModelTotals.TryGetValue(model, out var totals))
                                {
                                    perModelTotals[model] = (totals.Input + inputTokens, totals.Output + outputTokens);
                                }
                                else
                                {
                                    perModelTotals[model] = (inputTokens, outputTokens);
                                }
                            }
                        }
                    }

                    hasMore = json["has_more"]?.Value<bool>() ?? false;
                    nextPage = json["next_page"]?.ToString();
                }
                while (hasMore && !string.IsNullOrWhiteSpace(nextPage));

                if (perModelTotals.Count == 0)
                {
                    return (-1, -1);
                }

                long premiumInputTotal = 0, premiumOutputTotal = 0;
                long regularInputTotal = 0, regularOutputTotal = 0;

                foreach (var kv in perModelTotals)
                {
                    var modelName = kv.Key ?? string.Empty;
                    var input = kv.Value.Input;
                    var output = kv.Value.Output;

                    bool isRegular = regularModels.Any(rm =>
                        modelName.StartsWith(rm, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rm, modelName, StringComparison.OrdinalIgnoreCase)
                    );
                    bool isPremium = !isRegular && premiumModels.Any(pm =>
                        modelName.StartsWith(pm, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(pm, modelName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (isRegular)
                    {
                        regularInputTotal += input;
                        regularOutputTotal += output;
                    }
                    else if (isPremium)
                    {
                        premiumInputTotal += input;
                        premiumOutputTotal += output;
                    }
                }

                return ((int)(premiumInputTotal + premiumOutputTotal), (int)(regularInputTotal + regularOutputTotal));
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Request failed: {ex.Message}", LogLevel.Error);
                return (-1, -1);
            }
        }
    }
}
