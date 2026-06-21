using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewValley;
using Newtonsoft.Json;

namespace SmartphoneAppStardewSocial
{
    public sealed class StardewConnectTime
    {
        public string Season { get; set; } = "spring";
        public int Day { get; set; } = 1;
        public int Year { get; set; } = 1;
        public int TimeOfDay { get; set; } = 600;
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public sealed class StardewConnectPhoto
    {
        public string Path { get; set; } = "";
        public string Tag { get; set; } = "";
    }

    public sealed class StardewConnectComment
    {
        public string Id { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public bool AuthorIsPlayer { get; set; }
        public string Text { get; set; } = "";
        public StardewConnectTime CreatedTime { get; set; } = new();

        [JsonIgnore]
        public string Season { get => CreatedTime.Season; set => CreatedTime.Season = value; }
        [JsonIgnore]
        public int Day { get => CreatedTime.Day; set => CreatedTime.Day = value; }
        [JsonIgnore]
        public int Year { get => CreatedTime.Year; set => CreatedTime.Year = value; }
        [JsonIgnore]
        public int TimeOfDay { get => CreatedTime.TimeOfDay; set => CreatedTime.TimeOfDay = value; }
        [JsonIgnore]
        public long TotalGameTime { get => CreatedTime.Timestamp; set => CreatedTime.Timestamp = value; }
    }

    public sealed class StardewConnectPlayerReadCommentCount
    {
        public string PlayerName { get; set; } = "";
        public int Count { get; set; }
    }

    public sealed class StardewConnectPostAttachment
    {
        public string ImageFile { get; set; } = "";
        public bool FromPlayerFolder { get; set; }
        public string ImageTag { get; set; } = "";
    }

    public sealed class StardewConnectPost
    {
        public string Id { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public bool AuthorIsPlayer { get; set; }
        public List<string> Tagged { get; set; } = new();
        public string Text { get; set; } = "";
        public List<StardewConnectPhoto> Photo { get; set; } = new();
        public StardewConnectTime CreatedTime { get; set; } = new();
        public List<string> LikedBy { get; set; } = new();
        public List<StardewConnectComment> Comments { get; set; } = new();
        public List<string> PostTags { get; set; } = new();
        public List<StardewConnectPlayerReadCommentCount> PlayerReadCommentCounts { get; set; } = new();

        [JsonIgnore]
        public string Season { get => CreatedTime.Season; set => CreatedTime.Season = value; }
        [JsonIgnore]
        public int Day { get => CreatedTime.Day; set => CreatedTime.Day = value; }
        [JsonIgnore]
        public int Year { get => CreatedTime.Year; set => CreatedTime.Year = value; }
        [JsonIgnore]
        public int TimeOfDay { get => CreatedTime.TimeOfDay; set => CreatedTime.TimeOfDay = value; }
        [JsonIgnore]
        public long TotalGameTime { get => CreatedTime.Timestamp; set => CreatedTime.Timestamp = value; }
        [JsonIgnore]
        public string PostTag { get => string.Join(";", PostTags); set => PostTags = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }

        [JsonIgnore]
        public string AttachedImageFile { get => Photo.FirstOrDefault()?.Path ?? ""; set { if (Photo.Count == 0) Photo.Add(new StardewConnectPhoto()); Photo[0].Path = value; } }
        [JsonIgnore]
        public bool AttachmentFromPlayerFolder { get => true; set { } }
        [JsonIgnore]
        public List<StardewConnectPostAttachment> Attachments
        {
            get => Photo.Select(p => new StardewConnectPostAttachment { ImageFile = p.Path, FromPlayerFolder = true, ImageTag = p.Tag }).ToList();
            set => Photo = value.Select(a => new StardewConnectPhoto { Path = a.ImageFile, Tag = a.ImageTag }).ToList();
        }
    }

