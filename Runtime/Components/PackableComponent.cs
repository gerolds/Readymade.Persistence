#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Readymade.Persistence
{
    /// <inheritdoc cref="IPackableComponent"/>
    /// <summary>
    /// Convenient base class that implements <see cref="T:Readymade.Persistence.IPackableComponent" /> in a generic way.
    /// </summary>
    /// <typeparam name="T">The type that declares this component's packable state.</typeparam>
    /// <remarks>
    /// <para>
    /// Using this class is not required for persistence to work, creating a custom implementation of
    /// <see cref="T:Readymade.Persistence.IPackableComponent" /> is also fine. When doing so the contract declared in the interface must be implemented
    /// carefully with all the assumptions and constraints maintained.
    /// </para>
    /// <para>
    /// A simple way to implement persistent (packable) state is to directly use <see cref="Package"/> as the component's state.
    /// </para>
    /// </remarks>
    public abstract class PackableComponent<T> : PackableComponent
    {
        // we want this to be a field so we can directly change component values if T is a struct. 
        /// <summary>
        /// The packable state of this component. Will be packed and unpacked automatically.
        /// </summary>
        protected T Package;

        /// <inheritdoc />
        public override Type PackType => typeof(T);

        /// <inheritdoc />
        public override void Unpack([NotNull] object args, [NotNull] AssetLookup lookup)
        {
            Package = (T)args;
            OnUnpack(Package, lookup);
        }

        /// <summary>
        /// Called the <paramref name="package"/> data object was assigned to <see cref="Package"/> and any default handlers have been run. 
        /// </summary>
        /// <remarks>Implement this with custom post-processing to apply any state passed via <see cref="Package"/> to any
        /// internal handlers.</remarks>
        protected abstract void OnUnpack([NotNull] T package, [NotNull] AssetLookup lookup);

        /// <inheritdoc />
        public override object Pack()
        {
            Package = OnPack();
            return Package;
        }

        /// <summary>
        /// Called by the packing system when the state of this object is requested for serialization.
        /// </summary>
        /// <remarks>Implement this with custom processing to update the current <see cref="Package"/> with any internal state and return it.</remarks>
        protected abstract T OnPack();
    }

    /// <summary>
    /// A base class that allows referencing <see cref="PackableComponent{T}"/> without specifying the type.
    /// </summary>
    /// <remarks>This is supposed to be/become a base class for all MonoBehaviours in a project that minimally and unobtrusively
    /// implements all foundational interfaces which enable validation and tooling. We aim to put as little in this class as
    /// possible. Anything that is not absolutely required because of how unity works or can be achieved with reasonable workarounds
    /// should be kept out.</remarks>
    public abstract class PackableComponent : MonoBehaviour, IPackableComponent
    {
        [SerializeField] [ReadOnly] private string _key = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Generates a new key for this component. This key will be globally unique.
        /// </summary>
        [Button("New Component ID")]
        [DisableIf(nameof(IsPartOfPrefab))]
        [DisableInPlayMode]
        private void NewComponentKey() => _key = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Check whether this component is part of any prefab. Used to control whether the <see cref="NewComponentKey"/> button is enabled in the inspector UI.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        protected bool IsPartOfPrefab
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject);
#endif
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc />
        public string ComponentKey => _key;

        /// <inheritdoc />
        public abstract Type PackType { get; }

        /// <inheritdoc />
        public abstract void Unpack([NotNull] object args, [NotNull] AssetLookup lookup);

        /// <inheritdoc />
        public abstract object Pack();
    }
}