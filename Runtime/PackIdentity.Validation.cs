#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using System;
using System.Linq;
using UnityEngine;

namespace Readymade.Persistence
{
    public partial class PackIdentity
    {
        //
        // This part of the class contains debug properties and validations backing various NaughtyAttributes.
        // these properties and methods are ONLY used in the editor, many of them are part of WIP on UX improvements.
        //


        //  [ShowNativeProperty]
        private bool CanClearAssetID => !Application.isPlaying && HasAssetID && IsNotPrefab;

        private bool ValidateAssetID => CanHaveAssetID && HasAssetID && Guid.TryParse(assetID, out _);

        // [ShowNativeProperty]
        private bool IsSpawnableAsset => IsAsset;

        private bool ShouldHaveAssetID => !HasAssetID && IsAsset;

        private bool ShouldHaveEntityID => !HasEntityID && IsNonAssetInstance;

        private bool ShouldNotHaveEntityID => HasEntityID && IsAsset;

        private bool ShouldNotHaveAssetID => HasAssetID && IsNonAssetInstance;

        private bool CanHaveAssetID => IsPrefab;

        private bool CanHaveLifeID => IsNonAssetInstance;

        private bool IsNotPrefab => !IsPrefab;

        private bool CanGenerateAssetId => IsAsset && !IsInStageMode;

        private bool CanClearEntityID => HasEntityID && (IsAsset || IsStageRoot);

        private bool CanGenerateEntityID => IsNonAssetInstance && !IsStageRoot;


        // [ShowNativeProperty]
        private bool IsPartOfPrefab
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject);
#endif
                throw new NotImplementedException();
            }
        }

        private bool IsNonAssetInstance
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsPartOfNonAssetPrefabInstance(gameObject) ||
                    !UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject);
#endif
                throw new NotImplementedException();
            }
        }

        private bool IsPrefab
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject);
#endif
                throw new NotImplementedException();
            }
        }

        private bool IsPartOfScene
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsPartOfNonAssetPrefabInstance(gameObject);
#endif
                throw new NotImplementedException();
            }
        }

        // [ShowNativeProperty]
        private bool IsAsset
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject);
#endif
                throw new NotImplementedException();
            }
        }


        //  [ShowNativeProperty]
        private bool IsInStageMode
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != default;
#endif
                throw new NotImplementedException();
            }
        }

        //  [ShowNativeProperty]
        private bool IsStageRoot
        {
            get
            {
#if UNITY_EDITOR
                return IsInStageMode && transform.parent == null;
#endif
                throw new NotImplementedException();
            }
        }

        //  [ShowNativeProperty]
        private bool IsPrefabRoot
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject);
#endif
                throw new NotImplementedException();
            }
        }
    }
}