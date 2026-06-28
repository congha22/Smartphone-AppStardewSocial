using System;
using StardewModdingAPI;
using StardewValley;

namespace SmartphoneAppStardewSocial
{
    public interface IStardewSocialApi
    {
        /// <summary>
        /// Opens the create post screen and populates it with optional text, tagged NPC, and image path.
        /// The user will cancel or confirm it.
        /// </summary>
        /// <param name="text">The initial text for the post draft.</param>
        /// <param name="taggedNpc">The internal name of an NPC to tag.</param>
        /// <param name="imagePath">The absolute path to an image file on disk to attach.</param>
        /// <param name="postTags">The tag to assign to the attached image.</param>
        void CreateDraftPost(string? text = null, string? taggedNpc = null, string? imagePath = null, string? postTags = null);

        /// <summary>
        /// Programmatically creates a new post by an NPC immediately.
        /// Allowed on the host only. If called on a farmhand client, it does nothing.
        /// At least text or imagePath must be provided.
        /// </summary>
        /// <param name="authorName">The NPC internal name of the author.</param>
        /// <param name="taggedNpc">The NPC internal name to tag.</param>
        /// <param name="text">The text of the post.</param>
        /// <param name="imagePath">The absolute path to an image file on disk to attach.</param>
        /// <param name="postTags">The tag to assign to the attached image.</param>
        void CreateNpcPost(string authorName, string? taggedNpc = null, string? text = null, string? imagePath = null, string? postTags = null);

        /// <summary>
        /// Opens the profile screen of a user (NPC or player).
        /// When called, it opens the Stardew Social app and navigates to the user's profile.
        /// </summary>
        /// <param name="actorName">The internal name of the NPC or the name of the player.</param>
        void OpenProfile(string actorName);
    }
}
