using System;
using System.Text.RegularExpressions;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using UnityEngine;

namespace Readymade.Persistence
{
    // TODO: this might be starting to overcomplicate things, recommendation is to not use such scopes (yet).
    /// <summary>
    /// EXPERIMENTAL!! DO NOT USE!! Defines an auto-discoverable scope that allows partial saving and restoring of objects.
    /// </summary>
    public class PackScope : MonoBehaviour
    {
        private void Reset() => NewScopeID();

        [InfoBox("Do not use PackScopes yet. They are very experimental!",
#if ODIN_INSPECTOR
            InfoMessageType.Warning
#else
            EInfoBoxType.Warning
#endif
        )]
        [SerializeField]
        [ValidateInput(nameof(ValidateScopeId), "Please generate a valid ID for this component.")]
        [Tooltip("A unique key identifying this scope globally in the project.")]
        private string scopeID;

        /// <summary>
        /// A unique key identifying this scope globally in the project.
        /// </summary>
        public string ScopeID => scopeID;

        /// <summary>
        /// Validates the <see cref="ScopeID"/> property.
        /// </summary>
        private bool ValidateScopeId => !string.IsNullOrEmpty(scopeID);

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnValidate()
        {
            scopeID = Regex.Replace(scopeID, @"[^A-Za-z0-9-]", "_"); // replace all non-alphanum with '_'
            name = $"Scope:{scopeID}";
        }

        /// <summary>
        /// Generates a new <see cref="ScopeID"/> for this component. Only used in the Editor.
        /// </summary>
        private void NewScopeID()
        {
            scopeID = Guid.NewGuid().ToString("N");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.LogWarning($"[{nameof(PackScope)}] {name} generated new ScopeID {scopeID}", this);
#endif
        }

        private Guid _parsedScopeID;
    }
}