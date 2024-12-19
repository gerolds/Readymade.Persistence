using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Readymade.Persistence
{
    /// <summary>
    /// Data container used in <see cref="PackData"/>. 
    /// </summary>
    [Serializable]
    [JsonObject]
    [MessagePackObject]
    public class PackData
    {
        /// <summary>
        /// Version of the serializer that created this database. This is typically the app version.
        /// </summary>
        [Key(nameof(Version))]
        [JsonProperty]
        public string Version;

        /// <summary>
        /// Build ID with which this data object was last written.
        /// </summary>
        [Key(nameof(Build))]
        [JsonProperty]
        public string Build;

        /// <summary>
        /// Timestamp when the database was last committed.
        /// </summary>
        [Key(nameof(Modified))]
        [JsonProperty]
        public DateTimeOffset Modified;

        /// <summary>
        /// Data container when using <see cref="Backend.Json"/>.
        /// </summary>
        [JsonProperty]
        [Key(nameof(JsonEntries))]
        public Dictionary<string, JToken> JsonEntries = new();

        /// <summary>
        /// Data container when using <see cref="Backend.MessagePack"/>.
        /// </summary>
        [JsonIgnore]
        [Key(nameof(MessagePackEntries))]
        public Dictionary<string, byte[]> MessagePackEntries = new();
        
        /// <summary>
        /// Data container when using <see cref="Backend.MessagePack"/>.
        /// </summary>
        [JsonProperty]
        [Key(nameof(LazyJsonEntries))]
        public Dictionary<string, object> LazyJsonEntries = new();

        /// <summary>
        /// Intermediate data container when using <see cref="Backend.LazyJson"/>.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object> ObjectEntries = new();
    }
}