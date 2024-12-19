using System;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace Readymade.Persistence
{
    /// <summary>
    /// Generic implementation of <see cref="IPackableScriptableObject"/>. Usable as-is to avoid boilerplate code or
    /// as reference for custom implementation.
    /// </summary>
    public abstract class PackableScriptableObject<T> : PackableScriptableObject
    {
        // we want this to be a field so we can directly change component values if T is a struct. 
        /// <summary>
        /// The packable state of this component. Will be packed and unpacked automatically.
        /// </summary>
        protected T Package;

        /// <inheritdoc />
        public override Type PackType => typeof(T);

        /// <inheritdoc />
        public override void Unpack(object args)
        {
            Package = (T)args;
            OnUnpack(Package);
        }

        /// <summary>
        /// Called the <paramref name="package"/> data object was assigned to <see cref="Package"/> and any default handlers have been run. 
        /// </summary>
        /// <remarks>Implement this with custom post-processing to apply any state passed via <see cref="Package"/> to any
        /// internal handlers.</remarks>
        protected abstract void OnUnpack(T package);

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
    /// Generic implementation of <see cref="IPackableScriptableObject"/>. Usable as-is to avoid boilerplate code or
    /// as reference for custom implementation.
    /// </summary>
    public abstract class PackableScriptableObject : ScriptableObject, IPackableScriptableObject
    {
        /// <summary>
        /// Whether this component has a static ID.
        /// </summary>
        public bool HasConstId => !string.IsNullOrEmpty(constID);

        [FormerlySerializedAs("staticID")]
        [SerializeField]
        [ReadOnly]
        [ValidateInput(nameof(ValidateConstID), "Please generate a valid ConstID for this component.")]
        [Tooltip(
            "The build-constant ID (ConstID) of this scriptable object. This ID must be globally unique. Duplicates will not " +
            "be detected by this component, this responsibility is delegated to the lookup systems that operate " +
            "on these IDs.")]
        private string constID;

        private Guid _parsedConstGuid;

        public Guid AssetID
        {
            get
            {
                if (!HasConstId)
                {
                    throw new InvalidOperationException("Object has no ConstID.");
                }

                if (_parsedConstGuid == default)
                {
                    if (!Guid.TryParse(constID, out _parsedConstGuid))
                    {
                        Debug.LogWarning($"Failed to parse GUID {constID}", this);
                    }
                }

                return _parsedConstGuid;
            }
        }

        private bool ValidateConstID => HasConstId && Guid.TryParse(constID, out _);

        /// <summary>
        /// Clears a the <see cref="AssetID"/> of this component. Only used in the Editor.
        /// </summary>
#if ODIN_INSPECTOR
        [DisableInPlayMode]
        [Button]
#else
        [Button(enabledMode: EButtonEnableMode.Editor)]
#endif
        private void ClearConstID()
        {
            constID = default;
        }

        /// <summary>
        /// Generates a new <see cref="AssetID"/> for this component. Only used in the Editor.
        /// </summary>
#if ODIN_INSPECTOR
        [DisableInPlayMode]
        [Button]
#else
        [Button(enabledMode: EButtonEnableMode.Editor)]
#endif
        private void NewConstID()
        {
            _parsedConstGuid = Guid.NewGuid();
            constID = _parsedConstGuid.ToString("N");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.LogWarning($"[{nameof(PackableScriptableObject)}] {name} generated new ConstID {constID}", this);
#endif
        }

        /// <inheritdoc />
        public abstract Type PackType { get; }

        /// <inheritdoc />
        public abstract void Unpack(object data);

        /// <inheritdoc />
        public abstract object Pack();
    }
}