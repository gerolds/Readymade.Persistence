using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Readymade.Persistence
{
    /// <summary>
    /// Describes a <see cref="ScriptableObject" /> that can be packed by <see cref="PackSystem" />. Packed SOs are
    /// expected to be asset instances. SOs instantiated at runtime are not supported.
    /// </summary>
    /// <remarks>Before implementing this interface on a SO, consider using a pre-made abstract implementation like <see cref="PackableScriptableObject"/> or a
    /// <see cref="PackableComponent"/>-derived custom packer that can work with any type.</remarks>
    /// <seealso cref="PackSystem"/>
    /// <seealso cref="PackableComponent" />
    /// <seealso cref="PackableScriptableObject"/>
    /// <seealso cref="IAssetIdentity"/>
    public interface IPackableScriptableObject : IAssetIdentity
    {
        /// <summary>
        /// The data <see cref="object"/> that this component will be able to <see cref="Pack"/> and <see cref="Unpack"/>.
        /// </summary>
        [NotNull]
        Type PackType { get; }

        /// <summary>
        /// Unpacks the given <see cref="object"/> data and applies it to restore this component's state.
        /// </summary>
        /// <param name="data">The data object to unpack into <see cref="PackType"/>.</param>
        void Unpack([NotNull] object data);

        /// <summary>
        /// Packs this component's state into an <see cref="object"/> that can be serialized.
        /// </summary>
        /// <returns>The packed data of type <see cref="PackType"/>.</returns>
        [return: NotNull]
        object Pack();
    }
}