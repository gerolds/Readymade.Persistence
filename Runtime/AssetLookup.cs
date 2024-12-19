/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Readymade.Persistence
{
    /// <summary>
    /// A simple prefab reference lookup structure. Intended for use with <see cref="PackSystem"/>.
    /// </summary>
    [CreateAssetMenu(menuName = nameof(Readymade) + "/" + nameof(AssetLookup), fileName = nameof(AssetLookup),
        order = 0)]
    public class AssetLookup : ScriptableObject, ISerializationCallbackReceiver
    {
        private readonly Dictionary<Guid, Object> _registry = new();


        [HideInInspector] [SerializeField] private string[] _keys;

        [HideInInspector] [SerializeField] private Object[] _values;

        /// <summary>
        /// The lookup registry.
        /// </summary>
        public Dictionary<Guid, Object> Registry => _registry;

#if UNITY_EDITOR
        [Button]
        [Tooltip("Finds all instances of " + nameof(PackIdentity) + " with " + nameof(IAssetIdentity.AssetID) +
            " in the project.")]
        public void FindAll()
        {
            _registry.Clear();

            IEnumerable<PackIdentity> prefabs = FindPrefabsWithComponent<PackIdentity>();
            foreach (PackIdentity identity in prefabs)
            {
                if (!identity.HasAssetID)
                {
                    Debug.LogWarning(
                        $"[{nameof(AssetLookup)}] Object '{identity.name}' has no {nameof(IAssetIdentity.AssetID)} and will be ignored.",
                        identity);
                    continue;
                }

                if (!_registry.TryAdd(identity.AssetID, identity.gameObject))
                {
                    Debug.LogWarning(
                        $"[{nameof(AssetLookup)}] Duplicate ID detected on Prefab '{identity.name}'. Fix this and try again.",
                        identity);
                }
            }

            IEnumerable<ScriptableObject> SOs = FindAssets<ScriptableObject>();
            foreach (ScriptableObject so in SOs)
            {
                if (so is IAssetIdentity assetIdentity)
                {
                    if (!_registry.TryAdd(assetIdentity.AssetID, so))
                    {
                        Debug.LogWarning(
                            $"[{nameof(AssetLookup)}] Duplicate ID detected on ScriptableObject '{so.name}'. Fix this and try again.",
                            so);
                    }
                }
            }
            
            Debug.Log($"[{nameof(AssetLookup)}] {_registry.Count} assets registered.");


            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Finds all assets of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of assets to find.</typeparam>
        /// <returns>List of found assets of type <see cref="T"/>.</returns>
        public static IEnumerable<T> FindAssets<T>() where T : Object
        {
            List<T> assets = new();
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T)}");
            Debug.Log($"t:{typeof(T)}");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        /// <summary>
        /// Finds all prefabs with a given component.
        /// </summary>
        /// <typeparam name="T">The component that the prefabs must have.</typeparam>
        /// <returns>The prefabs with the given component.</returns>
        static IEnumerable<T> FindPrefabsWithComponent<T>() where T : Component
        {
            List<T> assets = new();
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(GameObject)}");
            Debug.Log($"t:{typeof(T)}");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

#endif

        /// <summary>
        /// Add a given key.
        /// </summary>
        /// <param name="guid">The key.</param>
        /// <param name="gameObject">The value associated with the key.</param>
        public void Add(Guid guid, GameObject gameObject)
        {
            _registry[guid] = gameObject;
        }

        /// <summary>
        /// Remove a given key.
        /// </summary>
        public void Remove(Guid guid)
        {
            _registry.Remove(guid);
        }

        /// <inheritdoc />
        public void OnBeforeSerialize()
        {
            // copy dictionary to arrays that can be serialized.
            _keys = _registry.Keys.Select(it => it.ToString("N")).ToArray();
            _values = _registry.Values.ToArray();
        }

        /// <inheritdoc />
        public void OnAfterDeserialize()
        {
            // copy pairs from serialized arrays back into dictionary.
            for (int i = 0; i < _keys.Length; i++)
            {
                _registry[Guid.Parse(_keys[i])] = _values[i];
            }
        }

        /// <summary>
        /// Whether a key is registered.
        /// </summary>
        public bool Contains(Guid key) => _registry.ContainsKey(key);

        /// <summary>
        /// Gets the asset associated with a given key.
        /// </summary>
        public Object GetObjectByID(Guid key) => _registry[key];

        /// <summary>
        /// Gets the asset associated with a given key.
        /// </summary>
        public GameObject GetPrefabByID(Guid key) => (GameObject)_registry[key];

        /// <summary>
        /// Gets the asset associated with a given key.
        /// </summary>
        public T GetObjectByID<T>(Guid key) where T : Object
        {
            Object obj = _registry[key];
            if (obj is GameObject go)
            {
                go.TryGetComponent<T>(out var component);
                if (component != null)
                {
                    return component;
                }

                throw new Exception("Could not find component of type " + typeof(T) + " on " + go.name + ".");
            }

            return (T)obj;
        }
    }
}