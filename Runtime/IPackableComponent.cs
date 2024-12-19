using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Readymade.Persistence
{
    /// <summary>
    /// Describes a component that can export its state into a serializable (packable) data object and restore its state from
    /// such an object.
    /// </summary>
    /// <remarks>
    /// <para>A component is typically understood to be a Unity <see cref="Component"/>, but it can technically be used for
    /// any type. The <see cref="ComponentKey"/> and <see cref="PackIdentity"/> properties allow it to be part of a GameObject
    /// hierarchy that can be extended in the UnityEditor without modifying any code so long as the object is a child of a
    /// <see cref="Persistence.PackIdentity"/>.
    /// </para>
    /// <para>
    /// An abstract default implementation can be found in <see cref="PackableComponent{T}"/>.
    /// </para></remarks>
    /// <seealso cref="PackableComponent{T}"/>
    public interface IPackableComponent
    {
        /// <summary>
        /// An key that identifies this component uniquely inside the scope of its parent. Typically a
        /// <see cref="Persistence.PackIdentity"/>.
        /// </summary>
        /// <remarks>
        /// In a GameObject context this should be a Unity serialized ID that is build-static. This ID will be combined
        /// with the parent's <see cref="PackIdentity.EntityID"/> to form a scope that can be partially build-static.
        /// </remarks>
        /// <seealso cref="PackExtensions.GetPackKey(IPackableComponent)"/>
        [NotNull]
        public string ComponentKey { get; }

        /// <summary>
        /// The data <see cref="object"/> that this component will be able to <see cref="Pack"/> and <see cref="Unpack"/>.
        /// </summary>
        [NotNull]
        Type PackType { get; }

        /// <summary>
        /// Unpacks the given <see cref="object"/> data and applies it to restore this component's state.
        /// </summary>
        /// <param name="data">The data object to unpack into <see cref="PackType"/>.</param>
        /// <param name="lookup">A repository that maps AssetIDs to AssetObjects.</param>
        void Unpack([NotNull] object data, AssetLookup lookup);

        /// <summary>
        /// Packs this component's state into an <see cref="object"/> that can be serialized.
        /// </summary>
        /// <returns>The packed data of type <see cref="PackType"/>.</returns>
        [return: NotNull]
        object Pack();
    }
}