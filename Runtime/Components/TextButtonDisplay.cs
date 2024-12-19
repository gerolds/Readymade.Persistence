using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Persistence {
    /// <summary>
    /// Components that display a button with a text label.
    /// </summary>
    public class TextButtonDisplay : MonoBehaviour {
        /// <summary>
        /// The button.
        /// </summary>
        [SerializeField]
        private Button button;

        /// <summary>
        /// The component for the button's label.
        /// </summary>
        [SerializeField]
        private TMP_Text label;

        /// <summary>
        /// A game object that will be enabled when the button is designated as the default button.
        /// </summary>
        [SerializeField]
        private GameObject defaultMarker;

        /// <summary>
        /// The button.
        /// </summary>
        public Button Button => button;

        /// <summary>
        /// The component for the button's label.
        /// </summary>
        public TMP_Text Label => label;

        /// <summary>
        /// The game object that can be enabled when the button is designated as the default button.
        /// </summary>
        public GameObject DefaultMarker => defaultMarker;
    }
}