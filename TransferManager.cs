using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Newtonsoft.Json;

namespace SmartphoneAppStardewSocial
{
    public class TransferChunkMessage
    {
        public string TransferId { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string DataType { get; set; } = ""; // "PostData", "Photo", "Avatar", "ActionRequest", "PhotoList", "StalePhotosList"
        public string FileName { get; set; } = "";
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public string ChunkData { get; set; } = "";
        public string SaveFolderName { get; set; } = "";
    }

    public class SendChunkJob
    {
        public TransferChunkMessage Message { get; set; } = null!;
        public long TargetPlayerId { get; set; }
    }

    public class ActionRequest
    {
        public string Action { get; set; } = ""; // "CreatePost", "LikePost", "CommentPost"
        public string PostId { get; set; } = "";
        public string Text { get; set; } = "";
        public List<string> Photos { get; set; } = new();
        public List<string> Tagged { get; set; } = new();
        public string ActorName { get; set; } = "";
        public string CommentText { get; set; } = "";
        public Dictionary<string, string> PhotoTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class StardewSocialUpdateMessage
    {
        public StardewConnectPost? Post { get; set; }
        public List<StardewConnectProfileStats> Stats { get; set; } = new();
    }

    public static class TransferManager
    {
        public static readonly List<SendChunkJob> SendQueue = new();
        private static readonly Dictionary<string, List<TransferChunkMessage>> IncomingTransfers = new(StringComparer.OrdinalIgnoreCase);
        public static long? PriorityPlayerId { get; set; }

        public static void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !Context.IsMultiplayer)
                return;

            SendChunkJob? job = null;
            lock (SendQueue)
            {
                if (SendQueue.Count > 0)
                {
                    // Prioritize the author player's chunks if set
                    if (PriorityPlayerId.HasValue)
                    {
                        int pIndex = SendQueue.FindIndex(j => j.TargetPlayerId == PriorityPlayerId.Value);
                        if (pIndex >= 0)
                        {
                            job = SendQueue[pIndex];
                            SendQueue.RemoveAt(pIndex);
                        }
                    }

                    if (job == null)
                    {
                        job = SendQueue[0];
                        SendQueue.RemoveAt(0);
                    }
                }
            }

            if (job != null)
            {
                try
                {
                    ModEntry.Instance.Helper.Multiplayer.SendMessage(
                        job.Message,
                        "StardewSocial_TransferChunk",
                        new[] { ModEntry.Instance.ModManifest.UniqueID },
                        new[] { job.TargetPlayerId }
                    );
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to send chunk: {ex}", LogLevel.Error);
                }
            }
        }

        public static void InitiateFarmhandSync()
        {
            if (Context.IsMainPlayer || !Context.IsMultiplayer)
                return;

            string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
            string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
            List<string> photoNames = new();

            if (Directory.Exists(photoSharedDir))
            {
                foreach (string filePath in Directory.GetFiles(photoSharedDir))
                {
                    photoNames.Add(Path.GetFileName(filePath));
                }
            }

            string json = JsonConvert.SerializeObject(photoNames);
            if (Game1.MasterPlayer != null)
            {
                ModEntry.SMonitor.Log($"Initiating farmhand sync, sending {photoNames.Count} local photo names to Host.", LogLevel.Info);
                SendDirectMessage(Game1.MasterPlayer.UniqueMultiplayerID, "PhotoList", json);
            }
        }

        public static void QueueSend(string dataType, string fileName, string absoluteFilePath, long targetPlayerId)
        {
            if (!File.Exists(absoluteFilePath))
            {
                ModEntry.SMonitor.Log($"Cannot transfer file: '{absoluteFilePath}' does not exist.", LogLevel.Warn);
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(absoluteFilePath);
                string base64 = Convert.ToBase64String(bytes);
                QueueSendBase64(dataType, fileName, base64, targetPlayerId);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to read file for transfer queue: {ex}", LogLevel.Error);
            }
        }

