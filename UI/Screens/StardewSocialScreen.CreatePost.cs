using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace SmartphoneAppStardewSocial
{
    public partial class StardewSocialScreen
    {
        private void DrawSocialCreatePostMenu(SpriteBatch b, Rectangle clipRect)
        {
            int cardX = clipRect.X + ScaleUiValue(15);
            int cursorY = clipRect.Y + ScaleUiValue(15);

            // 1. Textbox for writing the post
            Rectangle inputBounds = new Rectangle(cardX, cursorY, clipRect.Width - ScaleUiValue(30), ScaleUiValue(180));
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                inputBounds.X, inputBounds.Y, inputBounds.Width, inputBounds.Height,
                Color.White, 1f, false);

            this.postTextBox.Draw(b, inputBounds, this.phoneUiScale, this.Selected);

            cursorY += inputBounds.Height + ScaleUiValue(15);

            // 2. Square Photo selector button with Photo app icon (right aligned)
            int btnSize = ScaleUiValue(45);
            int emojiX = clipRect.Right - ScaleUiValue(15) - btnSize;
            int photoX = emojiX - ScaleUiValue(15) - btnSize;

            // Draw currently tagged people left-aligned next to the action buttons
            if (this.draftTagged.Count > 0)
            {
                string draftTagText = "with ";
                if (this.draftTagged.Count == 1)
                {
                    draftTagText += GetCleanName(this.draftTagged[0]);
                }
                else if (this.draftTagged.Count == 2)
                {
                    draftTagText += GetCleanName(this.draftTagged[0]) + ", " + GetCleanName(this.draftTagged[1]);
                }
                else
                {
                    draftTagText += GetCleanName(this.draftTagged[0]) + ", " + GetCleanName(this.draftTagged[1]) + " and others";
                }

                // Truncate if it exceeds available horizontal space
                int maxTextWidth = photoX - cardX - ScaleUiValue(15);
                string displayTagText = draftTagText;
                if (MeasurePhoneText(Game1.smallFont, displayTagText).X > maxTextWidth)
                {
                    while (displayTagText.Length > 3 && MeasurePhoneText(Game1.smallFont, displayTagText + "...").X > maxTextWidth)
                    {
                        displayTagText = displayTagText.Substring(0, displayTagText.Length - 1);
                    }
                    displayTagText += "...";
                }

                Vector2 tagTextSize = MeasurePhoneText(Game1.smallFont, displayTagText);
                DrawPhoneText(b, Game1.smallFont, displayTagText, new Vector2(cardX, cursorY + (btnSize - tagTextSize.Y) / 2f), Color.DarkSlateGray);
            }

            this.socialCreatePhotoSelectBounds = new Rectangle(photoX, cursorY, btnSize, btnSize);
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                this.socialCreatePhotoSelectBounds.X, this.socialCreatePhotoSelectBounds.Y,
                this.socialCreatePhotoSelectBounds.Width, this.socialCreatePhotoSelectBounds.Height,
                Color.White, 1f, false);

            Texture2D? photoIcon = this.smartphoneApi.GetAppTexture(AppIconType.Photo);
            if (photoIcon != null)
            {
                b.Draw(photoIcon, new Rectangle(this.socialCreatePhotoSelectBounds.X + ScaleUiValue(6), this.socialCreatePhotoSelectBounds.Y + ScaleUiValue(6), ScaleUiValue(33), ScaleUiValue(33)), Color.White);
            }

            // Draw photo count badge if photos are selected
            if (this.draftSelectedPhotos.Count > 0)
            {
                DrawSocialUnreadBadge(b, this.socialCreatePhotoSelectBounds.Right + ScaleUiValue(4), this.socialCreatePhotoSelectBounds.Y - ScaleUiValue(4), this.draftSelectedPhotos.Count);
            }

            // 3. Square Emoji button next to the Add Photos button (right aligned)
            this.socialCreateEmojiButtonBounds = new Rectangle(emojiX, cursorY, btnSize, btnSize);
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                this.socialCreateEmojiButtonBounds.X, this.socialCreateEmojiButtonBounds.Y,
                this.socialCreateEmojiButtonBounds.Width, this.socialCreateEmojiButtonBounds.Height,
                Color.White, 1f, false);

            var emojiIconTexture = Game1.chatBox?.emojiMenuIcon?.texture ?? Game1.mouseCursors;
            var emojiIconSource = Game1.chatBox?.emojiMenuIcon?.sourceRect ?? new Rectangle(0, 0, 64, 64);
            b.Draw(emojiIconTexture, new Rectangle(this.socialCreateEmojiButtonBounds.X + ScaleUiValue(8), this.socialCreateEmojiButtonBounds.Y + ScaleUiValue(8), ScaleUiValue(29), ScaleUiValue(29)), emojiIconSource, Color.White);

            cursorY += btnSize + ScaleUiValue(15);

            // 4. Draft photo preview panel with left/right navigation arrows
            if (this.draftSelectedTextures.Count > 0)
            {
                this.draftPhotoPreviewIndex = Math.Clamp(this.draftPhotoPreviewIndex, 0, this.draftSelectedTextures.Count - 1);
                Texture2D currentTexture = this.draftSelectedTextures[this.draftPhotoPreviewIndex];

                int previewPanelWidth = clipRect.Width - ScaleUiValue(30);
                int previewPanelHeight = ScaleUiValue(410);
                Rectangle previewRect = new Rectangle(cardX, cursorY, previewPanelWidth, previewPanelHeight);

                // Draw background panel
                IClickableMenu.drawTextureBox(
                    b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    previewRect.X, previewRect.Y,
                    previewRect.Width, previewRect.Height,
                    new Color(255, 255, 255, 220), 1f, false);

                // Scale image down inside the panel with padding
                float scale = Math.Min(
                    (previewRect.Width - ScaleUiValue(20)) / (float)Math.Max(1, currentTexture.Width),
                    (previewRect.Height - ScaleUiValue(20)) / (float)Math.Max(1, currentTexture.Height));
                scale = Math.Clamp(scale, 0.1f, 1f);

                int drawWidth = (int)Math.Round(currentTexture.Width * scale);
                int drawHeight = (int)Math.Round(currentTexture.Height * scale);
                Rectangle drawRect = new Rectangle(
                    previewRect.X + (previewRect.Width - drawWidth) / 2,
                    previewRect.Y + (previewRect.Height - drawHeight) / 2,
                    drawWidth,
                    drawHeight);

                b.Draw(currentTexture, drawRect, Color.White);

                // Draw navigation buttons if multiple photos exist
                if (this.draftSelectedTextures.Count > 1)
                {
                    this.socialCreatePhotoPrevBounds = new Rectangle(
                        previewRect.X + ScaleUiValue(8),
                        previewRect.Y + previewRect.Height / 2 - ScaleUiValue(20),
                        ScaleUiValue(40),
                        ScaleUiValue(40));

                    this.socialCreatePhotoNextBounds = new Rectangle(
                        previewRect.Right - ScaleUiValue(48),
                        previewRect.Y + previewRect.Height / 2 - ScaleUiValue(20),
                        ScaleUiValue(40),
                        ScaleUiValue(40));

                    // Left arrow (tile 44)
                    b.Draw(Game1.mouseCursors, this.socialCreatePhotoPrevBounds, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44), Color.White);
                    // Right arrow (tile 33)
                    b.Draw(Game1.mouseCursors, this.socialCreatePhotoNextBounds, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33), Color.White);
                }
                else
                {
                    this.socialCreatePhotoPrevBounds = Rectangle.Empty;
                    this.socialCreatePhotoNextBounds = Rectangle.Empty;
                }

                cursorY += previewRect.Height + ScaleUiValue(15);
            }
            else
            {
                this.socialCreatePhotoPrevBounds = Rectangle.Empty;
                this.socialCreatePhotoNextBounds = Rectangle.Empty;
            }

            // 5. Cancel and Publish buttons at the bottom of viewport
            int bottomBtnsY = clipRect.Bottom - ScaleUiValue(65);
            this.socialCreateCancelBounds = new Rectangle(cardX, bottomBtnsY, ScaleUiValue(120), ScaleUiValue(50));
            this.socialCreateSubmitBounds = new Rectangle(clipRect.Right - ScaleUiValue(135), bottomBtnsY, ScaleUiValue(120), ScaleUiValue(50));

            // Draw Cancel
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                this.socialCreateCancelBounds.X, this.socialCreateCancelBounds.Y,
                this.socialCreateCancelBounds.Width, this.socialCreateCancelBounds.Height,
                Color.White, 1f, false);
            Vector2 cancelSize = MeasurePhoneText(Game1.smallFont, "Cancel");
            Vector2 cancelPos = new Vector2(
                this.socialCreateCancelBounds.X + (this.socialCreateCancelBounds.Width - cancelSize.X) / 2f,
                this.socialCreateCancelBounds.Y + (this.socialCreateCancelBounds.Height - cancelSize.Y) / 2f
            );
            DrawPhoneText(b, Game1.smallFont, "Cancel", cancelPos, Color.Black);

            // Draw Publish
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                this.socialCreateSubmitBounds.X, this.socialCreateSubmitBounds.Y,
                this.socialCreateSubmitBounds.Width, this.socialCreateSubmitBounds.Height,
                Color.White, 1f, false);
            Vector2 publishSize = MeasurePhoneText(Game1.smallFont, "Publish");
            Vector2 publishPos = new Vector2(
                this.socialCreateSubmitBounds.X + (this.socialCreateSubmitBounds.Width - publishSize.X) / 2f,
                this.socialCreateSubmitBounds.Y + (this.socialCreateSubmitBounds.Height - publishSize.Y) / 2f
            );
            DrawPhoneText(b, Game1.smallFont, "Publish", publishPos, Color.Black);
        }

        private void DrawSocialTagMenu(SpriteBatch b, Rectangle clipRect)
        {
            // Translucent black backdrop over the Create Post menu
            b.Draw(Game1.staminaRect, clipRect, Color.Black * 0.75f);

            int margin = ScaleUiValue(15);
            Rectangle popupRect = new Rectangle(
                clipRect.X + margin,
                clipRect.Y + margin,
                clipRect.Width - (margin * 2),
                clipRect.Height - (margin * 2));

            // Tagging Menu card
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                popupRect.X, popupRect.Y, popupRect.Width, popupRect.Height,
                Color.White, 1f, false);

            // Title
            string titleText = "Tag People";
            Vector2 titleSize = MeasurePhoneText(Game1.smallFont, titleText, 1.1f);
            DrawPhoneText(b, Game1.smallFont, titleText, new Vector2(popupRect.X + (popupRect.Width - titleSize.X) / 2f, popupRect.Y + ScaleUiValue(15)), Color.Black, 1.1f);

            // Search Filter Box
            Rectangle searchBounds = new Rectangle(popupRect.X + ScaleUiValue(15), popupRect.Y + ScaleUiValue(50), popupRect.Width - ScaleUiValue(30), ScaleUiValue(45));
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                searchBounds.X, searchBounds.Y, searchBounds.Width, searchBounds.Height,
                Color.White, 1f, false);
            this.tagSearchTextBox.Draw(b, searchBounds, this.phoneUiScale, this.Selected);

            // Done button at bottom
            Rectangle doneBounds = new Rectangle(popupRect.X + ScaleUiValue(15), popupRect.Bottom - ScaleUiValue(60), popupRect.Width - ScaleUiValue(30), ScaleUiValue(45));
            this.socialTagMenuDoneBounds = doneBounds;
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                doneBounds.X, doneBounds.Y, doneBounds.Width, doneBounds.Height,
                Color.White, 1f, false);
            Vector2 doneSize = MeasurePhoneText(Game1.smallFont, "Done");
            DrawPhoneText(b, Game1.smallFont, "Done", new Vector2(doneBounds.X + (doneBounds.Width - doneSize.X) / 2f, doneBounds.Y + ScaleUiValue(12)), Color.Black);

            // Scrollable list area
            int listY = searchBounds.Bottom + ScaleUiValue(15);
            int listHeight = doneBounds.Y - listY - ScaleUiValue(15);
            Rectangle listClipRect = new Rectangle(popupRect.X, listY, popupRect.Width, listHeight);

            // Get filtered tag candidates
            var candidates = GetFilteredTagCandidates();

            int rowHeight = ScaleUiValue(55);
            int totalHeight = candidates.Count * rowHeight;
            this.maxScrollTagMenu = Math.Max(0, totalHeight - listHeight);
            this.tagMenuScrollOffset = Math.Clamp(this.tagMenuScrollOffset, 0, this.maxScrollTagMenu);

            this.socialTagItemBounds.Clear();

            // Render list with viewport clipping
            RasterizerState scissorState = new RasterizerState { ScissorTestEnable = true };
            RasterizerState originalState = b.GraphicsDevice.RasterizerState;
            Rectangle originalScissor = b.GraphicsDevice.ScissorRectangle;

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, scissorState);
            b.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(originalScissor, listClipRect);

            int currentY = listClipRect.Y - (int)this.tagMenuScrollOffset;
            foreach (var name in candidates)
            {
                Rectangle rowBounds = new Rectangle(popupRect.X + ScaleUiValue(15), currentY, popupRect.Width - ScaleUiValue(30), rowHeight);
                this.socialTagItemBounds[name] = rowBounds;

                // Separator line
                b.Draw(Game1.staminaRect, new Rectangle(rowBounds.X, rowBounds.Bottom - 1, rowBounds.Width, 1), Color.LightGray * 0.5f);

                // Avatar Icon
                Rectangle avatarBounds = new Rectangle(rowBounds.X + ScaleUiValue(5), rowBounds.Y + ScaleUiValue(5), ScaleUiValue(45), ScaleUiValue(45));
                bool isPlayer = Game1.getOnlineFarmers().Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                DrawSocialActorIcon(b, name, isPlayer, avatarBounds);

                // Display Name
                NPC? npc = Game1.getCharacterFromName(name);
                string displayName = npc?.displayName ?? name;
                DrawPhoneText(b, Game1.smallFont, displayName, new Vector2(avatarBounds.Right + ScaleUiValue(10), rowBounds.Y + ScaleUiValue(15)), Color.Black);

                // Tag checkmark checkbox
                bool isTagged = this.draftTagged.Contains(name);
                Rectangle checkRect = new Rectangle(rowBounds.Right - ScaleUiValue(45), rowBounds.Y + ScaleUiValue(10), ScaleUiValue(35), ScaleUiValue(35));
                
                IClickableMenu.drawTextureBox(
                    b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    checkRect.X, checkRect.Y, checkRect.Width, checkRect.Height,
                    isTagged ? Color.Green * 0.8f : Color.White, 1f, false);
                
                if (isTagged)
                {
                    b.Draw(Game1.mouseCursors, new Rectangle(checkRect.X + ScaleUiValue(5), checkRect.Y + ScaleUiValue(5), ScaleUiValue(25), ScaleUiValue(25)), new Rectangle(128, 256, 64, 64), Color.White);
                }

                currentY += rowHeight;
            }

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, originalState);
            b.GraphicsDevice.ScissorRectangle = originalScissor;
        }
    }
}
