using System;
using NaughtyAttributes.Editor;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Readymade.Persistence.Editor
{
    /// <summary>
    /// A custom editor for <see cref="PackIdentity"/> components. Primarily provides validation and feedback.
    /// </summary>
    [CustomEditor(typeof(PackIdentity))]
    public class PackIdentityEditor : 
#if ODIN_INSPECTOR
        Sirenix.OdinInspector.Editor.OdinEditor
#else
        NaughtyInspector
#endif
    {
        private PackIdentity _packIdentity;

        /// <summary>
        /// Checks whether the <paramref name="origin"/> is part of a prefab.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsPartOfPrefab(Transform origin)
        {
            return PrefabUtility.IsPartOfAnyPrefab(origin.gameObject);
        }

        /// <summary>
        /// Checks whether the <paramref name="origin"/> component is nested inside another <see cref="PackIdentity"/> component's scope.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsNested(PackIdentity origin)
        {
            return HasParent(origin.transform) && origin.transform.parent
                .GetComponentsInParent<PackIdentity>().Any();
        }

        /// <summary>
        /// Checks whether the <paramref name="origin"/> component has a parent.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool HasParent(Transform origin)
        {
            return origin.transform.parent != default;
        }

        /// <summary>
        /// Checks whether the <paramref name="origin"/> is a prefab override.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsPrefabOverride(Transform origin)
        {
            return HasParent(origin) && origin.transform.parent.GetComponentsInParent<PackIdentity>()
                .Any(it => IsPartOfPrefab(it.transform) &&
                    (PrefabUtility.IsAddedGameObjectOverride(origin.gameObject) ||
                        PrefabUtility.IsAddedComponentOverride(origin)
                    )
                );
        }

        private bool IsAsset(PackIdentity origin)
        {
            return PrefabUtility.IsPartOfPrefabAsset(origin.gameObject);
        }

        /// <summary>
        /// Checks whether the <paramref name="origin"/> is a prefab stage root.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsStageRoot(Transform origin)
        {
            return IsInStageMode && origin.parent == null;
        }

        /// <summary>
        /// Whether the editor is in prefab isolation mode.
        /// </summary>
        private bool IsInStageMode =>
            UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != default;

        /// <summary>
        /// Check whether the <paramref name="origin"/> is a non-prefab instance.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsNonPrefabInstance(PackIdentity origin)
        {
            return origin.gameObject.scene != default &&
                !PrefabUtility.IsPartOfAnyPrefab(origin.gameObject);
        }

        private bool IsPrefabInstanceInScene(PackIdentity origin)
        {
            return PrefabUtility.IsPartOfNonAssetPrefabInstance(origin.gameObject);
        }

        /// <summary>
        /// Check whether the <paramref name="origin"/> is a prefab asset.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsSpawnableInstance(PackIdentity origin) =>
            origin.HasAssetID &&
            origin.gameObject.scene == default &&
            (
                PrefabUtility.IsPartOfPrefabAsset(origin.gameObject) ||
                PrefabUtility.IsOutermostPrefabInstanceRoot(origin.gameObject)
            );

        /// <summary>
        /// Check whether the <paramref name="origin"/> is a prefab instance.
        /// </summary>
        /// <param name="origin">The target object of this inspector.</param>
        /// <returns>The check result.</returns>
        private bool IsLifetimeInstance(PackIdentity origin)
            => PrefabUtility.IsPartOfNonAssetPrefabInstance(origin.gameObject);

        protected override void OnEnable()
        {
            base.OnEnable();
            _packIdentity = (PackIdentity)target;
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            GUIStyle labelStyle = new(EditorStyles.boldLabel);
            labelStyle.fontSize = (int)(labelStyle.fontSize * 1.6f);

            if (Application.isPlaying)
            {
                NaughtyEditorGUI.BeginBoxGroup_Layout("Identity");
                if (_packIdentity.HasAssetID)
                {
                    EditorGUILayout.LabelField(nameof(IAssetIdentity.AssetID), _packIdentity.AssetID.ToString("N"));
                }

                EditorGUILayout.LabelField(nameof(IEntity.EntityID), _packIdentity.EntityID.ToString("N"));
                NaughtyEditorGUI.EndBoxGroup_Layout();
            }
            else
            {
                if (IsNonPrefabInstance(_packIdentity))
                {
                    if (_packIdentity.HasEntityID)
                    {
                        GUILayout.Label("Discoverable Instance", labelStyle);
                        NaughtyEditorGUI.HelpBox_Layout(
                            $"This {nameof(PackIdentity)} is DISCOVERABLE. It will be discovered automatically and its packable state will be overwritten when restored.",
                            MessageType.None);
                    }
                    else
                    {
                        NaughtyEditorGUI.HelpBox_Layout(
                            $"This {nameof(PackIdentity)} is a non-prefab instance. In its current configuration, absent a {nameof(IEntity.EntityID)}, it will not be packable.",
                            MessageType.Error);
                    }
                }
                else if (IsPrefabInstanceInScene(_packIdentity))
                {
                    if (_packIdentity.HasEntityID)
                    {
                        GUILayout.Label("Discoverable Prefab Instance", labelStyle);
                        NaughtyEditorGUI.HelpBox_Layout(
                            $"This {nameof(PackIdentity)} is DISCOVERABLE. It will be discovered automatically and its packable state will be overwritten when restored.",
                            MessageType.None);
                    }
                    else
                    {
                        NaughtyEditorGUI.HelpBox_Layout(
                            $"This {nameof(PackIdentity)} is a scene-loaded prefab instance. In its current configuration, absent a {nameof(IEntity.EntityID)}, it will not be packable.",
                            MessageType.Error);
                    }
                }


                if (IsSpawnableInstance(_packIdentity))
                {
                    GUILayout.Label("Spawnable", labelStyle);
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This {nameof(PackIdentity)} is SPAWNABLE. It will be instantiated automatically on restore if an object with the same {nameof(IEntity.EntityID)} is not found. Its packable state will be restored.",
                        MessageType.None);
                }

                if (IsNested(_packIdentity))
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This {nameof(PackIdentity)} is nested inside another such component's scope. This will lead to undefined behaviour. Please make sure that no nesting of " +
                        $"{nameof(PackIdentity)} instances occurs.", MessageType.Error);
                }

                if (IsPrefabOverride(_packIdentity.transform))
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This appears to be an override to a prefab. Such a relationship cannot be restored deterministically " +
                        $"and will lead to undefined behaviour. Make sure all {nameof(PackIdentity)} instances are present on " +
                        $"the prefab asset that this hierarchy was instantiated from.", MessageType.Error);
                }

                if (IsAsset(_packIdentity) && _packIdentity.HasEntityID)
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This appears to be a prefab asset with a {nameof(IEntity.EntityID)}, this is invalid. Please clear the {nameof(IEntity.EntityID)}.",
                        MessageType.Error);
                }

                if (IsAsset(_packIdentity) && !_packIdentity.HasAssetID)
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This appears to be a prefab asset that is missing an {nameof(IAssetIdentity.AssetID)}; This is invalid. Please generate an {nameof(IAssetIdentity.AssetID)}.",
                        MessageType.Error);
                }

                if (IsNonPrefabInstance(_packIdentity) && _packIdentity.HasAssetID)
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This appears to be a non-prefab scene instance that has a an {nameof(IAssetIdentity.AssetID)}; this is invalid. Please clear the {nameof(IAssetIdentity.AssetID)}.",
                        MessageType.Error);
                }

                if ((IsPrefabInstanceInScene(_packIdentity) || IsNonPrefabInstance(_packIdentity)) &&
                    (!_packIdentity.HasEntityID))
                {
                    NaughtyEditorGUI.HelpBox_Layout(
                        $"This appears to be a scene instance that is missing a {nameof(IEntity.EntityID)}; this is invalid. Please generate a {nameof(IEntity.EntityID)}.",
                        MessageType.Error);
                }

                // NaughtyEditorGUI.HelpBox_Layout ( $"This appears to be a correctly configured {nameof ( PackIdentity )}", MessageType.None );
                base.OnInspectorGUI();
            }
        }
    }
}