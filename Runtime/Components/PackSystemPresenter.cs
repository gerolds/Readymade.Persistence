/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using Cysharp.Threading.Tasks;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Readymade.Utils.Pooling;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = UnityEngine.Random;


namespace Readymade.Persistence
{
    /// <summary>
    /// Controls UI for saving and loading files.
    /// </summary>
    [RequireComponent(typeof(PackSystem))]
    public class PackSystemPresenter : MonoBehaviour
    {
        const string fragments =
            "red green white yellow blue black church fell hammer forge market run fast cushion impact forest wood binary " +
            "morning night dusk haven gloom mid top end head walk flow circus dilly dally dell soft hard hot cold winter " +
            "summer spring river road pine alder sage cherry wood circle round mast ring ramp brook lion sheep dog cat fish " +
            "dawn fest rich poor blot chot tonomy leg hand head foot boot shoe lost found high low shallow narrow deep musk " +
            "coil wool meat hoof toe case casket box boar clam breath voice song buck sow bowel bowl banner braid numb cream " +
            "bulb fire danger coast ankle barb certain basin crater cobalt iron oak copper gold silver zinc steel axe saw nose " +
            "hair claw nail screw pump clutter bath dudley sophia anne bush bury north south east west bitter sweet valley " +
            "ridge field acres lichen itch pea peach peanut on off left right campus camp lord pew shill hall dyke ford long " +
            "short moor ham beef pork hill woods gate york shire ling heat swelt way alley cross crossing worth park cray good " +
            "strand quay burn lady master fools harry frond order file function money loss shuffle upper lower lesser more " +
            "token dead birth new old far near other again charge ram rampart tower bridge raft cane bushel reeds reed adapter " +
            "fan stone rock log tape stripe stripe bear donkey horse eel cod cape arch town ship wreck bust buster folly hope " +
            "faith plate sheet deck rail path bark trim rope mast knot plug switch light dark sign store pen suit card maker " +
            "game play fun bug beetle roach gander goose bitch glow bright beach sand rubble hike pike peak rim edge corner " +
            "axis ear thumb stuck free port shark eye lid cup keg powder mixed bag trunk brake victory glass shard pebble " +
            "drill bit panel bevel chamfer pinion pin hole ravine if no yes bro laser ray beam space void null closure shop " +
            "row column step stair garage car traffic mare signal point index moon amber bion math angle bar crow swallow bird " +
            "king queen prince ruby diamond opal topaz glimmer crystal frag healy heaven hell tomb mouse tiger zebra bull cow " +
            "goat raven eagle falcon dove frank ford lloyd wright khan gabbo shale gist gneiss veld spar granite loam silt " +
            "wand rudine meth coca lulu city bend break snap twitch oar house gone article away bead bearer becket bellow " +
            "bible bilge dart cruise harness hail hook driver lance monkey morse pay pants pitch ream ride flag scrag whale " +
            "scrap scull bone size big lump limp lass whip socket spade shovel pick snub spout there stave steer pole suds " +
            "tack tackle tail fin bord line square thief thrash three two one four the six seven toggle waist helm mussel " +
            "pearl chest ice steam coal coke slag pellet sinter alloy element supply plant blank stare out in bushel true " +
            "false carbon cake reduction what where why how scale descale harden spit duplex loft extra stock feed ferro dip " +
            "ness mill bake turn stile hydra drake jay lance matte oil gas plasma powder dust reline edit grain coarse fine " +
            "coral crust frost creep glacier crawl ground magma marble moraine lagoon lava landslide flow playa pillow soil " +
            "slip stack queue salt sync transport tension thrust ash wave cut wall brick fall rise wonder tell said cob web " +
            "net hinge joint stare stand still slap dash jump ing kennel leash catch swim swelt sweat toil labour work type " +
            "make bake rake sake dance tie jot blot draw plan pinch clamp pull push drag ram stroke strike ball boule fletch " +
            "smith butcher tailor spy secret drown dive claps clutch shift sit stand jump jack quick brown fox badge tag " +
            "shield sword dagger epee rapier bell string wound wind sail cook eat drink tan train lift drown damp fog mist " +
            "roll wheel rim lever level slide glide";

