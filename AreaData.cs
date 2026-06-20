using System.Collections.Generic;
using Newtonsoft.Json;

namespace SmartphoneAppStardewSocial
{
    /// <summary>Defines a rectangular area within a game location used for NPC photo capture placement.</summary>
    public class AreaData
    {
        public int startX { get; set; }
        public int startY { get; set; }
        public int endX { get; set; }
        public int endY { get; set; }
        public string description { get; set; } = string.Empty;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? ownerNpc { get; set; }
    }
}
