using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace SmartphoneAppStardewSocial
{
    public partial class StardewSocialScreen
    {
        private void DrawSocialDetail(SpriteBatch b, StardewConnectPost selectedPost, Rectangle clipRect)
        {
            this.socialDetailProfileIconBounds.Clear();
            this.socialDetailPhotoPrevBounds.Clear();
            this.socialDetailPhotoNextBounds.Clear();

            int cardX = clipRect.X + ScaleUiValue(15);
            int cursorY = clipRect.Y + ScaleUiValue(10) - (int)this.socialDetailScrollOffset;
            int cardHeight = MeasurePostHeight(selectedPost, true);

            DrawSocialPostCard(b, selectedPost, cardX, cursorY, cardHeight, true);
        }

        private void DrawCommentInputBox(SpriteBatch b)
        {
            Rectangle contentBounds = GetContentBounds();
            int cardX = contentBounds.X + ScaleUiValue(15);
            int inputHeight = ScaleUiValue(60);
            
            Rectangle commentInputBounds = new Rectangle(cardX, contentBounds.Bottom - ScaleUiValue(75), contentBounds.Width - ScaleUiValue(95), inputHeight);
            this.socialDetailCommentSendBounds = new Rectangle(commentInputBounds.Right + ScaleUiValue(10), commentInputBounds.Y, ScaleUiValue(55), inputHeight);

            // Draw comment textbox background
            IClickableMenu.drawTextureBox(
                b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                commentInputBounds.X, commentInputBounds.Y, commentInputBounds.Width, commentInputBounds.Height,
                Color.White, 1f, false);

            this.commentTextBox.Draw(b, commentInputBounds, this.phoneUiScale, this.Selected);

            // Send comment button (ONLY draw the OK button texture to fix button-inside-button)
            b.Draw(Game1.mouseCursors, this.socialDetailCommentSendBounds, new Rectangle(128, 256, 64, 64), Color.White);
        }
    }
}
