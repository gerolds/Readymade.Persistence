using System;
using System.Collections.Generic;
using Readymade.Persistence;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Readymade.Persistence.Editor
{
    /// <inheritdoc />
    /// <summary>
    /// A custom editor for <see cref="T:Readymade.Persistence.PrefabLookup" /> objects. Provide convenience methods for filling the lookup.
    /// </summary>
    [CustomEditor(typeof(AssetLookup))]
    public class PersistentIdentityLookupEditor : UnityEditor.Editor
    {
        private AssetLookup _component;

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            _component = (AssetLookup)target;
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            //serializedObject.Update ();
            DrawDefaultInspector();
            if (_component.Registry.Count == 0)
            {
                GUILayout.Label("No prefabs registered.");
            }
            else
            {
                GUILayout.Label($"{_component.Registry.Count} prefabs registered.");
                GUILayout.Space(10);
            }

            foreach (KeyValuePair<Guid, Object> item in _component.Registry)
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = false;

                var key = item.Key.ToString("N");
                string keyStr = key.Length switch
                {
                    > 8 => $"{key[..6]} ... {key.Substring(key.Length - 2, 2)}",
                    _ => key
                };

                if (item.Value is GameObject go)
                {
                    EditorGUILayout.ObjectField(keyStr, item.Value, typeof(GameObject), allowSceneObjects: false);
                }

                if (item.Value is ScriptableObject so)
                {
                    EditorGUILayout.ObjectField(keyStr,
                        item.Value,
                        typeof(ScriptableObject), allowSceneObjects: false);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Typically assets are added here automatically, if however anything goes wrong, the lookup can be populated " +
                "through a search. This may take a while as it loads all assets one by one to check for components.",
                MessageType.None
            );
            if (GUILayout.Button("Find All"))
            {
                _component.FindAll();
            }
        }
    }
}