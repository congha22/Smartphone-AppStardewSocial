using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace SmartphoneAppStardewSocial
{
    public static class SocialWidget
    {
        private static Texture2D? widgetTexture2x1;
        private static Texture2D? widgetTexture2x2;
        private static string? lastIconStyle;

        public static void Draw(SpriteBatch b, Rectangle rect, AppSize size, Texture2D appIcon, Texture2D? appBackgroundTexture, ISmartPhoneApi api, string compositeId)
        {
            // Default 1x1 size: draw normal icon
            if (size == AppSize.Size1x1 || appIcon == null)
            {
                Texture2D? currentIcon = null;
                if (api != null)
                {
                    try
                    {
                        currentIcon = api.GetAppIconTexture(compositeId);
                    }
                    catch { }
                }
                if (currentIcon == null)
                {
                    currentIcon = appIcon;
                }

                if (currentIcon != null)
                {
                    b.Draw(currentIcon, rect, Color.White);
                }
                return;
            }

            // Dynamically check and update theme style based on active system theme selection
            string currentStyle = "default";
            if (api != null)
            {
                try
                {
                    Texture2D? currentIcon = api.GetAppIconTexture(compositeId);
                    if (currentIcon != null && currentIcon.Name != null && currentIcon.Name.Contains("v2", StringComparison.OrdinalIgnoreCase))
                    {
                        currentStyle = "v2";
                    }
                }
                catch { }
            }

            if (lastIconStyle != currentStyle)
            {
                lastIconStyle = currentStyle;
                try { widgetTexture2x1?.Dispose(); widgetTexture2x1 = null; } catch { }
                try { widgetTexture2x2?.Dispose(); widgetTexture2x2 = null; } catch { }

                try
                {
                    string path2x1 = $"assets/{currentStyle}/2x1.png";
                    widgetTexture2x1 = ModEntry.SHelper.ModContent.Load<Texture2D>(path2x1);
                }
                catch { }

                try
                {
                    string path2x2 = $"assets/{currentStyle}/2x2.png";
                    widgetTexture2x2 = ModEntry.SHelper.ModContent.Load<Texture2D>(path2x2);
                }
                catch { }
            }

            // Determine appropriate background texture to draw based on widget size
            Texture2D? bgTex = null;
            if (size == AppSize.Size2x1 && widgetTexture2x1 != null && !widgetTexture2x1.IsDisposed)
                bgTex = widgetTexture2x1;
            else if (size == AppSize.Size2x2 && widgetTexture2x2 != null && !widgetTexture2x2.IsDisposed)
                bgTex = widgetTexture2x2;
            else if (appBackgroundTexture != null && !appBackgroundTexture.IsDisposed)
                bgTex = appBackgroundTexture;

            if (bgTex != null)
            {
                b.Draw(bgTex, rect, Color.White);
            }
            else
            {
                b.Draw(Game1.staminaRect, rect, new Color(40, 50, 80));
            }

            // Get the latest post from Stardew Social manager
            var posts = StardewConnectManager.GetPostsSnapshot();
            var latestPost = posts.LastOrDefault();

            float scale = 1f;
            if (api != null)
            {
                try
                {
                    scale = api.GetPhoneUiScale();
                }
                catch { }
            }

            if (latestPost == null)
            {
                string text = ModEntry.SHelper.Translation.Get("widget.noPosts");
                Vector2 textSize = MeasurePhoneText(Game1.smallFont, text, 0.9f, scale);
                DrawPhoneText(b, Game1.smallFont, text, new Vector2(rect.X + (rect.Width - textSize.X) / 2f, rect.Y + (rect.Height - textSize.Y) / 2f), Color.Black * 0.8f, 0.9f, scale);
                return;
            }

            // Draw widget content depending on size
            if (size == AppSize.Size2x1)
            {
                int iconPaddingX = ScaleUiValue(14, scale);
                int actorIconSize = ScaleUiValue(40, scale); // Increased avatar dimensions for better visibility
                Rectangle actorBounds = new Rectangle(rect.X + iconPaddingX, rect.Y + (rect.Height - actorIconSize) / 2, actorIconSize, actorIconSize);
                DrawWidgetActorIcon(b, latestPost.AuthorName, latestPost.AuthorIsPlayer, actorBounds);

                string cleanName = latestPost.AuthorIsPlayer ? latestPost.AuthorName : (Game1.getCharacterFromName(latestPost.AuthorName)?.displayName ?? latestPost.AuthorName);

                // Truncate name slightly if it overflows widget boundary limits
                if (cleanName.Length > 14) cleanName = cleanName.Substring(0, 12) + "...";

                float nameScale = 0.95f; // Increased font size
                Vector2 nameSize = MeasurePhoneText(Game1.smallFont, cleanName, nameScale, scale);
                int textGap = ScaleUiValue(4, scale);
                int statHeight = ScaleUiValue(14, scale);
                int textStackHeight = (int)nameSize.Y + textGap + statHeight;
                int startTextY = rect.Y + (rect.Height - textStackHeight) / 2;

                // Author name on the right
                DrawPhoneText(b, Game1.smallFont, cleanName, new Vector2(actorBounds.Right + ScaleUiValue(12, scale), startTextY), Color.Black, nameScale, scale);

                // Like and comment metrics underneath the name
                int statY = startTextY + (int)nameSize.Y + textGap;
                string likeStr = latestPost.LikedBy.Count.ToString();
                string commentStr = latestPost.Comments.Count.ToString();

                float statScale = 0.85f; // Scaled up metrics text size
                float likeTextWidth = MeasurePhoneText(Game1.smallFont, likeStr, statScale, scale).X;

                // Larger metric icons (12x12)
                Rectangle heartRect = new Rectangle(actorBounds.Right + ScaleUiValue(12, scale), statY + ScaleUiValue(2, scale), ScaleUiValue(18, scale), ScaleUiValue(18, scale));
                b.Draw(Game1.mouseCursors, heartRect, new Rectangle(211, 428, 7, 7), Color.White);
                DrawPhoneText(b, Game1.smallFont, likeStr, new Vector2(heartRect.Right + ScaleUiValue(5, scale), statY), Color.Black, statScale, scale);

                Rectangle commentRect = new Rectangle((int)(heartRect.Right + ScaleUiValue(5f, scale) + likeTextWidth + ScaleUiValue(16f, scale)), statY + ScaleUiValue(2, scale), ScaleUiValue(20, scale), ScaleUiValue(18, scale));
                b.Draw(Game1.mouseCursors, commentRect, new Rectangle(139, 465, 24, 24), Color.White);
                DrawPhoneText(b, Game1.smallFont, commentStr, new Vector2(commentRect.Right + ScaleUiValue(4, scale), statY), Color.Black, 0.78f, scale);
            }
            else
            {
                // For Size 2x2 layout
                int actorIconSize = ScaleUiValue(32, scale);
                string cleanName = latestPost.AuthorIsPlayer ? latestPost.AuthorName : (Game1.getCharacterFromName(latestPost.AuthorName)?.displayName ?? latestPost.AuthorName);

                string snippet = latestPost.Text ?? "";
                float snippetScale = 0.8f;
                float snippetPhoneScale = GetPhoneTextScale(snippetScale, scale);
                int wrapWidth = (int)((rect.Width - ScaleUiValue(32, scale)) / snippetPhoneScale); // Expanded text width scaling
                List<string> wrappedLines = SplitTextIntoLines(snippet, Game1.smallFont, wrapWidth);
                if (wrappedLines.Count > 5)
                {
                    wrappedLines = wrappedLines.Take(5).ToList(); // Cap strictly at 5 lines
                }

                bool hasText = wrappedLines.Count > 0 && !string.IsNullOrWhiteSpace(snippet);

                string likeStr = latestPost.LikedBy.Count.ToString();
                string commentStr = latestPost.Comments.Count.ToString();

                float statScale = 0.9f; // Increased layout statistics presentation text scale
                float likeTextWidth = MeasurePhoneText(Game1.smallFont, likeStr, statScale, scale).X;
                float commentTextWidth = MeasurePhoneText(Game1.smallFont, commentStr, statScale, scale).X;

                // Adaptive layout spacing variables matching increased font scaling footprint
                float totalStatsWidth = ScaleUiValue(18f, scale) + ScaleUiValue(5f, scale) + likeTextWidth + ScaleUiValue(24f, scale) + ScaleUiValue(20f, scale) + ScaleUiValue(5f, scale) + commentTextWidth;
                int startStatsX = rect.X + (int)((rect.Width - totalStatsWidth) / 2f);

                if (hasText)
                {
                    // Fixed top anchoring positions when post contains string content records
                    Rectangle actorBounds = new Rectangle(rect.X + ScaleUiValue(16, scale), rect.Y + ScaleUiValue(14, scale), actorIconSize, actorIconSize);
                    DrawWidgetActorIcon(b, latestPost.AuthorName, latestPost.AuthorIsPlayer, actorBounds);
                    DrawPhoneText(b, Game1.smallFont, cleanName, new Vector2(actorBounds.Right + ScaleUiValue(8, scale), rect.Y + ScaleUiValue(14 + 4, scale)), Color.Black, 0.95f, scale);

                    int yCursor = actorBounds.Bottom + ScaleUiValue(6, scale);
                    int lineGap = ScaleUiValue(23, scale); // Increased line spacing loop index for comfortable reading
                    for (int i = 0; i < wrappedLines.Count; i++)
                    {
                        DrawPhoneText(b, Game1.smallFont, wrappedLines[i], new Vector2(rect.X + ScaleUiValue(16, scale), yCursor), Color.DimGray, snippetScale, scale);
                        yCursor += lineGap;
                    }

                    int statY = rect.Bottom - ScaleUiValue(30, scale);
                    Rectangle heartRect = new Rectangle(startStatsX, statY + ScaleUiValue(2, scale), ScaleUiValue(18, scale), ScaleUiValue(18, scale));
                    b.Draw(Game1.mouseCursors, heartRect, new Rectangle(211, 428, 7, 7), Color.White);
                    DrawPhoneText(b, Game1.smallFont, likeStr, new Vector2(heartRect.Right + ScaleUiValue(5, scale), statY), Color.Black, statScale, scale);

                    Rectangle commentRect = new Rectangle((int)(heartRect.Right + ScaleUiValue(5f, scale) + likeTextWidth + ScaleUiValue(24f, scale)), statY + ScaleUiValue(2, scale), ScaleUiValue(20, scale), ScaleUiValue(18, scale));
                    b.Draw(Game1.mouseCursors, commentRect, new Rectangle(139, 465, 24, 24), Color.White);
                    DrawPhoneText(b, Game1.smallFont, commentStr, new Vector2(commentRect.Right + ScaleUiValue(5, scale), statY), Color.Black, statScale, scale);
                }
                else
                {
                    // Perfectly auto-center header and base footer inside the widget workspace if text is missing
                    int totalContentHeight = actorIconSize + ScaleUiValue(28, scale) + ScaleUiValue(14, scale);
                    int startContentY = rect.Y + (rect.Height - totalContentHeight) / 2;

                    Rectangle actorBounds = new Rectangle(rect.X + ScaleUiValue(16, scale), startContentY, actorIconSize, actorIconSize);
                    DrawWidgetActorIcon(b, latestPost.AuthorName, latestPost.AuthorIsPlayer, actorBounds);
                    DrawPhoneText(b, Game1.smallFont, cleanName, new Vector2(actorBounds.Right + ScaleUiValue(8, scale), startContentY + ScaleUiValue(4, scale)), Color.Black, 0.95f, scale);

                    int statY = startContentY + actorIconSize + ScaleUiValue(28, scale);
                    Rectangle heartRect = new Rectangle(startStatsX, statY + ScaleUiValue(2, scale), ScaleUiValue(18, scale), ScaleUiValue(18, scale));
                    b.Draw(Game1.mouseCursors, heartRect, new Rectangle(211, 428, 7, 7), Color.White);
                    DrawPhoneText(b, Game1.smallFont, likeStr, new Vector2(heartRect.Right + ScaleUiValue(5, scale), statY), Color.Black, statScale, scale);

                    Rectangle commentRect = new Rectangle((int)(heartRect.Right + ScaleUiValue(5f, scale) + likeTextWidth + ScaleUiValue(24f, scale)), statY + ScaleUiValue(2, scale), ScaleUiValue(20, scale), ScaleUiValue(18, scale));
                    b.Draw(Game1.mouseCursors, commentRect, new Rectangle(139, 465, 24, 24), Color.White);
                    DrawPhoneText(b, Game1.smallFont, commentStr, new Vector2(commentRect.Right + ScaleUiValue(5, scale), statY), Color.Black, statScale, scale);
                }
            }
        }

        private static void DrawWidgetActorIcon(SpriteBatch b, string actorName, bool actorIsPlayer, Rectangle bounds)
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

                    if (File.Exists(avatarPath) && StardewSocialScreen.TryGetAvatarTexture(avatarPath, out Texture2D avatarTexture))
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

            Vector2 letterSize = Game1.smallFont.MeasureString(fallbackLetter) * 0.75f;
            Vector2 letterPos = new Vector2(
                bounds.X + (bounds.Width - letterSize.X) / 2f,
                bounds.Y + (bounds.Height - letterSize.Y) / 2f);
            b.DrawString(Game1.smallFont, fallbackLetter, letterPos, Color.White, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
        }

        private static List<string> SplitTextIntoLines(string text, SpriteFont font, int maxWidth)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            string[] paragraphs = text.Split('\n');
            foreach (var paragraph in paragraphs)
            {
                string[] words = paragraph.Split(' ');
                string currentLine = "";

                foreach (var word in words)
                {
                    string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                    float testWidth = font.MeasureString(testLine).X;

                    if (testWidth <= maxWidth)
                    {
                        currentLine = testLine;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(currentLine))
                            lines.Add(currentLine);
                        currentLine = word;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine) || words.Length == 0)
                    lines.Add(currentLine);
            }

            return lines;
        }
        private static int ScaleUiValue(int baseValue, float scale)
        {
            return (int)Math.Round(baseValue * scale);
        }

        private static float ScaleUiValue(float baseValue, float scale)
        {
            return baseValue * scale;
        }

        private static float GetPhoneTextScale(float localScale, float scale)
        {
            float globalScale = scale < 0.999f ? 0.85f : 1f;
            return Math.Max(0.01f, localScale * globalScale);
        }

        private static Vector2 MeasurePhoneText(SpriteFont font, string text, float localScale, float scale)
        {
            return font.MeasureString(text ?? string.Empty) * GetPhoneTextScale(localScale, scale);
        }

        private static void DrawPhoneText(SpriteBatch b, SpriteFont font, string text, Vector2 position, Color color, float localScale, float scale)
        {
            b.DrawString(
                font,
                text ?? string.Empty,
                position,
                color,
                0f,
                Vector2.Zero,
                GetPhoneTextScale(localScale, scale),
                SpriteEffects.None,
                1f);
        }
    }
}