        public static void QueueSendBase64(string dataType, string fileName, string base64Data, long targetPlayerId)
        {
            var targetFarmer = Game1.getOnlineFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == targetPlayerId);
            if (targetFarmer == null && targetPlayerId != Game1.player.UniqueMultiplayerID)
            {
                ModEntry.SMonitor.Log($"Cannot transfer data: target player ID '{targetPlayerId}' is offline.", LogLevel.Warn);
                return;
            }

            string receiverName = targetFarmer?.Name ?? Game1.player.Name;
            string transferId = Guid.NewGuid().ToString("N");
            string senderName = Game1.player.Name;

            int chunkSize = 10000; // 10KB chunk size limit
            int totalChunks = (int)Math.Ceiling(base64Data.Length / (double)chunkSize);
            if (totalChunks == 0)
                totalChunks = 1;

            ModEntry.SMonitor.Log($"Queueing transfer of '{fileName}' ({dataType}) to {receiverName} in {totalChunks} chunks.", LogLevel.Info);

            lock (SendQueue)
            {
                for (int i = 0; i < totalChunks; i++)
                {
                    int startIndex = i * chunkSize;
                    int length = Math.Min(chunkSize, base64Data.Length - startIndex);
                    string chunkData = length > 0 ? base64Data.Substring(startIndex, length) : "";

                    var msg = new TransferChunkMessage
                    {
                        TransferId = transferId,
                        SenderName = senderName,
                        ReceiverName = receiverName,
                        DataType = dataType,
                        FileName = fileName,
                        ChunkIndex = i,
                        TotalChunks = totalChunks,
                        ChunkData = chunkData,
                        SaveFolderName = StardewConnectManager.GetActiveSaveFolderName()
                    };

                    SendQueue.Add(new SendChunkJob
                    {
                        Message = msg,
                        TargetPlayerId = targetPlayerId
                    });
                }
            }
        }

        public static void SendDirectMessage(long targetPlayerId, string dataType, string content)
        {
            var targetFarmer = Game1.getOnlineFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == targetPlayerId);
            if (targetFarmer == null && targetPlayerId != Game1.player.UniqueMultiplayerID)
                return;

            var msg = new TransferChunkMessage
            {
                TransferId = Guid.NewGuid().ToString("N"),
                SenderName = Game1.player.Name,
                ReceiverName = targetFarmer?.Name ?? Game1.player.Name,
                DataType = dataType,
                FileName = "",
                ChunkIndex = 0,
                TotalChunks = 1,
                ChunkData = content,
                SaveFolderName = StardewConnectManager.GetActiveSaveFolderName()
            };