        [InfoBox("Use this component to create a simple save-system & UI around a " + nameof(PackSystem) +
            " component. Any files " +
            "saved to the path configured in the system and any files in a similar path inside StreamingAssets will be " +
            "exposed by this presenter.")]
        /*
        [Tooltip (
            "The file name pattern that will be used to search for files inside StreamingAssets and PersistentDataPath to be displayed by this presenter." )]
        [SerializeField]
        private string searchPattern = "*.pack";
        */
        [Tooltip("A file inside StreamingAssets that will be loaded when " + nameof(QuickStart) + " is called.")]
        [SerializeField]
        private string quickStartFile;

        [BoxGroup("Input")]
        [Tooltip("The input action that will trigger a quick start.")]
        [SerializeField]
        private InputAction quickStartAction = new("Quick Start", InputActionType.Button, "Keyboard/F12");

        [BoxGroup("Input")]
        [Tooltip("The input action that will trigger a quick load.")]
        [SerializeField]
        private InputAction quickLoadAction = new("Quick Load", InputActionType.Button, "Keyboard/F11");

        [Tooltip("The input action that will trigger a quick save.")]
        [BoxGroup("Input")]
        [SerializeField]
        private InputAction quickSaveAction = new("Quick Save", InputActionType.Button, "Keyboard/F5");

        [Tooltip("The input action reference that will trigger a quick start.")]
        [BoxGroup("Input")]
        [SerializeField]
        private InputActionReference quickStartRef;

        [Tooltip("The input action reference that will trigger a quick load.")]
        [BoxGroup("Input")]
        [SerializeField]
        private InputActionReference quickLoadRef;

        [Tooltip("The input action reference that will trigger a quick save.")]
        [BoxGroup("Input")]
        [SerializeField]
        private InputActionReference quickSaveActionRef;

        [BoxGroup("Panel")]
        [Tooltip(
            "The root GameObject of the panel that this presenter controls. Used to open & close it. This should potentially be parent to both the main file selection dialog and the confirmation dialog.")]
        [SerializeField]
        [Required]
        private GameObject panel;

        [Tooltip("The display used by this presenter component.")]
        [BoxGroup("Panel")]
        [SerializeField]
        [Required]
        private PackSystemPanelDisplay display;

        [Tooltip("The prefab to use for instantiating individual file displays.")]
        [BoxGroup("Panel")]
        [SerializeField]
        [Required]
        private PackSystemFileDisplay filePrefab;

        [Tooltip("The prefab to use for instantiating individual file displays.")]
        [BoxGroup("Panel")]
        [SerializeField]
        [Required]
        private PooledPackSystemFileDisplay pooledFileDisplay;

        [BoxGroup("Panel")]
        [FormerlySerializedAs("confirmationDialog")]
        [Tooltip(
            "The presenter to use for displaying a confirmation dialog. While this dialog is active all interaction " +
            "with this system will be disabled.")]
        [SerializeField]
        [Required]
        private ChoicePresenter confirmationPresenter;

        [Tooltip("Whether to close the panel when a load is started.")]
        [BoxGroup("Panel")]
        [SerializeField]
        private bool closeOnLoad;

        [Tooltip("Whether to start with the panel in a closed state.")]
        [BoxGroup("Panel")]
        [SerializeField]
        private bool startClosed;

        [Tooltip("The display to use for showing the progress of a load operation.")]
        [BoxGroup("Progress")]
        [SerializeField]
        [Required]
        private SimpleProgressDisplay loadProgressDisplay;

        [Tooltip("Object to activate to act as a loading screen and/or progress indicator.")]
        [BoxGroup("Progress")]
        [SerializeField]
        private GameObject whileLoading;

