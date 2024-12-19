#if true
// this is example code, included here for static analysis
using MessagePack;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Readymade.Persistence {
    // save- & restorable
    public class ValueComponentExample : MonoBehaviour, IPackableComponent {
        [Serializable]
        [JsonObject]
        [MessagePackObject]
        public class ValueComponentState {
            public float Value;
        }

        private ValueComponentState _state;

        [SerializeField]
        private string key = Guid.NewGuid ().ToString ();

        [Button]
        private void NewKey () => key = Guid.NewGuid ().ToString ();

        string IPackableComponent.ComponentKey => key;

        Type IPackableComponent.PackType => typeof ( ValueComponentState );

        void IPackableComponent.Unpack ( object args, AssetLookup lookup ) => _state = ( ValueComponentState ) args;

        object IPackableComponent.Pack () => _state;
    }

    // not save- & restorable
    public class ValueComponentPlain : MonoBehaviour {
        private float _state;
    }
}
#endif