            try
            {
                ModEntry.Instance.Helper.Multiplayer.SendMessage(
                    msg,
                    "StardewSocial_TransferChunk",
                    new[] { ModEntry.Instance.ModManifest.UniqueID },
                    new[] { targetPlayerId }
                );
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to send direct message ({dataType}): {ex}", LogLevel.Error);
            }
        }

        public static void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModEntry.Instance.ModManifest.UniqueID)
                return;

            if (e.Type == "StardewSocial_TransferChunk")
            {
                TransferChunkMessage? chunk = e.ReadAs<TransferChunkMessage>();
                if (chunk == null)
                    return;

                if (chunk.TotalChunks <= 1)
                {
                    ProcessCompletedTransfer(e.FromPlayerID, chunk.DataType, chunk.FileName, chunk.ChunkData, chunk.SaveFolderName);
                }
                else
                {
                    lock (IncomingTransfers)
                    {
                        if (!IncomingTransfers.TryGetValue(chunk.TransferId, out var chunks))
                        {
                            chunks = new List<TransferChunkMessage>();
                            IncomingTransfers[chunk.TransferId] = chunks;
                        }

                        chunks.Add(chunk);

                        if (chunks.Count >= chunk.TotalChunks)
                        {
                            var orderedChunks = chunks.OrderBy(c => c.ChunkIndex).ToList();
                            string fullBase64 = string.Concat(orderedChunks.Select(c => c.ChunkData));

                            ProcessCompletedTransfer(e.FromPlayerID, chunk.DataType, chunk.FileName, fullBase64, chunk.SaveFolderName);
                            IncomingTransfers.Remove(chunk.TransferId);
                        }
                    }
                }
            }
        }

        private static void ProcessCompletedTransfer(long fromPlayerId, string dataType, string fileName, string data, string saveFolderName)
        {
            string activeSave = !string.IsNullOrWhiteSpace(saveFolderName)
                ? saveFolderName
                : StardewConnectManager.GetActiveSaveFolderName();

            if (dataType == "PhotoList" && Context.IsMainPlayer)
            {
                try
                {
                    // Force Host save to disk to ensure data files exist and are up-to-date
                    StardewConnectManager.Save();

                    List<string> farmhandPhotos = JsonConvert.DeserializeObject<List<string>>(data) ?? new();
                    string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                    string hostPhotoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                    
                    HashSet<string> hostPhotos = new(StringComparer.OrdinalIgnoreCase);
                    if (Directory.Exists(hostPhotoSharedDir))
                    {
                        foreach (string filePath in Directory.GetFiles(hostPhotoSharedDir))
                        {
                            hostPhotos.Add(Path.GetFileName(filePath));
                        }
                    }

                    List<string> stalePhotos = new();
                    foreach (var p in farmhandPhotos)
                    {
                        if (!hostPhotos.Contains(p))
                        {
                            stalePhotos.Add(p);
                        }
                    }

                    // Send stale photos list back to farmhand
                    string staleJson = JsonConvert.SerializeObject(stalePhotos);
                    SendDirectMessage(fromPlayerId, "StalePhotosList", staleJson);

                    // Queue missing photos to farmhand
                    foreach (var p in hostPhotos)
                    {
                        if (!farmhandPhotos.Contains(p))
                        {
                            string absolutePath = Path.Combine(hostPhotoSharedDir, p);
                            QueueSend("Photo", p, absolutePath, fromPlayerId);
                        }
                    }

                    // Queue full data (post_data.json) to farmhand
                    string postDataPath = StardewConnectManager.GetSaveFilePath();
                    if (File.Exists(postDataPath))
                    {
                        QueueSend("PostData", "post_data.json", postDataPath, fromPlayerId);
                    }

                    // Queue full profile stats data to farmhand
                    string directory = Path.GetDirectoryName(postDataPath)!;
                    string statsFilePath = Path.Combine(directory, "profile_stat.json");
                    if (File.Exists(statsFilePath))
                    {
                        QueueSend("ProfileStatsData", "profile_stat.json", statsFilePath, fromPlayerId);
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Error processing PhotoList: {ex.Message}", LogLevel.Error);
                }
            }
            else if (dataType == "StalePhotosList" && !Context.IsMainPlayer)
            {
                try
                {
                    List<string> stalePhotos = JsonConvert.DeserializeObject<List<string>>(data) ?? new();
                    string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                    string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_shared");
                    if (Directory.Exists(photoSharedDir))
                    {
                        foreach (var p in stalePhotos)
                        {
                            string filePath = Path.Combine(photoSharedDir, p);
                            try
                            {
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                ModEntry.SMonitor.Log($"Failed to delete stale photo {p}: {ex.Message}", LogLevel.Warn);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Error processing StalePhotosList: {ex.Message}", LogLevel.Error);
                }
            }
            else if (dataType == "Photo" || dataType == "Avatar")
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(data);
                    string photoSharedDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", activeSave, "photo_shared");
                    Directory.CreateDirectory(photoSharedDir);

                    string destPath = Path.Combine(photoSharedDir, fileName);
                    File.WriteAllBytes(destPath, bytes);

                    // Broadcast to all other players if we are the Host
                    if (Context.IsMainPlayer)
                    {
                        foreach (var farmer in Game1.getOnlineFarmers())
                        {
                            if (farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID &&
                                farmer.UniqueMultiplayerID != fromPlayerId)
                            {
                                QueueSendBase64(dataType, fileName, data, farmer.UniqueMultiplayerID);
                            }
                        }
                    }

                    // Refresh active screen cache if open
                    if (Game1.activeClickableMenu is StardewSocialScreen screen)
                    {
                        screen.ClearCachesAndRecalculate();
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to process received {dataType}: {ex}", LogLevel.Error);
                }
            }
            else if (dataType == "PostData" && !Context.IsMainPlayer)
            {
                try
                {
                    string filePath = StardewConnectManager.GetSaveFilePath();
                    string directory = Path.GetDirectoryName(filePath)!;
                    Directory.CreateDirectory(directory);
                    byte[] bytes = RobustDecodeData(data);
                    File.WriteAllBytes(filePath, bytes);

                    StardewConnectManager.Load();

                    if (Game1.activeClickableMenu is StardewSocialScreen screen)
                    {
                        screen.ClearCachesAndRecalculate();
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to process received PostData: {ex}", LogLevel.Error);
                }
            }
            else if (dataType == "ProfileStatsData" && !Context.IsMainPlayer)
            {
                try
                {
                    string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
                    string filePath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "profile_stat.json");
                    string directory = Path.GetDirectoryName(filePath)!;
                    Directory.CreateDirectory(directory);
                    byte[] bytes = RobustDecodeData(data);
                    File.WriteAllBytes(filePath, bytes);

                    StardewConnectManager.Load();

                    if (Game1.activeClickableMenu is StardewSocialScreen screen)
                    {
                        screen.ClearCachesAndRecalculate();
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to process received ProfileStatsData: {ex}", LogLevel.Error);
                }
            }
            else if (dataType == "PostUpdate" && !Context.IsMainPlayer)
            {
                try
                {
                    byte[] bytes = RobustDecodeData(data);
                    string json = System.Text.Encoding.UTF8.GetString(bytes);
                    StardewSocialUpdateMessage? update = JsonConvert.DeserializeObject<StardewSocialUpdateMessage>(json);
                    if (update != null)
                    {
                        StardewConnectManager.ApplyIncrementalUpdate(update);
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to process received PostUpdate: {ex}", LogLevel.Error);
                }
            }
            else if (dataType == "PostDelete" && !Context.IsMainPlayer)
            {
                try
                {
                    StardewConnectManager.DeletePostLocally(data);
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to process received PostDelete: {ex}", LogLevel.Error);
                }
            }
            else if (dataType == "ActionRequest" && Context.IsMainPlayer)
            {
                try
                {
                    ActionRequest? req = JsonConvert.DeserializeObject<ActionRequest>(data);
                    if (req == null) return;

                    // Prioritize sending response to the sender farmhand
                    PriorityPlayerId = fromPlayerId;

                    if (req.Action == "CreatePost")
                    {
                        StardewConnectManager.AddPlayerPost(req.Text, req.Photos, req.Tagged, req.ActorName, authorIsPlayer: true, photoTags: req.PhotoTags);
                    }
                    else if (req.Action == "LikePost")
                    {
                        StardewConnectManager.TogglePostLikeByPlayer(req.PostId, req.ActorName);
                    }
                    else if (req.Action == "CommentPost")
                    {
                        StardewConnectManager.AddPlayerComment(req.PostId, req.CommentText, req.ActorName, authorIsPlayer: true);
                    }
                    else if (req.Action == "DeletePost")
                    {
                        StardewConnectManager.DeletePost(req.PostId);
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor.Log($"Failed to process ActionRequest: {ex}", LogLevel.Error);
                }
                finally
                {
                    PriorityPlayerId = null;
                }
            }
        }

        private static byte[] RobustDecodeData(string data)
        {
            string currentStr = (data ?? "").Trim();
            bool decoded = false;
            byte[] bytes = Array.Empty<byte>();

            while (true)
            {
                if (currentStr.StartsWith("{") || currentStr.StartsWith("["))
                {
                    bytes = System.Text.Encoding.UTF8.GetBytes(currentStr);
                    decoded = true;
                    break;
                }

                try
                {
                    byte[] decodedBytes = Convert.FromBase64String(currentStr);
                    currentStr = System.Text.Encoding.UTF8.GetString(decodedBytes).Trim();
                }
                catch
                {
                    break;
                }
            }

            if (!decoded)
            {
                // Fallback to normal base64 decode if it never parsed as JSON
                try
                {
                    bytes = Convert.FromBase64String(data);
                }
                catch
                {
                    bytes = System.Text.Encoding.UTF8.GetBytes(data);
                }
            }

            return bytes;
        }
    }
}
