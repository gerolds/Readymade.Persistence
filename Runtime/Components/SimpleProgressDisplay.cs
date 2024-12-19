using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Persistence {
    /// <summary>
    /// Components that facilitate the display an operation's progress.
    /// </summary>
    public class SimpleProgressDisplay : MonoBehaviour {
        [Tooltip("The image that will be used to display progress through its fill amount.")]
        [SerializeField]
        private Image fill;

        [SerializeField] private TMP_Text info;
        
        /// <summary>
        /// The image that can be used to display progress through its fill amount.
        /// </summary>
        public Image Fill => fill;

        public TMP_Text Info => info; }
}