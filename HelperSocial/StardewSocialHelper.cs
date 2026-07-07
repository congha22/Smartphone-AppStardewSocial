using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;

namespace SmartphoneAppStardewSocial
{
    public partial class ModEntry
    {
        public static List<NPC> CachedContactableNpcs = new();

        public static void UpdateContactableNpcsFromConfig()
        {
            List<NPC> validNpcs = new();
            if (Config != null && !string.IsNullOrWhiteSpace(Config.AllowedNpc))
            {
                var names = Config.AllowedNpc.Split(',')
                    .Select(n => n.Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();

                string req = Config.FriendshipRequirement; // "Meet" or "Friend"
                int requiredPoints = string.Equals(req, "Friend", StringComparison.OrdinalIgnoreCase) ? 250 : 1;

                foreach (var name in names)
                {
                    NPC npc = Game1.getCharacterFromName(name);
                    if (npc != null)
                    {
                        int points = Game1.player.getFriendshipLevelForNPC(name);
                        if (points >= requiredPoints)
                        {
                            validNpcs.Add(npc);
                        }
                    }
                }
            }
            CachedContactableNpcs = validNpcs;
        }

        public static List<NPC> GetContactableNpcsList()
        {
            return CachedContactableNpcs;
        }

        private static int SocialActionMaxDelayMilliseconds = 140000; //140000
        private static int SocialPostMaxDelayMilliseconds = 70000;//70000
        private static double SocialRepeatCommentChance = 0.30;
        private static readonly List<DailySocialPostPlan> DailyScheduledSocialPosts = new();
        private static bool DailySocialPostTextGenerationRequested = false;
        private static int DailySocialPostTextGenerationTime = -1;

        private sealed class DailySocialPostPlan
        {
            public string PlanId { get; set; } = StardewConnectManager.CreateShortAlphanumericId();
            public string AuthorName { get; set; } = string.Empty;
            public int ScheduledTime { get; set; } = 900;
            public bool IncludeText { get; set; }
            public int ImageCount { get; set; }
            public List<string> Attachments { get; set; } = new();
            public List<List<string>> AttachmentTags { get; set; } = new();
            public string NpcCharacteristic { get; set; } = string.Empty;
            public string GeneratedText { get; set; } = string.Empty;
            public List<string> GeneratedTags { get; set; } = new();
            public bool IsTextReady { get; set; }
            public bool IsPosted { get; set; }
        }

        // Engagement limits for random posts
        private static int SocialLikesWithin1DayMax = 3;
        private static int SocialCommentsWithin1DayMax = 5;
        private static int SocialLikesWithin3DaysMax = 2;
        private static int SocialCommentsWithin3DaysMax = 3;

        // Engagement limits for most popular posts
        private static int SocialLikesMostPopularWithin2DayMax = 2;
        private static int SocialCommentsMostPopularWithin2DayMax = 3;

        // Post selection sampling size for comments
        private static int SocialCommentRandomPostsWithin1DayCount = 2;
        private static int SocialCommentRandomPostsWithin3DaysCount = 0;
        private static int SocialCommentMostPopularWithin2DayCount = 1;

        // Post selection sampling size for likes
        private static int SocialLikeRandomPostsWithin1DayCount = 3;
        private static int SocialLikeRandomPostsWithin3DaysCount = 4;
        private static int SocialLikeMostPopularWithin2DayCount = 2;
        private static int SocialLikeMinPerPost = 1;

        // Social post weights. Keep total 100
        private static int SocialPostWeightText0Images = 27;
        private static int SocialPostWeightText1Image = 35;
        private static int SocialPostWeightText2Images = 16;
        private static int SocialPostWeightText3Images = 6;
        private static int SocialPostWeightNoText0Images = 0;
        private static int SocialPostWeightNoText1Image = 8;
        private static int SocialPostWeightNoText2Images = 5;
        private static int SocialPostWeightNoText3Images = 3;

        internal static void UpdatePostInteractionLimit()
        {
            int totalNpcs = Utility.getAllVillagers().Count(npc => npc.CanSocialize);
            if (totalNpcs > 130)
            {
                SocialLikesWithin1DayMax = 6;
                SocialLikesWithin3DaysMax = 5;
                SocialLikesMostPopularWithin2DayMax = 5;

                if (!IsAiUsageLimitEnabled())
                {
                    SocialCommentsWithin1DayMax = 7;
                    SocialCommentsWithin3DaysMax = 5;
                    SocialCommentsMostPopularWithin2DayMax = 5;
                }
            }
            else if (totalNpcs > 100)
            {
                SocialLikesWithin1DayMax = 5;
                SocialLikesWithin3DaysMax = 4;
                SocialLikesMostPopularWithin2DayMax = 4;

                if (!IsAiUsageLimitEnabled())
                {
                    SocialCommentsWithin1DayMax = 7;
                    SocialCommentsWithin3DaysMax = 5;
                    SocialCommentsMostPopularWithin2DayMax = 5;
                }
            }
            else if (totalNpcs > 60)
            {
                SocialLikesWithin1DayMax = 4;
                SocialLikesWithin3DaysMax = 4;
                SocialLikesMostPopularWithin2DayMax = 4;

                SocialCommentsWithin1DayMax = 6;
                SocialCommentsWithin3DaysMax = 4;
                SocialCommentsMostPopularWithin2DayMax = 4;
            }
            else if (totalNpcs > 40)
            {
                SocialLikesWithin1DayMax = 3;
                SocialLikesWithin3DaysMax = 3;
                SocialLikesMostPopularWithin2DayMax = 3;

                SocialCommentsWithin1DayMax = 6;
                SocialCommentsWithin3DaysMax = 4;
                SocialCommentsMostPopularWithin2DayMax = 4;
            }
        }

        internal static void UpdateSocialPostLimit()
        {
            if (IsAiUsageLimitEnabled())
                return;

            if (Config.PostPerDay == ModConfig.PostPerDayHigh)
            {
                SocialCommentRandomPostsWithin1DayCount = 3;
                SocialCommentRandomPostsWithin3DaysCount = 2;
            }
            else if (Config.PostPerDay == ModConfig.PostPerDayMedium)
            {
                SocialCommentRandomPostsWithin1DayCount = 2;
                SocialCommentRandomPostsWithin3DaysCount = 1;
            }
            else
            {
                SocialCommentRandomPostsWithin1DayCount = 2;
                SocialCommentRandomPostsWithin3DaysCount = 0;
            }
        }

        private static List<int> GetSocialCommentEngagementIntervalFromConfig()
        {
            if (IsAiUsageLimitEnabled())
                return new List<int> { 1100, 1930 };

            return Config?.PostPerDay switch
            {
                ModConfig.PostPerDayHigh => new List<int> { 800, 1130, 1700, 2130 },
                ModConfig.PostPerDayLow => new List<int> { 1100, 1930 },
                _ => new List<int> { 1000, 1400, 1930 }
            };
        }

        private static List<int> GetSocialLikeEngagementIntervalFromConfig()
        {
            if (IsAiUsageLimitEnabled())
                return new List<int> { 730, 1230, 1600, 2100 };

            return Config?.PostPerDay switch
            {
                ModConfig.PostPerDayHigh => new List<int> { 610, 900, 1130, 1500, 1800, 2130 },
                ModConfig.PostPerDayLow => new List<int> { 730, 1200, 1600, 2100 },
                _ => new List<int> { 700, 1100, 1400, 1700, 2130 }
            };
        }

        private static (int MinGapMinutes, int MaxGapMinutes, int MinPosts, int MaxPosts) GetSocialPostSchedulingConfig()
        {
            if (IsAiUsageLimitEnabled())
                return (300, 360, 1, 3);

            return Config?.PostPerDay switch
            {
                ModConfig.PostPerDayHigh => (180, 240, 2, 4),
                ModConfig.PostPerDayLow => (300, 360, 1, 3),
                _ => (240, 300, 2, 3)
            };
        }

        private static NPC? GetRandomWeightedNPC()
        {
            List<NPC> candidates = GetContactableNpcsList();
            if (candidates.Count == 0)
                return null;

            double getWeight(NPC npc)
            {
                return npc.Age switch
                {
                    1 => 0.50, // teens
                    2 => 0.35, // adults
                    0 => 0.15, // children
                    _ => 0.00
                };
            }

            double totalWeight = candidates.Sum(npc => getWeight(npc));
            double randomValue = Random.Shared.NextDouble() * totalWeight;
            double cumulativeWeight = 0;

            foreach (var npc in candidates)
            {
                cumulativeWeight += getWeight(npc);
                if (randomValue < cumulativeWeight)
                    return npc;
            }

            return null;
        }

        private static NPC? GetRandomVillagerNpc()
        {
            List<NPC> candidates = GetContactableNpcsList();
            if (candidates.Count == 0)
                return null;

            return candidates[Game1.random.Next(candidates.Count)];
        }

        internal static void ClearPendingRandomNpcSocialPost()
        {
            DailyScheduledSocialPosts.Clear();
            DailySocialPostTextGenerationRequested = false;
            DailySocialPostTextGenerationTime = -1;
        }

        private static void PrepareDailyRandomNpcSocialPosts()
        {
            ClearPendingRandomNpcSocialPost();
            if (!StardewModdingAPI.Context.IsWorldReady || Game1.player == null)
                return;

            (int minGapMinutes, int maxGapMinutes, int minPosts, int maxPosts) = GetSocialPostSchedulingConfig();
            int desiredPostCount = Game1.random.Next(minPosts, maxPosts + 1);
            List<int> scheduledTimes = BuildDailySocialPostTimes(desiredPostCount, minGapMinutes, maxGapMinutes);

            foreach (int scheduledTime in scheduledTimes)
            {
                NPC? selectedNpc = GetRandomWeightedNPC() ?? GetRandomVillagerNpc();
                if (selectedNpc == null || string.IsNullOrWhiteSpace(selectedNpc.Name))
                    continue;

                (bool includeText, int imageCount) = ChooseRandomSocialPostFormat(scheduledTime);
                int safeImageCount = Math.Clamp(imageCount, 0, 3);

                // Capture NPC photos via the framework API (saves to a shared location)
                List<string> attachments = StardewSocialNpcCaptureHelper.TryCaptureNpcPhoto(selectedNpc.Name, safeImageCount, scheduledTime);

                DailyScheduledSocialPosts.Add(new DailySocialPostPlan
                {
                    PlanId = StardewConnectManager.CreateShortAlphanumericId(),
                    AuthorName = selectedNpc.Name,
                    ScheduledTime = scheduledTime,
                    IncludeText = includeText,
                    ImageCount = safeImageCount,
                    Attachments = attachments,
                    AttachmentTags = attachments.Select(GetAttachmentTagsForPrompt).ToList(),
                    NpcCharacteristic = GetNpcCharacteristicForPrompt(selectedNpc),
                    IsTextReady = !includeText,
                    IsPosted = false
                });
            }

            DailyScheduledSocialPosts.Sort((left, right) => left.ScheduledTime.CompareTo(right.ScheduledTime));
            DailySocialPostTextGenerationTime = DailyScheduledSocialPosts.Count > 0
                ? DailyScheduledSocialPosts[0].ScheduledTime
                : -1;
        }

        private static void HandleScheduledSocialPostsOnTimeChanged(int currentTime)
        {
            if (DailyScheduledSocialPosts.Count == 0)
                return;

            TryGenerateScheduledSocialPostTexts(currentTime);
            TryPublishDueScheduledSocialPosts(currentTime);
        }

        private static void TryGenerateScheduledSocialPostTexts(int currentTime)
        {
            if (DailySocialPostTextGenerationRequested)
                return;

            if (DailySocialPostTextGenerationTime < 0 || currentTime < DailySocialPostTextGenerationTime)
                return;

            List<DailySocialPostPlan> postsNeedingText = DailyScheduledSocialPosts
                .Where(plan => plan.IncludeText && !plan.IsTextReady)
                .ToList();

            DailySocialPostTextGenerationRequested = true;

            if (postsNeedingText.Count == 0)
                return;

            QueueDelayedSocialFuncActionWithLimit(async () =>
            {
                await RunAiActionWithQueueAsync(async () =>
                {
                    Dictionary<string, GeneratedPostResult> generatedPosts = await GenerateNpcSocialPostTextsBatch(postsNeedingText);

                    foreach (DailySocialPostPlan plan in postsNeedingText)
                    {
                        GeneratedPostResult generated = generatedPosts.TryGetValue(plan.PlanId, out var result)
                            ? result
                            : new GeneratedPostResult();

                        plan.GeneratedText = NormalizeGeneratedSocialCommentText(plan.AuthorName, generated.Text);
                        plan.GeneratedTags = generated.Tags;
                        plan.IsTextReady = true;
                    }

                    TryPublishDueScheduledSocialPosts(Game1.timeOfDay);
                });
            }, SocialPostMaxDelayMilliseconds);
        }

        private static void TryPublishDueScheduledSocialPosts(int currentTime)
        {
            List<DailySocialPostPlan> duePlans = DailyScheduledSocialPosts
                .Where(plan => !plan.IsPosted && plan.ScheduledTime <= currentTime)
                .OrderBy(plan => plan.ScheduledTime)
                .ToList();

            foreach (DailySocialPostPlan plan in duePlans)
            {
                if (plan.IncludeText && !plan.IsTextReady)
                    continue;

                string postText = plan.IncludeText ? plan.GeneratedText : string.Empty;

                string? postId = StardewConnectManager.AddNpcPostWithAttachments(plan.AuthorName, postText, plan.Attachments, plan.GeneratedTags);
                plan.IsPosted = true;

                if (string.IsNullOrWhiteSpace(postId))
                    SMonitor.Log($"Unable to publish scheduled social post for '{plan.AuthorName}' at {plan.ScheduledTime}.", LogLevel.Trace);
            }
        }

        private static List<int> BuildDailySocialPostTimes(int desiredPostCount, int minGapMinutes, int maxGapMinutes)
        {
            const int firstPostEarliestMinute = 9 * 60 + 10;
            const int latestPostMinute = 23 * 60 + 40;

            int attemptPostCount = Math.Max(1, desiredPostCount);
            while (attemptPostCount > 0)
            {
                int latestFirstMinute = latestPostMinute - ((attemptPostCount - 1) * minGapMinutes);
                if (latestFirstMinute < firstPostEarliestMinute)
                {
                    attemptPostCount--;
                    continue;
                }

                int firstUpperBound = Math.Min(latestFirstMinute, firstPostEarliestMinute + 180);
                if (firstUpperBound < firstPostEarliestMinute)
                    firstUpperBound = latestFirstMinute;

                int firstMinute = RoundDownToTenMinutes(Game1.random.Next(firstPostEarliestMinute, firstUpperBound + 1));
                var minutes = new List<int> { firstMinute };

                bool validSchedule = true;
                int currentMinute = firstMinute;
                for (int i = 1; i < attemptPostCount; i++)
                {
                    int gap = Game1.random.Next(minGapMinutes, maxGapMinutes + 1);
                    currentMinute = RoundDownToTenMinutes(currentMinute + gap);

                    if (currentMinute > latestPostMinute)
                    {
                        validSchedule = false;
                        break;
                    }

                    minutes.Add(currentMinute);
                }

                if (!validSchedule)
                {
                    attemptPostCount--;
                    continue;
                }

                return minutes.Select(ConvertMinutesToTimeOfDay).ToList();
            }

            return new List<int>();
        }

        private static int RoundDownToTenMinutes(int totalMinutes)
        {
            return Math.Max(0, (totalMinutes / 10) * 10);
        }

        private static int ConvertMinutesToTimeOfDay(int totalMinutes)
        {
            int safeMinutes = Math.Max(0, totalMinutes);
            int hour = safeMinutes / 60;
            int minute = safeMinutes % 60;
            return (hour * 100) + minute;
        }


        private static List<string> GetAttachmentTagsForPrompt(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return new List<string>();

            string tag = GetNpcPhotoTag(imagePath);
            if (string.IsNullOrWhiteSpace(tag))
                return new List<string>();

            return tag
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (bool IncludeText, int ImageCount) ChooseRandomSocialPostFormat(int scheduledTime)
        {
            bool badWeather = Game1.currentLocation != null
                && (Game1.currentLocation.IsRainingHere() || Game1.currentLocation.IsGreenRainingHere() || Game1.currentLocation.IsLightningHere());

            if (scheduledTime >= 2300
               || (Game1.currentSeason == "winter" && scheduledTime >= 2100)
               || (badWeather && scheduledTime >= 2000))
                return (true, 0);

            var weightedFormats = new List<(bool IncludeText, int ImageCount, int Weight)>
            {
                (true, 0, SocialPostWeightText0Images),
                (true, 1, SocialPostWeightText1Image),
                (true, 2, SocialPostWeightText2Images),
                (true, 3, SocialPostWeightText3Images),
                (false, 0, SocialPostWeightNoText0Images),
                (false, 1, SocialPostWeightNoText1Image),
                (false, 2, SocialPostWeightNoText2Images),
                (false, 3, SocialPostWeightNoText3Images),
            };

            int totalWeight = weightedFormats.Sum(format => Math.Max(0, format.Weight));
            if (totalWeight <= 0)
                return (true, 0);

            int roll = Game1.random.Next(totalWeight);
            int cumulative = 0;
            foreach (var format in weightedFormats)
            {
                int safeWeight = Math.Max(0, format.Weight);
                if (safeWeight == 0)
                    continue;

                cumulative += safeWeight;
                if (roll < cumulative)
                    return (format.IncludeText, format.ImageCount);
            }

            return (true, 0);
        }

        private static void QueueRandomNpcCommentEngagement()
        {
            var commentAttemptsByPostId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            List<string> randomPostsWithin1Day = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithinDayRange(count, 0, 1),
                desiredCount: SocialCommentRandomPostsWithin1DayCount);
            QueueCommentAttemptsForPosts(randomPostsWithin1Day, SocialCommentsWithin1DayMax, commentAttemptsByPostId);

            List<string> randomPostsWithin3Days = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithinDayRange(count, 1, 3),
                desiredCount: SocialCommentRandomPostsWithin3DaysCount);
            QueueCommentAttemptsForPosts(randomPostsWithin3Days, SocialCommentsWithin3DaysMax, commentAttemptsByPostId);

            List<string> mostPopularWithin2Days = GetDistinctPopularPostIdsWithinDayRange(
                desiredCount: SocialCommentMostPopularWithin2DayCount,
                startDay: 0,
                endDay: 2);
            QueueCommentAttemptsForPosts(mostPopularWithin2Days, SocialCommentsMostPopularWithin2DayMax, commentAttemptsByPostId);

            QueueCommentsForPosts(commentAttemptsByPostId);
        }

