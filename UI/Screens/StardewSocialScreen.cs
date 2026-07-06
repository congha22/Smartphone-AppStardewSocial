using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TextCopy;

namespace SmartphoneAppStardewSocial
{
    public partial class StardewSocialScreen : IClickableMenu, IKeyboardSubscriber
    {
        private readonly ISmartPhoneApi smartphoneApi;
        private readonly Action onBack;

        // Layout settings
        private int phoneFrameWidth;
        private int phoneFrameHeight;
        private int phoneContentOffsetX;
        private int phoneContentOffsetY;
        private float phoneUiScale;

        private Texture2D? phoneFrameTexture;
        private Texture2D? phoneBackgroundTexture;

        private int contentWidth;
        private int contentHeight;

        // Navigation state
        private bool socialCreateMenuOpen = false;
        private bool socialProfileMenuOpen = false;
        private bool socialNotificationMenuOpen = false;
        private bool socialProfileDetailBackStack = false;
        private string selectedSocialPostId = "";

        // Profile details
        private string selectedSocialProfileActorName = "";
        private bool selectedSocialProfileActorIsPlayer = true;

        // Scrolling Targets (float)
        private float socialFeedScrollOffset = 0f;
        private float socialFeedScrollTarget = 0f;
        private float socialNotificationScrollOffset = 0f;
        private float socialNotificationScrollTarget = 0f;
        private float socialProfileScrollOffset = 0f;
        private float socialProfileScrollTarget = 0f;
        private float socialDetailScrollOffset = 0f;
        private float socialDetailScrollTarget = 0f;

        private int maxScrollFeed = 0;
        private int maxScrollNotification = 0;
        private int maxScrollProfile = 0;
        private int maxScrollDetail = 0;

        private bool initializedScrollTarget = false;
        private readonly Dictionary<string, Texture2D> postPhotoTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> postPhotoIndices = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedPhotoPrevBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedPhotoNextBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialDetailPhotoPrevBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialDetailPhotoNextBounds = new(StringComparer.OrdinalIgnoreCase);
        private string hoverText = "";
        private string customTagTooltipText = "";
        private List<string> customLikeTooltipNames = null;
        private int tooltipMouseX = 0;
        private int tooltipMouseY = 0;

        public static string FormatTime(string season, int day, int timeOfDay)
        {
            int hours = (timeOfDay / 100) % 24;
            int minutes = timeOfDay % 100;
            string ampm = "am";
            if (hours >= 12)
            {
                ampm = "pm";
                if (hours > 12) hours -= 12;
            }
            else if (hours == 0)
            {
                hours = 12;
            }
            string timeString = $"{hours}:{minutes:D2}{ampm}";

            string capSeason = string.IsNullOrEmpty(season) ? "" : char.ToUpper(season[0]) + season.Substring(1).ToLower();

            return ModEntry.SHelper.Translation.Get("time.format", new { time = timeString, season = capSeason, day = day.ToString() });
        }

        private Texture2D? GetPostPhotoTexture(StardewConnectPost post, StardewConnectPhoto photo)
        {
            if (string.IsNullOrWhiteSpace(photo.Path)) return null;

            if (this.postPhotoTextureCache.TryGetValue(photo.Path, out var cached) && cached != null && !cached.IsDisposed)
            {
                return cached;
            }

            string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
            string localSharedPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared", photo.Path);
            if (File.Exists(localSharedPath))
            {
                try
                {
                    using var stream = new FileStream(localSharedPath, FileMode.Open, FileAccess.Read);
                    var tex = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                    this.postPhotoTextureCache[photo.Path] = tex;
                    return tex;
                }
                catch { }
            }

            if (this.smartphoneApi != null)
            {
                try
                {
                    var tex = this.smartphoneApi.GetPlayerPhotoTexture(photo.Path);
                    if (tex != null && !tex.IsDisposed)
                    {
                        this.postPhotoTextureCache[photo.Path] = tex;
                        return tex;
                    }
                }
                catch { }
            }

            return null;
        }

        // Clickable bounds
        private Rectangle socialFeedOpenCreatePostBounds = Rectangle.Empty;
        private Rectangle socialFeedOpenProfileBounds = Rectangle.Empty;
        private Rectangle socialFeedOpenNotificationBounds = Rectangle.Empty;
        private Rectangle socialNotificationClearAllBounds = Rectangle.Empty;
        private Rectangle socialDetailCommentSendBounds = Rectangle.Empty;
        private Rectangle socialDetailLikeBounds = Rectangle.Empty;
        private Rectangle socialDetailDeletePostBounds = Rectangle.Empty;
        private Rectangle socialProfileAvatarCameraButtonBounds = Rectangle.Empty;
        private Rectangle socialCreateSubmitBounds = Rectangle.Empty;
        private Rectangle socialCreateEmojiButtonBounds = Rectangle.Empty;
        private Rectangle socialCreatePhotoSelectBounds = Rectangle.Empty;
        private Rectangle socialCreateCancelBounds = Rectangle.Empty;
        private Rectangle socialCreatePhotoPrevBounds = Rectangle.Empty;
        private Rectangle socialCreatePhotoNextBounds = Rectangle.Empty;
        private int draftPhotoPreviewIndex = 0;

        private readonly ChatBox mockChatBox = new();
        private EmojiMenu? emojiMenu;
        private readonly List<SelectedPhotoResult> draftSelectedPhotos = new();
        private readonly List<Texture2D> draftSelectedTextures = new();

        private readonly Dictionary<string, Rectangle> socialFeedPostBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedLikeBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedCommentBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfilePostBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfileLikeBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedLikeHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfileLikeHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialNotificationItemBounds = new(StringComparer.OrdinalIgnoreCase);

        // Text inputs
        private EditableTextBox commentTextBox = new() { IsMultiline = false };
        private EditableTextBox postTextBox = new() { IsMultiline = true };
        private EditableTextBox tagSearchTextBox = new() { IsMultiline = false };
        private bool socialTagMenuOpen = false;
        private readonly List<string> draftTagged = new();
        private float tagMenuScrollOffset = 0f;
        private float tagMenuScrollTarget = 0f;
        private int maxScrollTagMenu = 0;
        private readonly Dictionary<string, Rectangle> socialTagItemBounds = new(StringComparer.OrdinalIgnoreCase);
        private Rectangle socialTagMenuDoneBounds = Rectangle.Empty;

        // Interaction structures
        private sealed class SocialProfileClickableTarget
        {
            public Rectangle Bounds { get; init; }
            public string ActorName { get; init; } = "";
            public bool ActorIsPlayer { get; init; }
        }

        private readonly List<SocialProfileClickableTarget> socialFeedProfileIconBounds = new();
        private readonly List<SocialProfileClickableTarget> socialDetailProfileIconBounds = new();
        private readonly List<SocialProfileClickableTarget> socialProfileIconBounds = new();

        // Drag state
        private bool isDragging = false;
        private int dragOffsetX;
        private int dragOffsetY;

        // Touch scrolling
        private bool isScrolling = false;
        private int lastScrollMouseY = 0;
        private int touchScrollStartY = 0;
        private bool hasTouchScrolled = false;

        // Text scale definitions
        private const float SocialPostTextScale = 0.9f;
        private const float SocialCommentTextScale = 0.9f;
        private const float SocialHeaderMetaScale = 0.72f;

        // Height caches
        private readonly Dictionary<string, int> socialCardHeightCache = new(StringComparer.OrdinalIgnoreCase);

        // Android keyboard fields
        private System.Threading.Tasks.Task<string>? pendingKeyboardTask = null;
        private enum ActiveInput
        {
            None,
            Post,
            Comment,
            TagSearch
        }
        private ActiveInput activeAndroidInput = ActiveInput.None;

        // IKeyboardSubscriber Implementation
        public bool Selected { get; set; }

        public StardewSocialScreen(ISmartPhoneApi api, Action onBack)
            : base()
        {
            this.smartphoneApi = api;
            this.onBack = onBack;

            // Sync phone dimensions and textures
            var (px, py) = api.GetPhonePosition();
            this.xPositionOnScreen = px;
            this.yPositionOnScreen = py;

            this.phoneFrameWidth = api.GetPhoneFrameWidth();
            this.phoneFrameHeight = api.GetPhoneFrameHeight();
            var (offX, offY) = api.GetPhoneContentOffset();
            this.phoneContentOffsetX = offX;
            this.phoneContentOffsetY = offY;
            this.phoneUiScale = api.GetPhoneUiScale();
            this.phoneFrameTexture = api.GetPhoneFrameTexture();
            this.phoneBackgroundTexture = api.GetPhoneBackgroundTexture();

            this.width = this.phoneFrameWidth;
            this.height = this.phoneFrameHeight;

            this.contentWidth = Math.Max(1, this.phoneFrameWidth - (this.phoneContentOffsetX * 2));
            this.contentHeight = Math.Max(1, this.phoneFrameHeight - this.phoneContentOffsetY - ScaleUiValue(135));

            this.Selected = true;
            Game1.keyboardDispatcher.Subscriber = this;

            this.commentTextBox.Clear();
            this.postTextBox.Clear();

            CalculateLayout();
        }

        public StardewSocialScreen(ISmartPhoneApi api, string npcName, Action onBack)
            : this(api, onBack)
        {
            this.selectedSocialProfileActorName = npcName;
            this.selectedSocialProfileActorIsPlayer = Game1.getOnlineFarmers().Any(f => string.Equals(f.Name, npcName, StringComparison.OrdinalIgnoreCase)) || string.Equals(Game1.player?.Name, npcName, StringComparison.OrdinalIgnoreCase);
            this.selectedSocialPostId = "";
            this.socialProfileMenuOpen = true;
            this.socialProfileDetailBackStack = false;
            this.socialProfileScrollOffset = 0f;
            this.socialProfileScrollTarget = 0f;
            CalculateLayout();
        }