        [Tooltip("A button outside the modal dialog to enable quick-saving at any time.")]
        [BoxGroup("Input")]
        [SerializeField]
        private Button quickSaveButton;

        [BoxGroup("Events")] [SerializeField] private UnityEvent onLoadStarted;

        [BoxGroup("Events")] [SerializeField] private UnityEvent onLoadCompleted;

        private static string[] s_tokens;
        private bool _isLoadProcessRunning;
        private PackSystem _system;
        [SerializeField] private float screenDelay = 1f;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            SetVisible(!startClosed);

            if (whileLoading)
            {
                whileLoading.SetActive(false);
            }

            if (display.Filename)
            {
                display.Filename.SetTextWithoutNotify(GenerateRandomName());
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            _system = GetComponent<PackSystem>();
            Debug.Assert(_system != null, "ASSERTION FAILED: _system != null", this);

            if (!_system)
            {
                enabled = false;
                return;
            }

            panel.SetActive(true);
            RefreshCanvas();
            if (quickSaveButton)
            {
                quickSaveButton.onClick.AddListener(SaveClickedHandler);
            }

            if (display.Filename)
            {
                display.Filename.onEndEdit.AddListener(FilenameChangedHandler);
            }

            if (display.GenerateFilenameButton)
            {
                display.GenerateFilenameButton.onClick.AddListener(GenerateRandomFilename);
            }

            // quick start
            if (quickStartRef != null)
            {
                quickStartAction = quickStartRef;
            }

            if (quickStartAction != null)
            {
                quickStartAction.performed += QuickStartHandler;
            }

            // quick load
            if (quickLoadRef != null)
            {
                quickLoadAction = quickLoadRef;
            }

            if (quickLoadAction != null)
            {
                quickLoadAction.performed += QuickLoadHandler;
            }

            // quick save
            if (quickSaveActionRef)
            {
                quickSaveAction = quickSaveActionRef.action;
            }

            if (quickSaveAction != null)
            {
                quickSaveAction.performed += QuickSaveActionHandler;
            }

            GetComponent<PackSystem>().Saved += SavedHandler;
            GetComponent<PackSystem>().Loaded += LoadedHandler;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable()
        {
            if (panel)
            {
                panel.SetActive(false);
            }

            if (quickSaveButton)
            {
                quickSaveButton.onClick.RemoveListener(SaveClickedHandler);
            }

            if (display.Filename)
            {
                display.Filename.onEndEdit.RemoveListener(FilenameChangedHandler);
            }

            if (quickLoadAction != null)
            {
                quickLoadAction.performed -= QuickLoadHandler;
            }

            if (quickStartAction != null)
            {
                quickStartAction.performed -= QuickStartHandler;
            }

            if (quickSaveAction != null)
            {
                quickSaveAction.performed -= QuickSaveActionHandler;
            }

            GetComponent<PackSystem>().Saved -= SavedHandler;
        }

        /// <summary>
        /// Refreshes the display of the file list. Destroying all existing displays and creating new ones.  
        /// </summary>
        public void RefreshCanvas(bool force = true)
        {
            if (!panel.activeInHierarchy && !force)
            {
                return;
            }

            for (int i = display.FileContainer.transform.childCount - 1; i >= 0; i--)
            {
                pooledFileDisplay
                    .GetPool()
                    .Release(display.FileContainer.transform.GetChild(i).GetComponent<PooledInstance>());
            }

            IEnumerable<FileInfo> files = GetFilesInOrder();
            foreach (FileInfo file in files)
            {
                pooledFileDisplay.TryGetInstance(out PackSystemFileDisplay fileDisplay);
                fileDisplay.transform.SetParent(display.FileContainer.transform);
                fileDisplay.transform.localScale = Vector3.one;
                TimeSpan fileAge = DateTime.Now - file.LastWriteTime;
                string agoString = GetAgoString(fileAge);
                bool isProtected = CheckProtectedPath(file);
                if (fileDisplay.age)
                {
                    fileDisplay.age.text = agoString;
                }

                if (fileDisplay.date)
                {
                    fileDisplay.date.text = file.LastWriteTime.ToString("yyyy-MM-dd");
                }

                if (fileDisplay.filename)
                {
                    fileDisplay.filename.text = Path.GetFileNameWithoutExtension(file.FullName);
                }

                if (fileDisplay.path)
                {
                    fileDisplay.path.text = file.FullName;
                }

                if (fileDisplay.size)
                {
                    fileDisplay.size.text = ConvertToHumanFileSize(file.Length);
                }

                if (fileDisplay.loadButton)
                {
                    fileDisplay.loadButton.onClick.AddListener(() =>
                    {
                        if (closeOnLoad)
                        {
                            SetVisible(false);
                        }

                        LoadHandlerAsync(file.FullName, true).Forget();
                    });
                }

                if (fileDisplay.overwriteButton)
                {
                    if (!isProtected)
                    {
                        fileDisplay.overwriteButton.onClick.AddListener(() =>
                        {
                            SaveHandlerAsync(file.FullName, true).Forget();
                        });
                        fileDisplay.overwriteButton.interactable = true;
                        if (fileDisplay.overwriteGroup)
                        {
                            fileDisplay.overwriteGroup.interactable = true;
                            fileDisplay.overwriteGroup.alpha = 1.0f;
                        }
                    }
                    else
                    {
                        fileDisplay.overwriteButton.interactable = false;
                        if (fileDisplay.overwriteGroup)
                        {
                            fileDisplay.overwriteGroup.interactable = false;
                            fileDisplay.overwriteGroup.alpha = 0.0f;
                        }
                    }
                }

                if (fileDisplay.deleteButton)
                {
                    if (!isProtected)
                    {
                        fileDisplay.deleteButton.onClick.AddListener(() =>
                        {
                            DeleteHandlerAsync(file.FullName, true).Forget();
                        });
                        fileDisplay.deleteButton.interactable = true;
                        if (fileDisplay.deleteGroup)
                        {
                            fileDisplay.deleteGroup.interactable = true;
                            fileDisplay.deleteGroup.alpha = 1.0f;
                        }
                    }
                    else
                    {
                        fileDisplay.deleteButton.interactable = false;
                        if (fileDisplay.deleteGroup)
                        {
                            fileDisplay.deleteGroup.interactable = false;
                            fileDisplay.deleteGroup.alpha = 0.0f;
                        }
                    }
                }

                if (fileDisplay.protectedGroup)
                {
                    fileDisplay.protectedGroup.alpha = isProtected ? 1f : 0f;
                }
            }
        }

        /// <summary>
        /// Called when the user clicks the delete button.
        /// </summary>
        /// <param name="path">The path to the file to delete.</param>
        /// <param name="requireConfirmation">Whether to show a confirmation dialog.</param>
        private async UniTask DeleteHandlerAsync(string path, bool requireConfirmation = true)
        {
            if (requireConfirmation)
            {
                if (await confirmationPresenter.TryPrompt(
                    $"Delete File",
                    $"Are you sure? The selected file {Path.GetFileName(path)} will be deleted.",
                    0, 0, "Delete", "Cancel") is not 0)
                {
                    return;
                }
            }

            _system.DB.DeleteFile(path);
            RefreshCanvas();
        }

        /// <summary>
        /// Called when the user clicks the save button.
        /// </summary>
        /// <param name="path">The path to the file to save to.</param>
        /// <param name="requireConfirmation">Whether to show a confirmation dialog.</param>
        private async UniTask SaveHandlerAsync(string path, bool requireConfirmation = false)
        {
            if (requireConfirmation)
            {
                if (await confirmationPresenter.TryPrompt(
                    $"Overwrite File",
                    "Are you sure? The selected file will be overwritten.",
                    0, 0, "Overwrite", "Cancel") is not 0)
                {
                    return;
                }
            }

            await _system.SaveAsync(path);
            RefreshCanvas();
        }

        /// <summary>
        /// Called when the user clicks the load button.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="requireConfirmation">Whether to show a confirmation dialog.</param>
        private async UniTaskVoid LoadHandlerAsync(string path, bool requireConfirmation = true)
        {
            PackSystem system = GetComponent<PackSystem>();
            if (_isLoadProcessRunning)
            {
                Debug.LogWarning(
                    $"{nameof(PackSystemPresenter)} Another load process is already running. Command ignored.");
                return;
            }

            if (requireConfirmation)
            {
                if (await confirmationPresenter.TryPrompt(
                    $"Load File",
                    "Are you sure? All progress will be lost.",
                    0, 0, "Load", "Cancel") is not 0)
                {
                    return;
                }
            }

            _isLoadProcessRunning = true;
            if (whileLoading)
            {
                whileLoading.SetActive(_isLoadProcessRunning);
            }

            try
            {
                onLoadStarted.Invoke();
                await UniTask.NextFrame();

                if (loadProgressDisplay.Fill)
                {
                    loadProgressDisplay.Fill.fillAmount = 0;
                    Progress<float> progressObserver = new();
                    progressObserver.ProgressChanged += (_, progress) => loadProgressDisplay.Fill.fillAmount = progress;
                    await system.RestoreAsync(path, progressObserver);
                }
                else
                {
                    await system.RestoreAsync(path);
                }

                await UniTask.NextFrame();
                onLoadCompleted.Invoke();
                await UniTask.Delay(TimeSpan.FromSeconds(screenDelay));
            }
            finally
            {
                _isLoadProcessRunning = false;
                if (whileLoading)
                    whileLoading.SetActive(_isLoadProcessRunning);
            }
        }

        /// <summary>
        /// Check whether the given file is in a protected path.
        /// </summary>
        /// <param name="file">The file to check.</param>
        /// <returns>Whether the file is in a protected path.</returns>
        private bool CheckProtectedPath(FileInfo file) =>
            file.Directory.FullName.Replace('\\', '/').ToLower().Contains("streamingassets");


        /// <summary>
        /// Generates a random filename.
        /// </summary>
        public void GenerateRandomFilename()
        {
            if (CheckModality())
            {
                return;
            }

            if (display.Filename)
            {
                display.Filename.SetTextWithoutNotify(GenerateRandomName());
            }
        }

        /// <summary>
        /// Generates a random name.
        /// </summary>
        /// <returns>A random name.</returns>
        public string GenerateRandomName(bool fastMode = true)
        {
            s_tokens ??= fragments.Split(" ");
            if (fastMode)
            {
                int rngA = Random.Range(0, s_tokens.Length);
                int rngB = Random.Range(0, s_tokens.Length);
                return char.ToUpper(s_tokens[rngA][0]) + s_tokens[rngA][1..] + s_tokens[rngB];
            }
            else
            {
                Shuffle(s_tokens);
                return char.ToUpper(s_tokens[0][0]) + s_tokens[0][1..] + s_tokens[1];
            }
        }

        /// <summary>
        /// Shuffles the given array in-place. Uses fisher-yates algorithm.
        /// </summary>
        /// <param name="array">The array to shuffle.</param>
        /// <typeparam name="T">The type of the array.</typeparam>
        public static void Shuffle<T>(T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = Random.Range(0, n--);
                (array[n], array[k]) = (array[k], array[n]);
            }
        }

