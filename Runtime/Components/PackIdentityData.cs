using MessagePack;
using Newtonsoft.Json;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Readymade.Persistence
{
    /// <summary>
    /// Represents the packed state of a <see cref="PackIdentity"/>.
    /// </summary>
    [JsonObject]
    [MessagePackObject]
    [Serializable]
    public class PackIdentityData
    {
        public PackIdentityData(
            string packKey,
            string scope,
            Guid assetID,
            Guid parentID,
            Vector3 position,
            Quaternion rotation,
            string description = default
        )
        {
            PackKey = packKey;
            Scope = scope;
            ParentID = parentID;
            AssetID = assetID;
            Position = position;
            Rotation = rotation;
            Description = description;
        }

        /// <summary>
        /// The ID of the particular GameObject instance being packed.
        /// </summary>
        [Key(nameof(PackKey))]
        public string PackKey { get; }

        /// <summary>
        /// The ID of the scope this object belongs to.
        /// </summary>
        [Key(nameof(Scope))]
        public string Scope { get; }

        /// <summary>
        /// ID of the parent <see cref="PackIdentity"/> under which the GameObject instance will be created and activated.
        /// </summary>
        [Key(nameof(ParentID))]
        public Guid ParentID { get; }

        /// <summary>
        /// ID of the prefab that will be used to restore the GameObject instance.
        /// </summary>
        [Key(nameof(AssetID))]
        public Guid AssetID { get; }

        /// <summary>
        /// The world position where the GameObject instance will be created and activated.
        /// </summary>
        [Key(nameof(Position))]
        public Vector3 Position { get; }

        /// <summary>
        /// The world rotation in which the GameObject instance will be created and activated.
        /// </summary>
        [Key(nameof(Rotation))]
        public Quaternion Rotation { get; }

        /// <summary>
        /// A description of the object being packed. This is optional and only used for debugging.
        /// </summary>
        [Key(nameof(Description))]
        public string Description { get; }
    }
}