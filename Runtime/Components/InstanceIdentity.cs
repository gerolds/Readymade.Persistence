#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace Readymade.Persistence
{
    /// <inheritdoc cref="IEntity"/>
    public class InstanceIdentity : MonoBehaviour, IEntity
    {
        private static readonly Dictionary<Guid, IEntity> s_allInstances = new();

#if UNITY_EDITOR
        /// <summary>
        /// Cleanup the instance tracking when entering playmode.
        /// </summary>
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void EnterPlaymodeHandler()
        {
            // we manually clear the instance tracking in case UnityEditor.EditorSettings.enterPlayModeOptions > 0 
            s_allInstances.Clear();
        }
#endif

        /// <summary>
        /// Attempts to find <see cref="IEntity"/> associated with a given <see cref="Guid"/>. 
        /// </summary>
        /// <param name="id">The <see cref="Guid"/> to query.</param>
        /// <param name="identity">The associated object, if it exists, null otherwise.</param>
        /// <returns>Whether an associated object was found.</returns>
        /// <remarks>All loaded and started GameObjects with a valid <see cref="InstanceIdentity"/> can be discovered by this API.</remarks>
        public static bool TryFindById(Guid id, out IEntity identity) =>
            s_allInstances.TryGetValue(id, out identity);

        [JsonProperty("Id")] private Guid _id;

        /// <inheritdoc cref="IAssetIdentity"/>
        [JsonIgnore]
        public Guid EntityID => _id;

        /// <inheritdoc />
        public GameObject GetObject() => gameObject;


#if ODIN_INSPECTOR
        [ReadOnly]
        [ShowInInspector]
#else
        [ShowNativeProperty]
#endif
        private string Identity => _id.ToString();

        /// <summary>
        /// A collection of all <see cref="IEntity"/> instances that are currently loaded and started.
        /// </summary>
        internal static IReadOnlyCollection<IEntity> AllInstances => s_allInstances.Values;

        [Tooltip("Whether to generate a new " + nameof(EntityID) + " in Start() if no override was assigned yet.")]
        [SerializeField]
        private bool generateWhenInvalid = false;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            if (_id == default && generateWhenInvalid)
            {
                NewId();
            }

            if (_id == default)
            {
                Debug.LogWarning(
                    $"[{nameof(InstanceIdentity)}] Object started without valid identity. Make sure the ID of this component is set before Start() is called. For example by manually calling OverrideId() or via JsonConvert.PopulateObject()");
            }
            else
            {
                RegisterIdImmediately(default);
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDestroy()
        {
            if (s_allInstances.ContainsKey(EntityID))
            {
                s_allInstances.Remove(EntityID);
            }
        }

        /// <summary>
        /// Generate a new <see cref="Guid"/> to be used as <see cref="EntityID"/> of this instance.
        /// </summary>
        /// <remarks>Calling this will make the instance immediately discoverable via <see cref="TryFindById"/>.</remarks>
        [Button]
        [Tooltip("Create a new GUID for this object.")]
        public void NewId()
        {
            s_allInstances.Remove(_id);
            _id = Guid.NewGuid();
            s_allInstances.Add(_id, this);
        }

        /// <summary>
        /// Override the <see cref="EntityID"/> of this instance.
        /// </summary>
        /// <param name="id">The <see cref="Guid"/> to use as <see cref="EntityID"/>.</param>
        /// <remarks>Calling this will make the instance immediately discoverable via <see cref="TryFindById"/>.</remarks>
        public void OverrideId(Guid id)
        {
            s_allInstances.Remove(_id);
            _id = id;
            s_allInstances.Add(_id, this);
        }

        /// <summary>
        /// Immediately registers this object for discovery by <see cref="TryFindById"/>.
        /// </summary>
        /// <remarks>Call this for example after <see cref="JsonConvert.PopulateObject(string, object)"/>.</remarks>
        [OnDeserialized]
        public void RegisterIdImmediately(StreamingContext _)
        {
            if (_id != default)
            {
                s_allInstances[_id] = this;
            }
        }
    }
}