    public sealed class StardewConnectProfileStats
    {
        public string ActorName { get; set; } = "";
        public bool ActorIsPlayer { get; set; }
        public int TotalPosts { get; set; }
        public int TotalLikesReceived { get; set; }
        public int TotalCommentsReceived { get; set; }
        public int TotalLikesGiven { get; set; }
        public int TotalCommentsGiven { get; set; }
        public Dictionary<string, int> InteractionsFrom { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> InteractionsTo { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class StardewConnectProfileInteraction
    {
        public string ActorName { get; set; } = "";
        public bool ActorIsPlayer { get; set; }
        public int Count { get; set; }
    }

    public sealed class StardewSocialNotification
    {
        public string Id { get; set; } = "";
        public string PostId { get; set; } = "";
        public string Text { get; set; } = "";
        public long Timestamp { get; set; }
    }

    public static class StardewConnectManager
    {
        private static readonly List<StardewConnectPost> Posts = new();
        private static readonly HashSet<string> DismissedNotifications = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, StardewConnectProfileStats> ProfileStats = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string SaveFileName = "post_data.json";
        private static readonly Random random = new();

        static StardewConnectManager()
        {
            Load();
        }

        public static string GeneratePostId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            char[] stringChars = new char[9];
            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
            return new string(stringChars);
        }

        /// <summary>Alias used by the social simulation helpers.</summary>
        public static string CreateShortAlphanumericId() => GeneratePostId();

        private static string NormalizeSaveFolderName(string saveFolderName)
        {
            string normalizedValue = (saveFolderName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new System.Text.StringBuilder(normalizedValue.Length);
            foreach (char character in normalizedValue)
            {
                if (character == '/' || character == '\\' || Array.IndexOf(invalidChars, character) >= 0)
                    continue;

                builder.Append(character);
            }

            return builder.ToString().Trim();
        }

        public static string GetActiveSaveFolderName()
        {
            string constantsSaveFolder = NormalizeSaveFolderName(StardewModdingAPI.Constants.SaveFolderName);

            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
            {
                int underscoreIndex = constantsSaveFolder.IndexOf('_');
                if (underscoreIndex != -1)
                {
                    return constantsSaveFolder.Substring(underscoreIndex);
                }
            }

            long uniqueId = 0;
            if (StardewModdingAPI.Context.IsWorldReady && StardewModdingAPI.Context.IsMultiplayer && Game1.MasterPlayer != null)
                uniqueId = Game1.MasterPlayer.UniqueMultiplayerID;
            else if (StardewModdingAPI.Context.IsWorldReady && Game1.player != null)
                uniqueId = Game1.player.UniqueMultiplayerID;

            if (uniqueId > 0)
                return $"_{uniqueId}";

            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
            {
                int lastUnderscore = constantsSaveFolder.LastIndexOf('_');
                if (lastUnderscore >= 0 && lastUnderscore < constantsSaveFolder.Length - 1)
                {
                    string possibleId = constantsSaveFolder.Substring(lastUnderscore + 1);
                    if (long.TryParse(possibleId, out _))
                        return $"_{possibleId}";
                }
                return constantsSaveFolder;
            }

            return "default";
        }

        public static string GetSaveFilePath()
        {
            string saveFolder = GetActiveSaveFolderName();
            return Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, SaveFileName);
        }

        private static void TrimPosts()
        {
            lock (Posts)
            {
                int maxPosts = ModEntry.Config?.MaxStardewConnectPosts ?? 100;
                if (maxPosts <= 0) maxPosts = 100;

                if (Posts.Count > maxPosts)
                {
                    var sorted = Posts.OrderBy(p => p.CreatedTime.Timestamp).ToList();
                    int removeCount = sorted.Count - maxPosts;
                    for (int i = 0; i < removeCount; i++)
                    {
                        Posts.Remove(sorted[i]);
                    }
                    RebuildProfileStatsFromPosts();
                }
            }
        }

        public static void EnforcePhotoSharedRetention()
        {
            try
            {
                string saveFolder = GetActiveSaveFolderName();
                string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                if (!Directory.Exists(photoSharedDir))
                    return;

                int maxPhotos = ModEntry.Config?.MaxPhoto ?? 200;
                if (maxPhotos <= 0) maxPhotos = 200;

                var files = new List<FileInfo>();
                foreach (string filePath in Directory.GetFiles(photoSharedDir))
                {
                    if (Path.GetFileName(filePath).Contains("_avatar", StringComparison.OrdinalIgnoreCase))
                        continue;
                    files.Add(new FileInfo(filePath));
                }

                if (files.Count <= maxPhotos)
                    return;

                files.Sort((a, b) => a.LastWriteTime.CompareTo(b.LastWriteTime));

                int deleteCount = files.Count - maxPhotos;
                for (int i = 0; i < deleteCount; i++)
                {
                    try
                    {
                        files[i].Delete();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Failed to delete old photo file {files[i].FullName}: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Error enforcing photo shared retention: {ex.Message}", StardewModdingAPI.LogLevel.Error);
            }
        }

        public static void Save()
        {
            try
            {
                string filePath = GetSaveFilePath();
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string json = JsonConvert.SerializeObject(Posts, Formatting.Indented);
                File.WriteAllText(filePath, json);

                string statsFilePath = Path.Combine(directory, "profile_stat.json");
                string statsJson = JsonConvert.SerializeObject(ProfileStats, Formatting.Indented);
                File.WriteAllText(statsFilePath, statsJson);

                string dismissedFilePath = Path.Combine(directory, "dismissed_notifications.json");
                string dismissedJson = JsonConvert.SerializeObject(DismissedNotifications, Formatting.Indented);
                File.WriteAllText(dismissedFilePath, dismissedJson);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to save posts/stats: {ex}", StardewModdingAPI.LogLevel.Error);
            }
        }

        public static void Load()
        {
            try
            {
                string filePath = GetSaveFilePath();
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonConvert.DeserializeObject<List<StardewConnectPost>>(json);
                    if (loaded != null)
                    {
                        lock (Posts)
                        {
                            Posts.Clear();
                            Posts.AddRange(loaded);
                        }
                    }
                }

                string directory = Path.GetDirectoryName(filePath);
                string statsFilePath = Path.Combine(directory, "profile_stat.json");
                if (File.Exists(statsFilePath))
                {
                    string json = File.ReadAllText(statsFilePath);
                    var loadedStats = JsonConvert.DeserializeObject<Dictionary<string, StardewConnectProfileStats>>(json);
                    if (loadedStats != null)
                    {
                        lock (ProfileStats)
                        {
                            ProfileStats.Clear();
                            foreach (var pair in loadedStats)
                            {
                                ProfileStats[pair.Key] = pair.Value;
                            }
                        }
                    }
                }
                else
                {
                    RebuildProfileStatsFromPosts();
                }

                string dismissedFilePath = Path.Combine(directory, "dismissed_notifications.json");
                if (File.Exists(dismissedFilePath))
                {
                    string json = File.ReadAllText(dismissedFilePath);
                    var loadedDismissed = JsonConvert.DeserializeObject<HashSet<string>>(json);
                    if (loadedDismissed != null)
                    {
                        lock (DismissedNotifications)
                        {
                            DismissedNotifications.Clear();
                            foreach (var id in loadedDismissed)
                            {
                                DismissedNotifications.Add(id);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load posts/stats: {ex}", StardewModdingAPI.LogLevel.Error);
            }
        }

        public static long GetLastVisitTime()
        {
            try
            {
                string saveFolder = GetActiveSaveFolderName();
                string filePath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "last_visit.json");
                if (File.Exists(filePath))
                {
                    string txt = File.ReadAllText(filePath);
                    if (long.TryParse(txt.Trim(), out long timestamp))
                    {
                        return timestamp;
                    }
                }
            }
            catch { }
            return 0;
        }

        public static void SaveLastVisitTime()
        {
            try
            {
                string saveFolder = GetActiveSaveFolderName();
                string filePath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "last_visit.json");
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                File.WriteAllText(filePath, now.ToString());
            }
            catch { }
        }

        public static List<StardewConnectPost> GetPostsSnapshot()
        {
            lock (Posts)
            {
                return Posts.OrderBy(p => p.CreatedTime.Timestamp).ToList();
            }
        }

        public static StardewConnectPost? GetPost(string id)
        {
            lock (Posts)
            {
                return Posts.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static bool IsSocialNotificationDismissed(string key)
        {
            return DismissedNotifications.Contains(key);
        }

        public static void DismissSocialNotification(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                lock (DismissedNotifications)
                {
                    DismissedNotifications.Add(key);
                }
                Save();
            }
        }

        public static void DismissSocialNotifications(string[] notificationIds)
        {
            lock (DismissedNotifications)
            {
                foreach (var id in notificationIds)
                {
                    DismissedNotifications.Add(id);
                }
            }
            Save();
        }

        public static void PruneSocialNotificationDismissals(IEnumerable<string> activeKeys)
        {
        }

        public static int GetPlayerReadCommentCount(StardewConnectPost post, string playerName)
        {
            if (post == null)
                return 0;

            post.Comments ??= new List<StardewConnectComment>();
            post.PlayerReadCommentCounts ??= new List<StardewConnectPlayerReadCommentCount>();

            string normalizedPlayerName = (playerName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedPlayerName))
                normalizedPlayerName = Game1.player?.Name ?? "Player";

            if (string.IsNullOrWhiteSpace(normalizedPlayerName))
                return 0;

            StardewConnectPlayerReadCommentCount? found = post.PlayerReadCommentCounts
                .FirstOrDefault(entry => entry != null
                    && string.Equals(entry.PlayerName ?? "", normalizedPlayerName, StringComparison.OrdinalIgnoreCase));

            if (found == null)
                return 0;

            return Math.Clamp(found.Count, 0, post.Comments.Count);
        }

        public static void SetPlayerReadCommentCount(StardewConnectPost post, string playerName, int count)
        {
            if (post == null)
                return;

            post.Comments ??= new List<StardewConnectComment>();
            post.PlayerReadCommentCounts ??= new List<StardewConnectPlayerReadCommentCount>();

            string normalizedPlayerName = (playerName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedPlayerName))
                normalizedPlayerName = Game1.player?.Name ?? "Player";

            if (string.IsNullOrWhiteSpace(normalizedPlayerName))
                return;

            int normalizedCount = Math.Clamp(count, 0, post.Comments.Count);

            StardewConnectPlayerReadCommentCount? existing = post.PlayerReadCommentCounts
                .FirstOrDefault(entry => entry != null
                    && string.Equals(entry.PlayerName ?? "", normalizedPlayerName, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                post.PlayerReadCommentCounts.Add(new StardewConnectPlayerReadCommentCount
                {
                    PlayerName = normalizedPlayerName,
                    Count = normalizedCount
                });
            }
            else
            {
                existing.PlayerName = normalizedPlayerName;
                existing.Count = normalizedCount;
            }
        }

        public static void MarkPostCommentsRead(string postId)
        {
            var post = GetPost(postId);
            if (post != null)
            {
                SetPlayerReadCommentCount(post, Game1.player?.Name ?? "Player", post.Comments.Count);
                Save();
            }
        }

        public static bool TogglePostLikeByPlayer(string postId)
        {
            var post = GetPost(postId);
            if (post == null) return false;

            bool result;
            string likerName = Game1.player?.Name ?? "Player";
            lock (post.LikedBy)
            {
                bool alreadyLiked = post.LikedBy.Contains("Player") || post.LikedBy.Contains(likerName);
                if (alreadyLiked)
                {
                    post.LikedBy.Remove("Player");
                    post.LikedBy.Remove(likerName);
                    result = false;
                }
                else
                {
                    post.LikedBy.Add(likerName);
                    result = true;
                }
            }
            ApplyLikeStats(post, likerName, true, result);
            Save();
            return result;
        }

        public static bool IsPostLikedByPlayer(StardewConnectPost post)
        {
            if (post?.LikedBy == null) return false;
            string likerName = Game1.player?.Name ?? "Player";
            return post.LikedBy.Contains("Player", StringComparer.OrdinalIgnoreCase) || post.LikedBy.Contains(likerName, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsPostLikedBy(StardewConnectPost post, string actorName)
        {
            if (post == null || string.IsNullOrWhiteSpace(actorName)) return false;
            return post.LikedBy.Contains(actorName, StringComparer.OrdinalIgnoreCase);
        }

        public static int GetUnreadCommentCount(StardewConnectPost post)
        {
            return 0;
        }

        public static List<StardewConnectPost> GetPostsByAuthor(string name, bool isPlayer)
        {
            lock (Posts)
            {
                return Posts.Where(p => p.AuthorIsPlayer == isPlayer && string.Equals(p.AuthorName, name, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(p => p.CreatedTime.Timestamp)
                            .ToList();
            }
        }

        public static StardewConnectProfileStats GetProfileStatsSnapshot(string actorName, bool actorIsPlayer)
        {
            lock (ProfileStats)
            {
                string key = BuildActorKey(actorName, actorIsPlayer);
                if (ProfileStats.TryGetValue(key, out var stats) && stats != null)
                {
                    return CloneProfileStats(stats);
                }
                return new StardewConnectProfileStats
                {
                    ActorName = actorName,
                    ActorIsPlayer = actorIsPlayer,
                    InteractionsFrom = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    InteractionsTo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                };
            }
        }

        public static List<StardewConnectProfileInteraction> GetTopInteractionsFrom(string actorName, bool actorIsPlayer, int count = 3)
        {
            var stats = GetProfileStatsSnapshot(actorName, actorIsPlayer);
            return BuildTopInteractionList(stats.InteractionsFrom, count);
        }

        public static List<StardewConnectProfileInteraction> GetTopInteractionsTo(string actorName, bool actorIsPlayer, int count = 3)
        {
            var stats = GetProfileStatsSnapshot(actorName, actorIsPlayer);
            return BuildTopInteractionList(stats.InteractionsTo, count);
        }

        public static int GetAttachmentCount(StardewConnectPost post)
        {
            return post?.Photo?.Count ?? 0;
        }

        public static string ResolveAttachmentAbsolutePath(StardewConnectPost post, int index)
        {
            if (post?.Photo == null || index < 0 || index >= post.Photo.Count)
                return "";
            
            string saveFolder = GetActiveSaveFolderName();
            return Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared", post.Photo[index].Path);
        }

        public static string ResolveSharedPlayerAvatarAbsolutePath(string actorName)
        {
            return "";
        }

        public static void MarkSocialAppVisitedNow()
        {
            SaveLastVisitTime();
        }

        public static void AddPlayerPost(string text, List<string>? attachedImages = null, List<string>? tagged = null)
        {
            string id = GeneratePostId();
            var newPost = new StardewConnectPost
            {
                Id = id,
                AuthorName = Game1.player?.Name ?? "Player",
                AuthorIsPlayer = true,
                Text = text,
                CreatedTime = new StardewConnectTime
                {
                    Season = Game1.currentSeason ?? "spring",
                    Day = Game1.dayOfMonth,
                    Year = Game1.year,
                    TimeOfDay = Game1.timeOfDay,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                },
                LikedBy = new List<string>()
            };

            if (tagged != null)
            {
                newPost.Tagged.AddRange(tagged);
            }

            if (attachedImages != null && attachedImages.Count > 0)
            {
                foreach (var img in attachedImages)
                {
                    string tag = "";
                    if (ModEntry.iSmartphoneApi != null)
                    {
                        try
                        {
                            string metaJson = ModEntry.iSmartphoneApi.GetPlayerPhotoMetadata(img);
                            if (!string.IsNullOrWhiteSpace(metaJson))
                            {
                                var meta = Newtonsoft.Json.Linq.JObject.Parse(metaJson);
                                if (meta != null && meta.TryGetValue("tag", out var tagVal))
                                {
                                    tag = tagVal.ToString();
                                }
                            }
                        }
                        catch { }
                    }

                    newPost.Photo.Add(new StardewConnectPhoto
                    {
                        Path = img,
                        Tag = tag
                    });
                }

                var uniqueTags = newPost.Photo
                    .SelectMany(p => (p.Tag ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                newPost.PostTags = uniqueTags;
            }

            lock (Posts)
            {
                Posts.Add(newPost);
            }
            TrimPosts();

            var authorStats = GetOrCreateProfileStats(newPost.AuthorName, true);
            authorStats.TotalPosts++;

            Save();
        }

        public static void AddPlayerComment(string postId, string text)
        {
            var post = GetPost(postId);
            if (post == null) return;

            string id = "player_comment_" + Guid.NewGuid().ToString().Substring(0, 8);
            var newComment = new StardewConnectComment
            {
                Id = id,
                AuthorName = Game1.player?.Name ?? "Player",
                AuthorIsPlayer = true,
                Text = text,
                CreatedTime = new StardewConnectTime
                {
                    Season = Game1.currentSeason ?? "spring",
                    Day = Game1.dayOfMonth,
                    Year = Game1.year,
                    TimeOfDay = Game1.timeOfDay,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };
            lock (post.Comments)
            {
                post.Comments.Add(newComment);
            }
            ApplyCommentStats(post, newComment.AuthorName, true);
            Save();
        }

        public static bool DeletePost(string postId)
        {
            var post = GetPost(postId);
            if (post == null) return false;
            bool deleted;
            lock (Posts)
            {
                deleted = Posts.Remove(post);
            }
            if (deleted)
            {
                RebuildProfileStatsFromPosts();
                Save();
            }
            return deleted;
        }

        public static List<StardewSocialNotification> GetActiveSocialNotifications()
        {
            var notifications = new List<StardewSocialNotification>();
            string playerName = Game1.player?.Name ?? "Player";
            string tagPattern = "@" + playerName;

            var posts = GetPostsSnapshot();
            foreach (var post in posts)
            {
                if (post.AuthorIsPlayer)
                {
                    // 1. Comment notifications
                    var npcComments = post.Comments
                        .Where(c => !c.AuthorIsPlayer)
                        .OrderBy(c => c.CreatedTime.Timestamp)
                        .ToList();

                    if (npcComments.Count > 0)
                    {
                        string commentNotifId = "comment_" + post.Id;
                        bool isDismissed;
                        lock (DismissedNotifications)
                        {
                            isDismissed = DismissedNotifications.Contains(commentNotifId);
                        }

                        if (!isDismissed)
                        {
                            var uniqueNpcs = new List<string>();
                            foreach (var c in npcComments)
                            {
                                string cleanName = Game1.getCharacterFromName(c.AuthorName)?.displayName ?? c.AuthorName;
                                if (!uniqueNpcs.Contains(cleanName))
                                {
                                    uniqueNpcs.Add(cleanName);
                                }
                            }

                            string text = "";
                            if (uniqueNpcs.Count == 1)
                            {
                                text = $"{uniqueNpcs[0]} has commented on your post.";
                            }
                            else if (uniqueNpcs.Count == 2)
                            {
                                text = $"{uniqueNpcs[0]} and {uniqueNpcs[1]} has commented on your post.";
                            }
                            else
                            {
                                text = $"{uniqueNpcs[0]}, {uniqueNpcs[1]} and others has commented on your post.";
                            }

                            notifications.Add(new StardewSocialNotification
                            {
                                Id = commentNotifId,
                                PostId = post.Id,
                                Text = text,
                                Timestamp = npcComments.Last().CreatedTime.Timestamp
                            });
                        }
                    }

                    // 2. Tag notifications
                    foreach (var comment in npcComments)
                    {
                        if ((comment.Text ?? "").Contains(tagPattern, StringComparison.OrdinalIgnoreCase))
                        {
                            string tagNotifId = "tag_" + comment.Id;
                            bool isDismissed;
                            lock (DismissedNotifications)
                            {
                                isDismissed = DismissedNotifications.Contains(tagNotifId);
                            }

                            if (!isDismissed)
                            {
                                string cleanAuthorName = Game1.getCharacterFromName(comment.AuthorName)?.displayName ?? comment.AuthorName;
                                notifications.Add(new StardewSocialNotification
                                {
                                    Id = tagNotifId,
                                    PostId = post.Id,
                                    Text = $"{cleanAuthorName} has tagged you in a comment.",
                                    Timestamp = comment.CreatedTime.Timestamp
                                });
                            }
                        }
                    }
                }
            }

            return notifications.OrderByDescending(n => n.Timestamp).ToList();
        }

        public static int GetActiveSocialNotificationCount()
        {
            return GetActiveSocialNotifications().Count;
        }

        private static string BuildActorKey(string actorName, bool actorIsPlayer)
        {
            string prefix = actorIsPlayer ? "P" : "N";
            return $"{prefix}|{actorName}";
        }

        private static bool TryParseActorKey(string actorKey, out string actorName, out bool actorIsPlayer)
        {
            actorName = "";
            actorIsPlayer = false;

            if (string.IsNullOrWhiteSpace(actorKey) || actorKey.Length < 3)
                return false;

            actorIsPlayer = actorKey.StartsWith("P|", StringComparison.OrdinalIgnoreCase);
            if (!actorIsPlayer && !actorKey.StartsWith("N|", StringComparison.OrdinalIgnoreCase))
                return false;

            actorName = actorKey.Substring(2).Trim();
            return !string.IsNullOrWhiteSpace(actorName);
        }

        private static StardewConnectProfileStats GetOrCreateProfileStats(string actorName, bool actorIsPlayer)
        {
            string key = BuildActorKey(actorName, actorIsPlayer);
            lock (ProfileStats)
            {
                if (!ProfileStats.TryGetValue(key, out var stats))
                {
                    stats = new StardewConnectProfileStats
                    {
                        ActorName = actorName,
                        ActorIsPlayer = actorIsPlayer,
                        InteractionsFrom = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                        InteractionsTo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    };
                    ProfileStats[key] = stats;
                }
                return stats;
            }
        }

        private static void ApplyLikeStats(StardewConnectPost post, string actorName, bool actorIsPlayer, bool added)
        {
            StardewConnectProfileStats sourceStats = GetOrCreateProfileStats(actorName, actorIsPlayer);
            StardewConnectProfileStats targetStats = GetOrCreateProfileStats(post.AuthorName, post.AuthorIsPlayer);

            int delta = added ? 1 : -1;
            sourceStats.TotalLikesGiven = Math.Max(0, sourceStats.TotalLikesGiven + delta);
            targetStats.TotalLikesReceived = Math.Max(0, targetStats.TotalLikesReceived + delta);

            string sourceKey = BuildActorKey(actorName, actorIsPlayer);
            string targetKey = BuildActorKey(post.AuthorName, post.AuthorIsPlayer);
            if (!string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                ApplyInteractionDelta(sourceStats.InteractionsTo, targetKey, delta);
                ApplyInteractionDelta(targetStats.InteractionsFrom, sourceKey, delta);
            }
        }

        private static void ApplyCommentStats(StardewConnectPost post, string actorName, bool actorIsPlayer)
        {
            StardewConnectProfileStats sourceStats = GetOrCreateProfileStats(actorName, actorIsPlayer);
            StardewConnectProfileStats targetStats = GetOrCreateProfileStats(post.AuthorName, post.AuthorIsPlayer);

            sourceStats.TotalCommentsGiven++;
            targetStats.TotalCommentsReceived++;

            string sourceKey = BuildActorKey(actorName, actorIsPlayer);
            string targetKey = BuildActorKey(post.AuthorName, post.AuthorIsPlayer);
            if (!string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                ApplyInteractionDelta(sourceStats.InteractionsTo, targetKey, 1);
                ApplyInteractionDelta(targetStats.InteractionsFrom, sourceKey, 1);
            }
        }

        private static void ApplyInteractionDelta(Dictionary<string, int> map, string key, int delta)
        {
            if (map == null || string.IsNullOrWhiteSpace(key) || delta == 0)
                return;

            map.TryGetValue(key, out int currentValue);
            int next = Math.Max(0, currentValue + delta);
            if (next <= 0)
                map.Remove(key);
            else
                map[key] = next;
        }

        private static StardewConnectProfileStats CloneProfileStats(StardewConnectProfileStats stats)
        {
            return new StardewConnectProfileStats
            {
                ActorName = stats.ActorName,
                ActorIsPlayer = stats.ActorIsPlayer,
                TotalPosts = stats.TotalPosts,
                TotalLikesReceived = stats.TotalLikesReceived,
                TotalCommentsReceived = stats.TotalCommentsReceived,
                TotalLikesGiven = stats.TotalLikesGiven,
                TotalCommentsGiven = stats.TotalCommentsGiven,
                InteractionsFrom = new Dictionary<string, int>(stats.InteractionsFrom ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase),
                InteractionsTo = new Dictionary<string, int>(stats.InteractionsTo ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase)
            };
        }

        public static void RebuildProfileStatsFromPosts()
        {
            lock (ProfileStats)
            {
                ProfileStats.Clear();
            }
            lock (Posts)
            {
                foreach (var post in Posts)
                {
                    var authorStats = GetOrCreateProfileStats(post.AuthorName, post.AuthorIsPlayer);
                    authorStats.TotalPosts++;

                    post.LikedBy ??= new List<string>();
                    foreach (var liker in post.LikedBy)
                    {
                        bool likerIsPlayer = string.Equals(liker, "Player", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(liker, Game1.player?.Name ?? "Player", StringComparison.OrdinalIgnoreCase);
                        ApplyLikeStats(post, liker, likerIsPlayer, true);
                    }

                    post.Comments ??= new List<StardewConnectComment>();
                    foreach (var comment in post.Comments)
                    {
                        ApplyCommentStats(post, comment.AuthorName, comment.AuthorIsPlayer);
                    }
                }
            }
        }

        private static List<StardewConnectProfileInteraction> BuildTopInteractionList(Dictionary<string, int>? interactions, int count)
        {
            if (interactions == null || interactions.Count == 0 || count <= 0)
                return new List<StardewConnectProfileInteraction>();

            var result = new List<StardewConnectProfileInteraction>();
            foreach (var pair in interactions)
            {
                if (pair.Value <= 0)
                    continue;

                if (!TryParseActorKey(pair.Key, out string targetName, out bool targetIsPlayer))
                    continue;

                result.Add(new StardewConnectProfileInteraction
                {
                    ActorName = targetName,
                    ActorIsPlayer = targetIsPlayer,
                    Count = pair.Value
                });
            }

            return result
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.ActorName, StringComparer.OrdinalIgnoreCase)
                .Take(count)
                .ToList();
        }

        // ===== NPC social simulation helpers =====

        /// <summary>Adds a new post by an NPC. Returns the post ID, or null if failed.</summary>
        public static string? AddNpcPostWithAttachments(string npcName, string postText, IEnumerable<string>? attachedImageFiles)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return null;

            if (Game1.getCharacterFromName(npcName, mustBeVillager: false) == null)
                return null;

            string text = (postText ?? string.Empty).Trim();
            var photos = new List<StardewConnectPhoto>();

            foreach (string file in attachedImageFiles ?? Enumerable.Empty<string>())
            {
                string trimmedFile = (file ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(trimmedFile))
                    continue;

                // Extract tag from the NPC photo metadata stored by the framework's CaptureNpcPhoto
                string tag = ModEntry.GetNpcPhotoTag(trimmedFile);
                string fileName = System.IO.Path.GetFileName(trimmedFile);
                string saveFolder = GetActiveSaveFolderName();
                string photoSharedDir = System.IO.Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                System.IO.Directory.CreateDirectory(photoSharedDir);
                string destPath = System.IO.Path.Combine(photoSharedDir, fileName);

                try
                {
                    if (System.IO.File.Exists(trimmedFile))
                    {
                        if (System.IO.File.Exists(destPath))
                            System.IO.File.Delete(destPath);
                        System.IO.File.Move(trimmedFile, destPath);
                        EnforcePhotoSharedRetention();
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to move NPC photo from temp to shared: {ex.Message}", StardewModdingAPI.LogLevel.Error);
                }

                photos.Add(new StardewConnectPhoto { Path = fileName, Tag = tag });
            }

            if (string.IsNullOrWhiteSpace(text) && photos.Count == 0)
                return null;

            string id = CreateShortAlphanumericId();
            var uniqueTags = photos
                .SelectMany(p => (p.Tag ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var post = new StardewConnectPost
            {
                Id = id,
                AuthorName = npcName,
                AuthorIsPlayer = false,
                Text = text,
                Photo = photos,
                PostTags = uniqueTags,
                CreatedTime = new StardewConnectTime
                {
                    Season = Game1.currentSeason ?? "spring",
                    Day = Game1.dayOfMonth,
                    Year = Game1.year,
                    TimeOfDay = Game1.timeOfDay,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                },
                LikedBy = new List<string>(),
                Comments = new List<StardewConnectComment>()
            };

            lock (Posts)
            {
                Posts.Add(post);
            }
            TrimPosts();

            var authorStats = GetOrCreateProfileStats(npcName, false);
            authorStats.TotalPosts++;

            Save();
            return id;
        }

        /// <summary>Adds a comment by an NPC on a post. Returns true on success.</summary>
        public static bool AddNpcComment(string postId, string npcName, string commentText)
        {
            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(npcName))
                return false;

            if (Game1.getCharacterFromName(npcName, mustBeVillager: true) == null)
                return false;

            StardewConnectPost? post = GetPost(postId);
            if (post == null)
                return false;

            string text = (commentText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Prevent NPC author from re-commenting unless other NPCs/players already commented
            if (!post.AuthorIsPlayer && string.Equals(post.AuthorName, npcName, StringComparison.OrdinalIgnoreCase))
            {
                bool hasOtherComments = post.Comments != null
                    && post.Comments.Any(c => c != null && !string.Equals(c.AuthorName, npcName, StringComparison.OrdinalIgnoreCase));

                if (!hasOtherComments)
                    return false;
            }

            var comment = new StardewConnectComment
            {
                Id = CreateShortAlphanumericId(),
                AuthorName = npcName,
                AuthorIsPlayer = false,
                Text = text,
                CreatedTime = new StardewConnectTime
                {
                    Season = Game1.currentSeason ?? "spring",
                    Day = Game1.dayOfMonth,
                    Year = Game1.year,
                    TimeOfDay = Game1.timeOfDay,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            };

            lock (post.Comments)
            {
                post.Comments.Add(comment);
            }

            ApplyCommentStats(post, npcName, false);
            Save();
            return true;
        }

        /// <summary>Sets or clears an NPC like on a post.</summary>
        public static bool SetPostLike(string postId, string npcName, bool liked)
        {
            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(npcName))
                return false;

            StardewConnectPost? post = GetPost(postId);
            if (post == null)
                return false;

            bool changed = false;
            lock (post.LikedBy)
            {
                bool alreadyLiked = post.LikedBy.Contains(npcName, StringComparer.OrdinalIgnoreCase);

                if (liked && !alreadyLiked)
                {
                    post.LikedBy.Add(npcName);
                    changed = true;
                }
                else if (!liked && alreadyLiked)
                {
                    post.LikedBy.RemoveAll(n => string.Equals(n, npcName, StringComparison.OrdinalIgnoreCase));
                    changed = true;
                }
            }

            if (changed)
            {
                ApplyLikeStats(post, npcName, false, liked);
                Save();
            }

            return changed;
        }

        private const int DaysPerSeason = 28;
        private const int SeasonsPerYear = 4;

        private static int GetSeasonIndex(string season)
        {
            return (season ?? string.Empty).ToLowerInvariant() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => -1
            };
        }

        private static int GetAbsoluteDayIndex(string season, int day, int year)
        {
            int seasonIndex = GetSeasonIndex(season);
            if (seasonIndex < 0) seasonIndex = 0;
            int safeDay = Math.Clamp(day, 1, DaysPerSeason);
            int safeYear = Math.Max(1, year);
            return ((safeYear - 1) * SeasonsPerYear * DaysPerSeason)
                + (seasonIndex * DaysPerSeason)
                + (safeDay - 1);
        }

        private static List<StardewConnectPost> GetPostsWithinPastDays(int startDay, int endDays)
        {
            lock (Posts)
            {
                if (Posts.Count == 0 || startDay < 0 || endDays < 0 || endDays < startDay)
                    return new List<StardewConnectPost>();

                int currentDayIndex = GetAbsoluteDayIndex(Game1.currentSeason, Game1.dayOfMonth, Game1.year);
                int startDayIndex = currentDayIndex - startDay;
                int endDayIndex = currentDayIndex - endDays;

                return Posts.Where(post =>
                {
                    int postDayIndex = GetAbsoluteDayIndex(post.Season, post.Day, post.Year);
                    return postDayIndex >= endDayIndex && postDayIndex <= startDayIndex;
                }).ToList();
            }
        }

        public static List<string> GetRandomPostIdWithinDayRange(int count = 1, int startDay = 0, int endDay = 1)
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(startDay, endDay);
            if (recentPosts.Count == 0)
                return new List<string>();

            var result = new List<string>();
            for (int i = 0; i < count; i++)
                result.Add(recentPosts[Game1.random.Next(recentPosts.Count)].Id);

            return result;
        }

        public static string GetOnePopularPostIdWithinDayRange(int startDay = 1, int endDay = 2)
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(startDay, endDay);
            if (recentPosts.Count == 0)
                return string.Empty;

            var topPosts = recentPosts
                .OrderByDescending(post => post.LikedBy.Count)
                .Take(3)
                .ToList();

            return topPosts[Game1.random.Next(topPosts.Count)].Id;
        }

    }
}
