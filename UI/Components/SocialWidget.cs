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

            // Draw widget background (Stardew Social style)
            if (appBackgroundTexture != null && !appBackgroundTexture.IsDisposed)
            {
                b.Draw(appBackgroundTexture, rect, Color.White);
            }
            else
            {
                // Fallback elegant dark blue/slate color
                b.Draw(Game1.staminaRect, rect, new Color(40, 50, 80));
            }

            // Draw a subtle border frame to match the phone theme
            UI.CardDrawing.DrawCard(
                b,
                rect.X, rect.Y, rect.Width, rect.Height,
                Color.White * 0.5f, 0.5f, false);

            // Get the latest post from Stardew Social manager
            var posts = StardewConnectManager.GetPostsSnapshot();
            var latestPost = posts.LastOrDefault();

            if (latestPost == null)
            {
                // Draw no posts text beautifully centered
                string text = "No posts yet.";
                Vector2 textSize = Game1.smallFont.MeasureString(text) * 0.75f;
                b.DrawString(Game1.smallFont, text, new Vector2(rect.X + (rect.Width - textSize.X) / 2f, rect.Y + (rect.Height - textSize.Y) / 2f), Color.White * 0.8f, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
                return;
            }

            // Draw widget content depending on size
            if (size == AppSize.Size2x1)
            {
                int iconPadding = 6;
                int actorIconSize = 28;
                Rectangle actorBounds = new Rectangle(rect.X + iconPadding, rect.Y + (rect.Height - actorIconSize) / 2, actorIconSize, actorIconSize);
                DrawWidgetActorIcon(b, latestPost.AuthorName, latestPost.AuthorIsPlayer, actorBounds);

                // Author name
                string cleanName = latestPost.AuthorIsPlayer ? latestPost.AuthorName : (Game1.getCharacterFromName(latestPost.AuthorName)?.displayName ?? latestPost.AuthorName);
                Vector2 nameSize = Game1.smallFont.MeasureString(cleanName) * 0.7f;
                b.DrawString(Game1.smallFont, cleanName, new Vector2(actorBounds.Right + 6, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 1f);

                // Draw snippet of post text
                string snippet = latestPost.Text ?? "";
                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    if (snippet.Length > 20) snippet = snippet.Substring(0, 18) + "...";
                    b.DrawString(Game1.smallFont, snippet, new Vector2(actorBounds.Right + 6, rect.Y + 4 + nameSize.Y + 2), Color.LightGray * 0.9f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);
                }
            }
            else
            {
                // For 2x2, 3x2, or larger sizes
                int pad = 8;
                int actorIconSize = 32;
                Rectangle actorBounds = new Rectangle(rect.X + pad, rect.Y + pad, actorIconSize, actorIconSize);
                DrawWidgetActorIcon(b, latestPost.AuthorName, latestPost.AuthorIsPlayer, actorBounds);

                // Author name
                string cleanName = latestPost.AuthorIsPlayer ? latestPost.AuthorName : (Game1.getCharacterFromName(latestPost.AuthorName)?.displayName ?? latestPost.AuthorName);
                b.DrawString(Game1.smallFont, cleanName, new Vector2(actorBounds.Right + 6, rect.Y + pad + 2), Color.White, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);

                // Draw post text snippet wrapped
                string snippet = latestPost.Text ?? "";
                int wrapWidth = (int)((rect.Width - pad * 2) / 0.65f);
                List<string> wrappedLines = SplitTextIntoLines(snippet, Game1.smallFont, wrapWidth);
                int yCursor = actorBounds.Bottom + 6;
                int maxLines = (rect.Height - yCursor - 20) / 14;
                for (int i = 0; i < wrappedLines.Count && i < maxLines; i++)
                {
                    b.DrawString(Game1.smallFont, wrappedLines[i], new Vector2(rect.X + pad, yCursor), Color.LightGray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);
                    yCursor += 14;
                }

                // Draw stats at bottom (likes and comments)
                int statY = rect.Bottom - 18;
                // Like icon
                Rectangle heartRect = new Rectangle(rect.X + pad, statY + 2, 10, 10);
                b.Draw(Game1.mouseCursors, heartRect, new Rectangle(211, 428, 7, 7), Color.White);
                b.DrawString(Game1.smallFont, latestPost.LikedBy.Count.ToString(), new Vector2(heartRect.Right + 4, statY), Color.White * 0.8f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);

                // Comment icon
                Rectangle commentRect = new Rectangle(rect.X + pad + 40, statY + 2, 10, 10);
                b.Draw(Game1.mouseCursors, commentRect, new Rectangle(139, 465, 24, 24), Color.White);
                b.DrawString(Game1.smallFont, latestPost.Comments.Count.ToString(), new Vector2(commentRect.Right + 4, statY), Color.White * 0.8f, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);
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
    }
}