        /// <summary>
        /// Shuffles the given array in-place. Uses fisher-yates algorithm.
        /// </summary>
        /// <param name="rng">The random number generator to use.</param>
        /// <param name="array">The array to shuffle.</param>
        /// <typeparam name="T">The type of the array.</typeparam>
        public static void Shuffle<T>(System.Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                (array[n], array[k]) = (array[k], array[n]);
            }
        }

        /// <summary>
        /// Retrieves all files in the configured search paths in descending order of last write time (newest first).
        /// </summary>
        /// <returns>All files in the configured search paths in descending order of last write time (newest first).</returns>
        public IEnumerable<FileInfo> GetFilesInOrder()
        {
            PackSystem system = GetComponent<PackSystem>();
            string userPath = system.DefaultFolderPath;
            string buildPath = Path.Combine(Application.streamingAssetsPath, system.FolderName);
            Debug.Log($"[{nameof(PackSystemPresenter)}] Getting files from {userPath}");
            Debug.Log($"[{nameof(PackSystemPresenter)}] Getting files from {buildPath}");
            return Enumerable
                .Empty<FileInfo>()
                .Concat(Directory.Exists(userPath)
                    ? new DirectoryInfo(userPath).EnumerateFiles($"*{system.FileExtension}")
                    : Enumerable.Empty<FileInfo>()
                )
                .Concat(Directory.Exists(buildPath)
                    ? new DirectoryInfo(buildPath).EnumerateFiles($"*{system.FileExtension}")
                    : Enumerable.Empty<FileInfo>())
                .OrderByDescending(it => it.LastWriteTimeUtc);
        }

