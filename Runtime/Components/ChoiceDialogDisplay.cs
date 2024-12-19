using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Persistence {
    public class ChoiceDialogDisplay : MonoBehaviour {
        [SerializeField]
        [Tooltip ( "The parent under which the choice buttons are instantiated." )]
        private LayoutGroup layout;

        [SerializeField]
        [Tooltip ( "A text component to display the prompt message." )]
        private TMP_Text message;

        [SerializeField]
        [Tooltip ( "A text component to display the title of the dialog." )]
        private TMP_Text title;

        [SerializeField]
        [Tooltip (
            "The interactable scope that will be enabled while a choice is presented. Typically this is the dialog panel." )]
        private CanvasGroup dialogGroup;

        [SerializeField]
        [Tooltip ( "An image to display a timer via its fill property." )]
        private Image fill;

        /// <summary>
        /// The parent under which the choice buttons are instantiated.
        /// </summary>
        public LayoutGroup Layout => layout;

        /// <summary>
        /// The text component to display the prompt message.
        /// </summary>
        public TMP_Text Message => message;

        /// <summary>
        /// The text component to display the title of the dialog.
        /// </summary>
        public TMP_Text Title => title;

        /// <summary>
        /// The interactable scope that will be enabled while a choice is presented. Typically this is the dialog panel.
        /// </summary>
        public CanvasGroup DialogGroup => dialogGroup;

        /// <summary>
        /// the image to display a timer via its fill property.
        /// </summary>
        public Image Fill => fill;
    }
}