        public StardewSocialScreen(ISmartPhoneApi api, Action onBack, string? text, string? taggedNpc, string? imagePath, string? postTags = null)
            : this(api, onBack)
        {
            this.socialCreateMenuOpen = true;
            if (!string.IsNullOrWhiteSpace(text))
            {
                this.postTextBox.Text = text;
                this.postTextBox.CursorIndex = text.Length;
                this.postTextBox.SelectionAnchorIndex = text.Length;
            }
            if (!string.IsNullOrWhiteSpace(taggedNpc))
            {
                this.draftTagged.Add(taggedNpc);
            }
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(imagePath);
                    string fileName = Path.GetFileName(imagePath);
                    var photoResult = new SelectedPhotoResult
                    {
                        AbsolutePath = imagePath,
                        FileName = fileName,
                        TextureData = data,
                        Tag = !string.IsNullOrWhiteSpace(postTags) ? postTags : ModEntry.GetNpcPhotoTag(imagePath)
                    };
                    this.draftSelectedPhotos.Add(photoResult);

                    var tex = Texture2D.FromStream(Game1.graphics.GraphicsDevice, new MemoryStream(data));
                    this.draftSelectedTextures.Add(tex);
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to load draft photo from {imagePath}: {ex.Message}", LogLevel.Error);
                }
            }
            CalculateLayout();
        }

        public void ClearCachesAndRecalculate()
        {
            this.socialCardHeightCache.Clear();
            CalculateLayout();
        }

        private void ToggleLike(string postId)
        {
            if (!Context.IsMultiplayer || Context.IsMainPlayer)
            {
                bool liked = StardewConnectManager.TogglePostLikeByPlayer(postId);
                Game1.playSound(liked ? "money" : "bigDeSelect");
            }
            else
            {
                if (Game1.MasterPlayer != null)
                {
                    var req = new ActionRequest
                    {
                        Action = "LikePost",
                        PostId = postId,
                        ActorName = Game1.player.Name
                    };
                    string reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(req);
                    TransferManager.SendDirectMessage(Game1.MasterPlayer.UniqueMultiplayerID, "ActionRequest", reqJson);
                }
                Game1.playSound("money");
            }
            this.socialCardHeightCache.Remove(postId + "_detail");
            this.socialCardHeightCache.Remove(postId + "_feed");
        }

        private int ScaleUiValue(int baseValue)
        {
            return (int)Math.Round(baseValue * this.phoneUiScale);
        }

        private float ScaleUiValue(float baseValue)
        {
            return baseValue * this.phoneUiScale;
        }

        private float GetPhoneTextScale(float localScale = 1f)
        {
            float globalScale = this.phoneUiScale < 0.999f ? 0.85f : 1f;
            return Math.Max(0.01f, localScale * globalScale);
        }

        private int GetPhoneScaledWrapWidth(int maxWidth, float localScale = 1f)
        {
            float safeScale = GetPhoneTextScale(localScale);
            return Math.Max(1, (int)Math.Floor(maxWidth / safeScale));
        }

        private int GetPhoneScaledLineHeight(SpriteFont font, float localScale = 1f, int extraPadding = 4)
        {
            int baseLineHeight = (int)font.MeasureString("A").Y + extraPadding;
            return Math.Max(1, (int)Math.Ceiling(baseLineHeight * GetPhoneTextScale(localScale)));
        }

        private Vector2 MeasurePhoneText(SpriteFont font, string text, float localScale = 1f)
        {
            return font.MeasureString(text ?? string.Empty) * GetPhoneTextScale(localScale);
        }

        private void DrawPhoneText(SpriteBatch b, SpriteFont font, string text, Vector2 position, Color color, float localScale = 1f)
        {
            b.DrawString(
                font,
                text ?? string.Empty,
                position,
                color,
                0f,
                Vector2.Zero,
                GetPhoneTextScale(localScale),
                SpriteEffects.None,
                1f);
        }

        private Rectangle GetFrameBounds()
        {
            return new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.phoneFrameWidth, this.phoneFrameHeight);
        }

        private Rectangle GetContentBounds()
        {
            return new Rectangle(
                this.xPositionOnScreen + this.phoneContentOffsetX,
                this.yPositionOnScreen + this.phoneContentOffsetY,
                this.contentWidth,
                this.contentHeight);
        }

        private Rectangle SocialContentViewportRect
        {
            get
            {
                Rectangle content = GetContentBounds();
                int headerHeight = ScaleUiValue(10);
                int bottomGap = ScaleUiValue(10);
                return new Rectangle(
                    content.X,
                    content.Y + headerHeight,
                    content.Width,
                    content.Height - headerHeight - bottomGap);
            }
        }

        private Rectangle SocialDetailContentViewportRect
        {
            get
            {
                Rectangle content = GetContentBounds();
                int headerHeight = ScaleUiValue(10);
                int gapAboveInput = ScaleUiValue(10);
                int cropHeight = content.Bottom - ScaleUiValue(75) - gapAboveInput - (content.Y + headerHeight);
                return new Rectangle(
                    content.X,
                    content.Y + headerHeight,
                    content.Width,
                    Math.Max(1, cropHeight));
            }
        }

        private void CalculateLayout()
        {
            Rectangle clipRect = SocialContentViewportRect;

            // Feed Max Scroll
            List<StardewConnectPost> feedPosts = StardewConnectManager.GetPostsSnapshot();
            int feedY = ScaleUiValue(10);
            int targetScrollY = -1;
            bool foundNewPost = false;
            long lastVisitTime = StardewConnectManager.GetLastVisitTime();

            foreach (var post in feedPosts)
            {
                int postHeight = MeasurePostHeight(post, false);
                if (!initializedScrollTarget && !foundNewPost && post.CreatedTime.Timestamp > lastVisitTime)
                {
                    targetScrollY = feedY;
                    foundNewPost = true;
                }
                feedY += postHeight + ScaleUiValue(14);
            }
            this.maxScrollFeed = Math.Max(0, feedY - clipRect.Height);

            if (!initializedScrollTarget)
            {
                if (foundNewPost)
                {
                    this.socialFeedScrollTarget = Math.Clamp(targetScrollY, 0, this.maxScrollFeed);
                }
                else
                {
                    this.socialFeedScrollTarget = this.maxScrollFeed;
                }
                this.socialFeedScrollOffset = this.socialFeedScrollTarget;
                initializedScrollTarget = true;
            }

            // Notifications Max Scroll
            int notifY = ScaleUiValue(10);
            int notifCardWidth = clipRect.Width - ScaleUiValue(30);
            foreach (var notif in StardewConnectManager.GetActiveSocialNotifications())
            {
                notifY += MeasureNotificationHeight(notif, notifCardWidth) + ScaleUiValue(10);
            }
            this.maxScrollNotification = Math.Max(0, notifY - clipRect.Height);

            // Profile Max Scroll
            if (this.socialProfileMenuOpen)
            {
                List<StardewConnectPost> profilePosts = StardewConnectManager.GetPostsByAuthor(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer);
                int profY = ScaleUiValue(659); // height of stats + headers
                foreach (var post in profilePosts)
                {
                    profY += MeasurePostHeight(post, false) + ScaleUiValue(14);
                }
                this.maxScrollProfile = Math.Max(0, profY - clipRect.Height);
            }

            // Detail Max Scroll
            if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                var post = StardewConnectManager.GetPost(this.selectedSocialPostId);
                if (post != null)
                {
                    int detailY = ScaleUiValue(10) + MeasurePostHeight(post, true) + ScaleUiValue(10);
                    int detailViewportHeight = SocialDetailContentViewportRect.Height;
                    this.maxScrollDetail = Math.Max(0, detailY - detailViewportHeight);
                }
            }

            // Clamp scroll targets and offsets to their respective max bounds
            this.socialFeedScrollTarget = Math.Clamp(this.socialFeedScrollTarget, 0, this.maxScrollFeed);
            this.socialFeedScrollOffset = Math.Clamp(this.socialFeedScrollOffset, 0, this.maxScrollFeed);

            this.socialNotificationScrollTarget = Math.Clamp(this.socialNotificationScrollTarget, 0, this.maxScrollNotification);
            this.socialNotificationScrollOffset = Math.Clamp(this.socialNotificationScrollOffset, 0, this.maxScrollNotification);

            this.socialProfileScrollTarget = Math.Clamp(this.socialProfileScrollTarget, 0, this.maxScrollProfile);
            this.socialProfileScrollOffset = Math.Clamp(this.socialProfileScrollOffset, 0, this.maxScrollProfile);

            this.socialDetailScrollTarget = Math.Clamp(this.socialDetailScrollTarget, 0, this.maxScrollDetail);
            this.socialDetailScrollOffset = Math.Clamp(this.socialDetailScrollOffset, 0, this.maxScrollDetail);
        }

        private int MeasureNotificationHeight(StardewSocialNotification notif, int cardWidth)
        {
            if (notif == null) return 0;
            int textWrapWidth = GetPhoneScaledWrapWidth(cardWidth - ScaleUiValue(30), 0.9f);
            List<string> lines = SplitTextIntoLines(notif.Text, Game1.smallFont, textWrapWidth);
            int lineHeight = GetPhoneScaledLineHeight(Game1.smallFont, 0.9f);
            int innerHeight = lines.Count * lineHeight;
            int totalHeight = innerHeight + ScaleUiValue(34);
            return Math.Max(ScaleUiValue(70), totalHeight);
        }

        private int MeasurePostHeight(StardewConnectPost post, bool isDetail)
        {
            string key = post.Id + (isDetail ? "_detail" : "_feed") + "_" + post.Comments.Count + "_" + post.LikedBy.Count;
            if (this.socialCardHeightCache.TryGetValue(key, out int cachedHeight))
                return cachedHeight;

            int cardWidth = this.contentWidth - ScaleUiValue(30);
            int postLineHeight = GetPhoneScaledLineHeight(Game1.smallFont, SocialPostTextScale);
            int height = ScaleUiValue(15) + ScaleUiValue(56) + ScaleUiValue(10); // Header area

            if (!string.IsNullOrWhiteSpace(post.Text))
            {
                int postTextWrapWidth = GetPhoneScaledWrapWidth(cardWidth - ScaleUiValue(30), SocialPostTextScale);
                List<string> lines = SplitTextIntoLines(post.Text, Game1.smallFont, postTextWrapWidth);
                height += (lines.Count * postLineHeight) + ScaleUiValue(6);
            }

            if (ModEntry.Config.ShowSocialImageTags && post.PostTags != null && post.PostTags.Count > 0)
            {
                height += GetPhoneScaledLineHeight(Game1.smallFont, SocialHeaderMetaScale) + ScaleUiValue(6);
            }

            if (post.Photo != null && post.Photo.Count > 0)
            {
                int maxPhotoW = cardWidth - ScaleUiValue(30);
                int photoH = GetAdaptivePhotoHeight(post, maxPhotoW);
                height += photoH + ScaleUiValue(10);
            }

            height += ScaleUiValue(34); // like/comment action block

            // Comments preview
            int commentLineHeight = GetPhoneScaledLineHeight(Game1.smallFont, SocialCommentTextScale);
            int totalComments = post.Comments.Count;
            int limit = isDetail ? 0 : Math.Max(0, totalComments - 2);

            if (totalComments - limit > 0)
            {
                height += ScaleUiValue(12); // space and thin line separator
            }

            for (int i = totalComments - 1; i >= limit; i--)
            {
                var comment = post.Comments[i];
                height += ScaleUiValue(34); // Commenter icon & name/time header (commentIconSize + ScaleUiValue(4))
                int commentTextWrapWidth = GetPhoneScaledWrapWidth(cardWidth - ScaleUiValue(45), SocialCommentTextScale);
                List<string> lines = SplitTextIntoLines(comment.Text ?? "", Game1.smallFont, commentTextWrapWidth);
                height += (lines.Count * commentLineHeight) + ScaleUiValue(6);
            }

            int measuredHeight = height + ScaleUiValue(15);
            this.socialCardHeightCache[key] = measuredHeight;
            return measuredHeight;
        }

        public override void draw(SpriteBatch b)
        {
            this.customTagTooltipText = "";
            this.customLikeTooltipNames = null;

            // Dim background
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.6f);

            Rectangle contentRect = GetContentBounds();
            Rectangle frameRect = GetFrameBounds();

            // Draw phone background (extend slightly downward to cover any 1px gap)
            if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
            {
                Rectangle bgRect = new Rectangle(contentRect.X, contentRect.Y, contentRect.Width, contentRect.Height + ScaleUiValue(20));
                b.Draw(this.phoneBackgroundTexture, bgRect, Color.White);
            }
            else
            {
                Rectangle bgRect = new Rectangle(contentRect.X, contentRect.Y, contentRect.Width, contentRect.Height + ScaleUiValue(20));
                b.Draw(Game1.staminaRect, bgRect, new Color(30, 30, 30));
            }



            // Setup Scissor clipping viewport
            bool isDetailView = !this.socialCreateMenuOpen && !this.socialNotificationMenuOpen && !this.socialProfileMenuOpen && !string.IsNullOrWhiteSpace(this.selectedSocialPostId);
            Rectangle clipRect = isDetailView ? SocialDetailContentViewportRect : SocialContentViewportRect;
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });
            Rectangle previousScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            if (this.socialCreateMenuOpen)
            {
                DrawSocialCreatePostMenu(b, clipRect);
            }
            else if (this.socialNotificationMenuOpen)
            {
                DrawSocialNotificationMenu(b, clipRect);
            }
            else if (this.socialProfileMenuOpen)
            {
                DrawSocialProfile(b, clipRect);
            }
            else if (isDetailView)
            {
                var post = StardewConnectManager.GetPost(this.selectedSocialPostId);
                if (post != null)
                {
                    DrawSocialDetail(b, post, clipRect);
                }
            }
            else
            {
                DrawSocialFeed(b, clipRect);
            }

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);


            // Draw phone border on top
            if (this.phoneFrameTexture != null && !this.phoneFrameTexture.IsDisposed)
            {
                b.Draw(this.phoneFrameTexture, frameRect, Color.White);
            }

            if (isDetailView)
            {
                DrawCommentInputBox(b);

                // Draw delete post button for author in the top nav bar area
                var post = StardewConnectManager.GetPost(this.selectedSocialPostId);
                if (post != null && post.AuthorIsPlayer && string.Equals(post.AuthorName, Game1.player?.Name ?? "Player", StringComparison.OrdinalIgnoreCase))
                {
                    int topButtonsY = this.yPositionOnScreen + this.phoneContentOffsetY - ScaleUiValue(54);
                    int deleteBtnX = this.xPositionOnScreen + this.contentWidth - ScaleUiValue(160);
                    this.socialDetailDeletePostBounds = new Rectangle(deleteBtnX, topButtonsY, ScaleUiValue(50), ScaleUiValue(52));

                    UI.CardDrawing.DrawCard(
                        b,
                        this.socialDetailDeletePostBounds.X, this.socialDetailDeletePostBounds.Y,
                        this.socialDetailDeletePostBounds.Width, this.socialDetailDeletePostBounds.Height,
                        Color.OrangeRed, 1f, false);

                    int trashW = ScaleUiValue(24);
                    int trashH = ScaleUiValue(39);
                    b.Draw(
                        Game1.mouseCursors,
                        new Rectangle(
                            this.socialDetailDeletePostBounds.X + (this.socialDetailDeletePostBounds.Width - trashW) / 2,
                            this.socialDetailDeletePostBounds.Y + (this.socialDetailDeletePostBounds.Height - trashH) / 2,
                            trashW,
                            trashH
                        ),
                        new Rectangle(564, 102, 16, 26),
                        Color.White
                    );
                }
                else
                {
                    this.socialDetailDeletePostBounds = Rectangle.Empty;
                }
            }
            else
            {
                this.socialDetailDeletePostBounds = Rectangle.Empty;
            }

            // Draw Static Top Buttons if viewing feed
            bool isViewingFeed = !this.socialCreateMenuOpen && !this.socialNotificationMenuOpen && !this.socialProfileMenuOpen && string.IsNullOrWhiteSpace(this.selectedSocialPostId);
            if (isViewingFeed)
            {
                int topButtonsY = this.yPositionOnScreen + this.phoneContentOffsetY - ScaleUiValue(52);
                int createBtnX = this.xPositionOnScreen + this.phoneContentOffsetX + ScaleUiValue(70);

                this.socialFeedOpenCreatePostBounds = new Rectangle(createBtnX, topButtonsY, ScaleUiValue(150), ScaleUiValue(45));
                this.socialFeedOpenProfileBounds = new Rectangle(this.socialFeedOpenCreatePostBounds.Right + ScaleUiValue(8), topButtonsY, ScaleUiValue(45), ScaleUiValue(45));
                this.socialFeedOpenNotificationBounds = new Rectangle(this.socialFeedOpenProfileBounds.Right + ScaleUiValue(8), topButtonsY, ScaleUiValue(45), ScaleUiValue(45));

                // Draw Create Post button
                UI.CardDrawing.DrawCard(
                    b,
                    this.socialFeedOpenCreatePostBounds.X, this.socialFeedOpenCreatePostBounds.Y,
                    this.socialFeedOpenCreatePostBounds.Width, this.socialFeedOpenCreatePostBounds.Height,
                    Color.LightGreen, 1f, false);
                DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("feed.createPost"), new Vector2(this.socialFeedOpenCreatePostBounds.X + ScaleUiValue(14), this.socialFeedOpenCreatePostBounds.Y + ScaleUiValue(8)), Color.Black);

                // Draw Profile button
                DrawSocialActorIcon(b, Game1.player?.Name ?? "Player", true, this.socialFeedOpenProfileBounds);

                // Draw Notification button
                UI.CardDrawing.DrawCard(
                    b,
                    this.socialFeedOpenNotificationBounds.X, this.socialFeedOpenNotificationBounds.Y,
                    this.socialFeedOpenNotificationBounds.Width, this.socialFeedOpenNotificationBounds.Height,
                    new Color(255, 255, 255, 220), 1f, false);

                Texture2D? notifIcon = this.smartphoneApi.GetAppTexture(AppIconType.Notification);
                if (notifIcon != null)
                {
                    b.Draw(notifIcon, new Rectangle(this.socialFeedOpenNotificationBounds.X + ScaleUiValue(5), this.socialFeedOpenNotificationBounds.Y + ScaleUiValue(5), ScaleUiValue(35), ScaleUiValue(35)), Color.White);
                }

                int notifCount = StardewConnectManager.GetActiveSocialNotificationCount();
                if (notifCount > 0)
                {
                    DrawSocialUnreadBadge(b, this.socialFeedOpenNotificationBounds.Right + ScaleUiValue(4), this.socialFeedOpenNotificationBounds.Y - ScaleUiValue(4), notifCount);
                }
            }
            else
            {
                this.socialFeedOpenCreatePostBounds = Rectangle.Empty;
                this.socialFeedOpenProfileBounds = Rectangle.Empty;
                this.socialFeedOpenNotificationBounds = Rectangle.Empty;
            }

            if (this.emojiMenu != null)
            {
                this.emojiMenu.draw(b);
            }

            if (this.socialCreateMenuOpen && this.socialTagMenuOpen)
            {
                DrawSocialTagMenu(b, contentRect);
            }

            if (!string.IsNullOrEmpty(this.hoverText))
            {
                DrawHoverTextCard(b, this.hoverText, Game1.getMouseX(), Game1.getMouseY());
                this.hoverText = "";
            }

            if (!string.IsNullOrEmpty(this.customTagTooltipText))
            {
                DrawCustomTagTooltip(b, this.customTagTooltipText, this.tooltipMouseX, this.tooltipMouseY);
            }

            if (this.customLikeTooltipNames != null && this.customLikeTooltipNames.Count > 0)
            {
                DrawCustomLikeTooltip(b, this.customLikeTooltipNames, this.tooltipMouseX, this.tooltipMouseY);
            }

            // Draw scale adjustment buttons
            this.smartphoneApi.DrawPhoneSizeButtons(b, this.xPositionOnScreen, this.yPositionOnScreen);

            drawMouse(b);
        }

        private void DrawSocialFeed(SpriteBatch b, Rectangle clipRect)
        {
            this.socialFeedPostBounds.Clear();
            this.socialFeedLikeBounds.Clear();
            this.socialFeedCommentBounds.Clear();
            this.socialFeedProfileIconBounds.Clear();
            this.socialFeedLikeHoverBounds.Clear();
            this.socialFeedPhotoPrevBounds.Clear();
            this.socialFeedPhotoNextBounds.Clear();

            int cardX = clipRect.X + ScaleUiValue(15);
            int currentY = clipRect.Y + ScaleUiValue(10) - (int)this.socialFeedScrollOffset;
            List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();

            if (posts.Count == 0)
            {
                DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("feed.noPosts"), new Vector2(clipRect.X + ScaleUiValue(30), currentY + ScaleUiValue(20)), Color.Black);
            }
            else
            {
                foreach (var post in posts)
                {
                    int postHeight = MeasurePostHeight(post, false);
                    if (currentY + postHeight >= clipRect.Top && currentY <= clipRect.Bottom)
                    {
                        DrawSocialPostCard(b, post, cardX, currentY, postHeight, false);
                    }
                    currentY += postHeight + ScaleUiValue(14);
                }
            }
        }

        private void DrawSocialPostCard(SpriteBatch b, StardewConnectPost post, int x, int y, int cardHeight, bool isDetail)
        {
            int cardWidth = this.contentWidth - ScaleUiValue(30);
            Rectangle cardBounds = new Rectangle(x, y, cardWidth, cardHeight);

            if (!isDetail)
            {
                if (this.socialProfileMenuOpen)
                {
                    this.socialProfilePostBounds[post.Id] = cardBounds;
                }
                else
                {
                    // Register feed bounds
                    this.socialFeedPostBounds[post.Id] = cardBounds;
                }
            }

            // Card body background
            UI.CardDrawing.DrawCard(
                b,
                cardBounds.X, cardBounds.Y, cardBounds.Width, cardBounds.Height,
                Color.White, 1f, false);

            if (!isDetail)
            {
                int readCount = StardewConnectManager.GetPlayerReadCommentCount(post, Game1.player?.Name ?? "Player");
                int unreadCommentsCount = post.Comments.Count - readCount;
                if (ModEntry.Config.ShowUnreadComment && unreadCommentsCount > 0)
                {
                    DrawSocialUnreadBadge(b, cardBounds.Right - ScaleUiValue(10), cardBounds.Y + ScaleUiValue(10), unreadCommentsCount);
                }
            }

            int cursorY = y + ScaleUiValue(15);

            // Actor icon - increased size to 56
            int iconSize = ScaleUiValue(56);
            Rectangle actorIconBounds = new Rectangle(x + ScaleUiValue(15), cursorY, iconSize, iconSize);
            DrawSocialActorIcon(b, post.AuthorName, post.AuthorIsPlayer, actorIconBounds);

            // Profile bounds registration
            var target = new SocialProfileClickableTarget
            {
                Bounds = actorIconBounds,
                ActorName = post.AuthorName,
                ActorIsPlayer = post.AuthorIsPlayer
            };
            if (isDetail)
                this.socialDetailProfileIconBounds.Add(target);
            else if (this.socialProfileMenuOpen)
                this.socialProfileIconBounds.Add(target);
            else
                this.socialFeedProfileIconBounds.Add(target);

            // Header Meta info
            string cleanName = post.AuthorIsPlayer ? post.AuthorName : (Game1.getCharacterFromName(post.AuthorName)?.displayName ?? post.AuthorName);
            string authorDisplayText = cleanName;
            if (post.Tagged != null && post.Tagged.Count > 0)
            {
                if (post.Tagged.Count == 1)
                {
                    authorDisplayText += " " + ModEntry.SHelper.Translation.Get("feed.with", new { names = GetCleanName(post.Tagged[0]) });
                }
                else if (post.Tagged.Count == 2)
                {
                    authorDisplayText += " " + ModEntry.SHelper.Translation.Get("feed.with", new { names = GetCleanName(post.Tagged[0]) + ", " + GetCleanName(post.Tagged[1]) });
                }
                else
                {
                    authorDisplayText += " " + ModEntry.SHelper.Translation.Get("feed.withOthers");
                }
            }
            string timeString = FormatTime(post.CreatedTime.Season, post.CreatedTime.Day, post.CreatedTime.TimeOfDay);
            DrawPhoneText(b, Game1.smallFont, authorDisplayText, new Vector2(actorIconBounds.Right + ScaleUiValue(10), cursorY + ScaleUiValue(2)), Color.Black);

            // Reduced size & faded gray color (gap increased a bit more)
            DrawPhoneText(b, Game1.smallFont, timeString, new Vector2(actorIconBounds.Right + ScaleUiValue(10), cursorY + ScaleUiValue(36)), Color.Gray * 0.8f, SocialHeaderMetaScale);

            cursorY += iconSize + ScaleUiValue(10);

            // Post content text wrapped dynamically with SocialPostTextScale applied
            if (!string.IsNullOrWhiteSpace(post.Text))
            {
                int postTextWrapWidth = GetPhoneScaledWrapWidth(cardWidth - ScaleUiValue(30), SocialPostTextScale);
                List<string> lines = SplitTextIntoLines(post.Text, Game1.smallFont, postTextWrapWidth);
                int postLineHeight = GetPhoneScaledLineHeight(Game1.smallFont, SocialPostTextScale);
                foreach (string line in lines)
                {
                    DrawPhoneText(b, Game1.smallFont, line, new Vector2(x + ScaleUiValue(15), cursorY), Color.Black, SocialPostTextScale);
                    cursorY += postLineHeight;
                }
                cursorY += ScaleUiValue(6);
            }

            // Post Tags (size and color same with post create time text, max 30 chars, tooltip on hover)
            if (ModEntry.Config.ShowSocialImageTags && post.PostTags != null && post.PostTags.Count > 0)
            {
                string tagsText = string.Join(" ", post.PostTags.Select(t => t.StartsWith("#") ? t : "#" + t));
                string displayedTagsText = tagsText;
                if (displayedTagsText.Length > 30)
                {
                    displayedTagsText = displayedTagsText.Substring(0, 27) + "...";
                }

                Vector2 tagSize = MeasurePhoneText(Game1.smallFont, displayedTagsText, SocialHeaderMetaScale);
                Rectangle tagBounds = new Rectangle(x + ScaleUiValue(15), cursorY, (int)tagSize.X, (int)tagSize.Y);

                if (tagBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                {
                    string tagStr = string.Join(" ", post.PostTags.Select(t => t.StartsWith("#") ? t : "#" + t));
                    this.customTagTooltipText = tagStr;
                    this.tooltipMouseX = Game1.getMouseX();
                    this.tooltipMouseY = Game1.getMouseY();
                }

                DrawPhoneText(b, Game1.smallFont, displayedTagsText, new Vector2(x + ScaleUiValue(15), cursorY), Color.Gray * 0.8f, SocialHeaderMetaScale);
                cursorY += GetPhoneScaledLineHeight(Game1.smallFont, SocialHeaderMetaScale) + ScaleUiValue(6);
            }

            // Photos navigation carousel (doubled allowed height to adaptive, static controls)
            if (post.Photo != null && post.Photo.Count > 0)
            {
                if (!postPhotoIndices.TryGetValue(post.Id, out int photoIdx))
                {
                    photoIdx = 0;
                }
                photoIdx = Math.Clamp(photoIdx, 0, post.Photo.Count - 1);

                var photo = post.Photo[photoIdx];
                var tex = GetPostPhotoTexture(post, photo);
                int maxPhotoW = cardWidth - ScaleUiValue(30);
                int photoH = GetAdaptivePhotoHeight(post, maxPhotoW);

                if (tex != null && !tex.IsDisposed)
                {
                    float scale = Math.Min((float)maxPhotoW / tex.Width, (float)ScaleUiValue(360) / tex.Height);
                    int drawW = (int)(tex.Width * scale);
                    int drawH = (int)(tex.Height * scale);

                    Rectangle photoRect = new Rectangle(
                        x + ScaleUiValue(15) + (maxPhotoW - drawW) / 2,
                        cursorY + (photoH - drawH) / 2,
                        drawW,
                        drawH
                    );
                    b.Draw(tex, photoRect, Color.White);

                    if (post.Photo.Count > 1)
                    {
                        int photoAreaX = x + ScaleUiValue(15);
                        int photoAreaWidth = cardWidth - ScaleUiValue(30);
                        Rectangle prevBtn = new Rectangle(photoAreaX + ScaleUiValue(8), cursorY + photoH / 2 - ScaleUiValue(15), ScaleUiValue(30), ScaleUiValue(30));
                        Rectangle nextBtn = new Rectangle(photoAreaX + photoAreaWidth - ScaleUiValue(38), cursorY + photoH / 2 - ScaleUiValue(15), ScaleUiValue(30), ScaleUiValue(30));

                        if (isDetail)
                        {
                            this.socialDetailPhotoPrevBounds[post.Id] = prevBtn;
                            this.socialDetailPhotoNextBounds[post.Id] = nextBtn;
                        }
                        else
                        {
                            this.socialFeedPhotoPrevBounds[post.Id] = prevBtn;
                            this.socialFeedPhotoNextBounds[post.Id] = nextBtn;
                        }

                        // Left arrow (tile 44)
                        b.Draw(Game1.mouseCursors, prevBtn, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44), Color.White);
                        // Right arrow (tile 33)
                        b.Draw(Game1.mouseCursors, nextBtn, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33), Color.White);
                    }
                }
                cursorY += photoH + ScaleUiValue(10);
            }

            // Register hitboxes precisely where they are drawn!
            Rectangle cardLikeBounds = new Rectangle(x + ScaleUiValue(14), cursorY, ScaleUiValue(92), ScaleUiValue(30));
            Rectangle cardCommentBounds = new Rectangle(x + ScaleUiValue(132), cursorY, ScaleUiValue(94), ScaleUiValue(30));
            if (isDetail)
            {
                this.socialDetailLikeBounds = cardLikeBounds;
            }
            else
            {
                if (this.socialProfileMenuOpen)
                {
                    this.socialProfileLikeBounds[post.Id] = cardLikeBounds;
                }
                else
                {
                    this.socialFeedLikeBounds[post.Id] = cardLikeBounds;
                    this.socialFeedCommentBounds[post.Id] = cardCommentBounds;
                }
            }

            // Liking UI area
            Rectangle likeIconBounds = new Rectangle(x + ScaleUiValue(18), cursorY + ScaleUiValue(2), ScaleUiValue(24), ScaleUiValue(24));
            bool likedByPlayer = StardewConnectManager.IsPostLikedByPlayer(post);
            Rectangle heartSource = likedByPlayer ? new Rectangle(211, 428, 7, 7) : new Rectangle(218, 428, 7, 7);
            b.Draw(Game1.mouseCursors, likeIconBounds, heartSource, Color.White);

            string likeCountText = post.LikedBy.Count.ToString();
            Vector2 likeCountSize = MeasurePhoneText(Game1.smallFont, likeCountText);
            Rectangle likeCountNumberBounds = new Rectangle(likeIconBounds.Right + ScaleUiValue(8), cursorY, (int)likeCountSize.X, (int)likeCountSize.Y);

            if (likeCountNumberBounds.Contains(Game1.getMouseX(), Game1.getMouseY()) && post.LikedBy.Count > 0)
            {
                List<string> lastLiked = new List<string>();
                List<string> likers;
                lock (post.LikedBy)
                {
                    likers = new List<string>(post.LikedBy);
                }
                int count = likers.Count;
                for (int i = count - 1; i >= Math.Max(0, count - 5); i--)
                {
                    lastLiked.Add(likers[i]);
                }
                if (count > 5)
                {
                    lastLiked.Add("...");
                }
                this.customLikeTooltipNames = lastLiked;
                this.tooltipMouseX = Game1.getMouseX();
                this.tooltipMouseY = Game1.getMouseY();
            }

            DrawPhoneText(b, Game1.smallFont, likeCountText, new Vector2(likeIconBounds.Right + ScaleUiValue(8), cursorY), Color.Black);

            // Commenting UI icon area
            Rectangle commentIconBounds = new Rectangle(x + ScaleUiValue(132), cursorY + ScaleUiValue(4), ScaleUiValue(24), ScaleUiValue(24));
            b.Draw(Game1.mouseCursors, commentIconBounds, new Rectangle(139, 465, 24, 24), Color.White);
            DrawPhoneText(b, Game1.smallFont, post.Comments.Count.ToString(), new Vector2(commentIconBounds.Right + ScaleUiValue(8), cursorY), Color.Black);

            cursorY += ScaleUiValue(34); // like/comment action block height

            // Render comments block below
            int totalComments = post.Comments.Count;
            int limit = isDetail ? 0 : Math.Max(0, totalComments - 2);

            if (totalComments - limit > 0)
            {
                // Thin line separator placed below likes block
                b.Draw(Game1.staminaRect, new Rectangle(x + ScaleUiValue(15), cursorY + ScaleUiValue(4), cardWidth - ScaleUiValue(30), 1), Color.Gray * 0.4f);
                cursorY += ScaleUiValue(12);
            }

            int commentLineHeight = GetPhoneScaledLineHeight(Game1.smallFont, SocialCommentTextScale);
            for (int i = totalComments - 1; i >= limit; i--)
            {
                var comment = post.Comments[i];
                int commentIconSize = ScaleUiValue(30);
                Rectangle commenterIconBounds = new Rectangle(x + ScaleUiValue(15), cursorY, commentIconSize, commentIconSize);
                DrawSocialActorIcon(b, comment.AuthorName, comment.AuthorIsPlayer, commenterIconBounds);

                var commentProfileTarget = new SocialProfileClickableTarget
                {
                    Bounds = commenterIconBounds,
                    ActorName = comment.AuthorName,
                    ActorIsPlayer = comment.AuthorIsPlayer
                };
                if (isDetail)
                    this.socialDetailProfileIconBounds.Add(commentProfileTarget);
                else if (this.socialProfileMenuOpen)
                    this.socialProfileIconBounds.Add(commentProfileTarget);
                else
                    this.socialFeedProfileIconBounds.Add(commentProfileTarget);

                string commenterCleanName = comment.AuthorIsPlayer ? comment.AuthorName : (Game1.getCharacterFromName(comment.AuthorName)?.displayName ?? comment.AuthorName);
                string commentTimeString = FormatTime(comment.CreatedTime.Season, comment.CreatedTime.Day, comment.CreatedTime.TimeOfDay);

                // Author name and time text on the same line with perfect vertical alignment
                Vector2 nameSize = MeasurePhoneText(Game1.smallFont, commenterCleanName, 1.0f);
                Vector2 timeSize = MeasurePhoneText(Game1.smallFont, commentTimeString, SocialHeaderMetaScale);
                DrawPhoneText(b, Game1.smallFont, commenterCleanName, new Vector2(commenterIconBounds.Right + ScaleUiValue(8), cursorY + ScaleUiValue(2)), Color.DarkSlateGray);
                DrawPhoneText(b, Game1.smallFont, commentTimeString, new Vector2(commenterIconBounds.Right + ScaleUiValue(8) + nameSize.X + ScaleUiValue(10), cursorY + ScaleUiValue(2) + (nameSize.Y - timeSize.Y) / 2f), Color.Gray * 0.7f, SocialHeaderMetaScale);

                cursorY += commentIconSize + ScaleUiValue(4);

                int commentTextWrapWidth = GetPhoneScaledWrapWidth(cardWidth - ScaleUiValue(45), SocialCommentTextScale);
                List<string> commentLines = SplitTextIntoLines(comment.Text ?? "", Game1.smallFont, commentTextWrapWidth);
                foreach (string line in commentLines)
                {
                    DrawPhoneText(b, Game1.smallFont, line, new Vector2(x + ScaleUiValue(30), cursorY), Color.Black, SocialCommentTextScale);
                    cursorY += commentLineHeight;
                }
                cursorY += ScaleUiValue(6);
            }
        }

        private void DrawSocialActorIcon(SpriteBatch b, string actorName, bool actorIsPlayer, Rectangle bounds)
        {
            if (!actorIsPlayer)
            {
                NPC? npc = Game1.getCharacterFromName(actorName, mustBeVillager: false);
                if (npc?.Portrait != null)
                {
                    b.Draw(npc.Portrait, bounds, new Rectangle(0, 0, 64, 64), Color.White);
                    return;
                }
            }
            else
            {
                long? playerId = null;
                if (string.Equals(actorName, Game1.player?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    playerId = Game1.player.UniqueMultiplayerID;
                }
                else
                {
                    var farmer = Game1.getAllFarmers().FirstOrDefault(f => string.Equals(f.Name, actorName, StringComparison.OrdinalIgnoreCase));
                    if (farmer != null)
                    {
                        playerId = farmer.UniqueMultiplayerID;
                    }
                }

                if (playerId.HasValue)
                {
                    string id = playerId.Value.ToString();
                    string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                    string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                    string avatarPath = Path.Combine(photoSharedDir, $"{id}_avatar.jpg");

                    if (File.Exists(avatarPath) && TryGetAvatarTexture(avatarPath, out Texture2D avatarTexture))
                    {
                        b.Draw(avatarTexture, bounds, Color.White);
                        return;
                    }
                }
            }

            b.Draw(Game1.staminaRect, bounds, new Color(65, 95, 135, 220));

            string fallbackLetter = "P";
            if (!string.IsNullOrWhiteSpace(actorName))
                fallbackLetter = actorName.Trim()[0].ToString().ToUpperInvariant();

            Vector2 letterSize = MeasurePhoneText(Game1.smallFont, fallbackLetter);
            Vector2 letterPos = new Vector2(
                bounds.X + (bounds.Width - letterSize.X) / 2f,
                bounds.Y + (bounds.Height - letterSize.Y) / 2f);
            DrawPhoneText(b, Game1.smallFont, fallbackLetter, letterPos, Color.White);
        }

        private void DrawSocialUnreadBadge(SpriteBatch b, int rightX, int y, int unreadCount)
        {
            string text = Math.Min(99, unreadCount).ToString();
            Vector2 textSize = MeasurePhoneText(Game1.smallFont, text);

            int badgeWidth = Math.Max(ScaleUiValue(26), (int)textSize.X + ScaleUiValue(12));
            int badgeHeight = Math.Max(ScaleUiValue(20), (int)textSize.Y + ScaleUiValue(4));
            int badgeX = rightX - badgeWidth;

            UI.CardDrawing.DrawCard(
                b,
                badgeX, y, badgeWidth, badgeHeight,
                new Color(220, 0, 0, 170), 1f, false);

            DrawPhoneText(b, Game1.smallFont, text, new Vector2(badgeX + (badgeWidth - textSize.X) / 2f, y + (badgeHeight - textSize.Y) / 2f), Color.White);
        }

        private void DrawSocialNotificationMenu(SpriteBatch b, Rectangle clipRect)
        {
            this.socialNotificationItemBounds.Clear();

            int cardX = clipRect.X + ScaleUiValue(15);
            int cursorY = clipRect.Y + ScaleUiValue(10) - (int)this.socialNotificationScrollOffset;
            int cardWidth = clipRect.Width - ScaleUiValue(30);

            var activeNotifications = StardewConnectManager.GetActiveSocialNotifications();
            if (activeNotifications.Count == 0)
            {
                DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("notifications.none"), new Vector2(cardX, cursorY + ScaleUiValue(15)), Color.Black, 0.9f);
            }
            else
            {
                foreach (var notif in activeNotifications)
                {
                    int cardHeight = MeasureNotificationHeight(notif, cardWidth);
                    Rectangle cardBounds = new Rectangle(cardX, cursorY, cardWidth, cardHeight);
                    this.socialNotificationItemBounds[notif.Id] = cardBounds;

                    UI.CardDrawing.DrawCard(
                        b,
                        cardBounds.X, cardBounds.Y, cardBounds.Width, cardBounds.Height,
                        notif.Read ? new Color(180, 180, 180, 230) : new Color(255, 255, 255, 230), 1f, false);

                    int textWrapWidth = GetPhoneScaledWrapWidth(cardWidth - ScaleUiValue(30), 0.9f);
                    List<string> lines = SplitTextIntoLines(notif.Text, Game1.smallFont, textWrapWidth);
                    int lineHeight = GetPhoneScaledLineHeight(Game1.smallFont, 0.9f);

                    Color textColor = notif.Read ? Color.DimGray : Color.Black;
                    int startTextY = cardBounds.Y + (cardBounds.Height - lines.Count * lineHeight) / 2;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        DrawPhoneText(b, Game1.smallFont, lines[i], new Vector2(cardBounds.X + ScaleUiValue(14), startTextY + i * lineHeight), textColor, 0.9f);
                    }

                    cursorY += cardHeight + ScaleUiValue(10);
                }
            }

            // Clear all button at bottom of scrolled view
            int clearBtnY = clipRect.Bottom - ScaleUiValue(75);
            this.socialNotificationClearAllBounds = new Rectangle(clipRect.Right - ScaleUiValue(75), clearBtnY, ScaleUiValue(60), ScaleUiValue(60));

            UI.CardDrawing.DrawCard(
                b,
                this.socialNotificationClearAllBounds.X, this.socialNotificationClearAllBounds.Y,
                this.socialNotificationClearAllBounds.Width, this.socialNotificationClearAllBounds.Height,
                Color.White, 1f, false);
            b.Draw(Game1.mouseCursors, this.socialNotificationClearAllBounds, new Rectangle(128, 256, 64, 64), Color.White);
        }

        private void DrawSocialProfile(SpriteBatch b, Rectangle clipRect)
        {
            this.socialProfileIconBounds.Clear();
            this.socialProfilePostBounds.Clear();
            this.socialProfileLikeBounds.Clear();
            this.socialProfileLikeHoverBounds.Clear();

            int cardX = clipRect.X + ScaleUiValue(15);
            int cursorY = clipRect.Y + ScaleUiValue(10) - (int)this.socialProfileScrollOffset;
            int cardWidth = clipRect.Width - ScaleUiValue(30);

            // Profile Info header box
            int headerHeight = ScaleUiValue(210 + 30);
            Rectangle headerBounds = new Rectangle(cardX, cursorY, cardWidth, headerHeight);
            UI.CardDrawing.DrawCard(
                b,
                headerBounds.X, headerBounds.Y, headerBounds.Width / 2 + ScaleUiValue(5), headerBounds.Height,
                new Color(255, 255, 255, 230), 1f, false);

            int halfWidth = (headerBounds.Width - ScaleUiValue(40)) / 2;
            Rectangle avatarBounds = new Rectangle(headerBounds.X + ScaleUiValue(15), headerBounds.Y + ScaleUiValue(15), halfWidth, ScaleUiValue(210));
            Rectangle infoBounds = new Rectangle(avatarBounds.Right + ScaleUiValue(10), avatarBounds.Y, halfWidth, ScaleUiValue(210));

            DrawSocialActorIcon(b, this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer, avatarBounds);

            // If viewing own player profile, draw camera button
            if (this.selectedSocialProfileActorIsPlayer && string.Equals(this.selectedSocialProfileActorName, Game1.player?.Name ?? "Player", StringComparison.OrdinalIgnoreCase))
            {
                this.socialProfileAvatarCameraButtonBounds = new Rectangle(
                    avatarBounds.Right - ScaleUiValue(44) - ScaleUiValue(3),
                    avatarBounds.Bottom - ScaleUiValue(44) - ScaleUiValue(3),
                    ScaleUiValue(44),
                    ScaleUiValue(44));

                DrawSocialProfileAvatarCameraButton(b, this.socialProfileAvatarCameraButtonBounds);
            }
            else
            {
                this.socialProfileAvatarCameraButtonBounds = Rectangle.Empty;
            }

            string ageLabel = GetSocialProfileAgeLabel(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer);
            string birthdayLabel = GetSocialProfileBirthdayLabel(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer);
            string cleanName = this.selectedSocialProfileActorIsPlayer
                ? this.selectedSocialProfileActorName
                : (Game1.getCharacterFromName(this.selectedSocialProfileActorName)?.displayName ?? this.selectedSocialProfileActorName);

            string[] infoLines =
            {
                ModEntry.SHelper.Translation.Get("profile.nameLabel", new { name = cleanName }),
                ModEntry.SHelper.Translation.Get("profile.ageLabel", new { age = ageLabel }),
                ModEntry.SHelper.Translation.Get("profile.birthdayLabel", new { birthday = birthdayLabel })
            };

            int infoLineHeight = Math.Max(ScaleUiValue(24), infoBounds.Height / 3);
            for (int i = 0; i < infoLines.Length; i++)
            {
                string line = infoLines[i];
                Vector2 size = MeasurePhoneText(Game1.smallFont, line, 0.9f);
                float lineX = infoBounds.X + ScaleUiValue(8);
                float lineY = infoBounds.Y + (i * infoLineHeight) + (infoLineHeight - size.Y) / 2f;
                DrawPhoneText(b, Game1.smallFont, line, new Vector2(lineX, lineY), Color.Black, 0.9f);
            }

            cursorY += headerHeight + ScaleUiValue(12);

            // Stats box
            Rectangle statsBounds = new Rectangle(cardX, cursorY, cardWidth, ScaleUiValue(135));
            UI.CardDrawing.DrawCard(
                b,
                statsBounds.X, statsBounds.Y, statsBounds.Width, statsBounds.Height,
                new Color(255, 255, 255, 230), 1f, false);

            var stats = StardewConnectManager.GetProfileStatsSnapshot(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer);
            int statsTextX = statsBounds.X + ScaleUiValue(18);
            int statsTopY = statsBounds.Y + ScaleUiValue(16);
            int statsLineHeight = Math.Max(ScaleUiValue(22), GetPhoneScaledLineHeight(Game1.smallFont, 0.85f));

            DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("profile.totalPosts", new { count = stats.TotalPosts.ToString() }), new Vector2(statsTextX, statsTopY), Color.Black, 0.9f);

            int metricIconX = statsTextX + ScaleUiValue(82);

            int receivedLineY = statsTopY + statsLineHeight;
            DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("profile.received"), new Vector2(statsTextX, receivedLineY), Color.Black, 0.9f);

            Rectangle receivedHeartBounds = new Rectangle(
                metricIconX + ScaleUiValue(30),
                receivedLineY + ScaleUiValue(8),
                ScaleUiValue(18),
                ScaleUiValue(18));
            b.Draw(Game1.mouseCursors, receivedHeartBounds, new Rectangle(211, 428, 7, 7), Color.White);
            DrawPhoneText(b, Game1.smallFont, stats.TotalLikesReceived.ToString(), new Vector2(receivedHeartBounds.Right + ScaleUiValue(6), receivedLineY), Color.Black, 0.9f);

            Rectangle receivedCommentBounds = new Rectangle(
                receivedHeartBounds.Right + ScaleUiValue(125),
                receivedLineY + ScaleUiValue(6),
                ScaleUiValue(18),
                ScaleUiValue(18));
            b.Draw(Game1.mouseCursors, receivedCommentBounds, new Rectangle(139, 465, 24, 24), Color.White);
            DrawPhoneText(b, Game1.smallFont, stats.TotalCommentsReceived.ToString(), new Vector2(receivedCommentBounds.Right + ScaleUiValue(6), receivedLineY), Color.Black, 0.9f);

            int sentLineY = receivedLineY + statsLineHeight;
            DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("profile.sent"), new Vector2(statsTextX, sentLineY), Color.Black, 0.9f);

            Rectangle sentHeartBounds = new Rectangle(
                metricIconX + ScaleUiValue(30),
                sentLineY + ScaleUiValue(8),
                ScaleUiValue(18),
                ScaleUiValue(18));
            b.Draw(Game1.mouseCursors, sentHeartBounds, new Rectangle(211, 428, 7, 7), Color.White);
            DrawPhoneText(b, Game1.smallFont, stats.TotalLikesGiven.ToString(), new Vector2(sentHeartBounds.Right + ScaleUiValue(6), sentLineY), Color.Black, 0.9f);

            Rectangle sentCommentBounds = new Rectangle(
                sentHeartBounds.Right + ScaleUiValue(125),
                sentLineY + ScaleUiValue(6),
                ScaleUiValue(18),
                ScaleUiValue(18));
            b.Draw(Game1.mouseCursors, sentCommentBounds, new Rectangle(139, 465, 24, 24), Color.White);
            DrawPhoneText(b, Game1.smallFont, stats.TotalCommentsGiven.ToString(), new Vector2(sentCommentBounds.Right + ScaleUiValue(6), sentLineY), Color.Black, 0.9f);

            cursorY += statsBounds.Height + ScaleUiValue(12);

            // Interactions box
            Rectangle interBounds = new Rectangle(cardX, cursorY, cardWidth, ScaleUiValue(178));
            UI.CardDrawing.DrawCard(
                b,
                interBounds.X, interBounds.Y, interBounds.Width, interBounds.Height,
                new Color(255, 255, 255, 230), 1f, false);

            int columnGap = ScaleUiValue(8);
            int innerPadding = ScaleUiValue(15);
            int columnWidth = (interBounds.Width - innerPadding * 2 - columnGap) / 2;
            Rectangle fromBounds = new Rectangle(interBounds.X + innerPadding, interBounds.Y + innerPadding, columnWidth, interBounds.Height - innerPadding * 2);
            Rectangle toBounds = new Rectangle(fromBounds.Right + columnGap, fromBounds.Y, columnWidth, fromBounds.Height);

            var topFrom = StardewConnectManager.GetTopInteractionsFrom(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer, 3);
            var topTo = StardewConnectManager.GetTopInteractionsTo(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer, 3);

            DrawSocialInteractionColumn(b, fromBounds, ModEntry.SHelper.Translation.Get("profile.topInteractFrom"), topFrom);
            DrawSocialInteractionColumn(b, toBounds, ModEntry.SHelper.Translation.Get("profile.topInteractTo"), topTo);

            cursorY += interBounds.Height + ScaleUiValue(12);

            // Posts header
            Rectangle postsHeaderBounds = new Rectangle(cardX, cursorY, cardWidth, ScaleUiValue(46));
            DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("profile.postsHeader"), new Vector2(postsHeaderBounds.X + ScaleUiValue(14), postsHeaderBounds.Y + ScaleUiValue(9)), Color.Black);

            cursorY += postsHeaderBounds.Height + ScaleUiValue(14);

            // Posts lists (ordered from newest to oldest)
            List<StardewConnectPost> profilePosts = StardewConnectManager.GetPostsByAuthor(this.selectedSocialProfileActorName, this.selectedSocialProfileActorIsPlayer);
            var sortedPosts = profilePosts.OrderByDescending(p => p.CreatedTime.Timestamp).ToList();

            if (sortedPosts.Count == 0)
            {
                DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("profile.noPosts"), new Vector2(cardX + ScaleUiValue(14), cursorY + ScaleUiValue(10)), Color.Black);
            }
            else
            {
                foreach (var post in sortedPosts)
                {
                    int postHeight = MeasurePostHeight(post, false);
                    if (cursorY + postHeight >= clipRect.Top && cursorY <= clipRect.Bottom)
                    {
                        DrawSocialPostCard(b, post, cardX, cursorY, postHeight, false);
                    }

                    // Register poster icon bounds for navigation
                    int iconSize = ScaleUiValue(56);
                    Rectangle actorIconBounds = new Rectangle(cardX + ScaleUiValue(15), cursorY + ScaleUiValue(15), iconSize, iconSize);
                    var target = new SocialProfileClickableTarget
                    {
                        Bounds = actorIconBounds,
                        ActorName = post.AuthorName,
                        ActorIsPlayer = post.AuthorIsPlayer
                    };
                    this.socialProfileIconBounds.Add(target);

                    cursorY += postHeight + ScaleUiValue(14);
                }
            }
        }

        private void DrawSocialProfileAvatarCameraButton(SpriteBatch b, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color boxColor = hovered
                ? new Color(95, 145, 185, 135)
                : new Color(20, 20, 20, 110);

            UI.CardDrawing.DrawCard(
                b,
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                boxColor, 1f, false);

            int iconPadding = ScaleUiValue(7);
            Rectangle iconBounds = new Rectangle(
                bounds.X + iconPadding,
                bounds.Y + iconPadding,
                Math.Max(1, bounds.Width - (iconPadding * 2)),
                Math.Max(1, bounds.Height - (iconPadding * 2)));

            b.Draw(Game1.mouseCursors2, iconBounds, new Rectangle(72, 32, 18, 15), Color.White);
        }

        private void DrawSocialInteractionColumn(SpriteBatch b, Rectangle bounds, string title, List<StardewConnectProfileInteraction> interactions)
        {
            DrawPhoneText(b, Game1.smallFont, title, new Vector2(bounds.X, bounds.Y), Color.Black, 0.9f);

            int lineHeight = Math.Max(ScaleUiValue(22), GetPhoneScaledLineHeight(Game1.smallFont, 0.85f));
            int y = bounds.Y + lineHeight + ScaleUiValue(4);

            if (interactions == null || interactions.Count == 0)
            {
                DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("profile.noData"), new Vector2(bounds.X, y), Color.DimGray, 0.9f);
                return;
            }

            for (int i = 0; i < interactions.Count && i < 3; i++)
            {
                StardewConnectProfileInteraction row = interactions[i];
                string cleanName = row.ActorIsPlayer
                    ? row.ActorName
                    : (Game1.getCharacterFromName(row.ActorName)?.displayName ?? row.ActorName);
                string label = $"{i + 1}. {cleanName} ({row.Count})";
                DrawPhoneText(b, Game1.smallFont, label, new Vector2(bounds.X, y), Color.DarkSlateGray, 0.9f);
                y += lineHeight;
            }
        }

        private string GetSocialProfileAgeLabel(string actorName, bool actorIsPlayer)
        {
            if (actorIsPlayer)
            {
                // Find the specific farmer by their name profile target
                Farmer? farmer = Game1.getAllFarmers().FirstOrDefault(f => string.Equals(f.Name, actorName, StringComparison.OrdinalIgnoreCase));
                if (farmer != null)
                {
                    string messengerModId = "d5a1lamdtd.Smartphone-AppMessenger";
                    if (farmer.modData.TryGetValue($"{messengerModId}/Age", out string fAge) && !string.IsNullOrWhiteSpace(fAge))
                    {
                        return fAge;
                    }
                }
                return ModEntry.SHelper.Translation.Get("profile.unknown");
            }

            NPC? npc = Game1.getCharacterFromName(actorName);
            if (npc == null)
                return ModEntry.SHelper.Translation.Get("profile.unknown");

            return npc.Age == 0 ? ModEntry.SHelper.Translation.Get("profile.adult") : npc.Age == 1 ? ModEntry.SHelper.Translation.Get("profile.teens") : npc.Age == 2 ? ModEntry.SHelper.Translation.Get("profile.child") : ModEntry.SHelper.Translation.Get("profile.adult");
        }

        private string GetSocialProfileBirthdayLabel(string actorName, bool actorIsPlayer)
        {
            if (actorIsPlayer)
            {
                // Find the specific farmer by their name profile target
                Farmer? farmer = Game1.getAllFarmers().FirstOrDefault(f => string.Equals(f.Name, actorName, StringComparison.OrdinalIgnoreCase));
                if (farmer != null)
                {
                    string messengerModId = "d5a1lamdtd.Smartphone-AppMessenger";
                    if (farmer.modData.TryGetValue($"{messengerModId}/BirthDate", out string fDay) &&
                        farmer.modData.TryGetValue($"{messengerModId}/BirthSeason", out string fSeason))
                    {
                        if (!string.IsNullOrWhiteSpace(fDay) && !string.IsNullOrWhiteSpace(fSeason))
                        {
                            // Clean up string casing (e.g., "spring" -> "Spring")
                            string fSeasonLabel = char.ToUpperInvariant(fSeason[0]) + fSeason.Substring(1).ToLowerInvariant();
                            return $"{fSeasonLabel} {fDay}";
                        }
                    }
                }
                return ModEntry.SHelper.Translation.Get("profile.unknown");
            }

            NPC? npc = Game1.getCharacterFromName(actorName, mustBeVillager: false);
            if (npc == null || npc.Birthday_Day <= 0 || string.IsNullOrWhiteSpace(npc.Birthday_Season))
                return ModEntry.SHelper.Translation.Get("profile.unknown");

            string season = npc.Birthday_Season.Trim();
            string seasonLabel = char.ToUpperInvariant(season[0]) + season.Substring(1).ToLowerInvariant();
            return $"{seasonLabel} {npc.Birthday_Day}";
        }





        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            if (this.emojiMenu != null)
            {
                this.emojiMenu.performHoverAction(x, y);
            }

            if (this.socialNotificationMenuOpen && !this.socialNotificationClearAllBounds.IsEmpty && this.socialNotificationClearAllBounds.Contains(x, y))
            {
                this.hoverText = ModEntry.SHelper.Translation.Get("notifications.markAllRead");
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.emojiMenu != null)
            {
                if (this.emojiMenu.isWithinBounds(x, y))
                {
                    this.emojiMenu.receiveLeftClick(x, y);
                }
                else
                {
                    this.emojiMenu = null;
                    Game1.playSound("bigDeSelect");
                }
                return;
            }

            // Bottom navigation clicking
            if (this.smartphoneApi.HandlePhoneAppBottomNavClick(x, y, this.xPositionOnScreen, this.yPositionOnScreen, onBack: NavigateBack))
            {
                return;
            }

            // Size buttons click handling
            if (this.smartphoneApi.HandlePhoneSizeButtonsClick(x, y, this.xPositionOnScreen, this.yPositionOnScreen))
            {
                return;
            }

            Rectangle contentRect = GetContentBounds();
            Rectangle clipRect = SocialContentViewportRect;

            // Set text box click cursor
            if (contentRect.Contains(x, y))
            {
                this.Selected = true;
                Game1.keyboardDispatcher.Subscriber = this;

                if (this.socialCreateMenuOpen)
                {
                    if (this.socialTagMenuOpen)
                    {
                        // Focus tag search text area
                        int margin = ScaleUiValue(15);
                        Rectangle popupRect = new Rectangle(
                            clipRect.X + margin,
                            clipRect.Y + margin,
                            clipRect.Width - (margin * 2),
                            clipRect.Height - (margin * 2));
                        Rectangle searchBounds = new Rectangle(popupRect.X + ScaleUiValue(15), popupRect.Y + ScaleUiValue(50), popupRect.Width - ScaleUiValue(30), ScaleUiValue(56));

                        if (searchBounds.Contains(x, y))
                        {
                            if (Constants.TargetPlatform == GamePlatform.Android)
                            {
                                TriggerAndroidKeyboard(ActiveInput.TagSearch, ModEntry.SHelper.Translation.Get("keyboard.title.tagPeople"), ModEntry.SHelper.Translation.Get("keyboard.description"), this.tagSearchTextBox.Text);
                            }
                            else
                            {
                                this.tagSearchTextBox.SetCursorFromClick(x, searchBounds, this.phoneUiScale);
                            }
                        }
                    }
                    else
                    {
                        // Focus post draft text area
                        Rectangle inputBounds = new Rectangle(clipRect.X + ScaleUiValue(15), clipRect.Y + ScaleUiValue(15), clipRect.Width - ScaleUiValue(30), ScaleUiValue(180));
                        if (inputBounds.Contains(x, y))
                        {
                            if (Constants.TargetPlatform == GamePlatform.Android)
                            {
                                TriggerAndroidKeyboard(ActiveInput.Post, ModEntry.SHelper.Translation.Get("keyboard.title.createPost"), ModEntry.SHelper.Translation.Get("keyboard.description"), this.postTextBox.Text);
                            }
                            else
                            {
                                this.postTextBox.SetCursorFromClick(x, inputBounds, this.phoneUiScale);
                            }
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
                {
                    // Focus comment draft text area
                    Rectangle commentInputBounds = new Rectangle(clipRect.X + ScaleUiValue(15), clipRect.Bottom - ScaleUiValue(75), clipRect.Width - ScaleUiValue(95), ScaleUiValue(60));
                    if (commentInputBounds.Contains(x, y))
                    {
                        if (Constants.TargetPlatform == GamePlatform.Android)
                        {
                            TriggerAndroidKeyboard(ActiveInput.Comment, ModEntry.SHelper.Translation.Get("keyboard.title.comment"), ModEntry.SHelper.Translation.Get("keyboard.description"), this.commentTextBox.Text);
                        }
                        else
                        {
                            this.commentTextBox.SetCursorFromClick(x, commentInputBounds, this.phoneUiScale);
                        }
                    }
                }
            }

            this.lastScrollMouseY = y;
            this.touchScrollStartY = y;
            this.hasTouchScrolled = false;
            this.isScrolling = false;
        }

        private void NavigateBack()
        {
            if (this.emojiMenu != null)
            {
                this.emojiMenu = null;
                Game1.playSound("bigDeSelect");
            }
            else if (this.socialCreateMenuOpen)
            {
                if (this.socialTagMenuOpen)
                {
                    this.socialTagMenuOpen = false;
                    this.tagSearchTextBox.Clear();
                    Game1.playSound("bigDeSelect");
                }
                else
                {
                    this.socialCreateMenuOpen = false;
                    this.postTextBox.Clear();
                    this.draftTagged.Clear();
                    this.tagSearchTextBox.Clear();
                    foreach (var tex in this.draftSelectedTextures)
                    {
                        tex?.Dispose();
                    }
                    this.draftSelectedTextures.Clear();
                    this.draftSelectedPhotos.Clear();
                    this.draftPhotoPreviewIndex = 0;
                    Game1.playSound("bigDeSelect");
                }
            }
            else if (this.socialNotificationMenuOpen)
            {
                this.socialNotificationMenuOpen = false;
                Game1.playSound("bigDeSelect");
            }
            else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                var post = StardewConnectManager.GetPost(this.selectedSocialPostId);
                if (post != null)
                {
                    StardewConnectManager.SetPlayerReadCommentCount(post, Game1.player?.Name ?? "Player", post.Comments.Count);
                    StardewConnectManager.Save();
                }
                this.selectedSocialPostId = "";
                this.commentTextBox.Clear();
                if (this.socialProfileDetailBackStack)
                {
                    this.socialProfileMenuOpen = true;
                    this.socialProfileDetailBackStack = false;
                }
                Game1.playSound("bigDeSelect");
            }
            else if (this.socialProfileMenuOpen)
            {
                this.socialProfileMenuOpen = false;
                Game1.playSound("bigDeSelect");
            }
            else
            {
                StardewConnectManager.SaveLastVisitTime();
                if (Game1.keyboardDispatcher.Subscriber == this)
                {
                    Game1.keyboardDispatcher.Subscriber = null;
                }
                this.onBack?.Invoke();
            }
            CalculateLayout();
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);

            int scrollAmount = ScaleUiValue(40);
            if (this.socialCreateMenuOpen)
            {
                if (this.socialTagMenuOpen)
                {
                    if (direction > 0) this.tagMenuScrollTarget -= scrollAmount;
                    else if (direction < 0) this.tagMenuScrollTarget += scrollAmount;
                    this.tagMenuScrollTarget = Math.Clamp(this.tagMenuScrollTarget, 0, this.maxScrollTagMenu);
                }
                return;
            }

            if (this.socialNotificationMenuOpen)
            {
                if (direction > 0) this.socialNotificationScrollTarget -= scrollAmount;
                else if (direction < 0) this.socialNotificationScrollTarget += scrollAmount;
                this.socialNotificationScrollTarget = Math.Clamp(this.socialNotificationScrollTarget, 0, this.maxScrollNotification);
            }
            else if (this.socialProfileMenuOpen)
            {
                if (direction > 0) this.socialProfileScrollTarget -= scrollAmount;
                else if (direction < 0) this.socialProfileScrollTarget += scrollAmount;
                this.socialProfileScrollTarget = Math.Clamp(this.socialProfileScrollTarget, 0, this.maxScrollProfile);
            }
            else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                if (direction > 0) this.socialDetailScrollTarget -= scrollAmount;
                else if (direction < 0) this.socialDetailScrollTarget += scrollAmount;
                this.socialDetailScrollTarget = Math.Clamp(this.socialDetailScrollTarget, 0, this.maxScrollDetail);
            }
            else
            {
                if (direction > 0) this.socialFeedScrollTarget -= scrollAmount;
                else if (direction < 0) this.socialFeedScrollTarget += scrollAmount;
                this.socialFeedScrollTarget = Math.Clamp(this.socialFeedScrollTarget, 0, this.maxScrollFeed);
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);

            if (this.emojiMenu != null)
            {
                this.emojiMenu.releaseLeftClick(x, y);
                return;
            }

            bool wasDragging = this.isDragging;
            this.isDragging = false;
            this.isScrolling = false;

            if (this.hasTouchScrolled)
            {
                this.hasTouchScrolled = false;
                return;
            }

            if (wasDragging)
            {
                return;
            }

            // Check next/prev photo clicks
            var activePrevBounds = this.socialCreateMenuOpen ? new Dictionary<string, Rectangle>() : (string.IsNullOrWhiteSpace(this.selectedSocialPostId) ? this.socialFeedPhotoPrevBounds : this.socialDetailPhotoPrevBounds);
            var activeNextBounds = this.socialCreateMenuOpen ? new Dictionary<string, Rectangle>() : (string.IsNullOrWhiteSpace(this.selectedSocialPostId) ? this.socialFeedPhotoNextBounds : this.socialDetailPhotoNextBounds);

            foreach (var pair in activePrevBounds)
            {
                if (pair.Value.Contains(x, y))
                {
                    var post = StardewConnectManager.GetPost(pair.Key);
                    if (post != null && post.Photo.Count > 1)
                    {
                        if (!this.postPhotoIndices.TryGetValue(post.Id, out int idx)) idx = 0;
                        idx--;
                        if (idx < 0) idx = post.Photo.Count - 1;
                        this.postPhotoIndices[post.Id] = idx;
                        Game1.playSound("shwip");
                        CalculateLayout();
                        return;
                    }
                }
            }

            foreach (var pair in activeNextBounds)
            {
                if (pair.Value.Contains(x, y))
                {
                    var post = StardewConnectManager.GetPost(pair.Key);
                    if (post != null && post.Photo.Count > 1)
                    {
                        if (!this.postPhotoIndices.TryGetValue(post.Id, out int idx)) idx = 0;
                        idx = (idx + 1) % post.Photo.Count;
                        this.postPhotoIndices[post.Id] = idx;
                        Game1.playSound("shwip");
                        CalculateLayout();
                        return;
                    }
                }
            }

            if (this.socialCreateMenuOpen && this.socialTagMenuOpen)
            {
                if (this.socialTagMenuDoneBounds.Contains(x, y))
                {
                    this.socialTagMenuOpen = false;
                    this.tagSearchTextBox.Clear();
                    Game1.playSound("bigDeSelect");
                    return;
                }

                Rectangle tagClipRect = SocialContentViewportRect;
                if (tagClipRect.Contains(x, y))
                {
                    foreach (var pair in this.socialTagItemBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            if (this.draftTagged.Contains(pair.Key))
                            {
                                this.draftTagged.Remove(pair.Key);
                                Game1.playSound("bigDeSelect");
                            }
                            else
                            {
                                this.draftTagged.Add(pair.Key);
                                Game1.playSound("smallSelect");
                            }
                            return;
                        }
                    }
                }
                return;
            }

            Rectangle contentRect = GetContentBounds();
            Rectangle clipRect = SocialContentViewportRect;

            // Click Profile icon to redirect
            if (clipRect.Contains(x, y))
            {
                var activeProfileList = this.socialFeedProfileIconBounds;
                if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId)) activeProfileList = this.socialDetailProfileIconBounds;
                else if (this.socialProfileMenuOpen) activeProfileList = this.socialProfileIconBounds;

                foreach (var target in activeProfileList)
                {
                    if (target.Bounds.Contains(x, y))
                    {
                        if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
                        {
                            var post = StardewConnectManager.GetPost(this.selectedSocialPostId);
                            if (post != null)
                            {
                                StardewConnectManager.SetPlayerReadCommentCount(post, Game1.player?.Name ?? "Player", post.Comments.Count);
                                StardewConnectManager.Save();
                            }
                        }

                        this.selectedSocialProfileActorName = target.ActorName;
                        this.selectedSocialProfileActorIsPlayer = target.ActorIsPlayer;
                        this.selectedSocialPostId = "";
                        this.socialProfileMenuOpen = true;
                        this.socialProfileDetailBackStack = false;
                        this.socialFeedOpenCreatePostBounds = Rectangle.Empty;
                        this.socialProfileScrollOffset = 0f;
                        this.socialProfileScrollTarget = 0f;
                        CalculateLayout();
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
            }

            if (this.socialCreateMenuOpen)
            {
                if (this.draftSelectedTextures.Count > 1)
                {
                    if (this.socialCreatePhotoPrevBounds.Contains(x, y))
                    {
                        this.draftPhotoPreviewIndex--;
                        if (this.draftPhotoPreviewIndex < 0)
                            this.draftPhotoPreviewIndex = this.draftSelectedTextures.Count - 1;
                        Game1.playSound("shwip");
                        return;
                    }

                    if (this.socialCreatePhotoNextBounds.Contains(x, y))
                    {
                        this.draftPhotoPreviewIndex = (this.draftPhotoPreviewIndex + 1) % this.draftSelectedTextures.Count;
                        Game1.playSound("shwip");
                        return;
                    }
                }

                if (this.socialCreateSubmitBounds.Contains(x, y))
                {
                    if (!string.IsNullOrWhiteSpace(this.postTextBox.Text) || this.draftSelectedPhotos.Count > 0)
                    {
                        var attachmentFiles = this.draftSelectedPhotos.Select(p => p.FileName).ToList();

                        // Copy draft photos to local photo_shared folder
                        string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                        string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                        Directory.CreateDirectory(photoSharedDir);
                        foreach (var photo in this.draftSelectedPhotos)
                        {
                            if (photo.TextureData != null && photo.TextureData.Length > 0)
                            {
                                string destPath = Path.Combine(photoSharedDir, photo.FileName);
                                try
                                {
                                    File.WriteAllBytes(destPath, photo.TextureData);
                                }
                                catch (Exception ex)
                                {
                                    ModEntry.SMonitor.Log($"Failed to write draft photo {photo.FileName}: {ex.Message}", LogLevel.Error);
                                }
                            }
                        }

                        var photoTags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var photo in this.draftSelectedPhotos)
                        {
                            photoTags[photo.FileName] = photo.Tag ?? "";
                        }

                        if (!Context.IsMultiplayer || Context.IsMainPlayer)
                        {
                            StardewConnectManager.AddPlayerPost(this.postTextBox.Text, attachmentFiles, this.draftTagged, photoTags: photoTags);
                        }
                        else
                        {
                            // Farmhand sends photos to Host, and requests post creation
                            if (Game1.MasterPlayer != null)
                            {
                                foreach (var photo in this.draftSelectedPhotos)
                                {
                                    string absolutePath = Path.Combine(photoSharedDir, photo.FileName);
                                    TransferManager.QueueSend("Photo", photo.FileName, absolutePath, Game1.MasterPlayer.UniqueMultiplayerID);
                                }

                                var req = new ActionRequest
                                {
                                    Action = "CreatePost",
                                    Text = this.postTextBox.Text,
                                    Photos = attachmentFiles,
                                    Tagged = this.draftTagged,
                                    ActorName = Game1.player.Name,
                                    PhotoTags = photoTags
                                };
                                string reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(req);
                                TransferManager.SendDirectMessage(Game1.MasterPlayer.UniqueMultiplayerID, "ActionRequest", reqJson);
                            }
                        }

                        this.postTextBox.Clear();
                        this.draftTagged.Clear();
                        this.tagSearchTextBox.Clear();
                        foreach (var tex in this.draftSelectedTextures)
                        {
                            tex?.Dispose();
                        }
                        this.draftSelectedTextures.Clear();
                        this.draftSelectedPhotos.Clear();
                        this.draftPhotoPreviewIndex = 0;

                        this.socialCreateMenuOpen = false;
                        CalculateLayout();
                        Game1.playSound("money");
                    }
                    return;
                }

                if (this.socialCreateCancelBounds.Contains(x, y))
                {
                    NavigateBack();
                    return;
                }

                if (this.socialCreatePhotoSelectBounds.Contains(x, y))
                {
                    var currentScreen = this;
                    Game1.activeClickableMenu = null;
                    this.smartphoneApi.RetrievePhotos(3, true, true, (jsonResult) =>
                    {
                        Game1.activeClickableMenu = currentScreen;
                        try
                        {
                            var results = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SelectedPhotoResult>>(jsonResult);
                            if (results != null)
                            {
                                foreach (var tex in this.draftSelectedTextures)
                                {
                                    tex?.Dispose();
                                }
                                this.draftSelectedTextures.Clear();
                                this.draftSelectedPhotos.Clear();

                                foreach (var result in results)
                                {
                                    if (result.TextureData != null && result.TextureData.Length > 0)
                                    {
                                        var tex = Texture2D.FromStream(Game1.graphics.GraphicsDevice, new MemoryStream(result.TextureData));
                                        this.draftSelectedTextures.Add(tex);
                                        this.draftSelectedPhotos.Add(result);
                                    }
                                }
                                this.draftPhotoPreviewIndex = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.SMonitor.Log($"Error processing selected photos: {ex.Message}", LogLevel.Error);
                        }
                    });
                    Game1.playSound("smallSelect");
                    return;
                }

                if (this.socialCreateEmojiButtonBounds.Contains(x, y))
                {
                    this.socialTagMenuOpen = true;
                    this.tagSearchTextBox.Clear();
                    this.tagMenuScrollOffset = 0f;
                    this.tagMenuScrollTarget = 0f;
                    Game1.playSound("smallSelect");
                    return;
                }
            }
            else if (this.socialNotificationMenuOpen)
            {
                if (this.socialNotificationClearAllBounds.Contains(x, y))
                {
                    var notifs = StardewConnectManager.GetActiveSocialNotifications();
                    StardewConnectManager.DismissSocialNotifications(notifs.Select(n => n.Id).ToArray());
                    CalculateLayout();
                    Game1.playSound("bigDeSelect");
                    return;
                }

                if (clipRect.Contains(x, y))
                {
                    foreach (var pair in this.socialNotificationItemBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            var notifs = StardewConnectManager.GetActiveSocialNotifications();
                            var clickedNotif = notifs.FirstOrDefault(n => string.Equals(n.Id, pair.Key, StringComparison.OrdinalIgnoreCase));
                            if (clickedNotif != null)
                            {
                                this.selectedSocialPostId = clickedNotif.PostId;
                                StardewConnectManager.DismissSocialNotification(clickedNotif.Id);
                            }
                            this.socialNotificationMenuOpen = false;
                            this.socialProfileDetailBackStack = false;
                            this.socialDetailScrollOffset = 0f;
                            this.socialDetailScrollTarget = 0f;
                            CalculateLayout();
                            Game1.playSound("bigSelect");
                            return;
                        }
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                if (!this.socialDetailDeletePostBounds.IsEmpty && this.socialDetailDeletePostBounds.Contains(x, y))
                {
                    DeleteCurrentPost();
                    return;
                }

                if (this.socialDetailCommentSendBounds.Contains(x, y))
                {
                    SendComment();
                    return;
                }

                if (this.socialDetailLikeBounds.Contains(x, y))
                {
                    ToggleLike(this.selectedSocialPostId);
                    return;
                }
            }
            else if (this.socialProfileMenuOpen)
            {
                if (this.socialProfileAvatarCameraButtonBounds.Contains(x, y))
                {
                    Game1.playSound("smallSelect");
                    Game1.activeClickableMenu = null;

                    var api = this.smartphoneApi;
                    var backAction = this.onBack;

                    api.RetrievePhotos(limit: 1, getTexture: true, getMetadata: false, onComplete: (jsonResult) =>
                    {
                        var screen = new StardewSocialScreen(api, backAction)
                        {
                            socialProfileMenuOpen = true,
                            selectedSocialProfileActorName = Game1.player?.Name ?? "Player",
                            selectedSocialProfileActorIsPlayer = true
                        };
                        Game1.activeClickableMenu = screen;

                        List<SelectedPhotoResult>? results = null;
                        try
                        {
                            results = string.IsNullOrWhiteSpace(jsonResult)
                                ? null
                                : Newtonsoft.Json.JsonConvert.DeserializeObject<List<SelectedPhotoResult>>(jsonResult);
                        }
                        catch (Exception ex)
                        {
                            ModEntry.SMonitor.Log($"Failed to deserialize avatar photo result: {ex.Message}", LogLevel.Error);
                        }

                        if (results != null && results.Count > 0 && results[0].TextureData != null)
                        {
                            try
                            {
                                string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                                string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                                Directory.CreateDirectory(photoSharedDir);

                                string id = Game1.player.UniqueMultiplayerID.ToString();
                                foreach (var oldFile in Directory.GetFiles(photoSharedDir, $"{id}_avatar.*"))
                                {
                                    try { File.Delete(oldFile); } catch { }
                                }

                                string destPath = Path.Combine(photoSharedDir, $"{id}_avatar.jpg");
                                File.WriteAllBytes(destPath, results[0].TextureData);

                                if (Context.IsMultiplayer)
                                {
                                    if (Context.IsMainPlayer)
                                    {
                                        foreach (var farmer in Game1.getOnlineFarmers())
                                        {
                                            if (farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
                                            {
                                                TransferManager.QueueSend("Avatar", $"{id}_avatar.jpg", destPath, farmer.UniqueMultiplayerID);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (Game1.MasterPlayer != null)
                                        {
                                            TransferManager.QueueSend("Avatar", $"{id}_avatar.jpg", destPath, Game1.MasterPlayer.UniqueMultiplayerID);
                                        }
                                    }
                                }

                                ClearAvatarCache();
                            }
                            catch (Exception ex)
                            {
                                ModEntry.SMonitor.Log($"Failed to save avatar photo: {ex.Message}", LogLevel.Error);
                            }
                        }
                    }, squareOnly: true);
                    return;
                }

                if (clipRect.Contains(x, y))
                {
                    foreach (var pair in this.socialProfileLikeBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            ToggleLike(pair.Key);
                            return;
                        }
                    }

                    foreach (var pair in this.socialProfilePostBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            this.selectedSocialPostId = pair.Key;
                            this.socialProfileMenuOpen = false;
                            this.socialProfileDetailBackStack = true;
                            this.socialDetailScrollOffset = 0f;
                            this.socialDetailScrollTarget = 0f;
                            CalculateLayout();
                            Game1.playSound("bigSelect");
                            return;
                        }
                    }
                }
            }
            else
            {
                // Feed list
                if (this.socialFeedOpenCreatePostBounds.Contains(x, y))
                {
                    this.socialCreateMenuOpen = true;
                    Game1.playSound("smallSelect");
                    return;
                }

                if (this.socialFeedOpenProfileBounds.Contains(x, y))
                {
                    this.selectedSocialProfileActorName = Game1.player?.Name ?? "Player";
                    this.selectedSocialProfileActorIsPlayer = true;
                    this.socialProfileMenuOpen = true;
                    this.socialProfileScrollOffset = 0f;
                    this.socialProfileScrollTarget = 0f;
                    CalculateLayout();
                    Game1.playSound("smallSelect");
                    return;
                }

                if (this.socialFeedOpenNotificationBounds.Contains(x, y))
                {
                    this.socialNotificationMenuOpen = true;
                    this.socialNotificationScrollOffset = 0f;
                    this.socialNotificationScrollTarget = 0f;
                    CalculateLayout();
                    Game1.playSound("smallSelect");
                    return;
                }

                if (clipRect.Contains(x, y))
                {
                    foreach (var pair in this.socialFeedCommentBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            this.selectedSocialPostId = pair.Key;
                            this.socialProfileDetailBackStack = false;
                            this.socialDetailScrollOffset = 0f;
                            this.socialDetailScrollTarget = 0f;
                            CalculateLayout();
                            Game1.playSound("bigSelect");
                            return;
                        }
                    }

                    foreach (var pair in this.socialFeedLikeBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            ToggleLike(pair.Key);
                            return;
                        }
                    }

                    foreach (var pair in this.socialFeedPostBounds)
                    {
                        if (pair.Value.Contains(x, y))
                        {
                            this.selectedSocialPostId = pair.Key;
                            this.socialProfileDetailBackStack = false;
                            this.socialDetailScrollOffset = 0f;
                            this.socialDetailScrollTarget = 0f;
                            CalculateLayout();
                            Game1.playSound("bigSelect");
                            return;
                        }
                    }
                }
            }

        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            if (!this.isDragging && !this.isScrolling)
            {
                Rectangle frameBounds = GetFrameBounds();
                Rectangle contentBounds = GetContentBounds();

                if (contentBounds.Contains(x, y))
                {
                    this.isScrolling = true;
                    this.lastScrollMouseY = y;
                }
                else if (frameBounds.Contains(x, y))
                {
                    bool isViewingFeed = !this.socialCreateMenuOpen && !this.socialNotificationMenuOpen && !this.socialProfileMenuOpen && string.IsNullOrWhiteSpace(this.selectedSocialPostId);
                    bool clickingTopButton = (isViewingFeed && (this.socialFeedOpenCreatePostBounds.Contains(x, y) || this.socialFeedOpenProfileBounds.Contains(x, y) || this.socialFeedOpenNotificationBounds.Contains(x, y)))
                        || (!string.IsNullOrWhiteSpace(this.selectedSocialPostId) && !this.socialDetailDeletePostBounds.IsEmpty && this.socialDetailDeletePostBounds.Contains(x, y));
                    if (!clickingTopButton)
                    {
                        this.isDragging = true;
                        this.dragOffsetX = x - this.xPositionOnScreen;
                        this.dragOffsetY = y - this.yPositionOnScreen;
                    }
                }
            }

            if (this.isScrolling)
            {
                if (Math.Abs(y - this.touchScrollStartY) > 5)
                    this.hasTouchScrolled = true;

                int deltaY = y - this.lastScrollMouseY;
                this.lastScrollMouseY = y;
                if (deltaY != 0)
                {
                    if (this.socialCreateMenuOpen)
                    {
                        if (this.socialTagMenuOpen)
                        {
                            this.tagMenuScrollTarget -= deltaY;
                            this.tagMenuScrollTarget = Math.Clamp(this.tagMenuScrollTarget, 0, this.maxScrollTagMenu);
                        }
                    }
                    else if (this.socialNotificationMenuOpen)
                    {
                        this.socialNotificationScrollTarget -= deltaY;
                        this.socialNotificationScrollTarget = Math.Clamp(this.socialNotificationScrollTarget, 0, this.maxScrollNotification);
                    }
                    else if (this.socialProfileMenuOpen)
                    {
                        this.socialProfileScrollTarget -= deltaY;
                        this.socialProfileScrollTarget = Math.Clamp(this.socialProfileScrollTarget, 0, this.maxScrollProfile);
                    }
                    else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
                    {
                        this.socialDetailScrollTarget -= deltaY;
                        this.socialDetailScrollTarget = Math.Clamp(this.socialDetailScrollTarget, 0, this.maxScrollDetail);
                    }
                    else
                    {
                        this.socialFeedScrollTarget -= deltaY;
                        this.socialFeedScrollTarget = Math.Clamp(this.socialFeedScrollTarget, 0, this.maxScrollFeed);
                    }
                }
            }
        }

        public override void update(GameTime time)
        {
            // Sync from API if modified externally
            float activeScale = this.smartphoneApi.GetPhoneUiScale();
            if (Math.Abs(this.phoneUiScale - activeScale) > 0.001f)
            {
                this.phoneUiScale = activeScale;
                this.phoneFrameWidth = this.smartphoneApi.GetPhoneFrameWidth();
                this.phoneFrameHeight = this.smartphoneApi.GetPhoneFrameHeight();
                var (offX, offY) = this.smartphoneApi.GetPhoneContentOffset();
                this.phoneContentOffsetX = offX;
                this.phoneContentOffsetY = offY;
                this.phoneFrameTexture = this.smartphoneApi.GetPhoneFrameTexture();
                this.phoneBackgroundTexture = this.smartphoneApi.GetPhoneBackgroundTexture();

                this.width = this.phoneFrameWidth;
                this.height = this.phoneFrameHeight;

                this.contentWidth = Math.Max(1, this.phoneFrameWidth - (this.phoneContentOffsetX * 2));
                this.contentHeight = Math.Max(1, this.phoneFrameHeight - this.phoneContentOffsetY - ScaleUiValue(135));
                this.ClearCachesAndRecalculate();
            }

            CalculateLayout();
            base.update(time);

            // Update scrolling animation
            float lerpFactor = (float)(time.ElapsedGameTime.TotalSeconds * 16f);
            this.socialFeedScrollOffset = MathHelper.Lerp(this.socialFeedScrollOffset, this.socialFeedScrollTarget, Math.Min(1f, lerpFactor));
            this.socialNotificationScrollOffset = MathHelper.Lerp(this.socialNotificationScrollOffset, this.socialNotificationScrollTarget, Math.Min(1f, lerpFactor));
            this.socialProfileScrollOffset = MathHelper.Lerp(this.socialProfileScrollOffset, this.socialProfileScrollTarget, Math.Min(1f, lerpFactor));
            this.socialDetailScrollOffset = MathHelper.Lerp(this.socialDetailScrollOffset, this.socialDetailScrollTarget, Math.Min(1f, lerpFactor));
            this.tagMenuScrollOffset = MathHelper.Lerp(this.tagMenuScrollOffset, this.tagMenuScrollTarget, Math.Min(1f, lerpFactor));

            // TextBox update
            UpdateAndroidKeyboard();
            this.postTextBox.Update(time, this.Selected && this.socialCreateMenuOpen && !this.socialTagMenuOpen);
            this.tagSearchTextBox.Update(time, this.Selected && this.socialCreateMenuOpen && this.socialTagMenuOpen);
            this.commentTextBox.Update(time, this.Selected && !string.IsNullOrWhiteSpace(this.selectedSocialPostId));

            // Emoji Menu update
            if (this.emojiMenu != null)
            {
                this.emojiMenu.update(time);
            }

            // Sync emojis from mockChatBox
            if (this.mockChatBox != null)
            {
                string emojiText = GetTextFromChatTextBox(this.mockChatBox);
                if (!string.IsNullOrEmpty(emojiText))
                {
                    ClearChatTextBox(this.mockChatBox);
                    if (this.socialCreateMenuOpen)
                    {
                        this.postTextBox.RecieveTextInput(emojiText);
                    }
                    else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
                    {
                        this.commentTextBox.RecieveTextInput(emojiText);
                    }
                }
            }

            // Force keyboard dispatcher focus (unless emojiMenu is open)
            if (Game1.keyboardDispatcher.Subscriber != this && this.emojiMenu == null)
            {
                Game1.keyboardDispatcher.Subscriber = this;
                this.Selected = true;
            }

            if (this.isDragging)
            {
                int oldX = this.xPositionOnScreen;
                int oldY = this.yPositionOnScreen;
                this.xPositionOnScreen = Game1.getMouseX() - this.dragOffsetX;
                this.yPositionOnScreen = Game1.getMouseY() - this.dragOffsetY;
                if (this.xPositionOnScreen != oldX || this.yPositionOnScreen != oldY)
                {
                    this.smartphoneApi.SetPhonePosition(this.xPositionOnScreen, this.yPositionOnScreen);
                }
            }

            // Sync from API if modified externally
            var (targetX, targetY) = this.smartphoneApi.GetPhonePosition();
            if (this.xPositionOnScreen != targetX || this.yPositionOnScreen != targetY)
            {
                this.xPositionOnScreen = targetX;
                this.yPositionOnScreen = targetY;
            }
        }



        protected override void cleanupBeforeExit()
        {
            StardewConnectManager.SaveLastVisitTime();
            if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                var post = StardewConnectManager.GetPost(this.selectedSocialPostId);
                if (post != null)
                {
                    StardewConnectManager.SetPlayerReadCommentCount(post, Game1.player?.Name ?? "Player", post.Comments.Count);
                    StardewConnectManager.Save();
                }
            }
            if (Game1.keyboardDispatcher.Subscriber == this)
            {
                Game1.keyboardDispatcher.Subscriber = null;
            }
            foreach (var tex in this.draftSelectedTextures)
            {
                tex?.Dispose();
            }
            this.draftSelectedTextures.Clear();
            this.draftSelectedPhotos.Clear();
            this.draftPhotoPreviewIndex = 0;

            base.cleanupBeforeExit();
        }

        private void TriggerAndroidKeyboard(ActiveInput fieldType, string title, string description, string currentText)
        {
            if (Constants.TargetPlatform != GamePlatform.Android) return;
            this.activeAndroidInput = fieldType;

            try
            {
                Type? keyboardInputType = typeof(Microsoft.Xna.Framework.Input.Keyboard).Assembly.GetType("Microsoft.Xna.Framework.Input.KeyboardInput");
                if (keyboardInputType != null)
                {
                    var showMethod = keyboardInputType.GetMethod("Show", new[] { typeof(string), typeof(string), typeof(string), typeof(bool) });
                    if (showMethod != null)
                    {
                        this.pendingKeyboardTask = (System.Threading.Tasks.Task<string>)showMethod.Invoke(null, new object[] { title, description, currentText, false })!;
                    }
                }
            }
            catch (Exception)
            {
                this.pendingKeyboardTask = null;
                this.activeAndroidInput = ActiveInput.None;
            }
        }

        private void UpdateAndroidKeyboard()
        {
            if (this.pendingKeyboardTask != null && this.pendingKeyboardTask.IsCompleted)
            {
                if (!this.pendingKeyboardTask.IsFaulted && this.pendingKeyboardTask.Result != null)
                {
                    string result = this.pendingKeyboardTask.Result;
                    switch (this.activeAndroidInput)
                    {
                        case ActiveInput.Post:
                            this.postTextBox.Text = result;
                            this.postTextBox.CursorIndex = result.Length;
                            this.postTextBox.SelectionAnchorIndex = result.Length;
                            break;
                        case ActiveInput.Comment:
                            this.commentTextBox.Text = result;
                            this.commentTextBox.CursorIndex = result.Length;
                            this.commentTextBox.SelectionAnchorIndex = result.Length;
                            break;
                        case ActiveInput.TagSearch:
                            this.tagSearchTextBox.Text = result;
                            this.tagSearchTextBox.CursorIndex = result.Length;
                            this.tagSearchTextBox.SelectionAnchorIndex = result.Length;
                            break;
                    }
                }
                this.pendingKeyboardTask = null;
                this.activeAndroidInput = ActiveInput.None;
            }
        }

        // IKeyboardSubscriber Keyboard handling
        public void RecieveTextInput(char inputChar)
        {
            if (!Selected) return;

            if (this.socialCreateMenuOpen)
            {
                if (this.socialTagMenuOpen)
                {
                    if (!char.IsControl(inputChar))
                        this.tagSearchTextBox.RecieveTextInput(inputChar.ToString());
                }
                else
                {
                    if (!char.IsControl(inputChar))
                        this.postTextBox.RecieveTextInput(inputChar.ToString());
                }
            }
            else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                if (!char.IsControl(inputChar))
                    this.commentTextBox.RecieveTextInput(inputChar.ToString());
            }
        }

        public void RecieveTextInput(string text)
        {
            if (!Selected) return;

            if (this.socialCreateMenuOpen)
            {
                if (this.socialTagMenuOpen)
                {
                    this.tagSearchTextBox.RecieveTextInput(text);
                }
                else
                {
                    this.postTextBox.RecieveTextInput(text);
                }
            }
            else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                this.commentTextBox.RecieveTextInput(text);
            }
        }

        public void RecieveCommandInput(char command)
        {
            if (!Selected) return;

            if (command == '\b') // Backspace
            {
                if (this.socialCreateMenuOpen)
                {
                    if (this.socialTagMenuOpen)
                        this.tagSearchTextBox.RecieveBackspace();
                    else
                        this.postTextBox.RecieveBackspace();
                }
                else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
                    this.commentTextBox.RecieveBackspace();
            }
        }

        private void SendComment()
        {
            if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId) && !string.IsNullOrWhiteSpace(this.commentTextBox.Text))
            {
                if (!Context.IsMultiplayer || Context.IsMainPlayer)
                {
                    StardewConnectManager.AddPlayerComment(this.selectedSocialPostId, this.commentTextBox.Text);
                }
                else
                {
                    if (Game1.MasterPlayer != null)
                    {
                        var req = new ActionRequest
                        {
                            Action = "CommentPost",
                            PostId = this.selectedSocialPostId,
                            CommentText = this.commentTextBox.Text,
                            ActorName = Game1.player.Name
                        };
                        string reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(req);
                        TransferManager.SendDirectMessage(Game1.MasterPlayer.UniqueMultiplayerID, "ActionRequest", reqJson);
                    }
                }
                this.commentTextBox.Clear();
                this.socialCardHeightCache.Remove(this.selectedSocialPostId + "_detail");
                this.socialCardHeightCache.Remove(this.selectedSocialPostId + "_feed");
                CalculateLayout();
                this.socialDetailScrollTarget = 0f;
                Game1.playSound("money");
            }
        }

        private void DeleteCurrentPost()
        {
            if (string.IsNullOrWhiteSpace(this.selectedSocialPostId)) return;

            string postId = this.selectedSocialPostId;

            if (!Context.IsMultiplayer || Context.IsMainPlayer)
            {
                StardewConnectManager.DeletePost(postId);
                Game1.playSound("trashcan");
                this.selectedSocialPostId = "";
                ClearCachesAndRecalculate();
            }
            else
            {
                if (Game1.MasterPlayer != null)
                {
                    var req = new ActionRequest
                    {
                        Action = "DeletePost",
                        PostId = postId,
                        ActorName = Game1.player.Name
                    };
                    string reqJson = Newtonsoft.Json.JsonConvert.SerializeObject(req);
                    TransferManager.SendDirectMessage(Game1.MasterPlayer.UniqueMultiplayerID, "ActionRequest", reqJson);
                }
                Game1.playSound("trashcan");
                this.selectedSocialPostId = "";
                StardewConnectManager.DeletePostLocally(postId);
            }
        }

        public void RecieveSpecialInput(Keys key)
        {
            if (!Selected) return;

            if (this.socialCreateMenuOpen)
            {
                if (this.socialTagMenuOpen)
                    this.tagSearchTextBox.HandleKeyPress(key);
                else
                    this.postTextBox.HandleKeyPress(key);
            }
            else if (!string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                if (key == Keys.Enter)
                    SendComment();
                else
                    this.commentTextBox.HandleKeyPress(key);
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            bool isTyping = this.Selected && (this.socialCreateMenuOpen || !string.IsNullOrWhiteSpace(this.selectedSocialPostId));
            if (!isTyping)
            {
                string keyStr = key.ToString();
                if (keyStr == this.smartphoneApi.GetDecreaseSizeKey())
                {
                    this.smartphoneApi.AdjustPhoneSize(-0.1f);
                    return;
                }
                if (keyStr == this.smartphoneApi.GetIncreaseSizeKey())
                {
                    this.smartphoneApi.AdjustPhoneSize(0.1f);
                    return;
                }
            }

            if (key == Keys.Escape)
            {
                NavigateBack();
                return;
            }

            if (key == Keys.Enter && this.Selected && !string.IsNullOrWhiteSpace(this.selectedSocialPostId))
            {
                SendComment();
                return;
            }

            // Block keys (like E/F) from opening inventory/closing menu when typing in a text box
            if (this.Selected && (this.socialCreateMenuOpen || !string.IsNullOrWhiteSpace(this.selectedSocialPostId)))
            {
                return;
            }

            base.receiveKeyPress(key);
        }

        private static bool IsCjk(char c)
        {
            return (c >= 0x4e00 && c <= 0x9fff) || // CJK Unified Ideographs
                   (c >= 0x3040 && c <= 0x309f) || // Hiragana
                   (c >= 0x30a0 && c <= 0x30ff) || // Katakana
                   (c >= 0xac00 && c <= 0xd7af) || // Hangul Syllables
                   (c >= 0xff00 && c <= 0xffef) || // Halfwidth and Fullwidth Forms
                   (c >= 0x3000 && c <= 0x303f);   // CJK Symbols and Punctuation
        }

        private static List<string> SplitTextIntoLines(string text, SpriteFont font, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            string[] paragraphs = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            List<string> allLines = new List<string>();

            foreach (var paragraph in paragraphs)
            {
                List<string> tokens = new List<string>();
                string currentWord = "";

                for (int i = 0; i < paragraph.Length; i++)
                {
                    char c = paragraph[i];
                    if (c == ' ')
                    {
                        if (currentWord != "")
                        {
                            tokens.Add(currentWord);
                            currentWord = "";
                        }
                        tokens.Add(" ");
                    }
                    else if (IsCjk(c))
                    {
                        if (currentWord != "")
                        {
                            tokens.Add(currentWord);
                            currentWord = "";
                        }
                        tokens.Add(c.ToString());
                    }
                    else
                    {
                        currentWord += c;
                    }
                }
                if (currentWord != "")
                {
                    tokens.Add(currentWord);
                }

                List<string> lines = new List<string>();
                string currentLine = "";

                foreach (var token in tokens)
                {
                    if (token == " ")
                    {
                        if (currentLine != "" && !currentLine.EndsWith(" "))
                        {
                            if (font.MeasureString(currentLine + " ").X <= maxWidth)
                            {
                                currentLine += " ";
                            }
                        }
                        continue;
                    }

                    string testLine = currentLine;
                    testLine += token;

                    if (font.MeasureString(testLine).X <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (currentLine != "")
                        {
                            lines.Add(currentLine.TrimEnd());
                            currentLine = "";
                        }

                        if (font.MeasureString(token).X <= maxWidth)
                        {
                            currentLine = token;
                        }
                        else
                        {
                            for (int j = 0; j < token.Length; j++)
                            {
                                char tc = token[j];
                                string nextTest = currentLine + tc;
                                if (font.MeasureString(nextTest).X <= maxWidth)
                                {
                                    currentLine = nextTest;
                                }
                                else
                                {
                                    if (currentLine != "")
                                    {
                                        lines.Add(currentLine.TrimEnd());
                                    }
                                    currentLine = tc.ToString();
                                }
                            }
                        }
                    }
                }

                if (currentLine != "")
                {
                    lines.Add(currentLine.TrimEnd());
                }

                if (lines.Count == 0)
                {
                    allLines.Add("");
                }
                else
                {
                    allLines.AddRange(lines);
                }
            }

            return allLines;
        }

        private string GetTextFromChatTextBox(ChatBox chatBoxInstance)
        {
            if (chatBoxInstance?.chatBox == null) return "";

            var list = chatBoxInstance.chatBox.finalText;
            if (list == null) return "";

            var sb = new System.Text.StringBuilder();
            foreach (var snippet in list)
            {
                if (snippet == null) continue;
                if (snippet.emojiIndex != -1)
                {
                    sb.Append($"[{snippet.emojiIndex}]");
                }
                else
                {
                    sb.Append(snippet.message);
                }
            }

            return sb.ToString();
        }

        private void ClearChatTextBox(ChatBox chatBoxInstance)
        {
            if (chatBoxInstance == null) return;

            chatBoxInstance.chatBox?.setText("");
            chatBoxInstance.chatBox?.finalText?.Clear();
            chatBoxInstance.chatBox?.updateWidth();
        }

        private string GetCleanName(string name)
        {
            if (string.Equals(name, Game1.player?.Name, StringComparison.OrdinalIgnoreCase))
                return Game1.player.Name;

            var npc = Game1.getCharacterFromName(name);
            if (npc != null)
                return npc.displayName;

            return name;
        }

        private List<string> GetFilteredTagCandidates()
        {
            var allNpcs = ModEntry.GetContactableNpcsList()
                .Where(npc => npc != null)
                .Select(npc => npc.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var onlinePlayers = Game1.getOnlineFarmers()
                .Select(f => f.Name)
                .Where(name => !string.Equals(name, Game1.player.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var allCandidates = allNpcs.Concat(onlinePlayers)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            allCandidates.Remove(Game1.player?.Name ?? "Player");

            string filter = this.tagSearchTextBox.Text;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                allCandidates = allCandidates
                    .Where(name =>
                    {
                        NPC? npc = Game1.getCharacterFromName(name);
                        string displayName = npc?.displayName ?? name;
                        return displayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
            }

            return allCandidates
                .OrderBy(name =>
                {
                    NPC? npc = Game1.getCharacterFromName(name);
                    return npc?.displayName ?? name;
                })
                .ToList();
        }

        private void DrawCustomTagTooltip(SpriteBatch b, string tagText, int mouseX, int mouseY)
        {
            if (string.IsNullOrWhiteSpace(tagText))
                return;

            int wrapWidth = GetPhoneScaledWrapWidth(ScaleUiValue(240), SocialHeaderMetaScale);
            string parsedText = Game1.parseText(tagText, Game1.smallFont, wrapWidth);

            string[] lines = parsedText.Split('\n');
            int lineHeight = GetPhoneScaledLineHeight(Game1.smallFont, SocialHeaderMetaScale, extraPadding: 2);
            int paddingX = ScaleUiValue(12);
            int paddingY = ScaleUiValue(10);

            int maxTextWidth = 0;
            foreach (string line in lines)
            {
                maxTextWidth = Math.Max(maxTextWidth, (int)Math.Ceiling(MeasurePhoneText(Game1.smallFont, line, SocialHeaderMetaScale).X));
            }

            int boxWidth = maxTextWidth + paddingX * 2;
            int boxHeight = paddingY * 2 + (lines.Length * lineHeight);

            int x = mouseX + ScaleUiValue(28);
            int y = mouseY + ScaleUiValue(15);
            int maxX = Game1.uiViewport.Width - boxWidth - ScaleUiValue(15);
            int maxY = Game1.uiViewport.Height - boxHeight - ScaleUiValue(15);
            x = Math.Clamp(x, ScaleUiValue(15), maxX);
            y = Math.Clamp(y, ScaleUiValue(15), maxY);

            UI.CardDrawing.DrawCard(
                b,
                x,
                y,
                boxWidth,
                boxHeight,
                Color.White,
                1f,
                false);

            for (int i = 0; i < lines.Length; i++)
            {
                DrawPhoneText(b, Game1.smallFont, lines[i], new Vector2(x + paddingX, y + paddingY + (i * lineHeight)), Color.Black, SocialHeaderMetaScale);
            }
        }

        private void DrawCustomLikeTooltip(SpriteBatch b, List<string> likerNames, int mouseX, int mouseY)
        {
            if (likerNames == null || likerNames.Count == 0)
                return;

            float textScale = 0.8f;
            int lineHeight = GetPhoneScaledLineHeight(Game1.smallFont, textScale, extraPadding: 2);
            int paddingX = ScaleUiValue(12);
            int paddingY = ScaleUiValue(10);

            int maxTextWidth = 0;
            foreach (string name in likerNames)
            {
                maxTextWidth = Math.Max(maxTextWidth, (int)Math.Ceiling(MeasurePhoneText(Game1.smallFont, name, textScale).X));
            }

            int boxWidth = Math.Max(ScaleUiValue(100), maxTextWidth + paddingX * 2);
            int boxHeight = paddingY * 2 + (likerNames.Count * lineHeight);

            int x = mouseX + ScaleUiValue(28);
            int y = mouseY + ScaleUiValue(15);
            int maxX = Game1.uiViewport.Width - boxWidth - ScaleUiValue(15);
            int maxY = Game1.uiViewport.Height - boxHeight - ScaleUiValue(15);
            x = Math.Clamp(x, ScaleUiValue(15), maxX);
            y = Math.Clamp(y, ScaleUiValue(15), maxY);

            UI.CardDrawing.DrawCard(
                b,
                x,
                y,
                boxWidth,
                boxHeight,
                Color.White,
                1f,
                false);

            for (int i = 0; i < likerNames.Count; i++)
            {
                DrawPhoneText(b, Game1.smallFont, likerNames[i], new Vector2(x + paddingX, y + paddingY + (i * lineHeight)), Game1.textColor, textScale);
            }
        }

        private void DrawHoverTextCard(SpriteBatch b, string text, int mouseX, int mouseY)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            int wrapWidth = GetPhoneScaledWrapWidth(ScaleUiValue(240), SocialHeaderMetaScale);
            string parsedText = Game1.parseText(text, Game1.smallFont, wrapWidth);

            string[] lines = parsedText.Split('\n');
            int lineHeight = GetPhoneScaledLineHeight(Game1.smallFont, SocialHeaderMetaScale, extraPadding: 2);
            int paddingX = ScaleUiValue(12);
            int paddingY = ScaleUiValue(10);

            int maxTextWidth = 0;
            foreach (string line in lines)
            {
                maxTextWidth = Math.Max(maxTextWidth, (int)Math.Ceiling(MeasurePhoneText(Game1.smallFont, line, SocialHeaderMetaScale).X));
            }

            int boxWidth = maxTextWidth + paddingX * 2;
            int boxHeight = paddingY * 2 + (lines.Length * lineHeight);

            int x = mouseX + ScaleUiValue(28);
            int y = mouseY + ScaleUiValue(15);
            int maxX = Game1.uiViewport.Width - boxWidth - ScaleUiValue(15);
            int maxY = Game1.uiViewport.Height - boxHeight - ScaleUiValue(15);
            x = Math.Clamp(x, ScaleUiValue(15), maxX);
            y = Math.Clamp(y, ScaleUiValue(15), maxY);

            UI.CardDrawing.DrawCard(
                b,
                x,
                y,
                boxWidth,
                boxHeight,
                Color.White,
                1f,
                false);

            for (int i = 0; i < lines.Length; i++)
            {
                DrawPhoneText(b, Game1.smallFont, lines[i], new Vector2(x + paddingX, y + paddingY + (i * lineHeight)), Color.Black, SocialHeaderMetaScale);
            }
        }
        private int GetAdaptivePhotoHeight(StardewConnectPost post, int maxPhotoW)
        {
            if (post.Photo == null || post.Photo.Count == 0)
                return 0;

            int maxPhotoH = ScaleUiValue(360);
            int highestPhotoHeight = 0;

            foreach (var photo in post.Photo)
            {
                var tex = GetPostPhotoTexture(post, photo);
                if (tex != null && !tex.IsDisposed)
                {
                    float scale = Math.Min((float)maxPhotoW / tex.Width, (float)maxPhotoH / tex.Height);
                    int drawH = (int)(tex.Height * scale);
                    if (drawH > highestPhotoHeight)
                    {
                        highestPhotoHeight = drawH;
                    }
                }
            }

            if (highestPhotoHeight == 0)
            {
                return maxPhotoH;
            }

            return highestPhotoHeight;
        }

        private static readonly Dictionary<string, Texture2D> avatarImageCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> avatarFailedImagePaths = new(StringComparer.OrdinalIgnoreCase);

        public static void ClearAvatarCache()
        {
            foreach (var texture in avatarImageCache.Values)
            {
                if (texture != null && !texture.IsDisposed)
                {
                    try { texture.Dispose(); } catch { }
                }
            }
            avatarImageCache.Clear();
            avatarFailedImagePaths.Clear();
        }

        public static bool TryGetAvatarTexture(string imagePath, out Texture2D texture)
        {
            texture = null!;
            if (string.IsNullOrWhiteSpace(imagePath))
                return false;

            if (avatarImageCache.TryGetValue(imagePath, out Texture2D? cachedTexture) && cachedTexture != null)
            {
                if (!cachedTexture.IsDisposed)
                {
                    texture = cachedTexture;
                    return true;
                }
                avatarImageCache.Remove(imagePath);
            }

            if (avatarFailedImagePaths.Contains(imagePath))
                return false;

            try
            {
                if (File.Exists(imagePath))
                {
                    using FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Texture2D loadedTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                    avatarImageCache[imagePath] = loadedTexture;
                    texture = loadedTexture;
                    return true;
                }
            }
            catch (Exception)
            {
                // ignore
            }

            avatarFailedImagePaths.Add(imagePath);
            return false;
        }
    }
}
