using System;
using UnityEngine;

namespace Readymade.Persistence {
    /// <summary>
    /// Represents an object that can be identified by a unique ID that is static across runtime sessions. This allows
    /// associating custom serialized data with unity assets that are external to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typically such a <see cref="IAssetIdentity"/> would be present on a <see cref="MonoBehaviour"/> or <see cref="ScriptableObject"/> and
    /// serialized by Unity as part of an asset (prefab or SO instance) that are referenced in a lookup table which then can
    /// be used to find associations between such objects and any runtime instances of the same value.
    /// </para>
    /// <para>
    /// By convention such <see cref="IAssetIdentity"/> instances cannot not be nested.
    /// </para>
    /// </remarks>
    /// <seealso cref="IEntity"/>
    public interface IAssetIdentity {
        /// <summary>
        /// The persistent ID of the object.
        /// </summary>
        public Guid AssetID { get; }
    }
}