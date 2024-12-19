#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Readymade.Utils.Patterns;
using UnityEngine;
using UnityEngine.Serialization;

namespace Readymade.Persistence
{
    /// <summary>
    /// Identifies a GameObject for packing. A GameObject identified this way, together with its <see cref="T:Readymade.Persistence.IPackableComponent" />
    /// components can be saved and restored by <see cref="T:Builder.Persistence.PackManager" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The component identifies the <see cref="GameObject"/> in two ways: 1) statically with an ID that can be looked up in a registry to
    /// find a prefab from which it can be spawned and 2) with a lifetime ID that discriminates between multiple copies of the
    /// same statically identified prefab.
    /// </para>
    /// <para>
    /// Most settings on this component are generated automatically.
    /// </para>
    /// </remarks>
    public partial class PackIdentity : MonoBehaviour, IEntity, IAssetIdentity, ISerializationCallbackReceiver
    {
        // A PackIdentity is a component, therefore it can only exist on a GameObject. Consequently it can occur
        // in the following situations:
        // ID RULES:
        // - scene objects only need life IDs
        // - assets only need asset IDs
        // - asset-instances in a scene may have both
        // Having a unnecessary ID is not a problem, it will be ignored, UI should however hide/clear it so it doesn't
        // confuse the user and become a problem when the object type changes (duplications, asset-creation, unpacking
        // etc.).

        public enum PackingPolicy
        {
            /// <summary>
            /// Persistence of this object requires it to be manually captured and restored.
            /// </summary>
            Manual,

            /// <summary>
            /// Restore state on start. Capture state on destroy. This should only be used for objects that have the
            /// same lifecycle as the scene they are part of. For dynamically spawned objects use <see cref="DynamicLifecycle"/>.
            /// </summary>
            SceneLifecycle,

            /// <summary>
            /// This object is spawned dynamically and should not be captured or restored based on lifecycle events.
            /// </summary>
            DynamicLifecycle,

            /// <summary>
            /// Do not capture or restore state of this object.
            /// </summary>
            Ghost
        }

        #region Static API

