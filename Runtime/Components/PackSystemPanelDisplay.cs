#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Persistence {
    /// <summary>
    /// Holds components that facilitate the display of a <see cref="PackSystemPresenter"/>.
    /// </summary>
    public class PackSystemPanelDisplay : MonoBehaviour {
        /// <summary>
        /// A button that generates a filename.
        /// </summary>
        [Tooltip ( "A button that generates a filename." )]
        [BoxGroup ( "Input" )]
        [SerializeField]
        [Required]
        private Button generateFilenameButton;

        /// <summary>
        /// The filename input field. Will be populated by events invoked by <see cref="generateFilenameButton"/>.
        /// </summary>
        [Tooltip ( "The filename input field. Will be populated by events invoked by " + nameof ( generateFilenameButton ) +
                   "." )]
        [BoxGroup ( "Input" )]
        [SerializeField]
        [Required]
        private TMP_InputField filename;

        /// <summary>
        /// The container that will hold the <see cref="PackSystemFileDisplay"/> instances.
        /// </summary>
        [Tooltip ( "The container that will hold the " + nameof ( PackSystemFileDisplay ) + " instances." )]
        [BoxGroup ( "Panel" )]
        [SerializeField]
        [Required]
        private LayoutGroup fileContainer;
        
        /// <summary>
        /// the filename input field.
        /// </summary>
        public TMP_InputField Filename => filename;

        /// <summary>
        /// the generate filename button.
        /// </summary>
        public Button GenerateFilenameButton => generateFilenameButton;

        /// <summary>
        /// The container that can hold the <see cref="PackSystemFileDisplay"/> instances.
        /// </summary>
        public LayoutGroup FileContainer => fileContainer;
    }
}