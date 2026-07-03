*** ADVANCED AI CUSTOM PROVIDER ***

Power users can route Smartphone AI calls to a custom service (local LLM or another provider) by editing config.json.

When CustomApiEndpoint is configured, Smartphone uses custom template mode for:
- NPC chat responses
- Daily conversation summaries
- StardewSocial post text generation
- StardewSocial comment generation

Network policy:
- Remote endpoints must use HTTPS.
- HTTP is only allowed for localhost or loopback hosts (localhost, 127.0.0.1, ::1).

Supported payload placeholders (plain TOKEN and {{TOKEN}} are both supported):
- INPUT_HERE (combined SYSTEM + USER text)
- SYSTEM_INPUT_HERE
- USER_INPUT_HERE
- SYSTEM_MESSAGE_HERE
- USER_MESSAGE_HERE
- MODEL_HERE

Example config.json values:

{
	"CustomApiEndpoint": "http://localhost:11434/v1/chat/completions",
	"CustomApiKey": "",
	"CustomApiKeyHeader": "Authorization",
	"CustomApiKeyPrefix": "Bearer",
	"CustomApiPayloadTemplate": "{\"model\":\"MODEL_HERE\",\"messages\":[{\"role\":\"system\",\"content\":\"SYSTEM_INPUT_HERE\"},{\"role\":\"user\",\"content\":\"USER_INPUT_HERE\"}]}",
	"CustomApiResponseTextPath": "choices[0].message.content",
	"CustomApiTimeoutSeconds": 45
}

Notes:
- CustomApiKey is optional.
- If your provider returns text in a different field, set CustomApiResponseTextPath (for example output_text, result.text, or candidates[0].content.parts[0].text).
- In custom template mode, function calling is disabled for chat. The mod use function call for Unlimited Event Expansion only, so you can fall back to use the button instead.


*** EXTERNAL APP API (FOR OTHER MODDERS) ***

Other mods can integrate with App Stardew Social by obtaining its API. Below is the documentation for the available API methods.

### Getting the API

You can access the API in your mod's `Entry` method via SMAPI's ModRegistry:

```csharp
public override void Entry(IModHelper helper)
{
    helper.Events.GameLoop.GameLaunched += OnGameLaunched;
}

private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
{
    var api = Helper.ModRegistry.GetApi<IStardewSocialApi>("d5a1lamdtd.Smartphone-AppStardewSocial");
    if (api != null)
    {
        // Use the API here
    }
}
```

---

### API Methods

#### `CreateDraftPost`
Opens the create post screen and populates it with optional text, a tagged NPC, and an image path. The user can then cancel or confirm/publish the draft.
```csharp
void CreateDraftPost(
    string? text = null,
    string? taggedNpc = null,
    string? imagePath = null,
    string? postTags = null
);
```
* **Parameters**:
  * `text`: (Optional) The initial text for the post draft.
  * `taggedNpc`: (Optional) The internal name of an NPC to tag.
  * `imagePath`: (Optional) The absolute path to an image file on disk to attach.
  * `postTags`: (Optional) The tag to assign to the attached image.

#### `CreateNpcPost`
Programmatically creates a new post by an NPC immediately. This is allowed on the host only; if called on a farmhand client, it does nothing. At least `text` or `imagePath` must be provided.
```csharp
void CreateNpcPost(
    string authorName,
    string? taggedNpc = null,
    string? text = null,
    string? imagePath = null,
    string? postTags = null
);
```
* **Parameters**:
  * `authorName`: The internal name of the NPC author.
  * `taggedNpc`: (Optional) The NPC internal name to tag.
  * `text`: (Optional) The text of the post.
  * `imagePath`: (Optional) The absolute path to an image file on disk to attach.
  * `postTags`: (Optional) The tag to assign to the attached image.

#### `OpenProfile`
Opens the profile screen of a user (NPC or player). When called, it opens the Stardew Social app and navigates to the specified user's profile.
```csharp
void OpenProfile(string actorName);
```
* **Parameters**:
  * `actorName`: The internal name of the NPC, or the name of the player.