        /// <summary>
        /// Called the progress information of a load operation changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="progress">The progress of the load operation.</param>
        private void ProgressChangedHandler(object sender, float progress)
        {
            if (loadProgressDisplay.Fill != null)
                loadProgressDisplay.Fill.fillAmount = progress;
        }

        /// <summary>
        /// Converts the given number of bytes to a human-readable string.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <returns>The human-readable string.</returns>
        private string ConvertToHumanFileSize(long bytes)
        {
            int orderSize = 1024;
            if (Mathf.Abs(bytes) < orderSize)
                return $"{bytes} B";

            float fraction = bytes;
            string[] units = { "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            int unitIndex = -1;

            do
            {
                fraction /= orderSize;
                ++unitIndex;
            } while (Math.Abs(fraction) >= orderSize && unitIndex < units.Length - 1);


            return $"{fraction:F0} {units[unitIndex]}";
        }

        /// <summary>
        /// Constructs a human-readable string describing the relative age of the given timespan.
        /// </summary>
        /// <param name="relativeAge">The timespan to describe.</param>
        /// <returns>the relative age of the given timespan.</returns>
        private static string GetAgoString(TimeSpan relativeAge)
        {
            return relativeAge.TotalSeconds switch
            {
                <= 60 => $"{relativeAge.Seconds} seconds ago",

                _ => relativeAge.TotalMinutes switch
                {
                    <= 1 => "about a minute ago",
                    < 60 => $"about {relativeAge.Minutes} minutes ago",
                    _ => relativeAge.TotalHours switch
                    {
                        <= 1 => "about an hour ago",
                        < 24 => $"about {relativeAge.Hours} hours ago",
                        _ => relativeAge.TotalDays switch
                        {
                            <= 1 => "yesterday",
                            <= 30 => $"about {relativeAge.Days} days ago",

                            <= 60 => "about a month ago",
                            < 365 => $"about {relativeAge.Days / 30} months ago",

                            <= 365 * 2 => "about a year ago",
                            _ => $"about {relativeAge.Days / 365} years ago"
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Sets the visibility of the panel and refreshes the file list.
        /// </summary>
        /// <param name="isVisible">Whether the panel should be visible.</param>
        public void SetVisible(bool isVisible)
        {
            if (CheckModality())
            {
                return;
            }

            if (isVisible)
            {
                RefreshCanvas();
                panel.SetActive(true);
            }
            else
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// Checks whether a modal dialog is active and logs a warning if so. This is a local modality check only.
        /// </summary>
        /// <returns>Whether a modal dialog is active.</returns>
        private bool CheckModality()
        {
            if (confirmationPresenter && confirmationPresenter.IsLocked)
            {
                Debug.LogWarning(
                    $"{nameof(PackSystemPresenter)} {nameof(SetVisible)} blocked due to modal dialog being active.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when the quick start action is triggered.
        /// </summary>
        /// <param name="ctx">The input action context that triggered the callback.</param>
        private void QuickStartHandler(InputAction.CallbackContext ctx)
        {
            if (!ctx.action.WasPressedThisFrame())
            {
                return;
            }

            QuickStart();
        }

        /// <summary>
        /// Immediately load the configured quickstart file.
        /// </summary>
        public void QuickStart()
        {
            if (CheckModality())
            {
                return;
            }

            string path = Path.Combine(Application.streamingAssetsPath, _system.FolderName, quickStartFile);
            if (!File.Exists(path))
            {
                Debug.LogError($"[{nameof(PackSystemPresenter)}] Default file {path} does not exist");
                return;
            }

            LoadHandlerAsync(path, false).Forget();
        }

        /// <summary>
        /// Immediately load the most recent save file.
        /// </summary>
        public void QuickLoad()
        {
            if (CheckModality())
            {
                return;
            }

            FileInfo file = GetFilesInOrder().FirstOrDefault();
            if (file == null)
            {
                Debug.LogWarning($"[{nameof(PackSystemPresenter)}] No save file available to load yet.");
                return;
            }

            LoadHandlerAsync(file.FullName, false).Forget();
        }

        /// <summary>
        /// Load the state at the given path.
        /// </summary>
        /// <param name="path"></param>
        public void Load(string path)
        {
            if (CheckModality())
            {
                return;
            }

            if (!File.Exists(path))
            {
                Debug.LogError($"[{nameof(PackSystemPresenter)}] Default file {path} does not exist");
                return;
            }

            LoadHandlerAsync(path, false).Forget();
        }

        /// <summary>
        /// Called when the user triggers the quick load action.
        /// </summary>
        /// <param name="ctx">The input action context that triggered the callback.</param>
        private void QuickLoadHandler(InputAction.CallbackContext ctx)
        {
            if (!ctx.action.WasPressedThisFrame())
            {
                return;
            }

            QuickLoad();
        }

        /// <summary>
        /// Called when the associated <see cref="PackSystem"/> has restored its state from persistent storage.
        /// </summary>
        private void LoadedHandler(string path)
        {
            if (display.Filename)
            {
                display.Filename.SetTextWithoutNotify(
                    PackSystem.GetBaseFilename(Path.GetFileNameWithoutExtension(path)));
            }
        }

        /// <summary>
        /// Called when the associated <see cref="PackSystem"/> has committed its current state to persistent storage.
        /// </summary>
        private void SavedHandler() => RefreshCanvas();

        /// <summary>
        /// Called when the user clicks the quick save button.
        /// </summary>
        /// <param name="ctx">The input action context that triggered the callback.</param>
        private void QuickSaveActionHandler(InputAction.CallbackContext ctx)
        {
            if (ctx.action.WasPressedThisFrame())
            {
                SaveClickedHandler();
            }
        }

        /// <summary>Called when the user changes the filename.</summary>
        /// <param name="value">The new value of the filename.</param>
        private void FilenameChangedHandler(string value)
        {
            if (display.Filename)
            {
                display.Filename.SetTextWithoutNotify(PackSystem.GetBaseFilename(value));
            }
        }

        /// <summary>
        /// Called when the user clicks the save button.
        /// </summary>
        private void SaveClickedHandler() => QuickSave();

        /// <summary>
        /// Immediately save the current state of the system.
        /// </summary>
        private void QuickSave()
        {
            if (display.Filename && !string.IsNullOrWhiteSpace(display.Filename.text))
            {
                Debug.Log($"[{nameof(PackSystemPresenter)}] Quick-saving to {display.Filename.text}");
                _system.SaveVersionAsync(display.Filename.text).Forget();
            }
            else
            {
                Debug.Log($"[{nameof(PackSystemPresenter)}] Quick-saving.");
                _system.SaveAsync().Forget();
            }
        }
    }
}