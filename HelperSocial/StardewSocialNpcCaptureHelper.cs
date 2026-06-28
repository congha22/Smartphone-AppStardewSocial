using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;

namespace SmartphoneAppStardewSocial
{
    public static class StardewSocialNpcCaptureHelper
    {
        public static List<string> TryCaptureNpcPhoto(string npcName, int photoCount, int? captureTimeOfDay = null)
        {
            var filePaths = new List<string>();
            if (photoCount <= 0 || ModEntry.iSmartphoneApi == null)
                return filePaths;

            int effectiveCaptureTime = NormalizeCaptureTimeOfDay(captureTimeOfDay);

            Dictionary<string, Dictionary<string, AreaData>>? customAreaTags = ModEntry.areaTags;
            if (customAreaTags == null || customAreaTags.Count == 0)
                return filePaths;

            bool sveInstalled = ModEntry.SHelper?.ModRegistry?.Get("FlashShifter.StardewValleyExpandedCP") != null;
            bool rsvInstalled = ModEntry.SHelper?.ModRegistry?.Get("Rafseazz.RSVCP") != null;

            List<string> remainingLocations = customAreaTags
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key)
                                && entry.Value != null
                                && entry.Value.Count > 0)
                .Select(entry => entry.Key)
                .ToList();

            const int maxLocationAttempts = 5;
            string selectedLocationName = string.Empty;
            GameLocation? selectedLocation = null;
            bool shouldWearSwimming = false;
            var selectedLocationAreas = new List<KeyValuePair<string, AreaData>>();

            for (int attempt = 0; attempt < maxLocationAttempts && remainingLocations.Count > 0; attempt++)
            {
                int randomLocationIndex = Game1.random.Next(remainingLocations.Count);
                string locationName = remainingLocations[randomLocationIndex];
                remainingLocations.RemoveAt(randomLocationIndex);

                if (!customAreaTags.TryGetValue(locationName, out Dictionary<string, AreaData>? areasByName)
                    || areasByName == null
                    || areasByName.Count == 0)
                {
                    continue;
                }

                GameLocation targetLocation = Game1.getLocationFromName(locationName);
                if (targetLocation == null || targetLocation.farmers.Count > 0)
                    continue;

                shouldWearSwimming = string.Equals(locationName, "Beach", StringComparison.OrdinalIgnoreCase)
                    && effectiveCaptureTime < 1830
                    && (Game1.GetSeasonForLocation(targetLocation) == Season.Summer || Game1.GetSeasonForLocation(targetLocation) == Season.Fall);

                List<KeyValuePair<string, AreaData>> candidateAreas = areasByName
                    .Where(entry => entry.Value != null && !string.IsNullOrWhiteSpace(entry.Key))
                    .Where(entry => !entry.Key.StartsWith("SVE_", StringComparison.OrdinalIgnoreCase) || sveInstalled)
                    .Where(entry => !entry.Key.StartsWith("RSV_", StringComparison.OrdinalIgnoreCase) || rsvInstalled)
                    .ToList();

                if (candidateAreas.Count == 0)
                    continue;

                selectedLocationName = locationName;
                selectedLocation = targetLocation;
                selectedLocationAreas = candidateAreas;
                break;
            }

            if (selectedLocation == null || selectedLocationAreas.Count == 0 || string.IsNullOrWhiteSpace(selectedLocationName))
                return filePaths;