        private static readonly Dictionary<Guid, PackIdentity> s_LoadedIdentities = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeOnLoad()
        {
            s_LoadedIdentities.Clear();
            Debug.Log($"[{nameof(PackIdentity)} ID repositories cleared.");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears static fields.
        /// </summary>
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void EnterPlaymodeHandler()
        {
            // we manually clear the instance tracking in case UnityEditor.EditorSettings.enterPlayModeOptions > 0 
            s_LoadedIdentities.Clear();
            Debug.Log($"[{nameof(PackIdentity)} ID repositories cleared.");
        }
#endif

        #endregion

        [SerializeField]
        [ReadOnly]
        [ShowIf(nameof(HasAssetID))]
        [ValidateInput(nameof(ValidateAssetID),
            "Please generate a valid " + nameof(IAssetIdentity.AssetID) + " for this component.")]
        [Tooltip(
            "The " + nameof(IAssetIdentity.AssetID) +
            " of the prefab from which this instance can be spawned. This ID must be globally unique. " +
            "Duplicates will not be detected by this component, this responsibility is delegated to the lookup or spawner " +
            "systems that operate on these IDs.")]
        private string assetID;

        [FormerlySerializedAs("lifeID")]
        [SerializeField]
        [ReadOnly]
        private string entityID; // serialized life ID.

        [Tooltip("Decide how this object will persist it state.\n" +
            "<b>[Manual]</b> Persistence of this object requires it to be manually captured and restored.\n" +
            "<b>[SceneLifecycle]</b> Restore state on start. Capture state on destroy. This should only be used for objects that have the same lifecycle as the scene they are part of. For dynamically spawned objects use DynamicLifecycle.\n" +
            "<b>[DynamicLifecycle]</b> This object is spawned dynamically and should not be captured or restored based on lifecycle events.\n" +
            "<b>[Ghost]</b> Do not capture or restore state."
        )]
        [SerializeField]
        private PackingPolicy packing;

        private Guid _parsedEntityID;
        private Guid _parsedAssetID;
        private bool _serializationRestoreEntityIDError;
        private IPackSystem _system;
        private static StringBuilder _sb = new();
        private static Stack<Transform> _path = new();

        /// <summary>
        /// A collection of all <see cref="PackIdentity"/> instances that have a <see cref="IAssetIdentity"/>.
        /// </summary>
        public static IReadOnlyCollection<PackIdentity> AllLoadedInstances => s_LoadedIdentities.Values;

        /// <summary>
        /// Whether this component has a ConstID.
        /// </summary>
        public bool HasAssetID => AssetID != default;

        public bool HasEntityID => EntityID != default;

        /// <inheritdoc cref="IEntity"/>
        public Guid EntityID
        {
            get
            {
                if (_parsedEntityID == default && Guid.TryParse(entityID, out Guid parsedID))
                {
                    Internal__RestoreEntityID(parsedID);
                }

                return _parsedEntityID;
            }
        }

        /// <inheritdoc cref="IAssetIdentity"/>
        public Guid AssetID
        {
            get
            {
                if (_parsedAssetID == default && Guid.TryParse(assetID, out var parsedID))
                {
                    Internal__RestoreAssetID(parsedID);
                }

                return _parsedAssetID;
            }
        }

        public bool IsSceneObject => gameObject && gameObject.scene.IsValid() && gameObject.scene != default;
        public PackingPolicy Policy => packing;

        private void Awake()
        {
            if (_serializationRestoreEntityIDError)
            {
                Debug.LogError(
                    $"[{nameof(PackIdentity)}] {name} failed to restore {nameof(IEntity.EntityID)} {_parsedEntityID}",
                    this);
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
#if UNITY_EDITOR
            if (ShouldHaveAssetID)
            {
                Debug.LogError($"[{nameof(PackIdentity)} {name} is a prefab but has no {nameof(AssetID)}.", this);
            }
#endif

            if (!HasEntityID)
            {
                Internal__NewEntityID();
            }

#if UNITY_EDITOR
            if (ShouldHaveEntityID)
            {
                Debug.LogError($"[{nameof(PackIdentity)} {name} is instance but has no {nameof(EntityID)}.", this);
            }
#endif

            if (EntityID == default)
            {
                Debug.LogWarning(
                    $"[{nameof(PackIdentity)}] Object started without valid " + nameof(IEntity.EntityID) +
                    ". Make sure the ID of this component is set before Start() is called.",
                    this);
            }
            else
            {
                OnDeserialized(default);
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDestroy()
        {
            UnRegisterCurrentEntityID();
        }

        /// <inheritdoc />
        public GameObject GetObject() => gameObject;

        /// <summary>
        /// On load we search for all assets and cache them under their keys.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(loadType: RuntimeInitializeLoadType.AfterSceneLoad)]
        protected static void RegisterInactiveObjects()
        {
            foreach (PackIdentity prefab in Resources.FindObjectsOfTypeAll<PackIdentity>())
            {
                if (prefab.HasAssetID)
                {
                    if (!s_LoadedIdentities.TryAdd(prefab.AssetID, prefab))
                    {
                        Debug.LogWarning(
                            $"[{nameof(PackIdentity)}] Duplicated {nameof(IAssetIdentity.AssetID)} {prefab.AssetID} detected on prefab '{prefab.name}'. Ensure all assets have unique {nameof(AssetID)}s.",
                            prefab);
                    }
                    else
                    {
                        Debug.Log(
                            $"[{nameof(PackIdentity)}] prefab '{prefab.name}' registered under {nameof(IAssetIdentity.AssetID)} {prefab.assetID}",
                            prefab);
                    }
                }
                else
                {
                    Debug.Log(
                        $"[{nameof(PackIdentity)}] prefab '{prefab.name}' ignored; no {nameof(IAssetIdentity.AssetID)}",
                        prefab);
                }
            }
        }

        /// <summary>
        /// Clears a the <see cref="AssetID"/> of this component. Only used in the Editor.
        /// </summary>
#if ODIN_INSPECTOR
        [DisableInPlayMode]
        [ShowIf("@" + nameof(ShouldNotHaveAssetID) + " || " + nameof(CanClearAssetID))]
#else
        [ShowIf(EConditionOperator.Or, nameof(ShouldNotHaveAssetID), nameof(CanClearAssetID))]
#endif
        [EnableIf(nameof(CanClearAssetID))]
        [Button]
        private void Internal__ClearAssetID()
        {
            _parsedAssetID = default;
            SerializeAssetID();
            Debug.Log($"[{nameof(PackIdentity)}] {name} cleared {nameof(IAssetIdentity.AssetID)}", this);
        }

        /// <summary>
        /// Generates a new <see cref="AssetID"/> for this component. Only used in the Editor.
        /// </summary>
#if ODIN_INSPECTOR
        [DisableInPlayMode]
        [ShowIf("@" + nameof(ShouldHaveAssetID) + " || " + nameof(CanGenerateAssetId))]
#else
        [ShowIf(EConditionOperator.Or, nameof(ShouldHaveAssetID), nameof(CanGenerateAssetId))]
#endif
        [Button]
        [EnableIf(nameof(CanGenerateAssetId))]
        private void Internal__NewAssetID()
        {
            _parsedAssetID = Guid.NewGuid();
            SerializeAssetID();
        }

        /// <summary>Collect the state of this <see cref="GameObject"/> into a serializable data package. This does not contain any
        /// <see cref="IPackableComponent"/> state.</summary>
        internal PackIdentityData Pack()
        {
            string scopeKey;
            PackScope packScope = GetComponentInParent<PackScope>();
            if (packScope)
            {
                scopeKey = $"{PackSystem.SCOPE_PREFIX}{packScope.ScopeID}";
            }
            else
            {
                scopeKey = $"{PackSystem.SCENE_PREFIX}{gameObject.scene.name}";
            }

            return new PackIdentityData(
                packKey: this.GetPackKey(),
                scope: scopeKey,
                assetID: HasAssetID ? AssetID : default,
                parentID: (transform.parent != null
                    ? (transform.parent.TryGetComponent(out PackIdentity parentPackIdentity)
                        ? parentPackIdentity.EntityID
                        : default)
                    : default),
                (transform.position),
                (transform.rotation),
                GetPath(transform)
            );
        }

        /// <summary>
        /// Generate a new <see cref="Guid"/> to be used as <see cref="EntityID"/> of this instance.
        /// </summary>
        /// <remarks>Calling this will make the instance immediately discoverable via <see cref="TryGetByEntityID"/>.</remarks>
        [Button("New EntityID")]
#if ODIN_INSPECTOR
        [ShowIf("@" + nameof(ShouldHaveEntityID) + " || " + nameof(CanGenerateEntityID))]
#else
        [ShowIf(EConditionOperator.Or, nameof(ShouldHaveEntityID), nameof(CanGenerateEntityID))]
#endif
        [EnableIf(nameof(CanGenerateEntityID))]
        [Tooltip("Create a new EntityID for this object.")]
        public void Internal__NewEntityID()
        {
            UnRegisterCurrentEntityID();
            _parsedEntityID = Guid.NewGuid();
            if (RegisterID(_parsedEntityID))
            {
                SerializeEntityID();
                Debug.Log($"[{nameof(PackIdentity)}] {name} generated new {nameof(IEntity.EntityID)} {entityID}",
                    this);
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(PackIdentity)}] {name} failed to generate new {nameof(IEntity.EntityID)} {entityID}",
                    this);
            }
        }

        /// <summary>
        /// Generate a new <see cref="Guid"/> to be used as <see cref="EntityID"/> of this instance.
        /// </summary>
        /// <remarks>Calling this will make the instance immediately discoverable via <see cref="TryGetByEntityID"/>.</remarks>
        [Button("Clear EntityID")]
#if ODIN_INSPECTOR
        [ShowIf("@" + nameof(ShouldNotHaveEntityID) + " || " + nameof(CanClearEntityID))]
#else
        [ShowIf(EConditionOperator.Or, nameof(ShouldNotHaveEnityID), nameof(CanClearEnityID))]
#endif
        [EnableIf(nameof(CanClearEntityID))]
        [Tooltip("Clear the EntityID for this object.")]
        public void Internal__ClearEntityID()
        {
            UnRegisterCurrentEntityID();
            _parsedEntityID = default;
            SerializeEntityID();
            Debug.Log($"[{nameof(PackIdentity)}] {name} cleared {nameof(IEntity.EntityID)}", this);
        }

        /// <summary>
        /// Override the <see cref="EntityID"/> of this instance. Called by <see cref="PackSystem"/>.
        /// </summary>
        /// <param name="id">The <see cref="Guid"/> to use as <see cref="EntityID"/>.</param>
        /// <remarks>Calling this will make the instance immediately discoverable via <see cref="TryGetByEntityID"/>.</remarks>
        internal void Internal__OverrideEntityID(Guid id)
        {
            UnRegisterCurrentEntityID();
            _parsedEntityID = id;
            if (RegisterID(_parsedEntityID))
            {
                SerializeEntityID();
                Debug.Log($"[{nameof(PackIdentity)}] {name} override {nameof(IEntity.EntityID)} {entityID}", this);
            }
            else
            {
                Debug.LogError(
                    $"[{nameof(PackIdentity)}] {name} failed to override {nameof(IEntity.EntityID)} {entityID}",
                    this);
            }
        }

        internal void Internal__RestoreEntityID(Guid id)
        {
            UnRegisterCurrentEntityID();
            _parsedEntityID = id;
            if (RegisterID(_parsedEntityID))
            {
                SerializeEntityID();
            }
            else
            {
                _serializationRestoreEntityIDError = true;
            }
        }

        internal void Internal__RestoreAssetID(Guid id)
        {
            _parsedAssetID = id;
            SerializeAssetID();
            // Debug.Log($"[{nameof(PackIdentity)}] Restored {nameof(IAssetIdentity.AssetID)} {assetID}", this);
        }

        private bool RegisterID(Guid id)
        {
            if (s_LoadedIdentities.TryGetValue(id, out var existing) &&
                (existing && existing != this))
            {
                Debug.LogWarning(
                    $"[{nameof(PackIdentity)}] Duplicate {nameof(AssetID)} or {nameof(EntityID)} {id} on {name}; Another object is already registered with it.",
                    this);
                return false;
            }
            else
            {
                s_LoadedIdentities[id] = this;
                return true;
            }
        }

        private void UnRegisterCurrentAssetID() => UnRegisterID(_parsedAssetID);

        private void UnRegisterCurrentEntityID() => UnRegisterID(_parsedEntityID);

        private void UnRegisterID(Guid id)
        {
            if (s_LoadedIdentities.TryGetValue(id, out var existing) &&
                (!existing || existing == this))
            {
                s_LoadedIdentities.Remove(id);
            }
        }

        private void SerializeEntityID()
        {
            var tmp = entityID;
            entityID = _parsedEntityID != default
                ? _parsedEntityID.ToString("N")
                : default;
#if UNITY_EDITOR
            if (tmp != entityID)
            {
                RecordPropertyChanges();
                Debug.Log($"[{nameof(PackIdentity)}] {name} stored new {nameof(IEntity.EntityID)} {entityID}",
                    this);
            }
#endif
        }

        private void SerializeAssetID()
        {
            var tmp = assetID;
            assetID = _parsedAssetID != default
                ? _parsedAssetID.ToString("N")
                : default;

#if UNITY_EDITOR
            if (tmp != assetID)
            {
                RecordPropertyChanges();
                Debug.Log($"[{nameof(PackIdentity)}] {name} stored new {nameof(IAssetIdentity.AssetID)} {assetID}",
                    this);
            }
#endif
        }

#if UNITY_EDITOR
        private void RecordPropertyChanges()
        {
            // For prefab instances we have to manually record the change to get saved.
            if (UnityEditor.PrefabUtility.IsPartOfNonAssetPrefabInstance(this))
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }

            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        private void OnValidate()
        {
            if (!IsInStageMode)
            {
                if (IsPartOfScene && !HasEntityID)
                {
                    Internal__NewEntityID();
                }

                if (IsAsset)
                {
                    if (HasEntityID)
                    {
                        Internal__ClearEntityID();
                    }

                    if (!HasAssetID)
                    {
                        Internal__NewAssetID();
                    }
                }

                if (IsNonAssetInstance && HasAssetID)
                {
                    Internal__ClearAssetID();
                }
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            OnDeserialized(default);
        }

        /// <summary>
        /// Immediately registers this object for discovery by <see cref="TryGetByEntityID"/>.
        /// </summary>
        [OnDeserialized]
        public void OnDeserialized(StreamingContext _)
        {
            if (Guid.TryParse(entityID, out Guid parsedEntityID))
            {
                Internal__RestoreEntityID(parsedEntityID);
            }

            if (Guid.TryParse(assetID, out Guid parsedAssetID))
            {
                Internal__RestoreAssetID(parsedAssetID);
            }
        }

        /// <summary>
        /// Checks whether a <see cref="PackIdentity"/> with a given <paramref name="id"/> exists currently.
        /// </summary>
        /// <param name="id">The <see cref="IEntity.EntityID"/> or <see cref="IAssetIdentity.AssetID"/> to check for.</param>
        /// <returns>Whether the check was successful.</returns>
        public static bool InstanceExists(Guid id) =>
            s_LoadedIdentities.TryGetValue(id, out PackIdentity value) && value;

        /// <summary>
        /// Gets the <see cref="PackIdentity"/> with a given ID.
        /// </summary>
        /// <param name="id">The ID to get.</param>
        /// <returns>The <see cref="PackIdentity"/> instance. Null if the instance is not found or not a scene instance.</returns>
        public static PackIdentity GetInstance(Guid id) =>
            s_LoadedIdentities.TryGetValue(id, out var identity) &&
            identity &&
            identity.HasEntityID &&
            identity.gameObject.scene != default
                ? identity
                : null;

        /// <summary>
        /// Attempts to find <see cref="PackIdentity"/> associated with a given <see cref="Guid"/>. 
        /// </summary>
        /// <param name="id">The <see cref="Guid"/> to query.</param>
        /// <param name="identity">The associated object, if it exists, null otherwise.</param>
        /// <returns>Whether an associated object was found.</returns>
        /// <remarks>All loaded and started GameObjects with a valid <see cref="PackIdentity"/> can be discovered by this API.</remarks>
        public static bool TryGetByEntityID(Guid id, out PackIdentity identity)
        {
            return s_LoadedIdentities.TryGetValue(id, out identity) && identity;
        }

        /// <summary>
        /// Constructs the scene hierarchy path to a given transform. Useful for debugging.
        /// </summary>
        /// <param name="transform">The transform to generate a path for.</param>
        /// <returns>The constructed path.</returns>
        private static string GetPath(Transform transform)
        {
            _sb.Clear();
            _path.Clear();
            Transform node = transform;
            int i = 0;
            do
            {
                _path.Push(node);
                node = node.parent;

                i++;
                if (i > 9)
                {
                    break;
                }
            } while (node != default);

            while (_path.Count > 0)
            {
                node = _path.Pop();
                _sb.Append("/");
                _sb.Append(node.name);
            }

            return _sb.ToString();
        }
    }
}