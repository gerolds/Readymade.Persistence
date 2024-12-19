using Cysharp.Threading.Tasks;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
#endif
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Readymade.Persistence {
    public class ChoicePresenter : MonoBehaviour {
        /// <summary>
        /// Represents the arguments for a prompt.
        /// </summary>
        [Serializable]
        public struct PromptArgs {
            /// <summary>
            /// The title of the prompt.
            /// </summary>
            public string title;

            /// <summary>
            /// The message to display to the user.
            /// </summary>
            public string prompt;

            /// <summary>
            /// The timeout after which the default choice gets confirmed.
            /// </summary>
            public float duration;

            /// <summary>
            /// The index of the default choice. Pass an out of range value (e.g. -1) if you don't want to display a default choice. The latter also disables the timeout.
            /// </summary>
            public int defaultChoice;

            /// <summary>
            /// The options that the user can chose from. The return value will be the index of the chosen option.
            /// </summary>
            public string[] options;
        }

        /// <summary>
        /// Concrete implementation of <see cref="UnityEvent{T0}"/> that can be used to invoke events with a choice index.
        /// </summary>
        [Serializable]
        public class ChoiceUnityEvent : UnityEvent<int> {
        }

        [InfoBox ( "Use this component to display a prompt that gives the user a number of options to choose from. Use for " +
                   "confirmation dialogs or any kind of branch selection." )]
        [Tooltip ( "The display to use for showing this components dialog." )]
        [SerializeField]
        private ChoiceDialogDisplay display;

        [SerializeField]
        [Tooltip ( "The prefab to use for instantiating choice buttons." )]
        private TextButtonDisplay buttonPrefab;

        [FormerlySerializedAs ( "parentGroup" )]
        [SerializeField]
        [Tooltip (
            "The (optional) interactable scope that will be disabled while a choice is presented. Typically this is the UI subtree that originated the prompt." )]
        private CanvasGroup modalGroup;

        /// <summary>
        /// Called when the prompt is shown.
        /// </summary>
        [SerializeField]
        private UnityEvent onShow;

        /// <summary>
        /// Called when a choice was made.
        /// </summary>
        [SerializeField]
        private ChoiceUnityEvent onSelected;

        [BoxGroup ( "Default Configuration" )]
        [InfoBox ( "The configuration of the prompt that will be shown when calling " + nameof ( ShowDefaultPrompt ) + ". " +
                   "This is for prototyping use only. Intended use of this component is for it to be called from scripts that " +
                   "pass in all required configuration for the prompt." )]
        [SerializeField]
        private PromptArgs defaultArgs;

        private UniTaskCompletionSource<int> _completion;
        private bool _locked;

        /// <summary>
        /// Whether this component is currently prompting the user for a choice. While true no further prompts can be made
        /// with this component.
        /// </summary>
        public bool IsLocked => _locked;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start () {
            Hide ();
        }

        /// <summary>
        /// Hide the prompt.
        /// </summary>
        private void Hide () {
            display.DialogGroup.interactable = false;
            display.DialogGroup.blocksRaycasts = false;
            display.DialogGroup.alpha = 0f;
        }

        /// <summary>
        /// Show the prompt.
        /// </summary>
        private void Show () {
            display.DialogGroup.interactable = true;
            display.DialogGroup.blocksRaycasts = true;
            display.DialogGroup.alpha = 1f;
            onShow.Invoke ();
        }

        /// <summary>
        /// Cancels the prompt.
        /// </summary>
        [Button]
        public void CancelPrompt () {
            _completion?.TrySetCanceled ();
        }

        /// <summary>
        /// Use <see cref="TryPrompt"/> if possible. Displays a dialog to a user that forces them to make a choice. This
        /// overload is intended to be called from UnityEvents and used only in prototyping.
        /// </summary>
        /// <remarks>
        /// Do not use this overload from code: Note that this method does not block, have arguments and return any value.
        /// The arguments and choice must be given/obtained in the editor-exposed properties.</remarks>
        [Button]
        public void ShowDefaultPrompt () {
            TryPrompt (
                defaultArgs.title,
                defaultArgs.prompt,
                defaultArgs.duration,
                defaultArgs.defaultChoice,
                defaultArgs.options
            ).Forget ();
        }

        /// <summary>
        /// Displays a dialog to a user that forces them to make a choice. The options can be passed as arguments.
        /// </summary>
        /// <param name="title">A title for the displayed dialog.</param>
        /// <param name="prompt">The message to display to the user.</param>
        /// <param name="duration">A timeout after which the default choice gets confirmed.</param>
        /// <param name="defaultChoice">The index of the default choice. Pass an out of range value (e.g. -1) if you don't want to display a default choice. The latter also disables the timeout.</param>
        /// <param name="options">An array of options that the user can chose from. The return value will be the index of the chosen option.</param>
        /// <returns>The index of the chosen option. Null if no choice was made or the prompt failed for another reason (contention).</returns>
        /// <exception cref="ArgumentException">When no options are given.</exception>
        /// <remarks>
        /// This method does not guarantee any kind of modality besides that it will accept only one choice at
        /// a time and block any subsequent ones. Blocking IO or responses of other systems must be handled separately.
        /// </remarks>
        public async UniTask<int?> TryPrompt (
            string title,
            string prompt,
            float duration = 0,
            int defaultChoice = 0,
            params string[] options
        ) {
            if ( _locked ) {
                return null;
            }

            try {
                _locked = true;
                if ( options.Length == 0 ) {
                    throw new ArgumentException ( "At least one choice must be given", nameof ( options ) );
                }

                bool isValidDefaultChoice = defaultChoice >= 0 && defaultChoice <= options.Length;
                TextButtonDisplay[] choiceDisplays = new TextButtonDisplay[options.Length];

                _completion?.TrySetCanceled ();
                _completion = new ();
                display.Message.text = prompt;
                display.Title.text = title;
                if ( modalGroup ) {
                    modalGroup.interactable = false;
                }

                Show ();
                for ( int i = 0; i < options.Length; i++ ) {
                    choiceDisplays[ i ] = Instantiate ( buttonPrefab, display.Layout.transform );
                    choiceDisplays[ i ].Label.text = options[ i ];
                    int iCopy = i;
                    choiceDisplays[ i ].Button.onClick.AddListener ( () => _completion.TrySetResult ( iCopy ) );
                    choiceDisplays[ i ].DefaultMarker.SetActive ( i == defaultChoice );
                    if ( i == defaultChoice ) {
                        choiceDisplays[ i ].Button.Select ();
                    }
                }

                CancellationTokenSource cts = new ();
                if ( isValidDefaultChoice && duration > 0 ) {
                    UniTask.Void ( async cancellationToken => {
                        float started = Time.time;
                        while ( true ) {
                            await UniTask.NextFrame ();
                            if ( cancellationToken.IsCancellationRequested ) {
                                return;
                            }

                            float t = Mathf.Clamp01 ( ( Time.time - started ) / duration );
                            display.Fill.fillAmount = 1f - t;
                            if ( t >= 1f ) {
                                _completion.TrySetResult ( defaultChoice );
                                break;
                            }
                        }
                    }, cts.Token );
                }

                int choice = await _completion.Task;
                cts.Cancel ();
                display.Message.text = string.Empty;

                foreach ( TextButtonDisplay choiceDisplay in choiceDisplays ) {
                    choiceDisplay.Button.interactable = false;
                }

                foreach ( TextButtonDisplay choiceDisplay in choiceDisplays ) {
                    Destroy ( choiceDisplay.gameObject );
                }

                onSelected.Invoke ( choice );
                return choice;
            }
            finally {
                _locked = false;
                if ( modalGroup ) {
                    modalGroup.interactable = true;
                }

                Hide ();
            }
        }
    }
}