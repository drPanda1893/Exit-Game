using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Abstract base class for all level builder EditorWindows.
/// Provides shared player setup, animation controller creation,
/// and background music wiring that every level needs.
/// </summary>
public abstract class LevelBuilderBase : EditorWindow
{
    // ═══════════════════════════════════════════════════════════════════════
    // Player
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates the BigYahu player character in <paramref name="scene"/> at <paramref name="spawnPos"/>.
    /// Tries to load the real idle/run FBX models; falls back to a capsule placeholder.
    /// </summary>
    protected GameObject AddPlayer(Scene scene, Vector3 spawnPos)
    {
        GameObject idleModel    = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu standing.fbx");
        GameObject runningModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Big Yahu/Big Yahu jogging.fbx");
        Material   playerMat    = AssetDatabase.LoadAssetAtPath<Material>("Assets/Big Yahu/Big Yahu material.mat");

        var character = new GameObject("BigYahu") { tag = "Player" };
        character.transform.position = spawnPos;

        if (idleModel != null && runningModel != null)
        {
            var idle = (GameObject)PrefabUtility.InstantiatePrefab(idleModel);
            PrefabUtility.UnpackPrefabInstance(idle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            idle.name = "IdleModel";
            idle.transform.SetParent(character.transform, false);
            idle.SetActive(true);
            try
            {
                SetupLoopController(idle,
                    "Assets/Big Yahu/Big Yahu standing.fbx",
                    "Assets/Big Yahu/BigYahu_Stand_Loop.anim",
                    "Assets/Big Yahu/BigYahu_Stand.controller",
                    "Stand");
            }
            catch (System.Exception e) { Debug.LogWarning("Stand-Anim: " + e.Message); }

            var run = (GameObject)PrefabUtility.InstantiatePrefab(runningModel);
            PrefabUtility.UnpackPrefabInstance(run, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            run.name = "RunningModel";
            run.transform.SetParent(character.transform, false);
            run.SetActive(false);
            try
            {
                SetupLoopController(run,
                    "Assets/Big Yahu/Big Yahu jogging.fbx",
                    "Assets/Big Yahu/BigYahu_Run_Loop.anim",
                    "Assets/Big Yahu/BigYahu_Run.controller",
                    "Run");
            }
            catch (System.Exception e) { Debug.LogWarning("Run-Anim: " + e.Message); }

            if (playerMat != null)
                foreach (var r in character.GetComponentsInChildren<Renderer>(true))
                    r.material = playerMat;
        }
        else
        {
            Debug.LogWarning("Big Yahu Modelle nicht gefunden. Platzhalter-Kapsel wird verwendet.");
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(character.transform, false);
            capsule.transform.localPosition = new Vector3(0, 1f, 0);
        }

        var col = character.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.3f;
        col.center = new Vector3(0, 0.9f, 0);

        var rb = character.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;

        character.AddComponent<CharacterAnimator>();
        character.AddComponent<PlayerController>();

        SceneManager.MoveGameObjectToScene(character, scene);
        return character;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Animation
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a looping AnimatorController for <paramref name="instance"/> from an FBX source clip.
    /// The controller and clip assets are written to disk at the supplied paths.
    /// </summary>
    protected void SetupLoopController(GameObject instance, string fbxPath, string clipPath,
                                       string ctrlPath, string stateName)
    {
        // Find the first non-preview clip inside the FBX
        AnimationClip src = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { src = c; break; }
        if (src == null) return;

        // Write looping clip asset
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
            AssetDatabase.DeleteAsset(clipPath);
        var loop = Object.Instantiate(src);
        loop.name = stateName + "_Loop";
        var settings = AnimationUtility.GetAnimationClipSettings(loop);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(loop, settings);
        AssetDatabase.CreateAsset(loop, clipPath);

        // Write AnimatorController asset
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null)
            AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm   = ctrl.layers[0].stateMachine;
        var st   = sm.AddState(stateName);
        st.motion = loop;
        sm.defaultState = st;
        AssetDatabase.SaveAssets();

        var anim = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Audio
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a "BackgroundMusic" GameObject in <paramref name="scene"/> wired to
    /// the shared ambient track (Assets/Big Yahu/Untitled.mp3).
    /// </summary>
    protected void AddBackgroundMusic(Scene scene)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Big Yahu/Untitled.mp3");
        if (clip == null) { Debug.LogWarning("[Music] Untitled.mp3 nicht gefunden."); return; }

        var go  = new GameObject("BackgroundMusic");
        var src = go.AddComponent<AudioSource>();
        src.clip         = clip;
        src.loop         = true;
        src.playOnAwake  = true;
        src.volume       = 0.6f;
        src.spatialBlend = 0f;
        go.AddComponent<BackgroundMusic>();
        SceneManager.MoveGameObjectToScene(go, scene);
    }
}
