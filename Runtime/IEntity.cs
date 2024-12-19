using System;
using UnityEngine;

namespace Readymade.Persistence
{
    /// <summary>
    /// Represents a dynamically spawned object that can be identified by a globally unique ID. This allows storing
    /// references between objects in serialized data and restoring these references in a unified way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typically such an ID would be present on a <see cref="MonoBehaviour"/> and restored upon deserialization, paired
    /// with a lookup to resolve AssetIDs to the associated prefab instance from which it can be created. By convention
    /// such <see cref="IAssetIdentity"/> instances cannot not be nested.
    /// </para>
    /// </remarks>
    /// <seealso cref="IAssetIdentity"/>
    /// <seealso cref="IScriptableEntity"/>
    public interface IEntity
    {
        /// <summary>
        /// The globally unique, persistent ID that defines an entity (an entity is always represented as a runtime instance).
        /// </summary>
        Guid EntityID { get; }
    }

    /// <summary>
    /// An <see cref="IEntity"/> that represents a <see cref="GameObject"/>.
    /// </summary>
    /// <seealso cref="IAssetIdentity"/>
    /// <seealso cref="IEntity"/>
    public interface IScriptableEntity : IEntity
    {
        /// <summary>
        /// Gets the <see cref="GameObject"/> of this identity.
        /// </summary>
        /// <remarks>This exists as a way for this interface to link back to its implementation as a MonoBehaviour.</remarks>
        /// <returns>The <see cref="GameObject"/> this component belongs to.</returns>
        GameObject GetObject();
    }
}