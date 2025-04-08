using Cysharp.Threading.Tasks;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Readymade.Persistence.Components;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Readymade.Persistence
{
    /// <summary>
    /// Declares and manages a <see cref="PackDB"/> instance and facilitates saving of scoped GameObjects and their
    /// component instances as well as ScriptableObject asset instances. Primarily designed to write and restore game
    /// state to/from save-files. The system does not impose any structure on whatever architecture that game state has
    /// and should support anything from state distributed into components to a centralized master-object
    /// with custom pack/unpack scripting. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// If scopes are undefined, the state of all loaded <see cref="PackIdentity"/> instances and their descendants
    /// is saved.
    /// </para>
    /// <para>Note: This system is designed to work via explicit implementation of interfaces and adding identifier-
    /// components (i.e. <see cref="PackIdentity"/>) to GameObjects. It does not rely on custom attributes, automatic
    /// instance-ID tracking or reflection. Instead, everything is exposed explicitly to the user. This way the system
    /// remains decoupled from whatever backend it uses for serialization and makes packing, especially the
    /// interference with standard unity object lifecycles, very explicit, discoverable and locally modifiable. It is
    /// left up to the user to optionally use the serialization attributes provided by the <see cref="PackDB"/>
    /// backends for their custom packing implementations.
    /// </para>
    /// <para>
    /// By default it is left to the user to manage object references in their packed data themselves during un-/packing.
    /// The recommended approach here is to rely on the ID-based object lookup provided statically by
    /// <see cref="PackIdentity"/> and implementing <see cref="IWhenAllUnpackedHandler"/> on any
    /// <see cref="IPackableComponent"/> that needs to resolve serialized IDs back into object references.
    /// <see cref="PackHelper"/> provide various convenience methods to do so.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>
    /// To use put a <see cref="PackSystem"/> on any GameObject in a scene. Put a <see cref="PackIdentity"/>
    /// on any GameObject, call <see cref="Pack()"/> to capture the current state into the default configuration (a
    /// file called <c>db.pack</c> inside persistent data. Followed by a later call to <see cref="RestoreLatest"/> to
    /// restore the previous state.
    /// </para>
    /// <para>
    /// Extend the system by implementing <see cref="IPackableComponent"/> or deriving <see cref="PackableComponent"/>
    /// on any component of a <see cref="PackIdentity"/>. See <see cref="PackableTransform"/> for an example.
    /// <see cref="IPackableComponent"/> can also be used to create a fully custom data structure for storing state.  
    /// </para>
    /// <para>
    /// <see cref="PackSystemPresenter"/> is provided to create a basic save/load UI for multiple save files. It also
    /// serves as an example for how this system can be used.
    /// </para>
    /// </example>
    [SelectionBase]
    public class PackSystem : MonoBehaviour, IPackSystem
    {
        public const string SCOPE_PREFIX = "scope:";
        public const string SCENE_PREFIX = "scene:";
        const string PackedGameObjects = "__GameObject_Keys";
        const string PackedComponents = "__Component_Keys";
        const string PackedScriptableObjects = "__Object_Keys";
        const string ContextKey = "__Context";
        private PackDB _db;


        [ValidateInput(nameof(ConformDefaultFileName))]
        [SerializeField]
        [InfoBox(
            "Put this component into a scene to be able to save and restore any GameObjects that have a PackIdentity " +
            "component and any IPackableComponent instances on any of their children."
#if ODIN_INSPECTOR
            , InfoMessageType.None
#endif
        )]
        private string defaultFileName = "db";

        [SerializeField]
        [ValidateInput(nameof(ConformFileExtension))]
        private string fileExtension = ".pack";

        [SerializeField]
        [ValidateInput(nameof(ConformFolderName), "Must be a valid relative path.")]
        private string folderName = "saves";

        [Tooltip(
            "Whether to throw exceptions instead of logging errors when exceptions are found in user-supplied packing procedures.")]
        [SerializeField]
        private bool isPedantic = true;

        [BoxGroup("Limited Scope")]
        [SerializeField]
        [Tooltip(
            "Whether to limit the scope of what is packed and stored persistently. When disabled (the default) all packable objects in the scene will be included.")]
        private bool limitScope = false;

        [BoxGroup("Limited Scope")]
        [ShowIf(nameof(limitScope))]
        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [ValidateInput(nameof(ValidateScopes),
            "Some scopes are not static " + nameof(PackIdentity) + " components.")]
        [Tooltip("Objects that are children of these root nodes will be packed/restored.")]
        private PackIdentity[] scopes;

        [BoxGroup("Objects")]
        [SerializeField]
        [ValidateInput(nameof(ValidateSOs), "Some objects do not implement IPackableObject.")]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [Tooltip("A set of objects to include in packing. They must implement IPackableObject.")]
        private ScriptableObject[] scriptableObjects;

        private bool ValidateSOs(ScriptableObject[] items) =>
            items?.Length == 0 || (items?.All(it => it is IPackableScriptableObject) ?? true);

        [BoxGroup("Clear Scene")]
        [Tooltip(
            "Whether to clear the scene before restoring objects from a saved state. When OFF, existing objects will be " +
            "updated with the packed values loaded from disk. Missing objects will be instantiated where possible.")]
        [SerializeField]
        private bool clearBeforeRestore;

        [FormerlySerializedAs("clearUnder")]
        [BoxGroup("Clear Scene")]
        [SerializeField]
        [ShowIf(nameof(clearBeforeRestore))]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [Tooltip("As part of any restore procedure clear all objects under these transforms.")]
        private Transform[] destroyChildrenOnRestore;

        [FormerlySerializedAs("autoRestore")]
        [FormerlySerializedAs("restoreOnLoad")]
        [BoxGroup("Limited Scope")]
        [Tooltip("Whether to unpack objects in scenes from the current database as they get loaded.")]
        [SerializeField]
        private bool autoUnpack = true;

        /// <summary>
        /// Validates <see cref="scopes"/>. Used to display warning messages in the editor.
        /// </summary>
        private bool ValidateScopes(PackIdentity[] value) =>
            !limitScope || value == null || value.All(it => it == null || it.HasAssetID);

        [FormerlySerializedAs("prefabLookup")]
        [SerializeField]
        [Tooltip(
            "The prefab lookup to use for discovering any prefabs that might need spawning as part of a load operation.")]
        private AssetLookup assetLookup;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called whenever a save process ws completed by this system.")]
        private UnityEvent onSaved;

        [BoxGroup("Events")]
        [SerializeField]
        [Tooltip("Called whenever a load process ws completed by this system.")]
        private UnityEvent onLoaded;

        [SerializeField]
        [Tooltip("Whether to log debug messages.")]
        private bool debug = false;

        [BoxGroup("Automation")]
        [SerializeField]
        [Tooltip("Whether to automatically save to a new file if no name is specified.")]
        private bool saveVersions = true;

        [Tooltip(
            "Whether to automatically save versions in a given interval to the most recently restored or saved database.")]
        [BoxGroup("Automation")]
        [ShowIf(nameof(saveVersions))]
        [SerializeField]
        private bool autoSave = false;

        [BoxGroup("Automation")]
        [SerializeField]
        [ShowIf(nameof(autoSave), nameof(saveVersions))]
        [MinValue(1)]
        [Tooltip("The auto save interval in minutes. Default is 10.")]
        private float autoSaveInterval = 10;

        [BoxGroup("Migrations")]
        [SerializeField]
        private bool useMigrations;

        [BoxGroup("Migrations")]
        [SerializeField]
        [ShowIf(nameof(useMigrations))]
        private PackMigrations migrations;

        // time of the next auto-save event, checked and updated continuously in Update().
        private float _nextAutoSaveTime;

        // whether we have at least once saved/restored a database which we'll use to auto-save subsequent versions for.
        private bool _allowAutoSave;

        // we track the most recently specified save file base name and use it for auto-save.
        private string _autoSaveBaseName;
        private string _selectedDatabasePath;
        private readonly HashSet<string> _capturedPackIdentityKeys = new();
        private readonly HashSet<string> _capturedComponentKeys = new();
        private readonly HashSet<string> _capturedScriptableObjectKeys = new(); // not used yet
        private readonly HashSet<string> _deletedGameObjectPackKeys = new();
        private readonly HashSet<string> _deletedComponentPackKeys = new();

        /// <summary>
        /// Called whenever a save process ws completed by this system.
        /// </summary>
        public event Action Saved;

        /// <summary>
        /// Called whenever a load process ws completed by this system.
        /// </summary>
        public event Action<string> Loaded;

        /// <summary>
        /// The default path where the backing database (save-files) are stored.
        /// </summary>
        public string DefaultFolderPath
        {
            get
            {
                EnsureDb();
                return DB.Settings.DefaultFolderPath;
            }
        }

        /// <summary>
        /// The subfolder used for all files created/managed by this system. This includes subfolders in persistent data path and streaming assets.
        /// </summary>
        public string FolderName => folderName;

        /// <summary>
        /// The file extension appended to all files created/managed by this system.
        /// </summary>
        public string FileExtension => fileExtension;

        /// <summary>
        /// The currently loaded backing database. Accessing this API directly is not recommended as it may violate assumptions
        /// this manager makes.
        /// </summary>
        public IKeyValueDatabase DB => _db;

        /// <summary>
        /// Event function.
        /// </summary>
        private void Awake()
        {
            EnsureDb();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += RestoreSceneObjects;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= RestoreSceneObjects;
        }

        /// <summary>
        /// Restore objects in a scene from the current state of the database.
        /// </summary>
        public void RestoreSceneObjects(Scene scene) => RestoreSceneObjects(scene, LoadSceneMode.Additive);

        /// <summary>
        /// Restores the state of objects in the given scene from the currently loaded database.
        /// </summary>
        private void RestoreSceneObjects(Scene scene, LoadSceneMode _)
        {
            if (!autoUnpack)
            {
                return;
            }

            if (string.IsNullOrEmpty(_selectedDatabasePath))
            {
                Debug.LogWarning(
                    $"[{nameof(PackSystem)}] No database loaded, skipping scene unpacking. Make sure to load/save at " +
                    $"least once with a valid path before attempting to restore objects.",
                    this);
                return;
            }

            RestoreSceneObjectsAsync(default, scene).Forget();

            async UniTaskVoid RestoreSceneObjectsAsync(IProgress<float> progress, Scene scopedScene)
            {
                EnsureDb();

                var cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] Unpacking scene {scopedScene.name}...");
                }

                List<PackIdentity> sceneObjects = scene
                    .GetRootGameObjects()
                    .SelectMany(it => it.GetComponentsInChildren<PackIdentity>(true))
                    .Where(it => it.HasEntityID)
                    .ToList();

                Stopwatch swStep = Stopwatch.StartNew();
                Stopwatch swTotal = Stopwatch.StartNew();
                var total = sceneObjects.Count;
                var i = 0;

                // a scene load may overlap with a save operation, we wait for the database to unlock before proceeding.
                if (_db.IsLocked)
                {
                    TimeSpan wait = TimeSpan.FromSeconds(5f);
                    Debug.LogWarning(
                        $"[{nameof(PackSystem)}] Database is locked. We'll await an unlock in the next {wait.Seconds} seconds before proceeding.",
                        this);
                    await UniTask
                        .WaitUntil(_db, db => !db.IsLocked, PlayerLoopTiming.Update, cts.Token)
                        .Timeout(wait, DelayType.DeltaTime, PlayerLoopTiming.Update, cts)
                        .SuppressCancellationThrow();
                    if (_db.IsLocked)
                    {
                        Debug.LogError(
                            $"[{nameof(PackSystem)}] Database is locked for more than {wait.Seconds} seconds while loading {scene.name}, skipping scene unpacking. Make sure to unlock the database before attempting to restore objects.",
                            this);
                        return;
                    }
                }

                // restore existing PackIdentities

                foreach (PackIdentity packIdentity in sceneObjects)
                {
                    if (packIdentity.Policy == PackIdentity.PackingPolicy.Ghost)
                    {
                        continue;
                    }

                    await ReportAsync(progress, swStep, i, total);
                    if (_db.TryGet(packIdentity.GetPackKey(), out PackIdentityData data, _selectedDatabasePath))
                    {
                        packIdentity.transform.SetPositionAndRotation(data.Position, data.Rotation);
                    }

                    i++;
                }

                // We restore PackIdentities missing in the loaded scenes. To do so we walk the save file for all game
                // objects that have a corresponding scene scope and add them to the list of scene objects.

                string[] allObjects = _db.Get<string[]>(PackedGameObjects, _selectedDatabasePath);
                total = allObjects.Length;
                i = 0;
                foreach (var id in allObjects)
                {
                    await ReportAsync(progress, swStep, i, total);
                    PackIdentityData package = _db.Get<PackIdentityData>(id, _selectedDatabasePath);
                    Guid packID = Guid.Parse(package.PackKey);
                    if (package.Scope.StartsWith(SCENE_PREFIX))
                    {
                        var sceneName = package.Scope[SCENE_PREFIX.Length..];
                        if (sceneName == scopedScene.name && !PackIdentity.InstanceExists(packID))
                        {
                            // no asset ID means we can't spawn the object
                            if (package.AssetID == default)
                            {
                                continue;
                            }

                            CreatePrefabInstance(package, sceneObjects);
                        }
                    }

                    i++;
                }

                // restore existing components

                total = sceneObjects.Count;
                i = 0;
                foreach (PackIdentity packIdentity in sceneObjects)
                {
                    if (packIdentity.Policy == PackIdentity.PackingPolicy.Ghost)
                    {
                        continue;
                    }

                    await ReportAsync(progress, swStep, i, total);
                    IEnumerable<IPackableComponent> components = packIdentity
                        .GetComponentsInChildren<Component>(true)
                        .OfType<IPackableComponent>();
                    foreach (var component in components)
                    {
                        string key = component.GetPackKey();
                        if (_db.TryGet(component.PackType, key, out object package, _selectedDatabasePath))
                        {
                            component.Unpack(package, assetLookup);
                            if (debug)
                            {
                                Debug.Log($"[{nameof(PackSystem)}] Restored component with PackKey {key}",
                                    component as Component);
                            }
                        }
                        else
                        {
                            if (debug)
                            {
                                Debug.Log($"[{nameof(PackSystem)}] No data found for component PackKey {key}",
                                    component as Component);
                            }
                        }
                    }

                    i++;
                }

                // TODO: implement restoring of object deletions in the spawned scene (maybe just disable?).
                // what can we do?
                // - the pack data must store some sort of "deleted" flag or a list of deleted objects

                if (debug)
                {
                    Debug.Log(
                        $"[{nameof(PackSystem)}] Scene {scopedScene.name} unpacked in {swTotal.ElapsedMilliseconds}ms",
                        this);
                }
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Update()
        {
            if (IsAutoActive && Time.time > _nextAutoSaveTime)
            {
                _nextAutoSaveTime = Time.time + Mathf.Max(autoSaveInterval * 60f, 60f);
                Debug.Log("Auto-saving ...");
                SaveVersionAsync(_autoSaveBaseName).Forget();
            }
        }

        /// <summary>
        /// Whether automatic saving is active.
        /// </summary>
        public bool IsAutoActive =>
            !string.IsNullOrEmpty(_autoSaveBaseName) &&
            _allowAutoSave &&
            saveVersions &&
            autoSave &&
            autoSaveInterval > 0;

        /// <summary>
        /// Time in seconds until the next auto-save. 
        /// </summary>
        public float UntilNextAutoSave => IsAutoActive ? Mathf.Max(_nextAutoSaveTime - Time.time, 0) : -1;

        /// <summary>
        /// Interval in seconds between auto-saves. 
        /// </summary>
        public float AutoSaveInterval => autoSaveInterval;

        public bool IsPacking { get; private set; }

        /// <inheritdoc cref="IPackSystem"/>
        public async UniTask CaptureAsync(PackIdentity packIdentity)
        {
            IsPacking = true;
            try
            {
                Debug.Assert(packIdentity.IsSceneObject, "packIdentity.IsSceneObject", this);
                if (!packIdentity.IsSceneObject)
                {
                    return;
                }

                if (packIdentity.Policy == PackIdentity.PackingPolicy.Ghost)
                {
                    return;
                }

                await EnsureLoadedAsync(_selectedDatabasePath);

                Debug.Log($"[{nameof(PackSystem)}] Capturing {packIdentity.EntityID} ...");
                PackGameObject(packIdentity, _db, _selectedDatabasePath, _capturedPackIdentityKeys);
                foreach (var it in packIdentity.GetComponentsInChildren<Component>(true).OfType<IPackableComponent>())
                {
                    PackComponent(it, _db, _selectedDatabasePath, _capturedComponentKeys);
                }
            }
            finally
            {
                IsPacking = false;
            }
        }

        /// <inheritdoc cref="IPackSystem"/>
        public async UniTask DeleteAsync(PackIdentity packIdentity)
        {
            IsPacking = true;
            try
            {
                if (packIdentity.Policy == PackIdentity.PackingPolicy.Ghost)
                {
                    return;
                }

                await EnsureLoadedAsync(_selectedDatabasePath);

                Debug.Log($"[{nameof(PackSystem)}] Deleting {packIdentity.EntityID} ...");
                foreach (IPackableComponent component in packIdentity.GetComponentsInChildren<Component>(true)
                    .OfType<IPackableComponent>())
                {
                    DeleteComponent(component, _deletedComponentPackKeys);
                }

                DeleteGameObject(packIdentity, _deletedGameObjectPackKeys);
            }
            finally
            {
                IsPacking = false;
            }
        }

        /// <summary>
        /// Called by a <see cref="PackIdentity"/> in Start() to restore its state from the currently loaded database.
        /// When the identity is not part of the current database, no state will be restored. 
        /// </summary>
        /// <param name="packIdentity">The pack identity to restore. Must be a scene instance with a valid life ID.</param>
        public async UniTask RestoreAsync(PackIdentity packIdentity)
        {
            IsPacking = true;
            try
            {
                Debug.Assert(packIdentity.IsSceneObject, "packIdentity.IsSceneObject", this);
                if (!packIdentity.IsSceneObject)
                {
                    return;
                }

                if (packIdentity.Policy == PackIdentity.PackingPolicy.Ghost)
                {
                    return;
                }

                await EnsureLoadedAsync(_selectedDatabasePath);

                Debug.Log($"[{nameof(PackSystem)}] Restoring {packIdentity.EntityID} ...");
                PackIdentityData idPackage = _db.Get<PackIdentityData>(packIdentity.EntityID.ToString());
                packIdentity.gameObject.SetActive(false);
                packIdentity.transform.SetPositionAndRotation(idPackage.Position, idPackage.Rotation);

                // gather IPackableComponent instances in children and create a lookup for quickly finding them in random order.
                List<IPackableComponent> components = packIdentity
                    .GetComponentsInChildren<Component>(true)
                    .OfType<IPackableComponent>()
                    .EnsureValidPackableComponent()
                    .Distinct(
                        new PackableComponentComparer()) // this will silence any default key errors, we should not do that!
                    .ToList();

                // iterate over all packed component keys in the serialized data and restore them one by one.
                foreach (var component in components)
                {
                    if (_db.TryGet(component.PackType, component.GetPackKey(), out object coPackage))
                    {
                        try
                        {
                            component.Unpack(coPackage, assetLookup);
                        }
                        catch (Exception e)
                        {
                            HandleUnpackException(e, component.PackType, component.GetPackKey(),
                                component as Component);
                        }
                    }
                }
            }
            finally
            {
                IsPacking = false;
            }
        }

        /// <summary>
        /// Load and restore the last saved state.
        /// </summary>

#if ODIN_INSPECTOR
        [DisableInEditorMode]
        [Button]
#else
        [Button(enabledMode: EButtonEnableMode.Playmode)]
#endif
        public void RestoreLatest() => RestoreLatestAsync().Forget();

        /// <summary>
        /// Load and restore the last saved state.
        /// </summary>
        public async UniTask RestoreLatestAsync(IProgress<float> progress = default)
        {
            await EnsureLoadedAsync(_selectedDatabasePath);

            if (saveVersions)
            {
                DirectoryInfo directoryInfo = new(_db.Settings.DefaultFolderPath);
                FileInfo latest = directoryInfo
                    .EnumerateFiles($"*{fileExtension}")
                    .OrderByDescending(it => it.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (latest != null)
                {
                    await RestoreAsync(latest.Name, progress);
                }
                else
                {
                    Debug.LogWarning(
                        $"{nameof(PackSystem)} No {fileExtension} files found in directory {directoryInfo.Name}");
                }
            }
            else
            {
                await RestoreAsync(defaultFileName, progress);
            }
        }

        /// <summary>
        /// Loads and restores the configured scopes using the default configuration.
        /// </summary>
        /// <param name="path">The path of the database to load.</param>
        /// <param name="progress">An optional progress object.</param>
        /// <remarks>This <see cref="PackSystem"/> interpretation does not support loading state without also restoring it.
        /// This means that it cannot be used for loading local or partial state as would be necessary if it were to support
        /// concepts like levels.</remarks>
        public async UniTask RestoreAsync(string path, IProgress<float> progress = default)
        {
            IsPacking = true;
            try
            {
                // in case something goes wrong in the subsequent procedure, we disable auto-save here.
                _allowAutoSave = false;

                progress?.Report(0);
                await UniTask.NextFrame();

                if (_db.HasUncommittedChanges)
                {
                    await _db.RevertAsync();
                    Debug.LogWarning($"[{nameof(PackSystem)}] Reverted uncommitted changes");
                }

                await EnsureLoadedAsync(path);

                SelectDatabase(path);

                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] Restoring '{path}' ...");
                }

                if (clearBeforeRestore)
                {
                    await ClearScene(destroyChildrenOnRestore, progress);
                }

                PackContext context = _db.Get<PackContext>(ContextKey, path);

                // we need to restore the context first to ensure that all scenes are loaded correctly.
                await RestoreContextAsync(context, path, progress);

                IReadOnlyCollection<PackIdentity> restoredGameObjects = await RestoreGameObjectsAsync(path, progress);
                IReadOnlyCollection<IPackableComponent> unpackedComponents =
                    await RestoreComponentsAsync(path, progress);
                IReadOnlyCollection<IPackableScriptableObject> unpackedSOs = await RestoreSOsAsync(path, progress);

                // the newly spawned GameObjects are activated here (after all unpacking is complete). This will trigger
                // their lifecycle (including Awake()) to run.
                foreach (PackIdentity instance in restoredGameObjects)
                {
                    instance.gameObject.SetActive(true);
                }

                // per-item post-processing callbacks

                IEnumerable<IWhenAllUnpackedHandler> postComponents =
                    unpackedComponents.OfType<IWhenAllUnpackedHandler>();
                IEnumerable<IWhenAllUnpackedHandler> postSOs = unpackedSOs.OfType<IWhenAllUnpackedHandler>();
                IWhenAllUnpackedHandler[] postUnion = postComponents.Concat(postSOs).ToArray();
                await WhenAllUnpackedAsync(postUnion, progress);

                // system post processing callbacks

                OnLoad();
                Loaded?.Invoke(path);
                onLoaded.Invoke();

                // finishing up

                progress?.Report(1.0f);
                await UniTask.NextFrame();
                PushAutoSaveTimer();
                _allowAutoSave = true;
            }
            finally
            {
                IsPacking = false;
            }

            return; // only local functions below

            async UniTask RestoreContextAsync(PackContext packContext, string _, IProgress<float> scopedProgress)
            {
                // A NOTE ON DESIGN: This procedure will load/unload scenes based on the state that they were in when
                // the database was committed last. This may come into conflict with custom scene management and scene
                // initialization that produces side effects different from what the pack system attempts to achieve.
                // The PackSystem's goals are to recreate the same state as at the time of the commit. In most cases,
                // deviations from this can be handled as post-processing via IWhenAllUnpackedHandler or the provided
                // event callbacks.

                int count = 0;
                float total = (packContext.LoadedScenes.Length + SceneManager.sceneCount) * 100;
                Stopwatch stopwatch = Stopwatch.StartNew();
                await ReportAsync(scopedProgress, stopwatch, count, total);

                // unload all scenes; We have to iterate backwards because we wait for the load to complete before
                // moving to the next scene, this means that the scene count and scene index will have changed when
                // we loop.
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene == gameObject.scene)
                    {
                        // do not unload the scene that holds the PackSystem. We expect this scene to be structurally
                        // stable and contain no dynamically spawned objects that are part of the packed state.
                        continue;
                    }

                    Debug.Log($"[{nameof(PackSystem)}] Unloading scene {scene.name} ...", this);
                    AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                    while (!op.isDone)
                    {
                        await ReportAsync(scopedProgress, stopwatch, (int)(count * 100 + op.progress * 100), total);
                    }

                    count++;
                }

                // Wait one frame to allow destroyed objects to be cleaned up.
                await UniTask.NextFrame();

                // load all scenes of the pack context
                foreach (var sceneName in packContext.LoadedScenes)
                {
                    if (sceneName == gameObject.scene.name)
                    {
                        // the scene containing the PackSystem is already loaded (it was never unloaded, so we skip it).
                        continue;
                    }

                    Debug.Log($"[{nameof(PackSystem)}] Loading scene {sceneName} ...", this);
                    AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                    while (!op.isDone)
                    {
                        await ReportAsync(scopedProgress, stopwatch, (int)(count * 100 + op.progress * 100), total);
                    }

                    count++;
                }

                // Wait one frame to allow loaded objects to be initialized.
                await UniTask.NextFrame();

                var activeScene = SceneManager.GetSceneByName(packContext.ActiveScene);
                if (activeScene.isLoaded)
                {
                    SceneManager.SetActiveScene(activeScene);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(PackSystem)}] Failed activate scene {packContext.ActiveScene}",
                        this);
                }
            }

            async UniTask<IReadOnlyCollection<PackIdentity>> RestoreGameObjectsAsync(string scopedPath,
                IProgress<float> scopedProgress)
            {
                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] Restoring game objects ...");
                }

                // We restore GameObjects in multiple passes. We want to make sure that existing objects are not duplicated and
                // missing ones are created and activated predictably.

                // deserialize all object data
                string[] gameObjectPackKeys = _db.Get<string[]>(PackedGameObjects, scopedPath);

                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] processing {gameObjectPackKeys.Length} objects...");
                }

                List<PackIdentityData> packages = new();

                int countProgress = 0;
                int totalProgress = gameObjectPackKeys.Length * 4;
                Stopwatch stopwatch = Stopwatch.StartNew();

                foreach (string key in gameObjectPackKeys)
                {
                    countProgress++;
                    await ReportAsync(scopedProgress, stopwatch, countProgress, totalProgress);

                    PackIdentityData data = _db.Get<PackIdentityData>(key, scopedPath);
                    packages.Add(data);
                }

                // update existing objects
                await UniTask.NextFrame();

                // we gather scene objects specifically to differentiate them from assets we've gathered on scene load.
                Dictionary<Guid, PackIdentity> sceneObjects =
                    FindObjectsByType<PackIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Where(it => it.HasEntityID)
                        .ToDictionary(it => it.EntityID, it => it);

                Dictionary<string, PackScope> scopeLookup =
                    FindObjectsByType<PackScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .ToDictionary(it => it.ScopeID, it => it);

                // instantiate missing objects from their prefabs. Actual instantiation is delegated as an
                // implementation detail. By default, regular GameObject instantiation at the serialized position is used.
                List<PackIdentity> instances = new();
                foreach (PackIdentityData package in packages)
                {
                    countProgress++;
                    await ReportAsync(scopedProgress, stopwatch, countProgress, totalProgress);

                    Guid packID = Guid.Parse(package.PackKey);
                    if (!PackIdentity.TryGetByEntityID(packID, out PackIdentity existingObject))
                    {
                        // OBJECT DOESNT EXIST - SPAWN IT

                        // no asset ID means we can't spawn the object
                        if (package.AssetID == default)
                        {
                            continue;
                        }

                        // if the object is part of a scope that isn't loaded we skip it
                        if (!IsPartOfLoadedScope(package, scopeLookup))
                        {
                            continue;
                        }

                        CreatePrefabInstance(package, instances);
                    }
                    else
                    {
                        if (existingObject.Policy == PackIdentity.PackingPolicy.Ghost)
                        {
                            continue;
                        }

                        // OBJECT EXISTS - RESTORE IT

                        // restore the pack identity's position and rotation
                        existingObject.gameObject.SetActive(false);
                        existingObject.transform.SetPositionAndRotation(package.Position, package.Rotation);
                        instances.Add(existingObject);
                        if (debug)
                        {
                            Debug.Log(
                                $"[{nameof(PackSystem)}] Restored position and rotation for {existingObject.name} with key {package.PackKey}",
                                existingObject);
                        }
                    }
                }

                // restoring their parent relationships (if available)
                foreach (PackIdentityData package in packages)
                {
                    countProgress++;
                    await ReportAsync(scopedProgress, stopwatch, countProgress, totalProgress);

                    Guid packID = Guid.Parse(package.PackKey);
                    if (package.ParentID != default &&
                        PackIdentity.TryGetByEntityID(packID, out PackIdentity existingObject))
                    {
                        if (existingObject.Policy == PackIdentity.PackingPolicy.Ghost)
                        {
                            continue;
                        }

                        existingObject.GetObject().transform.SetParent(
                            PackIdentity.TryGetByEntityID(package.ParentID, out PackIdentity parent)
                                ? parent.GetObject().transform
                                : null
                        );
                    }
                }

                // we do not activate game objects yet. We do want to restore components first.

                return instances;
            }

            // TODO: this looks very similar to restoring components, can they be unified?
            async UniTask<IReadOnlyCollection<IPackableScriptableObject>> RestoreSOsAsync(
                string scopedPath,
                IProgress<float> scopedProgress
            )
            {
                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] Restoring ScriptableObjects ...");
                }

                Dictionary<Guid, IPackableScriptableObject> registeredSOs = scriptableObjects
                    .Where(it => it != null)
                    .OfType<IPackableScriptableObject>()
                    .Distinct()
                    .ToDictionary(it => it.AssetID, it => it);

                // iterate over all packed keys in the serialized data and restore them one by one through
                // the lookup we performed above. This ensures that only object that are actually serialized will be restored
                // and unexpected configurations do not lead to errors because of missing data.
                string[] packedKeys = _db.Get<string[]>(PackedScriptableObjects, scopedPath);

                int count = 0;
                float total = packedKeys.Length;
                Stopwatch stopwatch = Stopwatch.StartNew();

                foreach (string packedKey in packedKeys)
                {
                    var packID = Guid.Parse(packedKey);
                    count++;
                    await ReportAsync(scopedProgress, stopwatch, count, total);

                    if (!registeredSOs.ContainsKey(packID))
                    {
                        if (debug)
                        {
                            Debug.LogWarning(
                                $"[{nameof(PackSystem)}] Failed to unpack SO ({packedKey}); Not found in lookup. This is expected if the component is no longer part of the {nameof(PackSystem)} configuration.");
                        }

                        continue;
                    }

                    IPackableScriptableObject so = registeredSOs[packID];
                    object package = _db.Get(so.PackType, packedKey, scopedPath);
                    try
                    {
                        so.Unpack(package);
                    }
                    catch (Exception e)
                    {
                        HandleUnpackException(e, so.PackType, packedKey, null);
                    }
                }

                return registeredSOs.Values;
            }


            async UniTask<IReadOnlyCollection<IPackableComponent>> RestoreComponentsAsync(
                string scopedPath,
                IProgress<float> scopedProgress
            )
            {
                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] Restoring Components ...", this);
                }

                // gather existing IPackableComponent components and create a lookup for quickly finding them in random order.
                Object[] allObjects = FindObjectsByType<Object>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                List<IPackableComponent> intermediateResult = allObjects
                    .OfType<IPackableComponent>()
                    .EnsureValidPackableComponent()
                    .ToList();

                // TODO: for debugging: warn about components with default keys in intermediateResult

                Dictionary<string, IPackableComponent> existingComponents = intermediateResult
                    .Distinct(
                        new PackableComponentComparer()) // this will silence any default key errors, we should not do that!
                    .ToDictionary(it => it.GetPackKey(), it => it);

                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] processing {existingComponents.Count} existing objects...");
                }

                // iterate over all packed component keys in the serialized data and restore them one by one through
                // the lookup we performed above. This ensures that only components that are actually serialized will be restored
                // and unexpected configurations in the scene-structure do not leads to errors because of missing data.
                string[] packedComponentKeys = _db.Get<string[]>(PackedComponents, scopedPath);

                int count = 0;
                float total = packedComponentKeys.Length;
                Stopwatch stopwatch = Stopwatch.StartNew();

                foreach (string componentKey in packedComponentKeys)
                {
                    count++;
                    await ReportAsync(scopedProgress, stopwatch, count, total);

                    if (!existingComponents.ContainsKey(componentKey))
                    {
                        if (debug)
                        {
                            Debug.LogWarning(
                                $"[{nameof(PackSystem)}] Failed to unpack component ({componentKey}); Not found in scene. This is expected if the component is no longer part of its parent prefab or scene.");
                        }

                        continue;
                    }


                    IPackableComponent component = existingComponents[componentKey];

                    // skip components on ghost objects.
                    if ((component as Component)?.GetComponentInParent<PackIdentity>().Policy ==
                        PackIdentity.PackingPolicy.Ghost)
                    {
                        continue;
                    }

                    object package = _db.Get(component.PackType, componentKey, scopedPath);
                    try
                    {
                        component.Unpack(package, assetLookup);
                    }
                    catch (Exception e)
                    {
                        HandleUnpackException(e, component.PackType, componentKey, null);
                    }
                }

                return existingComponents.Values;
            }

            async UniTask WhenAllUnpackedAsync(
                ICollection<IWhenAllUnpackedHandler> unpackedObjects,
                IProgress<float> scopedProgress
            )
            {
                int count = 0;
                float total = unpackedObjects.Count;
                Stopwatch stopwatch = Stopwatch.StartNew();
                foreach (IWhenAllUnpackedHandler handler in unpackedObjects)
                {
                    count++;
                    await ReportAsync(scopedProgress, stopwatch, count, total);

                    try
                    {
                        handler.OnAllUnpacked();
                    }
                    catch (Exception e)
                    {
                        HandleUnpackException(e, handler.GetType(), string.Empty, null);
                    }
                }
            }
        }

        private void SelectDatabase(string path)
        {
            _selectedDatabasePath = path;

            // auto save will always use the most recently specified path.
            _autoSaveBaseName = GetBaseFilename(path);
        }

        private void HandleUnpackException(Exception e, Type type, string info, Object context)
        {
            if (isPedantic)
            {
                throw e;
            }

            Debug.LogWarning(
                $"[{nameof(PackSystem)}] An exception occured during unpacking of {type.Name} ({info}); Check the exception log below for details.",
                context);

            Debug.LogException(e);
        }

        private static bool IsPartOfLoadedScope(PackIdentityData package, Dictionary<string, PackScope> scopeLookup)
        {
            // skip scopes (scenes) that aren't loaded
            if (package.Scope?.StartsWith(SCENE_PREFIX) ?? false)
            {
                string sceneName = package.Scope.Substring(SCENE_PREFIX.Length);
                if (!SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    return true;
                }
            }
            else if (package.Scope?.StartsWith(SCOPE_PREFIX) ?? false)
            {
                string scopeID = package.Scope.Substring(6);
                if (!scopeLookup.TryGetValue(scopeID, out PackScope scope))
                {
                    return true;
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(PackSystem)}] Scope '{package.Scope}' not recognized.");
                return true;
            }

            return false;
        }

        private void PushAutoSaveTimer()
        {
            _nextAutoSaveTime = Time.time + autoSaveInterval * 60f;
        }

        private async UniTask ClearScene(Transform[] transforms, IProgress<float> progress)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int total = transforms.Sum(it => it.childCount);
            int count = 0;
            foreach (Transform parent in transforms)
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    count++;
                    await ReportAsync(progress, stopwatch, count, total);
                    // in case the passed in transforms are nested we ensure that we do not run into exceptions for accessing
                    // invalid instances that have been previously destroyed.
                    if (parent)
                    {
                        GameObject go = parent.GetChild(i).gameObject;
                        if (go)
                        {
                            Destroy(go);
                        }
                    }
                }
            }

            if (debug)
            {
                Debug.Log($"[{nameof(PackSystem)}] Removed {total} objects");
            }
        }

        private static async UniTask ReportAsync(IProgress<float> progress, Stopwatch sw, int i, float total)
        {
            if (sw.ElapsedMilliseconds > 20)
            {
                progress?.Report(i / total);
                await UniTask.NextFrame();
                sw.Restart();
            }
        }

        /// <summary>
        /// Equality comparer for <see cref="IPackableComponent"/> instances that compares their <see cref="IPackableComponent.GetPackKey"/> values.
        /// </summary>
        private class PackableComponentComparer : IEqualityComparer<IPackableComponent>
        {
            public bool Equals(IPackableComponent x, IPackableComponent y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                if (x.GetPackKey() == y.GetPackKey())
                {
                    Component xc = (Component)x;
                    Component yc = (Component)x;
                    Debug.LogWarning(
                        $"[{nameof(PackSystem)}] {xc.name} and {yc.name} have the same pack key ({x.GetPackKey()}.");
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public int GetHashCode(IPackableComponent obj)
            {
                return HashCode.Combine(obj.GetPackKey());
            }
        }

#if ODIN_INSPECTOR
        [DisableInEditorMode]
        [Button]
#else
        [Button(enabledMode: EButtonEnableMode.Playmode)]
#endif
        public void Save()
        {
            Debug.Log($"[{nameof(PackSystem)}] Saving.", this);
            SaveAsync().Forget();
        }


        /// <summary>
        /// Collects and saves the configured scope using the default configuration.
        /// </summary>
        /// <remarks>All packable objects in the currently loaded scope will be added to the database and saved to
        /// disk. Note that for deleted objects to be registered, they must be deleted via
        /// <see cref="DeleteAsync(PackIdentity)"/> before calling <see cref="SaveAsync()"/>.</remarks>
        public async UniTask SaveAsync()
        {
            var basename = string.IsNullOrEmpty(_selectedDatabasePath)
                ? defaultFileName
                : GetBaseFilename(_selectedDatabasePath);

            if (saveVersions)
            {
                await SaveVersionAsync(basename);
            }
            else
            {
                await SaveAsync($"{basename}{fileExtension}");
            }
        }

        /// <summary>
        /// Collects and saves the configured scope using the default configuration to a versioned file, prefixed by a custom name.
        /// </summary>
        /// <remarks>All packable objects in the currently loaded scope will be added to the database and saved to
        /// disk. Note that for deleted objects to be registered, they must be deleted via
        /// <see cref="DeleteAsync(PackIdentity)"/> before calling <see cref="SaveAsync()"/>.</remarks>
        public async UniTask SaveVersionAsync(string baseName)
        {
            string path = $"{baseName}.{DateTimeOffset.UtcNow.UtcTicks}{fileExtension}";
            await SaveAsync(path);
        }

        /// <summary>
        /// Collects and saves the configured scopes using the default configuration in a given database.
        /// </summary>
        public async UniTask SaveAsync(string path)
        {
            IsPacking = true;
            try
            {
                var sw = Stopwatch.StartNew();
                if (debug)
                {
                    Debug.Log($"[{nameof(PackSystem)}] Packing to {path}");
                }

                // in case something goes wrong in the subsequent procedure, we disable auto-save here.
                _allowAutoSave = false;

                SelectDatabase(path);
                EnsureDb();

                if (saveVersions)
                {
                    await _db.CopyToAsync(path);
                }

                IEnumerable<PackIdentity> toPack = limitScope
                    ? scopes
                        .Where(it => it)
                        .SelectMany(it => it.GetComponentsInChildren<PackIdentity>(false))
                        .Where(it =>
                            it &&
                            it.HasEntityID &&
                            it.IsSceneObject &&
                            it.Policy != PackIdentity.PackingPolicy.Ghost
                        )
                    : PackIdentity.AllLoadedInstances
                        .Where(it =>
                            it &&
                            it.HasEntityID &&
                            it.IsSceneObject &&
                            it.Policy != PackIdentity.PackingPolicy.Ghost
                        );

                IEnumerable<IPackableScriptableObject> toPackSOs = scriptableObjects
                    .Where(it => it != null)
                    .OfType<IPackableScriptableObject>();

                {
                    GetIncrementalChanges(
                        path,
                        out HashSet<string> nextPackIdentityKeys,
                        out HashSet<string> nextComponentKeys,
                        out HashSet<string> nextScriptableObjectKeys
                    );

                    // pack the objects and add them to the key lists.
                    await PackGameObjectsAsync(_db, path, toPack, nextPackIdentityKeys);
                    await PackComponentsAsync(_db, path, nextComponentKeys);
                    await PackScriptableObjectsAsync(_db, path, toPackSOs, nextScriptableObjectKeys);

                    // write the key lists to the database.
                    _db.Set(PackedScriptableObjects, nextScriptableObjectKeys, path);
                    _db.Set(PackedGameObjects, nextPackIdentityKeys, path);
                    _db.Set(PackedComponents, nextComponentKeys, path);
                    ClearIncrementalChanges();

                    // construct a context for this current save.
                    _db.Set(ContextKey, new PackContext
                    {
                        LoadedScenes = GetLoadedSceneNames(),
                        ActiveScene = SceneManager.GetActiveScene().name
                    }, path);

                    if (debug)
                    {
                        Debug.Log($"[{nameof(PackSystem)}] {nextPackIdentityKeys.Count} Scene objects packed", this);
                        Debug.Log($"[{nameof(PackSystem)}] {nextComponentKeys.Count} Components packed", this);
                        Debug.Log($"[{nameof(PackSystem)}] {nextScriptableObjectKeys.Count} ScriptableObjects packed",
                            this);
                    }
                }

                OnSave();
                Debug.Log($"[{nameof(PackDB)}] Committing database.");
                await _db.CommitAsync();

                PushAutoSaveTimer();
                _allowAutoSave = true;
                Saved?.Invoke();
                onSaved.Invoke();

                Debug.Log($"[{nameof(PackSystem)}] Packed to {path} in {sw.ElapsedMilliseconds} ms");
            }
            finally
            {
                IsPacking = false;
            }

            return; // only local functions below

            async UniTask PackGameObjectsAsync(PackDB db, string scopedPath, IEnumerable<PackIdentity> packScopes,
                HashSet<string> keys)
            {
                var stopwatch = Stopwatch.StartNew();
                foreach (PackIdentity packIdentity in packScopes)
                {
                    Debug.Assert(packIdentity.Policy != PackIdentity.PackingPolicy.Ghost,
                        "packIdentity.Policy != PackIdentity.PackingPolicy.Ghost");
                    PackGameObject(packIdentity, db, scopedPath, keys);
                    if (stopwatch.ElapsedMilliseconds > 50)
                    {
                        await UniTask.NextFrame();
                        stopwatch.Restart();
                    }
                }
            }

            async UniTask PackComponentsAsync(PackDB db, string scopedPath, HashSet<string> keys)
            {
                var stopwatch = Stopwatch.StartNew();
                IEnumerable<IPackableComponent> packComponents;
                if (limitScope)
                {
                    packComponents = scopes
                        .Where(it => it != null)
                        .SelectMany(it => it.GetComponentsInChildren<Component>(true).OfType<IPackableComponent>());
                }
                else
                {
                    packComponents = FindObjectsByType<Component>(FindObjectsSortMode.None)
                        .OfType<IPackableComponent>();
                }

                foreach (IPackableComponent packComponent in packComponents)
                {
                    PackComponent(packComponent, db, scopedPath, keys);
                    if (stopwatch.ElapsedMilliseconds > 50)
                    {
                        await UniTask.NextFrame();
                        stopwatch.Restart();
                    }
                }
            }

            // TODO: This seems very similar to packing of components, maybe we can unify them?
            async UniTask PackScriptableObjectsAsync(PackDB db, string scopedPath,
                IEnumerable<IPackableScriptableObject> packSOs, HashSet<string> keys)
            {
                var stopwatch = Stopwatch.StartNew();
                foreach (IPackableScriptableObject so in packSOs)
                {
                    PackScriptableObject(so, db, scopedPath, keys);
                    if (stopwatch.ElapsedMilliseconds > 50)
                    {
                        await UniTask.NextFrame();
                        stopwatch.Restart();
                    }
                }
            }
        }

        private void ClearIncrementalChanges()
        {
            _capturedScriptableObjectKeys.Clear();
            _capturedComponentKeys.Clear();
            _capturedPackIdentityKeys.Clear();
            _deletedComponentPackKeys.Clear();
            _capturedPackIdentityKeys.Clear();
        }

#if ODIN_INSPECTOR
        [DisableInEditorMode]
        [Button]
#else
        [Button(enabledMode: EButtonEnableMode.Playmode)]
#endif
        public void Commit() => CommitAsync().Forget();

        /// <summary>
        /// Commits the current database to disk. This will not collect state from loaded objects. It will only save
        /// changes that have been recorded via <see cref="DeleteAsync"/> and <see cref="CaptureAsync"/>.
        /// </summary>
        public async UniTask CommitAsync()
        {
            var sw = Stopwatch.StartNew();
            if (debug)
            {
                Debug.Log($"[{nameof(PackSystem)}] Committing to {_selectedDatabasePath}");
            }

            await EnsureLoadedAsync(_selectedDatabasePath);

            // in case something goes wrong in the subsequent procedure, we disable auto-save here.
            _allowAutoSave = false;

            GetIncrementalChanges(
                _selectedDatabasePath,
                out HashSet<string> nextPackIdentityKeys,
                out HashSet<string> nextComponentKeys,
                out HashSet<string> nextScriptableObjectKeys
            );

            // write the key lists to the database.
            _db.Set(PackedScriptableObjects, nextScriptableObjectKeys, _selectedDatabasePath);
            _db.Set(PackedGameObjects, nextPackIdentityKeys, _selectedDatabasePath);
            _db.Set(PackedComponents, nextComponentKeys, _selectedDatabasePath);
            ClearIncrementalChanges();

            // record the current context.
            _db.Set(ContextKey,
                new PackContext
                {
                    LoadedScenes = GetLoadedSceneNames(),
                    ActiveScene = SceneManager.GetActiveScene().name
                },
                _selectedDatabasePath);

            OnSave();
            Debug.Log($"[{nameof(PackDB)}] Committing database.");
            await _db.CommitAsync();

            PushAutoSaveTimer();
            _allowAutoSave = true;
            Saved?.Invoke();
            onSaved.Invoke();

            Debug.Log($"[{nameof(PackSystem)}] Committed to {_selectedDatabasePath} in {sw.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Creates key sets by merging the current state of the database with the incremental changes that have been recorded. This will not clear the recorded changes.
        /// </summary>
        /// <param name="path">The database path. Provided only for API symmetry, expected to be identical to the selected and loaded database.</param>
        /// <param name="nextPackIdentityKeys">The set holding all <see cref="PackIdentity"/> keys.</param>
        /// <param name="nextComponentKeys">The set holding all <see cref="IPackableComponent"/> keys.</param>
        /// <param name="nextScriptableObjectKeys">The set holding all <see cref="IPackableScriptableObject"/> keys.</param>
        private void GetIncrementalChanges(string path, out HashSet<string> nextPackIdentityKeys,
            out HashSet<string> nextComponentKeys, out HashSet<string> nextScriptableObjectKeys)
        {
            Debug.Assert(path == _selectedDatabasePath, "path == _selectedDatabasePath");

            // construct new key lists from existing state in the database, incremental changes, deletions and the
            // queried objects from the currently loaded scope.

            if (!_db.TryGet(PackedScriptableObjects, out nextScriptableObjectKeys, path))
            {
                nextScriptableObjectKeys = new HashSet<string>();
            }

            if (!_db.TryGet(PackedGameObjects, out nextPackIdentityKeys, path))
            {
                nextPackIdentityKeys = new HashSet<string>();
            }

            if (!_db.TryGet(PackedComponents, out nextComponentKeys, path))
            {
                nextComponentKeys = new HashSet<string>();
            }

            nextPackIdentityKeys.ExceptWith(_deletedGameObjectPackKeys);
            nextComponentKeys.ExceptWith(_deletedComponentPackKeys);
            nextScriptableObjectKeys.UnionWith(_capturedScriptableObjectKeys);
            nextPackIdentityKeys.UnionWith(_capturedPackIdentityKeys);
            nextComponentKeys.UnionWith(_capturedComponentKeys);
        }

        private void PackGameObject(PackIdentity packIdentity, PackDB db, string scopedPath,
            HashSet<string> keys = default)
        {
            if (!packIdentity)
            {
                Debug.LogWarning(
                    $"[{nameof(PackSystem)}] Invalid {nameof(PackIdentity)} reference found. Ignored.",
                    packIdentity);
                return;
            }

            PackIdentityData packIdentityData = packIdentity.Pack();
            db.Set(packIdentityData.PackKey, packIdentityData, scopedPath);
            keys?.Add(packIdentityData.PackKey);
            if (debug)
            {
                Debug.Log($"[{nameof(PackSystem)}] {nameof(PackIdentity)} added ({packIdentityData.PackKey})",
                    packIdentity);
            }
        }

        private void DeleteGameObject(PackIdentity packIdentity, HashSet<string> keys = default)
        {
            _db.Delete(packIdentity.GetPackKey());
            keys?.Add(packIdentity.GetPackKey());
        }

        private void DeleteComponent(IPackableComponent component, HashSet<string> keys = default)
        {
            _db.Delete(component.GetPackKey(), _selectedDatabasePath);
            keys?.Add(component.GetPackKey());
        }

        /// <summary>
        /// Packs a single <see cref="IPackableScriptableObject"/> instance into a given database.
        /// </summary>
        /// <param name="so">The ScriptableObject to pack.</param>
        /// <param name="db">The database connection to write to.</param>
        /// <param name="scopedPath">The database to write to.</param>
        /// <param name="keys">A list of keys to add the components pack key to, given the packing was successful.</param>
        private void PackScriptableObject(IPackableScriptableObject so, PackDB db, string scopedPath,
            HashSet<string> keys = default)
        {
            string key = so.AssetID.ToString("N");
            try
            {
                if (!ValidateKey(key, (Object)so))
                    return;
                if (!ValidateNoDuplicate(db, key, scopedPath, (Object)so))
                    return;

                object package = so.Pack();
                if (!ValidatePackage(package, (Object)so))
                    return;
                keys?.Add(key);
                db.Set(key, package, scopedPath);
                if (debug)
                {
                    Debug.Log(
                        $"[{nameof(PackSystem)}] {nameof(IPackableScriptableObject)} added ({key}:{so?.GetType()?.Name})",
                        (Object)so);
                }
            }
            catch (Exception e)
            {
                HandlePackException(e, so?.GetType(), string.Empty, so as Object);
            }
        }

        /// <summary>
        /// Packs a single <see cref="IPackableComponent"/> instance into a given database.
        /// </summary>
        /// <param name="packComponent">The component to pack.</param>
        /// <param name="db">The database connection to write to.</param>
        /// <param name="scopedPath">The database to write to.</param>
        /// <param name="keys">A list of keys to add the components pack key to, given the packing was successful.</param>
        private void PackComponent(IPackableComponent packComponent, PackDB db, string scopedPath,
            HashSet<string> keys = default)
        {
            // We catch any user-code exceptions to protect the packing process, any components that produce
            // errors are ignored. Nevertheless we log an error and do not expect data to be clean.
            try
            {
                string key = packComponent.GetPackKey();

                // to help with maintaining data consistency we detect default-value keys even though the
                // implementations should actively prevent them.
                if (!ValidateKey(key, (Object)packComponent))
                    return;

                object package = packComponent.Pack();

                // even though we annotate the Pack() function to require a non-null return we still
                // check to help in debugging.
                if (!ValidatePackage(package, (Object)packComponent))
                    return;

                keys?.Add(key);
                db.Set(key, package, scopedPath);
                if (debug)
                {
                    Debug.Log(
                        $"[{nameof(PackSystem)}] {nameof(IPackableComponent)} added ({key}:{packComponent.GetType().Name})",
                        (Component)packComponent);
                }
            }
            catch (Exception e)
            {
                HandlePackException(e, packComponent?.GetType(), string.Empty, packComponent as Object);
            }
        }

        private void HandlePackException(Exception e, Type type, string info, Object context)
        {
            if (isPedantic)
            {
                throw e;
            }

            Debug.LogWarning(
                $"[{nameof(PackSystem)}] An exception occured during packing of {type.Name} ({info}); Check the exception log below for details.",
                context);

            Debug.LogException(e);
        }

        private static string[] GetLoadedSceneNames()
        {
            string[] sceneNames = new string[SceneManager.sceneCount];
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                sceneNames[i] = SceneManager.GetSceneAt(i).name;
            }

            return sceneNames;
        }

        /// <summary>
        /// Checks whether a given package is valid. This is used to detect null packages which are not allowed. 
        /// </summary>
        private static bool ValidatePackage(
            [NotNull] object package,
            [NotNull] Object context
        )
        {
            if (package == null)
            {
                Debug.LogWarning(
                    $"[{nameof(PackSystem)}] Packing of {context.GetType().Name} returned a null object and will be ignored",
                    context);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether a given key is already present in the database. This is used to detect duplicate keys which are not allowed.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="key"></param>
        /// <param name="scopedPath"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private static bool ValidateNoDuplicate(
            [NotNull] PackDB db,
            [NotNull] string key,
            [NotNull] string scopedPath,
            [NotNull] Object context
        )
        {
            if (db.Contains(key, scopedPath))
            {
                Debug.LogWarning(
                    $"[{nameof(PackSystem)}] ({context.GetType().Name})",
                    (Object)context);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether a given key is valid. This is used to detect default keys which are not allowed.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="context">The context to use for logging any warnings.</param>
        /// <returns>Whether the key is valid.</returns>
        private static bool ValidateKey(
            [NotNull] string key,
            [NotNull] Object context
        )
        {
            if (key.StartsWith("00000000"))
            {
                Debug.LogWarning(
                    $"[{nameof(PackSystem)}] An instance of {context.GetType().Name} has no unique key, it will be ignored",
                    context);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Conforms a given string, presumed to be a filename, to a valid filename.
        /// </summary>
        /// <param name="value">The filename to conform.</param>
        /// <returns>Always true.</returns>
        private bool ConformDefaultFileName([NotNull] string value)
        {
            defaultFileName = SanitizeForFilename(value);
            return true;
        }

        /// <summary>
        /// Conforms a given string, presumed to be a file extension, to a valid extension name.
        /// </summary>
        /// <param name="value">The extension to conform.</param>
        /// <returns>Always true.</returns>
        private bool ConformFileExtension([NotNull] string value)
        {
            fileExtension = SanitizeForExtension(value);
            return true;
        }

        /// <summary>
        /// Conforms a given string, presumed to be a folder name, to a valid folder name.
        /// </summary>
        /// <param name="value">The filename to conform.</param>
        /// <returns>Always true.</returns>
        private bool ConformFolderName([NotNull] string value)
        {
            // we conform to a filename to block any attempts to create subfolders.
            folderName = SanitizeForFilename(folderName);
            return true;
        }

        /// <summary>
        /// Creates a new instance of a prefab based on the asset ID of the given package.
        /// </summary>
        /// <param name="package">The package that defines which prefab to restore and where.</param>
        /// <param name="instances">A list to which the created instance will be added.</param>
        private void CreatePrefabInstance(
            [NotNull] PackIdentityData package,
            [MaybeNull] List<PackIdentity> instances
        )
        {
            // call the spawner to instantiate the object
            if (OnGetPrefabInstance(package, out GameObject instance))
            {
                PackIdentity packIdentity = instance.GetComponent<PackIdentity>();
                Debug.Assert(packIdentity != null, "packIdentity != null", this);
                Guid id = Guid.Parse(package.PackKey);
                packIdentity.Internal__OverrideEntityID(id);
                instances?.Add(packIdentity);

                if (debug)
                {
                    Debug.Log(
                        $"[{nameof(PackSystem)}] Spawned {instance.name} for key {package.PackKey}",
                        instance);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(PackSystem)}] No prefab found with ID {package.AssetID} while restoring key {package.PackKey}");
            }
        }

        /// <summary>
        /// Default implementation of GameObject instantiation with a basic lookup table. Can be overriden if more complex
        /// instantiation is required.
        /// </summary>
        /// <param name="package">The data-package to restore.</param>
        /// <param name="instance">The instantiated GameObject.</param>
        /// <returns></returns>
        protected virtual bool OnGetPrefabInstance(PackIdentityData package, out GameObject instance)
        {
            Scene scene = default;
            if (package.Scope.StartsWith(SCENE_PREFIX))
            {
                string sceneName = package.Scope[SCENE_PREFIX.Length..];
                scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.isLoaded)
                {
                    Debug.LogWarning(
                        $"[{nameof(PackSystem)}] Cannot restore object with scope '{package.Scope}' that is not currently loaded.",
                        this);
                    instance = default;
                    return false;
                }
            }

            if (!assetLookup.Contains(package.AssetID))
            {
                instance = default;
                return false;
            }

            GameObject prefab = assetLookup.GetPrefabByID(package.AssetID);
            prefab.SetActive(false);
            instance = Instantiate(
                prefab,
                package.Position,
                package.Rotation
            );

            if (scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            }

            prefab.SetActive(true);
            return true;
        }

        /// <summary>
        /// Called after saving has completed.
        /// </summary>
        protected virtual void OnSave()
        {
        }

        /// <summary>
        /// Called after loading has completed.
        /// </summary>
        protected virtual void OnLoad()
        {
        }

        /// <summary>
        /// Ensures that a database instance exists.
        /// </summary>
        private void EnsureDb()
        {
            if (_db != null)
                return;

            PackSettings settings = new PackSettings(
                Path.Combine(Application.persistentDataPath, folderName),
                Path.Combine($"{defaultFileName}{fileExtension}"),
                Backend.Json,
                Location.File,
                EncryptionType.None,
                CompressionType.None
            );
            _db = new PackDB(settings);
        }

        /// <summary>
        /// Ensures that a database instance exists and the currently selected path is loaded.
        /// </summary>
        private async UniTask EnsureLoadedAsync(string path)
        {
            EnsureDb();

            if (debug && !_db.IsLoaded(path))
            {
                Debug.Log($"[{nameof(PackSystem)}] Loading database ...");
            }

            await _db.EnsureLoadedAsync(path);

            if (!_db.Contains(PackedScriptableObjects, path))
            {
                _db.Set(PackedScriptableObjects, new HashSet<string>(), path);
            }

            if (!_db.Contains(PackedGameObjects, path))
            {
                _db.Set(PackedGameObjects, new HashSet<string>(), path);
            }

            if (!_db.Contains(PackedComponents, path))
            {
                _db.Set(PackedComponents, new HashSet<string>(), path);
            }
        }

        /// <summary>
        /// Sanitizes a given string to be used as a filename.
        /// </summary>
        /// <param name="str">The string to sanitize.</param>
        /// <returns>The sanitized string.</returns>
        public static string SanitizeForFilename(string str)
        {
            string sanitized = Regex.Replace(str, @"[^A-Za-z0-9-]", "_"); // replace all non-alphanum with '_'
            return string.IsNullOrEmpty(sanitized) ? "undefined" : sanitized;
        }

        /// <summary>
        /// Extracts the base filename from a given string. Removes all suffixes and extensions (including versions). This
        /// assumes that the string is a filename that was sanitized and constructed using <see cref="PackSystem"/>.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string GetBaseFilename(string str)
        {
            // remove all suffixes and extensions (including versions)
            string sanitized = Regex.Replace(Path.GetFileName(str), @"\..*", "");
            return string.IsNullOrEmpty(sanitized) ? "undefined" : sanitized;
        }

        /// <summary>
        /// Prepare a given string to be used as a file extension.
        /// </summary>
        /// <param name="str">The string to sanitize.</param>
        /// <returns>The sanitized string.</returns>
        public static string SanitizeForExtension(string str)
        {
            string sanitized = Regex.Replace(str, @"[^A-Za-z0-9-.]", "_"); // replace all non-alphanum with '_'
            return string.IsNullOrEmpty(sanitized) ? "undefined" : sanitized;
        }
    }

    [Serializable]
    public class PackContext
    {
        public string[] LoadedScenes;
        public string ActiveScene;
    }
}