        private static void QueueRandomNpcLikeEngagement()
        {
            List<string> randomPostsWithin1Day = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithinDayRange(count, 0, 1),
                desiredCount: SocialLikeRandomPostsWithin1DayCount);
            QueueLikesForPosts(randomPostsWithin1Day, SocialLikesWithin1DayMax);

            List<string> randomPostsWithin3Days = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithinDayRange(count, 1, 3),
                desiredCount: SocialLikeRandomPostsWithin3DaysCount);
            QueueLikesForPosts(randomPostsWithin3Days, SocialLikesWithin3DaysMax);

            List<string> mostPopularWithin2Days = GetDistinctPopularPostIdsWithinDayRange(
                desiredCount: SocialLikeMostPopularWithin2DayCount,
                startDay: 0,
                endDay: 2);
            QueueLikesForPosts(mostPopularWithin2Days, SocialLikesMostPopularWithin2DayMax);
        }

        private static List<string> GetDistinctPopularPostIdsWithinDayRange(int desiredCount, int startDay, int endDay)
        {
            return GetDistinctRandomPostIds(requestCount =>
            {
                var postIds = new List<string>();
                for (int i = 0; i < requestCount; i++)
                {
                    string postId = StardewConnectManager.GetOnePopularPostIdWithinDayRange(startDay, endDay);
                    if (!string.IsNullOrWhiteSpace(postId))
                        postIds.Add(postId);
                }
                return postIds;
            }, desiredCount);
        }

        private static void QueueLikesForPosts(IEnumerable<string> postIds, int maxLikes)
        {
            if (postIds == null)
                return;

            foreach (string postId in postIds)
                QueueLikesForPost(postId, maxLikes);
        }

        private static void QueueCommentAttemptsForPosts(IEnumerable<string> postIds, int maxComments, IDictionary<string, int> commentAttemptsByPostId)
        {
            if (postIds == null)
                return;

            foreach (string postId in postIds)
                QueueCommentAttemptsForPost(postId, maxComments, commentAttemptsByPostId);
        }

        private static void QueueCommentAttemptsForPost(string postId, int maxComments, IDictionary<string, int> commentAttemptsByPostId)
        {
            if (string.IsNullOrWhiteSpace(postId) || maxComments <= 0 || commentAttemptsByPostId == null)
                return;

            int commentAttempts = Game1.random.Next(maxComments + 1);
            if (commentAttempts <= 0)
                return;

            if (commentAttemptsByPostId.TryGetValue(postId, out int existingAttempts))
                commentAttemptsByPostId[postId] = existingAttempts + commentAttempts;
            else
                commentAttemptsByPostId[postId] = commentAttempts;
        }

        private static void QueueLikesForPost(string postId, int maxLikes)
        {
            if (string.IsNullOrWhiteSpace(postId) || maxLikes <= 0)
                return;

            int minLikeAttempts = Math.Clamp(SocialLikeMinPerPost, 1, maxLikes);
            int likeAttempts = Game1.random.Next(minLikeAttempts, maxLikes + 1);
            for (int i = 0; i < likeAttempts; i++)
            {
                QueueDelayedSocialAction(() =>
                {
                    StardewConnectPost? post = StardewConnectManager.GetPost(postId);
                    if (post == null)
                        return;

                    string actorName = PickRandomNpcActorName(post, excludeAlreadyLiked: true);
                    if (string.IsNullOrWhiteSpace(actorName))
                        return;

                    StardewConnectManager.SetPostLike(postId, actorName, true);
                });
            }
        }

        private static void QueueCommentsForPosts(IReadOnlyDictionary<string, int> commentAttemptsByPostId)
        {
            if (commentAttemptsByPostId == null || commentAttemptsByPostId.Count == 0)
                return;

            QueueDelayedSocialFuncAction(async () =>
            {
                await RunAiActionWithQueueAsync(async () =>
                {
                    var commenterNamesByPost = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (KeyValuePair<string, int> entry in commentAttemptsByPostId)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0)
                            continue;

                        StardewConnectPost? post = StardewConnectManager.GetPost(entry.Key);
                        if (post == null)
                            continue;

                        List<string> actorNames = PickRandomNpcActorNamesForCommentBatch(post, entry.Value);
                        if (actorNames.Count == 0)
                            continue;

                        commenterNamesByPost[entry.Key] = actorNames;
                    }

                    if (commenterNamesByPost.Count == 0)
                        return;

                    Dictionary<string, Dictionary<string, string>> generatedCommentsByPost = await GenerateNpcSocialPostCommentsBatch(commenterNamesByPost);
                    if (generatedCommentsByPost.Count == 0)
                        return;

                    foreach (KeyValuePair<string, Dictionary<string, string>> postEntry in generatedCommentsByPost)
                    {
                        string postId = postEntry.Key;
                        if (StardewConnectManager.GetPost(postId) == null)
                            continue;

                        Dictionary<string, string> postComments = postEntry.Value;
                        if (string.IsNullOrWhiteSpace(postId) || postComments == null || postComments.Count == 0)
                            continue;

                        foreach (KeyValuePair<string, string> commentEntry in postComments)
                        {
                            string commentAuthorName = commentEntry.Key?.Trim() ?? string.Empty;
                            string generatedComment = commentEntry.Value?.Trim() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(commentAuthorName) || string.IsNullOrWhiteSpace(generatedComment))
                                continue;

                            QueueDelayedSocialAction(() =>
                            {
                                if (StardewConnectManager.GetPost(postId) == null)
                                    return;
                                StardewConnectManager.AddNpcComment(postId, commentAuthorName, generatedComment);
                            });
                        }
                    }
                });
            });
        }

        private static string PickRandomNpcActorNameForComment(StardewConnectPost post)
        {
            return PickRandomNpcActorNameForComment(post, excludedNpcNames: null);
        }

        private static List<string> PickRandomNpcActorNamesForCommentBatch(StardewConnectPost post, int desiredCount)
        {
            var selectedActors = new List<string>();
            if (post == null || desiredCount <= 0)
                return selectedActors;

            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < desiredCount; i++)
            {
                string actorName = PickRandomNpcActorNameForComment(post, reservedNames);
                if (string.IsNullOrWhiteSpace(actorName))
                    break;

                if (reservedNames.Add(actorName))
                    selectedActors.Add(actorName);
            }

            return selectedActors;
        }

        private static string PickRandomNpcActorNameForComment(StardewConnectPost post, ISet<string>? excludedNpcNames)
        {
            if (post == null)
                return string.Empty;

            List<NPC> candidates = GetContactableNpcsList()
                .Where(npc => IsEligibleNpcCommentAuthorForPost(post, npc))
                .Where(npc => excludedNpcNames == null || !excludedNpcNames.Contains(npc.Name))
                .ToList();

            if (candidates.Count == 0)
                return string.Empty;

            NPC chosen = candidates[Game1.random.Next(candidates.Count)];
            if (!HasNpcAlreadyCommentedOnPost(post, chosen.Name))
                return chosen.Name;

            if (Game1.random.NextDouble() < SocialRepeatCommentChance)
                return chosen.Name;

            List<NPC> freshCandidates = candidates
                .Where(npc => !HasNpcAlreadyCommentedOnPost(post, npc.Name))
                .ToList();

            if (freshCandidates.Count == 0)
                return string.Empty;

            return freshCandidates[Game1.random.Next(freshCandidates.Count)].Name;
        }

        private static string PickRandomNpcActorName(StardewConnectPost post, bool excludeAlreadyLiked)
        {
            if (post == null)
                return string.Empty;

            List<NPC> candidates = GetContactableNpcsList()
                .Where(npc => !string.Equals(npc.Name, post.AuthorName, StringComparison.OrdinalIgnoreCase))
                .Where(npc => !excludeAlreadyLiked || !StardewConnectManager.IsPostLikedBy(post, npc.Name))
                .ToList();

            if (candidates.Count == 0)
                return string.Empty;

            return candidates[Game1.random.Next(candidates.Count)].Name;
        }

        private static bool HasNpcAlreadyCommentedOnPost(StardewConnectPost post, string npcName)
        {
            if (post?.Comments == null || string.IsNullOrWhiteSpace(npcName))
                return false;

            return post.Comments.Any(comment => comment != null
                && string.Equals(comment.AuthorName, npcName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsEligibleNpcCommentAuthorForPost(StardewConnectPost post, NPC npc)
        {
            if (post == null || npc == null)
                return false;

            if (!string.Equals(npc.Name, post.AuthorName, StringComparison.OrdinalIgnoreCase))
                return true;

            return CanPostAuthorNpcCommentOwnPost(post, npc.Name);
        }

        private static bool CanPostAuthorNpcCommentOwnPost(StardewConnectPost post, string postAuthorName)
        {
            if (post?.Comments == null || post.Comments.Count == 0 || string.IsNullOrWhiteSpace(postAuthorName))
                return false;

            return post.Comments.Any(comment =>
                comment != null
                && !comment.AuthorIsPlayer
                && !string.Equals(comment.AuthorName, postAuthorName, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetDistinctRandomPostIds(Func<int, List<string>> getter, int desiredCount)
        {
            var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (getter == null || desiredCount <= 0)
                return new List<string>();

            int attempts = 0;
            while (uniqueIds.Count < desiredCount && attempts < 4)
            {
                int requestCount = Math.Max(1, desiredCount * 2);
                List<string> batch = getter(requestCount) ?? new List<string>();
                foreach (string postId in batch)
                {
                    if (!string.IsNullOrWhiteSpace(postId))
                        uniqueIds.Add(postId.Trim());
                }

                attempts++;
            }

            List<string> result = uniqueIds.ToList();
            ShuffleList(result);
            if (result.Count > desiredCount)
                result = result.Take(desiredCount).ToList();

            return result;
        }

        private static void ShuffleList<T>(IList<T> items)
        {
            if (items == null)
                return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Game1.random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private static void QueueDelayedSocialAction(Action action)
        {
            if (action == null)
                return;

            QueueDelayedSocialFuncAction(() =>
            {
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            });
        }

        private static void QueueDelayedSocialFuncAction(Func<Task> action)
        {
            if (action == null)
                return;

            QueueDelayedSocialFuncActionWithLimit(action, SocialActionMaxDelayMilliseconds);
        }

        private static void QueueDelayedSocialFuncActionWithLimit(Func<Task> action, int maxDelayMilliseconds)
        {
            if (action == null)
                return;

            int delay = Game1.random.Next(0, Math.Max(0, maxDelayMilliseconds) + 1);
            DelayedAction.functionAfterDelay(() =>
            {
                _ = RunDelayedSocialActionAsync(action);
            }, delay);
        }

        private static async Task RunDelayedSocialActionAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Random social action failed: {ex}", LogLevel.Trace);
            }
        }
    }
}