            string saveFolder = StardewConnectManager.GetActiveSaveFolderName();
            string photoTempDir = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveFolder, "photo_temp");
            Directory.CreateDirectory(photoTempDir);

            const int maxAreaAttemptsPerPhoto = 4;
            for (int photoIndex = 0; photoIndex < photoCount; photoIndex++)
            {
                bool captureCompleted = false;

                for (int areaAttempt = 0; areaAttempt < maxAreaAttemptsPerPhoto && !captureCompleted; areaAttempt++)
                {
                    KeyValuePair<string, AreaData> selectedArea = selectedLocationAreas[Game1.random.Next(selectedLocationAreas.Count)];
                    if (!TryFindRandomWalkableInteriorTile(selectedLocation, selectedArea.Value, out Vector2 targetTile))
                        continue;

                    HashSet<string> ownerNpcNameSet = BuildOwnerNpcNameSet(selectedArea.Value.ownerNpc, npcName);
                    bool ownerNpcRequired = ownerNpcNameSet.Count > 0;

                    int additionalNpcCount = DetermineAdditionalNpcCount(selectedArea.Value, ownerNpcRequired);
                    List<NPC> additionalDummyNpcs = CreateAdditionalDummyNpcs(npcName, additionalNpcCount, ownerNpcNameSet);

                    if (ownerNpcRequired && !additionalDummyNpcs.Any(npc => ownerNpcNameSet.Contains(npc.Name)))
                        continue;

                    NPC? dummyNpc = CreateDummyNpc(npcName);
                    if (dummyNpc == null)
                        return filePaths;

                    var occupiedTiles = new HashSet<(int X, int Y)>
                    {
                        ((int)targetTile.X, (int)targetTile.Y)
                    };

                    List<NPC> visibleNpcAtTarget = selectedLocation.characters
                        .OfType<NPC>()
                        .Where(npc => npc.IsVillager && !npc.IsInvisible)
                        .ToList();

                    foreach (var character in visibleNpcAtTarget)
                    {
                        character.clearTextAboveHead();
                        character.IsInvisible = true;
                    }

                    var spawnedDummies = new List<NPC>();

                    try
                    {
                        Game1.warpCharacter(dummyNpc, selectedLocationName, targetTile);
                        ApplyNaturalNpcWarpOffset(dummyNpc);
                        if (shouldWearSwimming)
                            dummyNpc.wearIslandAttire();
                        spawnedDummies.Add(dummyNpc);

                        bool ownerNpcPlaced = false;

                        for (int additionalIndex = 0; additionalIndex < additionalDummyNpcs.Count; additionalIndex++)
                        {
                            NPC additionalDummyNpc = additionalDummyNpcs[additionalIndex];
                            Vector2 placementAnchorTile = DetermineAdditionalNpcPlacementAnchor(spawnedDummies, dummyNpc, additionalIndex);
                            int maxDistanceFromAnchor = additionalIndex switch
                            {
                                0 => 5,
                                1 => 4,
                                _ => 3
                            };

                            if (!TryFindNearbyWalkableInteriorTile(selectedLocation, selectedArea.Value, placementAnchorTile, maxDistanceFromAnchor, occupiedTiles, out Vector2 additionalTile))
                                continue;

                            occupiedTiles.Add(((int)additionalTile.X, (int)additionalTile.Y));
                            Game1.warpCharacter(additionalDummyNpc, selectedLocationName, additionalTile);

                            if (shouldWearSwimming)
                                additionalDummyNpc.wearIslandAttire();
                            ApplyNaturalNpcWarpOffset(additionalDummyNpc);
                            spawnedDummies.Add(additionalDummyNpc);

                            if (ownerNpcNameSet.Contains(additionalDummyNpc.Name))
                                ownerNpcPlaced = true;
                        }

                        if (ownerNpcRequired && !ownerNpcPlaced)
                            continue;

                        Vector2 captureCenterTile = DetermineGroupCaptureCenterTile(spawnedDummies, dummyNpc, selectedLocation);
                        ApplyGroupFacingDirections(spawnedDummies, dummyNpc, captureCenterTile);
                        float zoomLevel = GetRandomNpcCaptureZoomLevel(spawnedDummies.Count);

                        string targetPath = Path.Combine(photoTempDir, $"{Guid.NewGuid():N}.jpg");

                        // Trigger capture via Smartphone API
                        string jsonResult = ModEntry.iSmartphoneApi.CaptureNpcPhoto(
                            targetLocation: selectedLocation,
                            captureCenter: captureCenterTile,
                            npc: dummyNpc,
                            landscape: Game1.random.NextBool(),
                            square: Game1.random.NextDouble() < 0.3,
                            visibleNpcAtTarget: visibleNpcAtTarget,
                            zoomLevel: zoomLevel,
                            captureTimeOfDay: effectiveCaptureTime,
                            saveLocation: targetPath);

                        if (!string.IsNullOrWhiteSpace(jsonResult))
                        {
                            string tag = string.Empty;
                            try
                            {
                                var meta = Newtonsoft.Json.Linq.JObject.Parse(jsonResult);
                                tag = meta["tag"]?.ToString() ?? meta["Tag"]?.ToString() ?? string.Empty;
                            }
                            catch
                            {
                                tag = jsonResult; // Fallback if API returned plain tag
                            }

                            ModEntry.RegisterNpcPhotoTag(targetPath, tag);
                            filePaths.Add(targetPath);
                            captureCompleted = true;
                        }
                    }
                    finally
                    {
                        foreach (NPC character in visibleNpcAtTarget)
                            character.IsInvisible = false;

                        foreach (NPC spawnedDummy in spawnedDummies)
                            spawnedDummy.currentLocation?.characters?.Remove(spawnedDummy);
                    }
                }
            }

            return filePaths;
        }

        private static NPC? CreateDummyNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return null;

            NPC? realNpc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            if (realNpc == null)
                return null;

            try
            {
                NPC dummyNpc = new NPC(realNpc.Sprite, realNpc.DefaultPosition, realNpc.DefaultMap, 2, npcName, realNpc.Portrait, true);
                return dummyNpc;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to create dummy NPC for '{npcName}': {ex}", LogLevel.Trace);
                return null;
            }
        }

        private static int DetermineAdditionalNpcCount(AreaData area, bool ownerNpcRequired)
        {
            int width = Math.Max(0, Math.Abs(area.endX - area.startX) - 1);
            int height = Math.Max(0, Math.Abs(area.endY - area.startY) - 1);
            int interiorTileCount = width * height;

            int maxAdditional = interiorTileCount switch
            {
                < 12 => 0,
                < 48 => 1,
                < 120 => 2,
                _ => 3
            };

            maxAdditional = Game1.random.Next(0, maxAdditional + 1);

            if (ownerNpcRequired)
                maxAdditional = Math.Max(1, maxAdditional);

            int minAdditional = ownerNpcRequired ? 1 : 0;
            maxAdditional = Math.Clamp(maxAdditional, minAdditional, 3);

            return Game1.random.Next(minAdditional, maxAdditional + 1);
        }

        private static HashSet<string> BuildOwnerNpcNameSet(List<string>? ownerNpcNames, string primaryNpcName)
        {
            var ownerNpcNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ownerNpcNames == null)
                return ownerNpcNameSet;

            foreach (string ownerNpcName in ownerNpcNames)
            {
                if (string.IsNullOrWhiteSpace(ownerNpcName))
                    continue;

                string trimmedName = ownerNpcName.Trim();
                if (string.Equals(trimmedName, primaryNpcName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsEligibleCompanionNpc(trimmedName))
                    continue;

                ownerNpcNameSet.Add(trimmedName);
            }

            return ownerNpcNameSet;
        }

        private static bool IsEligibleCompanionNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return false;

            NPC? npc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            return npc != null && npc.IsVillager && npc.CanSocialize && !npc.IsInvisible;
        }

        private static List<NPC> CreateAdditionalDummyNpcs(string primaryNpcName, int additionalNpcCount, HashSet<string> ownerNpcNameSet)
        {
            var additionalDummyNpcs = new List<NPC>();
            if (additionalNpcCount <= 0)
                return additionalDummyNpcs;

            var selectedNpcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryNpcName };

            if (ownerNpcNameSet.Count > 0)
            {
                List<string> ownerCandidates = ownerNpcNameSet.ToList();
                while (ownerCandidates.Count > 0 && additionalDummyNpcs.Count == 0)
                {
                    int randomOwnerIndex = Game1.random.Next(ownerCandidates.Count);
                    string ownerNpcName = ownerCandidates[randomOwnerIndex];
                    ownerCandidates.RemoveAt(randomOwnerIndex);

                    NPC? ownerDummyNpc = CreateDummyNpc(ownerNpcName);
                    if (ownerDummyNpc == null)
                        continue;

                    additionalDummyNpcs.Add(ownerDummyNpc);
                    selectedNpcNames.Add(ownerNpcName);
                }
            }

            if (additionalDummyNpcs.Count >= additionalNpcCount)
                return additionalDummyNpcs;

            List<string> randomCandidates = ModEntry.CachedContactableNpcs
                .Select(npc => npc.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(name => !selectedNpcNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int primaryNpcAge = ResolveNpcAge(primaryNpcName);

            while (additionalDummyNpcs.Count < additionalNpcCount && randomCandidates.Count > 0)
            {
                string? candidateName = PickWeightedCompanionName(randomCandidates, primaryNpcAge);
                if (string.IsNullOrWhiteSpace(candidateName))
                    break;

                randomCandidates.RemoveAll(name => string.Equals(name, candidateName, StringComparison.OrdinalIgnoreCase));
                selectedNpcNames.Add(candidateName);

                NPC? companionDummyNpc = CreateDummyNpc(candidateName);
                if (companionDummyNpc != null)
                    additionalDummyNpcs.Add(companionDummyNpc);
            }

            return additionalDummyNpcs;
        }

        private static string? PickWeightedCompanionName(List<string> candidates, int primaryNpcAge)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var weightedCandidates = new List<(string Name, double CumulativeWeight)>(candidates.Count);
            double cumulativeWeight = 0;
            bool primaryIsTeen = primaryNpcAge == 1;

            foreach (string candidateName in candidates)
            {
                int candidateAge = ResolveNpcAge(candidateName);
                bool candidateIsTeen = candidateAge == 1;
                double weight = primaryIsTeen
                    ? (candidateIsTeen ? 3.0 : 1.5)
                    : (candidateIsTeen ? 1.0 : 2.5);

                cumulativeWeight += weight;
                weightedCandidates.Add((candidateName, cumulativeWeight));
            }

            double roll = Game1.random.NextDouble() * cumulativeWeight;
            foreach ((string Name, double CumulativeWeight) candidate in weightedCandidates)
            {
                if (roll <= candidate.CumulativeWeight)
                    return candidate.Name;
            }

            return weightedCandidates[weightedCandidates.Count - 1].Name;
        }

        private static int ResolveNpcAge(string npcName)
        {
            NPC? realNpc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            return realNpc?.Age ?? 0;
        }

        private static Vector2 DetermineAdditionalNpcPlacementAnchor(List<NPC> spawnedDummies, NPC mainDummyNpc, int additionalIndex)
        {
            if (spawnedDummies == null || spawnedDummies.Count == 0)
                return mainDummyNpc.Tile;

            if (additionalIndex <= 0)
                return mainDummyNpc.Tile;

            if (additionalIndex == 1)
            {
                List<NPC> previousTwoNpcs = spawnedDummies.Take(2).ToList();
                if (previousTwoNpcs.Count < 2)
                    return mainDummyNpc.Tile;

                return ComputeRoundedCenterTile(previousTwoNpcs.Select(npc => npc.Tile));
            }

            List<NPC> previousThreeNpcs = spawnedDummies.Take(3).ToList();
            if (previousThreeNpcs.Count < 3)
                previousThreeNpcs = spawnedDummies;

            return ComputeRoundedCenterTile(previousThreeNpcs.Select(npc => npc.Tile));
        }

        private static Vector2 ComputeRoundedCenterTile(IEnumerable<Vector2> tiles)
        {
            List<Vector2> tileList = tiles?.ToList() ?? new List<Vector2>();
            if (tileList.Count == 0)
                return Vector2.Zero;

            float averageX = tileList.Average(tile => tile.X);
            float averageY = tileList.Average(tile => tile.Y);
            return new Vector2((float)Math.Round(averageX), (float)Math.Round(averageY));
        }

        private static bool TryFindNearbyWalkableInteriorTile(GameLocation location, AreaData area, Vector2 anchorTile, int maxDistanceFromAnchor, HashSet<(int X, int Y)> excludedTiles, out Vector2 targetTile)
        {
            targetTile = Vector2.Zero;

            int minX = Math.Min(area.startX, area.endX) + 1;
            int maxX = Math.Max(area.startX, area.endX) - 1;
            int minY = Math.Min(area.startY, area.endY) + 1;
            int maxY = Math.Max(area.startY, area.endY) - 1;

            if (minX > maxX || minY > maxY)
                return false;

            int anchorX = (int)anchorTile.X;
            int anchorY = (int)anchorTile.Y;
            var nearbyTiles = new List<Vector2>();
            var preferredSpreadTiles = new List<Vector2>();

            for (int tileY = minY; tileY <= maxY; tileY++)
            {
                for (int tileX = minX; tileX <= maxX; tileX++)
                {
                    if (excludedTiles.Contains((tileX, tileY)))
                        continue;

                    if (!IsWalkableWarpTile(location, tileX, tileY) || !IsWalkableWarpTile(location, tileX, tileY - 1))
                        continue;

                    int distance = Math.Abs(tileX - anchorX) + Math.Abs(tileY - anchorY);
                    if (distance <= 0 || distance > maxDistanceFromAnchor)
                        continue;

                    Vector2 candidateTile = new Vector2(tileX, tileY);
                    nearbyTiles.Add(candidateTile);
                    if (distance >= 2)
                        preferredSpreadTiles.Add(candidateTile);
                }
            }

            if (nearbyTiles.Count == 0)
                return false;

            List<Vector2> finalCandidates = preferredSpreadTiles.Count > 0
                ? preferredSpreadTiles
                : nearbyTiles;

            targetTile = finalCandidates[Game1.random.Next(finalCandidates.Count)];
            return true;
        }

        private static Vector2 DetermineGroupCaptureCenterTile(List<NPC> spawnedDummies, NPC mainDummyNpc, GameLocation targetLocation)
        {
            if (spawnedDummies == null || spawnedDummies.Count <= 1)
                return mainDummyNpc.Tile;

            float averageX = spawnedDummies.Average(npc => npc.Tile.X);
            float averageY = spawnedDummies.Average(npc => npc.Tile.Y);
            Vector2 centerTile = new Vector2((float)Math.Round(averageX), (float)Math.Round(averageY));

            Vector2 offsetFromMain = centerTile - mainDummyNpc.Tile;
            float distanceFromMain = offsetFromMain.Length();
            if (distanceFromMain > 2f && distanceFromMain > 0f)
            {
                Vector2 clampedOffset = offsetFromMain * (2f / distanceFromMain);
                centerTile = mainDummyNpc.Tile + clampedOffset;
                centerTile = new Vector2((float)Math.Round(centerTile.X), (float)Math.Round(centerTile.Y));
            }

            if (!targetLocation.isTileOnMap(centerTile))
                return mainDummyNpc.Tile;

            return centerTile;
        }

        private static float GetRandomNpcCaptureZoomLevel(int totalNpcCount)
        {
            (double minZoom, double maxZoom) = totalNpcCount switch
            {
                <= 1 => (0.75, 1.25),
                <= 3 => (0.75, 1.0),
                _ => (0.7, 0.85)
            };

            return (float)(minZoom + (Game1.random.NextDouble() * (maxZoom - minZoom)));
        }

        private static void ApplyGroupFacingDirections(List<NPC> spawnedDummies, NPC mainDummyNpc, Vector2 groupCenterTile)
        {
            if (spawnedDummies == null || spawnedDummies.Count == 0)
                return;

            if (spawnedDummies.Count == 1)
            {
                mainDummyNpc.faceDirection(Game1.random.Next(4));
                return;
            }

            foreach (NPC spawnedDummy in spawnedDummies)
            {
                Vector2? randomOtherTile = GetRandomOtherNpcTile(spawnedDummy, spawnedDummies);
                Vector2 lookTarget = randomOtherTile ?? groupCenterTile;

                if (lookTarget == spawnedDummy.Tile)
                {
                    Vector2 randomNudge = new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2));
                    if (randomNudge != Vector2.Zero)
                        lookTarget += randomNudge;
                }
                var direction = GetFacingDirectionToward(spawnedDummy.Tile, lookTarget);
                spawnedDummy.faceDirection(direction);
            }
        }

        private static Vector2? GetRandomOtherNpcTile(NPC sourceNpc, List<NPC> npcGroup)
        {
            var otherNpcs = npcGroup
                .Where(candidateNpc => !ReferenceEquals(sourceNpc, candidateNpc))
                .ToList();

            if (otherNpcs.Count == 0)
                return null;

            NPC targetNpc = otherNpcs[Game1.random.Next(otherNpcs.Count)];
            return targetNpc.Tile;
        }

        private static void ApplyNaturalNpcWarpOffset(NPC npc)
        {
            if (npc == null)
                return;

            int maxOffsetX = Math.Max(2, Game1.tileSize / 6);
            int maxOffsetY = Math.Max(2, Game1.tileSize / 8);

            float offsetX = Game1.random.Next(-maxOffsetX, maxOffsetX + 1);
            float offsetY = Game1.random.Next(-maxOffsetY, maxOffsetY + 1);
            npc.Position += new Vector2(offsetX, offsetY);
        }

        private static int GetFacingDirectionToward(Vector2 fromTile, Vector2 toTile)
        {
            float dx = toTile.X - fromTile.X;
            float dy = toTile.Y - fromTile.Y;

            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return 2;

            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx >= 0 ? 1 : 3;

            return dy >= 0 ? 2 : 0;
        }

        private static bool TryFindRandomWalkableInteriorTile(GameLocation location, AreaData area, out Vector2 targetTile, HashSet<(int X, int Y)>? excludedTiles = null)
        {
            targetTile = Vector2.Zero;

            int minX = Math.Min(area.startX, area.endX) + 1;
            int maxX = Math.Max(area.startX, area.endX) - 1;
            int minY = Math.Min(area.startY, area.endY) + 1;
            int maxY = Math.Max(area.startY, area.endY) - 1;

            if (minX > maxX || minY > maxY)
                return false;

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int totalTileCount = width * height;
            int randomChecks = Math.Min(totalTileCount, 40);
            var checkedTiles = new HashSet<(int X, int Y)>();

            for (int i = 0; i < randomChecks; i++)
            {
                int tileX = Game1.random.Next(minX, maxX + 1);
                int tileY = Game1.random.Next(minY, maxY + 1);

                if (!checkedTiles.Add((tileX, tileY)))
                    continue;

                if (excludedTiles != null && excludedTiles.Contains((tileX, tileY)))
                    continue;

                if (!IsWalkableWarpTile(location, tileX, tileY) || !IsWalkableWarpTile(location, tileX, tileY - 1))
                    continue;

                targetTile = new Vector2(tileX, tileY);
                return true;
            }

            for (int tileY = minY; tileY <= maxY; tileY++)
            {
                for (int tileX = minX; tileX <= maxX; tileX++)
                {
                    if (checkedTiles.Contains((tileX, tileY)))
                        continue;

                    if (excludedTiles != null && excludedTiles.Contains((tileX, tileY)))
                        continue;

                    if (!IsWalkableWarpTile(location, tileX, tileY) || !IsWalkableWarpTile(location, tileX, tileY - 1))
                        continue;

                    targetTile = new Vector2(tileX, tileY);
                    return true;
                }
            }

            return false;
        }

        private static bool IsWalkableWarpTile(GameLocation location, int tileX, int tileY)
        {
            var tile = new Vector2(tileX, tileY);
            if (!location.CanSpawnCharacterHere(tile))
                return false;

            return true;
        }

        private static int NormalizeCaptureTimeOfDay(int? captureTimeOfDay)
        {
            int fallbackTime = Game1.timeOfDay > 0 ? Game1.timeOfDay : 600;
            int rawTime = captureTimeOfDay ?? fallbackTime;

            int hour = Math.Clamp(rawTime / 100, 6, 26);
            int minute = Math.Clamp(rawTime % 100, 0, 59);
            return (hour * 100) + minute;
        }
    }
}
