using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeSceneBuilder
{
    // Kept around only for its animation clip now (its own mesh has no usable
    // texture) - see PlayerCharacterFbx for the visual mesh.
    const string KickerSourceFbx = "Assets/Mixamo/Strike Foward Jog.fbx";
    const string KickerTripFbx = "Assets/Mixamo/Soccer Trip.fbx";
    const string PlayerCharacterFbx = "Assets/Mixamo/Player Character.fbx";

    // Downloaded "With Skin" directly on the textured CH38_NONPBR character,
    // with Mixamo's own Mirror option used for the left dive - no in-Unity
    // mirroring or extra root-motion scripting needed for these.
    const string KeeperIdleFbx = "Assets/Mixamo/Goalkeeper Idle.fbx";
    const string KeeperCatchFbx = "Assets/Mixamo/Goalkeeper Catch.fbx";
    const string KeeperDiveRightFbx = "Assets/Mixamo/Goalkeeper Dive Right.fbx";
    const string KeeperDiveLeftFbx = "Assets/Mixamo/Goalkeeper Dive Left.fbx";

    class HumanoidRig
    {
        public Transform root;
        public Transform hips;
        public Transform leftHipPivot;
        public Transform rightHipPivot;
        public Transform leftShoulderPivot;
        public Transform rightShoulderPivot;
    }

    static readonly bool[][] DigitSegments =
    {
        new[] { true, true, true, true, true, true, false },   // 0
        new[] { false, true, true, false, false, false, false }, // 1
        new[] { true, true, false, true, true, false, true },  // 2
        new[] { true, true, true, true, false, false, true },  // 3
        new[] { false, true, true, false, false, true, true }, // 4
        new[] { true, false, true, true, false, true, true },  // 5
        new[] { true, false, true, true, true, true, true },   // 6
        new[] { true, true, true, false, false, false, false }, // 7
        new[] { true, true, true, true, true, true, true },    // 8
        new[] { true, true, true, true, false, true, true },   // 9
    };

    [MenuItem("Happygoal/2) Build Penalty Prototype Scene")]
    public static void BuildScene()
    {
        // Picks up any files copied into Assets/ from outside Unity (audio,
        // new Mixamo downloads, etc.) that haven't been imported yet.
        AssetDatabase.Refresh();
        EnsureTags();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.6f, 0.75f, 0.9f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.3f, 0.2f);

        var pitch = GameObject.CreatePrimitive(PrimitiveType.Plane);
        pitch.name = "Pitch";
        pitch.transform.position = new Vector3(0, 0, 6);
        pitch.transform.localScale = new Vector3(4f, 1f, 4f);
        var grassTex = SaveAndImportTexture(GenerateGrassTexture(), "Assets/Generated/Textures/Grass.png", false);
        ApplyTexturedMaterial(pitch, grassTex, Color.white, new Vector2(2f, 10f));

        var stadium = new GameObject("Stadium");
        // The original Football Freekick project's own scene places its Goal
        // at (0, 0.082, 7.12) and its Football Arena at (34.75, 0.12, -103) -
        // i.e. the arena sits (34.75, 0.038, -110.12) away from the goal.
        // Our goal is at world (0, 0, 11), so applying that same relative
        // offset nests the arena around it the way the original author tuned
        // it, instead of guessing a position ourselves.
        var arenaModel = TryInstantiateFreekickArena(stadium.transform, new Vector3(34.75f, 0.038f, -99.12f));
        if (arenaModel == null)
        {
            var crowdTex = SaveAndImportTexture(GenerateCrowdTexture(), "Assets/Generated/Textures/Crowd.png", false);
            BuildStandBehindGoal(stadium.transform, crowdTex);
            BuildSideStand(stadium.transform, 1f, crowdTex);
            BuildSideStand(stadium.transform, -1f, crowdTex);
        }

        var goal = new GameObject("Goal");
        goal.transform.position = new Vector3(0, 0, 11f);

        float halfWidth = 3.66f;
        float height = 2.44f;
        float postRadius = 0.06f;

        var leftPost = CreatePost("LeftPost", goal.transform, new Vector3(-halfWidth, height / 2f, 0), height, postRadius);
        var rightPost = CreatePost("RightPost", goal.transform, new Vector3(halfWidth, height / 2f, 0), height, postRadius);

        var crossbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        crossbar.name = "Crossbar";
        crossbar.transform.SetParent(goal.transform);
        crossbar.transform.localPosition = new Vector3(0, height, 0);
        crossbar.transform.localRotation = Quaternion.Euler(0, 0, 90);
        crossbar.transform.localScale = new Vector3(postRadius * 2f, halfWidth, postRadius * 2f);
        ApplyColor(crossbar, Color.white);

        var netPhysics = new PhysicsMaterial("NetPhysics")
        {
            bounciness = 0.15f,
            dynamicFriction = 0.6f,
            staticFriction = 0.6f,
            frictionCombine = PhysicsMaterialCombine.Maximum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        float netDepth = 1.2f;

        var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
        net.name = "NetBackdrop";
        net.transform.SetParent(goal.transform);
        net.transform.localPosition = new Vector3(0, height / 2f, netDepth);
        net.transform.localScale = new Vector3(halfWidth * 2f, height, 0.05f);
        net.GetComponent<Collider>().material = netPhysics;
        ApplyTransparent(net, new Color(0.9f, 0.9f, 0.9f, 0.35f));

        // Invisible side/top panels so a scored ball is fully enclosed
        // instead of escaping past the net's edges once it's behind the
        // goal line.
        BuildNetWall(goal.transform, "NetLeft", new Vector3(-halfWidth, height / 2f, netDepth / 2f), new Vector3(0.05f, height, netDepth), netPhysics);
        BuildNetWall(goal.transform, "NetRight", new Vector3(halfWidth, height / 2f, netDepth / 2f), new Vector3(0.05f, height, netDepth), netPhysics);
        BuildNetWall(goal.transform, "NetTop", new Vector3(0, height, netDepth / 2f), new Vector3(halfWidth * 2f, 0.05f, netDepth), netPhysics);

        var goalLine = new GameObject("GoalLineTrigger");
        goalLine.transform.SetParent(goal.transform);
        goalLine.transform.localPosition = new Vector3(0, height / 2f, 0.4f);
        var glCollider = goalLine.AddComponent<BoxCollider>();
        glCollider.isTrigger = true;
        glCollider.size = new Vector3(halfWidth * 2f - 0.1f, height - 0.1f, 0.4f);
        goalLine.tag = "GoalLine";

        // Gameplay colliders above (posts, net containment walls, goal line
        // trigger) stay on our own primitives either way since their
        // coordinates are tuned and trusted - this only swaps what's visible.
        var goalVisual = TryInstantiateFreekickGoal(goal.transform, out var netRipple);
        if (goalVisual != null)
        {
            SetRendererEnabled(leftPost, false);
            SetRendererEnabled(rightPost, false);
            SetRendererEnabled(crossbar, false);
            SetRendererEnabled(net, false);

            // Align the model's own front edge (open mouth, the face closest
            // to the kicker - i.e. its smallest world Z) with our actual goal
            // line, instead of assuming the model's pivot already sits there.
            var b = ComputeRendererBounds(goalVisual);
            float zOffset = goal.transform.position.z - b.min.z;
            goalVisual.transform.localPosition += new Vector3(0, 0, zOffset);
            Debug.Log($"GoalVisual bounds before alignment: center={b.center} size={b.size} min={b.min} max={b.max} - shifted by {zOffset} on Z to put its front face at our goal line (world Z={goal.transform.position.z}).");
        }

        var goalCenterMarker = new GameObject("GoalCenterPoint");
        goalCenterMarker.transform.SetParent(goal.transform);
        goalCenterMarker.transform.localPosition = Vector3.zero;

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.position = new Vector3(0, 0.11f, 0f);
        ball.transform.localScale = Vector3.one * 0.22f;
        ApplyBallMaterial(ball);
        var rb = ball.AddComponent<Rigidbody>();
        rb.mass = 0.43f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;
        var ballScript = ball.AddComponent<BallController>();
        ballScript.netRipple = netRipple;
        ball.AddComponent<BallSkinManager>();

        var trail = ball.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.09f;
        trail.endWidth = 0.01f;
        trail.minVertexDistance = 0.04f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 1f, 1f, 0.55f);
        trail.endColor = new Color(1f, 1f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var faceTex = SaveAndImportTexture(GenerateFaceTexture(), "Assets/Generated/Textures/Face.png", true);

        bool useMixamo = IsMixamoConfigured();
        GoalkeeperAnimatorBase keeperScript;
        KickerAnimatorBase kickerAnim;

        if (useMixamo)
        {
            // Placeholder team/skin colors until these come from Flutter per
            // match - TintCharacterKit is what actually applies them, so
            // swapping these is all that's needed to support real teams later.
            Color kickerShirt = new Color(0.1f, 0.3f, 0.9f);
            Color keeperShirt = new Color(1f, 0.6f, 0f);
            Color skinTone = new Color(0.87f, 0.68f, 0.52f);

            keeperScript = BuildMixamoKeeper(new Vector3(0, 0, 10.3f), keeperShirt, skinTone);
            kickerAnim = BuildMixamoKicker(new Vector3(-0.6f, 0, -1.2f), kickerShirt, skinTone);
            Debug.Log("Using Mixamo characters.");
        }
        else
        {
            var keeperRig = BuildHumanoid("Keeper", new Vector3(0, 0, 10.3f), new Color(1f, 0.6f, 0f), new Color(0.9f, 0.72f, 0.56f), 1, faceTex, true);
            keeperRig.root.rotation = Quaternion.Euler(0, 180f, 0);
            var keeperGO = keeperRig.root.gameObject;
            var keeperCollider = keeperGO.AddComponent<CapsuleCollider>();
            keeperCollider.center = new Vector3(0, 0.95f, 0);
            keeperCollider.height = 1.9f;
            keeperCollider.radius = 0.32f;
            keeperGO.tag = "Keeper";
            var keeperRb = keeperGO.AddComponent<Rigidbody>();
            keeperRb.isKinematic = true;
            var proceduralKeeper = keeperGO.AddComponent<GoalkeeperController>();
            proceduralKeeper.hips = keeperRig.hips;
            proceduralKeeper.leftShoulderPivot = keeperRig.leftShoulderPivot;
            proceduralKeeper.rightShoulderPivot = keeperRig.rightShoulderPivot;
            keeperScript = proceduralKeeper;

            var kickerRig = BuildHumanoid("Kicker", new Vector3(-0.6f, 0, -1.2f), new Color(0.1f, 0.3f, 0.9f), new Color(0.9f, 0.72f, 0.56f), 9, faceTex, true);
            kickerRig.root.rotation = Quaternion.Euler(0, 15f, 0);
            var proceduralKicker = kickerRig.root.gameObject.AddComponent<KickerAnimator>();
            proceduralKicker.hips = kickerRig.hips;
            proceduralKicker.kickingLeg = kickerRig.rightHipPivot;
            proceduralKicker.plantLeg = kickerRig.leftHipPivot;
            proceduralKicker.rightArm = kickerRig.rightShoulderPivot;
            proceduralKicker.leftArm = kickerRig.leftShoulderPivot;
            kickerAnim = proceduralKicker;

            Debug.Log("Mixamo characters not configured - using procedural placeholders. Run Happygoal > 1) Configure Mixamo Imports first, then rebuild.");
        }

        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.transform.position = new Vector3(0f, 1.7f, -3.2f);
        camGO.transform.LookAt(new Vector3(0f, 1.2f, 8f));
        cam.fieldOfView = 55f;
        camGO.AddComponent<AudioListener>();
        var camEffects = camGO.AddComponent<CameraEffects>();

        // Reverse-angle camera used while the player defends - positioned
        // behind/above the keeper looking out at the pitch, like a real
        // goalkeeper broadcast cam. Off by default; GameManager swaps to it
        // when the control mode flips to PlayerDefends.
        var gkCamGO = new GameObject("Goalkeeper Camera");
        var gkCam = gkCamGO.AddComponent<Camera>();
        // Pulled back far enough to fit the full goal width (posts are 3.66m
        // either side) in frame, not just the keeper/kicker close up.
        gkCamGO.transform.position = new Vector3(0f, 3.2f, 18f);
        gkCamGO.transform.LookAt(new Vector3(0f, 1.0f, 0f));
        gkCam.fieldOfView = 55f;
        var gkListener = gkCamGO.AddComponent<AudioListener>();
        var gkCamEffects = gkCamGO.AddComponent<CameraEffects>();
        gkCam.enabled = false;
        gkListener.enabled = false;

        var confettiPS = BuildConfettiSystem(null);

        var gmGO = new GameObject("GameManager");
        var gm = gmGO.AddComponent<GameManager>();
        var input = gmGO.AddComponent<PenaltyKickInput>();
        var keeperInput = gmGO.AddComponent<GoalkeeperInput>();

        gm.ball = ballScript;
        gm.keeper = keeperScript;
        gm.input = input;
        gm.kicker = kickerAnim;
        gm.goalkeeperInput = keeperInput;
        gm.goalCenterPoint = goalCenterMarker.transform;
        gm.aiGoalHalfWidth = halfWidth - 0.3f;
        gm.aiGoalHeight = height - 0.2f;
        gm.cameraEffects = camEffects;
        gm.kickerCamera = cam;
        gm.kickerListener = camGO.GetComponent<AudioListener>();
        gm.goalkeeperCamera = gkCam;
        gm.goalkeeperListener = gkListener;
        gm.goalkeeperCameraEffects = gkCamEffects;
        gm.confetti = confettiPS;
        gm.gameAudio = BuildGameAudio(gmGO);

        var weatherGO = new GameObject("Weather");
        var weather = weatherGO.AddComponent<WeatherController>();
        weather.sun = light;
        weather.rain = BuildWeatherParticles(weatherGO.transform, "Rain", true);
        weather.snow = BuildWeatherParticles(weatherGO.transform, "Snow", false);

        var pitchSkin = pitch.AddComponent<PitchSkinManager>();
        pitchSkin.pitchRenderer = pitch.GetComponent<Renderer>();
        pitchSkin.sun = light;
        pitchSkin.weather = weather;
        input.ball = ballScript;
        input.kicker = kickerAnim;
        input.goalCenterPoint = goalCenterMarker.transform;
        input.goalHalfWidth = halfWidth - 0.3f;
        input.goalHeight = height - 0.2f;
        keeperInput.ball = ballScript;
        keeperInput.keeper = keeperScript;

        string scenesDir = "Assets/Scenes";
        if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);
        string scenePath = scenesDir + "/PenaltyPrototype.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

        Debug.Log("Penalty prototype scene built at " + scenePath);
    }

    // ---- Mixamo import + character setup -----------------------------------

    [MenuItem("Happygoal/1) Configure Mixamo Imports")]
    public static void ConfigureMixamoImports()
    {
        AssetDatabase.Refresh();

        // Each Mixamo download uses a differently-numbered bone prefix
        // (mixamorig:, mixamorig5:, mixamorig9:, ...), so "copy avatar from
        // another file" fails on a literal transform-name mismatch. Humanoid
        // retargeting works in an abstract muscle space regardless of the
        // source skeleton's naming, so every file just configures its own
        // self-contained avatar independently.
        ConfigureAvatarSource(KickerSourceFbx);
        ConfigureAvatarSource(KickerTripFbx);
        ConfigureAvatarSource(PlayerCharacterFbx, extractTextures: true);

        ConfigureAvatarSource(KeeperIdleFbx, extractTextures: true);
        ConfigureAvatarSource(KeeperCatchFbx, extractTextures: true);
        ConfigureAvatarSource(KeeperDiveRightFbx, extractTextures: true);
        ConfigureAvatarSource(KeeperDiveLeftFbx, extractTextures: true);
        SetClipLooping(KeeperIdleFbx, true);

        Debug.Log("Mixamo imports configured. Check the Console above for any warnings, then run 'Happygoal > 2) Build Penalty Prototype Scene'.");

        InspectMaterials(PlayerCharacterFbx);
    }

    static bool IsMixamoConfigured()
    {
        var kickerImporter = AssetImporter.GetAtPath(KickerSourceFbx) as ModelImporter;
        var keeperImporter = AssetImporter.GetAtPath(KeeperIdleFbx) as ModelImporter;
        var playerImporter = AssetImporter.GetAtPath(PlayerCharacterFbx) as ModelImporter;
        return kickerImporter != null && kickerImporter.animationType == ModelImporterAnimationType.Human
            && keeperImporter != null && keeperImporter.animationType == ModelImporterAnimationType.Human
            && playerImporter != null && playerImporter.animationType == ModelImporterAnimationType.Human;
    }

    static void ConfigureAvatarSource(string path, bool extractTextures = false)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning("Mixamo FBX not found: " + path);
            return;
        }
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

        if (extractTextures)
        {
            // Make sure Unity actually reads embedded materials/textures from
            // the FBX instead of leaving everything on the flat default
            // material, which is what a blank/white character usually means.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
        }

        importer.SaveAndReimport();

        if (extractTextures)
        {
            string texDir = "Assets/Mixamo/Textures";
            Directory.CreateDirectory(texDir);
            importer.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            importer.SaveAndReimport();
        }

        Avatar avatar = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Avatar a) { avatar = a; break; }
        }

        if (avatar != null && avatar.isValid && avatar.isHuman)
        {
            Debug.Log($"OK  {path}");
        }
        else
        {
            Debug.LogError($"FAILED avatar for {path} (found={avatar != null}, valid={avatar != null && avatar.isValid}, human={avatar != null && avatar.isHuman})");
        }
    }

    static void SetClipLooping(string fbxPath, bool loop)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
        if (importer == null) return;

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;

        for (int i = 0; i < clips.Length; i++) clips[i].loopTime = loop;

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    // Prints every material on the model to the Console (name + whether it
    // has a texture assigned) so we can see, from the Console output alone,
    // whether the jersey/shorts/skin are separate paintable materials or one
    // combined atlas, without needing to click through the Inspector.
    static void InspectMaterials(string fbxPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null)
        {
            Debug.LogWarning("Could not load model to inspect: " + fbxPath);
            return;
        }

        var renderers = model.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"--- Material inspection for {fbxPath} ({renderers.Length} renderer(s)) ---");
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null) { Debug.Log("  (null material slot)"); continue; }
                var tex = mat.mainTexture;
                Debug.Log($"  Material '{mat.name}' on '{renderer.name}' - texture: {(tex != null ? tex.name : "NONE")}");
            }
        }
    }

    // Boots cosmetics need to know whether the Mixamo character's shoes are
    // their own renderer/material (tintable like Ch38_Shirt/Socks/Body/Hair
    // already are in TintCharacterKit) or just painted into the Ch38_Body
    // texture (not isolatable with a simple tint). Run this and paste the
    // '--- Material inspection for ...' output back to Claude before wiring
    // up a BootsSkinManager - guessing a renderer name that doesn't exist
    // would silently no-op instead of erroring.
    [MenuItem("Happygoal/4) Inspect Character Renderers (for boots skins)")]
    public static void InspectCharacterRenderers()
    {
        AssetDatabase.Refresh();
        InspectMaterials(PlayerCharacterFbx);
        InspectMaterials(KeeperIdleFbx);
        Debug.Log("Character renderer inspection done - paste the '--- Material inspection for ...' sections above back to Claude.");
    }

    const string FreekickGoalFbx = "Assets/FreekickAssets/Models/Goal.fbx";
    const string FreekickArenaFbx = "Assets/FreekickAssets/Models/Football Arena.fbx";
    const string FreekickBallFbx = "Assets/FreekickAssets/Models/Ball.fbx";

    [MenuItem("Happygoal/3) Inspect Freekick Assets")]
    public static void InspectFreekickAssets()
    {
        AssetDatabase.Refresh();
        ConfigureGenericModel(FreekickGoalFbx);
        ConfigureGenericModel(FreekickArenaFbx);
        ConfigureGenericModel(FreekickBallFbx);

        InspectHierarchy(FreekickGoalFbx);
        InspectHierarchy(FreekickArenaFbx);
        InspectHierarchy(FreekickBallFbx);

        Debug.Log("Freekick asset inspection done - paste the '--- Hierarchy for ...' sections above back to Claude.");
    }

    static void ConfigureGenericModel(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogWarning("Model not found: " + path);
            return;
        }
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        // NetRipple needs to read and rewrite the net mesh's vertices at
        // runtime - without this the mesh is stripped to GPU-only data.
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    // Logs the full child hierarchy of a model - name, component types, and
    // for renderers, whether they're skinned (needed for Unity's Cloth
    // component) or a plain mesh - so the net/goal/ball can be wired up
    // correctly instead of guessed at.
    static void InspectHierarchy(string fbxPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null)
        {
            Debug.LogWarning("Could not load model to inspect: " + fbxPath);
            return;
        }

        Debug.Log($"--- Hierarchy for {fbxPath} ---");
        LogTransformRecursive(model.transform, 0);
    }

    static void LogTransformRecursive(Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        var parts = new System.Collections.Generic.List<string>();

        var mf = t.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) parts.Add($"MeshFilter(verts={mf.sharedMesh.vertexCount})");

        var mr = t.GetComponent<MeshRenderer>();
        if (mr != null) parts.Add($"MeshRenderer(mats={mr.sharedMaterials.Length})");

        var smr = t.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null) parts.Add($"SkinnedMeshRenderer(verts={smr.sharedMesh.vertexCount},mats={smr.sharedMaterials.Length})");

        string info = parts.Count > 0 ? " [" + string.Join(", ", parts) + "]" : "";
        Debug.Log($"{indent}{t.name}{info}");

        foreach (Transform child in t)
        {
            LogTransformRecursive(child, depth + 1);
        }
    }

    static AnimationClip FindFirstClip(string fbxPath)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        }
        Debug.LogWarning("No AnimationClip found in " + fbxPath);
        return null;
    }

    static GoalkeeperAnimatorBase BuildMixamoKeeper(Vector3 groundPosition, Color shirtColor, Color skinColor)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterFbx);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Keeper";
        instance.transform.position = groundPosition;
        instance.transform.rotation = Quaternion.Euler(0, 180f, 0);
        TintCharacterKit(instance, shirtColor, skinColor);
        instance.AddComponent<BootsSkinManager>();

        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        // MixamoGoalkeeperAnimator implements OnAnimatorMove and takes only
        // the clip's vertical (fall-to-ground) delta each frame; applyRootMotion
        // must be true for Unity to route root motion through that callback
        // at all. Horizontal movement is fully scripted regardless.
        animator.applyRootMotion = true;
        animator.runtimeAnimatorController = BuildKeeperAnimatorController();

        var collider = instance.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0, 0.9f, 0);
        collider.height = 1.8f;
        collider.radius = 0.3f;
        instance.tag = "Keeper";

        var rb = instance.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var script = instance.AddComponent<MixamoGoalkeeperAnimator>();
        script.animator = animator;
        return script;
    }

    static KickerAnimatorBase BuildMixamoKicker(Vector3 groundPosition, Color shirtColor, Color skinColor)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCharacterFbx);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "Kicker";
        instance.transform.position = groundPosition;
        instance.transform.rotation = Quaternion.Euler(0, 15f, 0);
        TintCharacterKit(instance, shirtColor, skinColor);
        instance.AddComponent<BootsSkinManager>();

        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.applyRootMotion = true;
        animator.runtimeAnimatorController = BuildKickerAnimatorController();

        var script = instance.AddComponent<MixamoKickerAnimator>();
        script.animator = animator;
        return script;
    }

    // Ch38_Shirt, Ch38_Body, Ch38_Shorts and Ch38_Socks are separate meshes
    // that currently all reference the same shared material/texture atlas -
    // cloning that material per mesh and tinting the clone lets us recolor
    // the jersey (and socks, which usually match) independently of skin tone
    // without touching the other pieces, since each clone is its own asset
    // even though they start from the same source texture. The base texture
    // is light/pale, so a multiply tint reproduces real colors accurately
    // (white * red = red) rather than looking muddy.
    static readonly Color HairColor = new Color(0.045f, 0.04f, 0.045f);

    static void TintCharacterKit(GameObject root, Color shirtColor, Color skinColor)
    {
        TintChildRenderer(root, "Ch38_Shirt", shirtColor);
        TintChildRenderer(root, "Ch38_Socks", shirtColor);
        TintChildRenderer(root, "Ch38_Body", skinColor);
        TintChildRenderer(root, "Ch38_Hair", HairColor);
    }

    static void TintChildRenderer(GameObject root, string childName, Color tint)
    {
        var t = FindDeepChild(root.transform, childName);
        if (t == null) return;
        var renderer = t.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return;

        var mat = new Material(renderer.sharedMaterial);
        mat.color = tint;
        renderer.sharedMaterial = mat;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static AnimatorController BuildKickerAnimatorController()
    {
        string dir = "Assets/Generated/Animators";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = dir + "/KickerController.controller";
        AssetDatabase.DeleteAsset(path);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var stateMachine = controller.layers[0].stateMachine;

        var kickState = stateMachine.AddState("Kick");
        kickState.motion = FindFirstClip(KickerSourceFbx);
        stateMachine.defaultState = kickState;

        return controller;
    }

    static AnimatorController BuildKeeperAnimatorController()
    {
        string dir = "Assets/Generated/Animators";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = dir + "/KeeperController.controller";
        AssetDatabase.DeleteAsset(path);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var stateMachine = controller.layers[0].stateMachine;

        var idleState = stateMachine.AddState("Idle");
        idleState.motion = FindFirstClip(KeeperIdleFbx);
        stateMachine.defaultState = idleState;

        var catchState = stateMachine.AddState("Catch");
        catchState.motion = FindFirstClip(KeeperCatchFbx);

        var diveRightState = stateMachine.AddState("DiveRight");
        diveRightState.motion = FindFirstClip(KeeperDiveRightFbx);

        var diveLeftState = stateMachine.AddState("DiveLeft");
        diveLeftState.motion = FindFirstClip(KeeperDiveLeftFbx);

        return controller;
    }

    static HumanoidRig BuildHumanoid(string name, Vector3 groundPosition, Color shirtColor, Color skinColor, int jerseyNumber, Texture2D faceTex, bool removeColliders)
    {
        var root = new GameObject(name);
        root.transform.position = groundPosition;

        var hips = new GameObject("Hips");
        hips.transform.SetParent(root.transform, false);
        hips.transform.localPosition = new Vector3(0, 0.95f, 0);

        var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.name = "Torso";
        torso.transform.SetParent(hips.transform, false);
        torso.transform.localPosition = new Vector3(0, 0.35f, 0);
        torso.transform.localScale = new Vector3(0.5f, 0.7f, 0.3f);
        ApplyColor(torso, shirtColor);
        if (removeColliders) Object.DestroyImmediate(torso.GetComponent<Collider>());

        var numberTex = SaveAndImportTexture(GenerateNumberTexture(jerseyNumber), $"Assets/Generated/Textures/Number{jerseyNumber}.png", true);
        var numberDecal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        numberDecal.name = "JerseyNumber";
        numberDecal.transform.SetParent(torso.transform, false);
        numberDecal.transform.localPosition = new Vector3(0, 0.05f, 0.51f);
        numberDecal.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        ApplyDecalMaterial(numberDecal, numberTex);
        Object.DestroyImmediate(numberDecal.GetComponent<Collider>());

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(hips.transform, false);
        head.transform.localPosition = new Vector3(0, 0.85f, 0);
        head.transform.localScale = Vector3.one * 0.3f;
        ApplyColor(head, skinColor);
        if (removeColliders) Object.DestroyImmediate(head.GetComponent<Collider>());

        var faceDecal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        faceDecal.name = "Face";
        faceDecal.transform.SetParent(head.transform, false);
        faceDecal.transform.localPosition = new Vector3(0, 0, 0.5f);
        faceDecal.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        ApplyDecalMaterial(faceDecal, faceTex);
        Object.DestroyImmediate(faceDecal.GetComponent<Collider>());

        var leftShoulderPivot = new GameObject("LeftShoulderPivot");
        leftShoulderPivot.transform.SetParent(hips.transform, false);
        leftShoulderPivot.transform.localPosition = new Vector3(-0.3f, 0.6f, 0);
        var leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftArm.name = "LeftArm";
        leftArm.transform.SetParent(leftShoulderPivot.transform, false);
        leftArm.transform.localPosition = new Vector3(0, -0.3f, 0);
        leftArm.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
        ApplyColor(leftArm, shirtColor);
        if (removeColliders) Object.DestroyImmediate(leftArm.GetComponent<Collider>());

        var leftHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftHand.name = "LeftHand";
        leftHand.transform.SetParent(leftShoulderPivot.transform, false);
        leftHand.transform.localPosition = new Vector3(0, -0.62f, 0);
        leftHand.transform.localScale = Vector3.one * 0.16f;
        ApplyColor(leftHand, skinColor);
        if (removeColliders) Object.DestroyImmediate(leftHand.GetComponent<Collider>());

        var rightShoulderPivot = new GameObject("RightShoulderPivot");
        rightShoulderPivot.transform.SetParent(hips.transform, false);
        rightShoulderPivot.transform.localPosition = new Vector3(0.3f, 0.6f, 0);
        var rightArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightArm.name = "RightArm";
        rightArm.transform.SetParent(rightShoulderPivot.transform, false);
        rightArm.transform.localPosition = new Vector3(0, -0.3f, 0);
        rightArm.transform.localScale = new Vector3(0.12f, 0.3f, 0.12f);
        ApplyColor(rightArm, shirtColor);
        if (removeColliders) Object.DestroyImmediate(rightArm.GetComponent<Collider>());

        var rightHand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightHand.name = "RightHand";
        rightHand.transform.SetParent(rightShoulderPivot.transform, false);
        rightHand.transform.localPosition = new Vector3(0, -0.62f, 0);
        rightHand.transform.localScale = Vector3.one * 0.16f;
        ApplyColor(rightHand, skinColor);
        if (removeColliders) Object.DestroyImmediate(rightHand.GetComponent<Collider>());

        Color bootColor = new Color(0.08f, 0.08f, 0.1f);

        var leftHipPivot = new GameObject("LeftHipPivot");
        leftHipPivot.transform.SetParent(hips.transform, false);
        leftHipPivot.transform.localPosition = new Vector3(-0.14f, 0f, 0);
        BuildLeg(leftHipPivot.transform, skinColor, shirtColor, bootColor, removeColliders);

        var rightHipPivot = new GameObject("RightHipPivot");
        rightHipPivot.transform.SetParent(hips.transform, false);
        rightHipPivot.transform.localPosition = new Vector3(0.14f, 0f, 0);
        BuildLeg(rightHipPivot.transform, skinColor, shirtColor, bootColor, removeColliders);

        return new HumanoidRig
        {
            root = root.transform,
            hips = hips.transform,
            leftHipPivot = leftHipPivot.transform,
            rightHipPivot = rightHipPivot.transform,
            leftShoulderPivot = leftShoulderPivot.transform,
            rightShoulderPivot = rightShoulderPivot.transform,
        };
    }

    static void BuildLeg(Transform hipPivot, Color skinColor, Color sockColor, Color bootColor, bool removeColliders)
    {
        var upper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        upper.name = "UpperLeg";
        upper.transform.SetParent(hipPivot, false);
        upper.transform.localPosition = new Vector3(0, -0.22f, 0);
        upper.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
        ApplyColor(upper, skinColor);
        if (removeColliders) Object.DestroyImmediate(upper.GetComponent<Collider>());

        var lower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lower.name = "LowerLeg";
        lower.transform.SetParent(hipPivot, false);
        lower.transform.localPosition = new Vector3(0, -0.62f, 0);
        lower.transform.localScale = new Vector3(0.13f, 0.22f, 0.13f);
        ApplyColor(lower, sockColor);
        if (removeColliders) Object.DestroyImmediate(lower.GetComponent<Collider>());

        var boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boot.name = "Boot";
        boot.transform.SetParent(hipPivot, false);
        boot.transform.localPosition = new Vector3(0, -0.92f, 0.05f);
        boot.transform.localScale = new Vector3(0.17f, 0.1f, 0.28f);
        ApplyColor(boot, bootColor);
        if (removeColliders) Object.DestroyImmediate(boot.GetComponent<Collider>());
    }

    static ParticleSystem BuildConfettiSystem(Transform parent)
    {
        var go = new GameObject("ConfettiSystem");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0, 3.5f, 8f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 2f;
        main.startSpeed = 5f;
        main.startSize = 0.1f;
        main.gravityModifier = 1.1f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f),
                new GradientColorKey(new Color(0.2f, 0.4f, 0.9f), 0.2f),
                new GradientColorKey(new Color(0.95f, 0.85f, 0.2f), 0.4f),
                new GradientColorKey(new Color(0.2f, 0.8f, 0.3f), 0.6f),
                new GradientColorKey(new Color(0.9f, 0.5f, 0.15f), 0.8f),
                new GradientColorKey(new Color(0.8f, 0.2f, 0.8f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(gradient);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.4f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var rotOverLifetime = ps.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        var quadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        renderer.mesh = quadGO.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(quadGO);
        renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Stop();
        return ps;
    }

    static void BuildStandBehindGoal(Transform parent, Texture2D crowdTex)
    {
        var section = new GameObject("StandBehindGoal");
        section.transform.SetParent(parent, false);

        int tiers = 6;
        float tierHeight = 1.1f;
        float tierDepth = 1.6f;
        float width = 16f;
        float startZ = 13.5f;

        for (int i = 0; i < tiers; i++)
        {
            var tier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier.name = "Tier" + i;
            tier.transform.SetParent(section.transform, false);
            float y = tierHeight * (i + 0.5f);
            float z = startZ + tierDepth * i;
            tier.transform.localPosition = new Vector3(0, y, z);
            tier.transform.localScale = new Vector3(width, tierHeight, tierDepth);
            ApplyTexturedMaterial(tier, crowdTex, Color.white, new Vector2(width / 2f, 1f));
        }

        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(section.transform, false);
        roof.transform.localPosition = new Vector3(0, tierHeight * tiers + 0.3f, startZ + tierDepth * tiers * 0.6f);
        roof.transform.localScale = new Vector3(width + 1f, 0.2f, tierDepth * tiers * 0.9f);
        ApplyColor(roof, new Color(0.15f, 0.15f, 0.18f));
    }

    static void BuildSideStand(Transform parent, float xSide, Texture2D crowdTex)
    {
        var section = new GameObject("StandSide_" + (xSide > 0 ? "Right" : "Left"));
        section.transform.SetParent(parent, false);

        int tiers = 4;
        float tierHeight = 1f;
        float tierDepth = 1.4f;
        float length = 34f;
        float startX = xSide * 9f;

        for (int i = 0; i < tiers; i++)
        {
            var tier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier.name = "Tier" + i;
            tier.transform.SetParent(section.transform, false);
            float y = tierHeight * (i + 0.5f);
            float x = startX + xSide * tierDepth * i;
            tier.transform.localPosition = new Vector3(x, y, 6f);
            tier.transform.localScale = new Vector3(tierDepth, tierHeight, length);
            ApplyTexturedMaterial(tier, crowdTex, Color.white, new Vector2(1f, length / 2f));
        }
    }

    static void BuildNetWall(Transform parent, string name, Vector3 localPos, Vector3 size, PhysicsMaterial physMat)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPos;
        var collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
        collider.material = physMat;
    }

    static GameObject CreatePost(string name, Transform parent, Vector3 localPos, float height, float radius)
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = name;
        post.transform.SetParent(parent);
        post.transform.localPosition = localPos;
        post.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
        ApplyColor(post, Color.white);
        return post;
    }

    const string FreekickBallDiffuse = "Assets/FreekickAssets/Textures/Football_Diffuse.jpg";
    const string FreekickBallNormal = "Assets/FreekickAssets/Textures/Football_Normal.jpg";

    static void ApplyBallMaterial(GameObject ball)
    {
        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(FreekickBallDiffuse);
        if (diffuse == null)
        {
            var proceduralTex = SaveAndImportTexture(GenerateBallTexture(), "Assets/Generated/Textures/BallPattern.png", false);
            ApplyTexturedMaterial(ball, proceduralTex, Color.white, Vector2.one);
            return;
        }

        var normalImporter = AssetImporter.GetAtPath(FreekickBallNormal) as TextureImporter;
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(FreekickBallNormal);

        var renderer = ball.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = diffuse;
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        renderer.sharedMaterial = mat;
    }

    static ParticleSystem BuildWeatherParticles(Transform parent, string name, bool isRain)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0, 10f, 8f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = isRain ? 1.4f : 5f;
        // Zero initial speed - gravityModifier alone pulls particles down, so
        // there's no shape-emission-direction guesswork to get wrong.
        main.startSpeed = 0f;
        main.gravityModifier = isRain ? 3.2f : 0.1f;
        main.startSize = isRain
            ? new ParticleSystem.MinMaxCurve(0.15f, 0.3f)
            : new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.maxParticles = isRain ? 1000 : 500;
        main.startColor = new Color(1f, 1f, 1f, isRain ? 0.5f : 0.85f);

        var emission = ps.emission;
        emission.rateOverTime = isRain ? 550f : 90f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(26f, 0.2f, 26f);

        if (!isRain)
        {
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            // All three axes must share the same curve mode (Unity throws
            // "Particle Velocity curves must all be in the same mode"
            // otherwise) - y is explicitly set to a matching TwoConstants
            // curve even though it's just 0, instead of being left on the
            // default Constant mode.
            vol.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            vol.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vol.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        }

        var tex = SaveAndImportTexture(
            isRain ? GenerateRainDropTexture() : GenerateSoftDotTexture(),
            isRain ? "Assets/Generated/Textures/RainDrop.png" : "Assets/Generated/Textures/SnowFlake.png",
            true);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (isRain)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 3f;
        }
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Stop();
        return ps;
    }

    static Texture2D GenerateRainDropTexture()
    {
        int w = 8, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float lenFade = 1f - Mathf.Abs((y / (float)(h - 1)) - 0.5f) * 1.7f;
            lenFade = Mathf.Clamp01(lenFade);
            for (int x = 0; x < w; x++)
            {
                float edgeFade = 1f - Mathf.Abs((x / (float)(w - 1)) - 0.5f) * 2.2f;
                edgeFade = Mathf.Clamp01(edgeFade);
                tex.SetPixel(x, y, new Color(0.85f, 0.9f, 1f, lenFade * edgeFade));
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateSoftDotTexture()
    {
        int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                float alpha = Mathf.Clamp01(1f - d);
                alpha *= alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    static GameAudio BuildGameAudio(GameObject host)
    {
        var audio = host.AddComponent<GameAudio>();
        audio.whistle = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/whistle.mp3");
        audio.kick = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/kick.mp3");
        audio.goal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/goal.mp3");
        audio.crowdCheer = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/crowd_cheer.mp3");
        audio.goalkeeperSave = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/goalkeeper_save.mp3");
        audio.background = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/background.mp3");
        return audio;
    }

    static void SetRendererEnabled(GameObject go, bool enabled)
    {
        if (go == null) return;
        var r = go.GetComponent<Renderer>();
        if (r != null) r.enabled = enabled;
    }

    static Bounds ComputeRendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    // Instantiates the recovered Goal.fbx (posts+crossbar as "Wood", net mesh
    // as "Net") as a purely visual overlay - gameplay colliders (posts,
    // net containment walls, goal line trigger) stay on our own primitives
    // since their coordinates are already tuned and trusted. Adds NetRipple
    // to the Net child since it's a plain mesh, not skinned, so Unity's Cloth
    // component can't attach to it directly.
    static GameObject TryInstantiateFreekickGoal(Transform parent, out NetRipple netRipple)
    {
        netRipple = null;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FreekickGoalFbx);
        if (model == null) return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "GoalVisual";
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        // The model's open mouth faces away from the kicker by default (its
        // closed net end sits on the near/kicker side instead) - flip it.
        instance.transform.localRotation = Quaternion.Euler(0, 180f, 0);

        var netChild = instance.transform.Find("Net");
        if (netChild != null)
        {
            Object.DestroyImmediate(netChild.GetComponent<Collider>());
            netRipple = netChild.gameObject.AddComponent<NetRipple>();
        }

        return instance;
    }

    static GameObject TryInstantiateFreekickArena(Transform parent, Vector3 position)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FreekickArenaFbx);
        if (model == null) return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "FootballArena";
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;

        // The arena brings its own pitch mesh, which doesn't line up with
        // ours (different orientation/markings) and shows through as
        // conflicting diagonal lines. We already have our own textured
        // pitch as the actual playing surface, so hide the arena's copy and
        // keep only the stands/structure.
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name.IndexOf("Field", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                renderer.enabled = false;
            }
        }

        return instance;
    }

    static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    static void ApplyTexturedMaterial(GameObject go, Texture2D tex, Color tint, Vector2 tiling)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = tint;
        mat.mainTexture = tex;
        mat.mainTextureScale = tiling;
        renderer.sharedMaterial = mat;
    }

    static void ApplyTransparent(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        renderer.sharedMaterial = mat;
    }

    // Sprites/Default is unlit, alpha-blended and renders both sides of the quad,
    // which sidesteps any guesswork about which way a Quad primitive's normal faces.
    static void ApplyDecalMaterial(GameObject go, Texture2D tex)
    {
        var renderer = go.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Texture2D GenerateGrassTexture()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color light = new Color(0.24f, 0.56f, 0.24f);
        Color dark = new Color(0.17f, 0.46f, 0.19f);
        int stripeCount = 8;

        for (int y = 0; y < size; y++)
        {
            int stripe = (y * stripeCount) / size;
            Color c = (stripe % 2 == 0) ? light : dark;
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    // A grid of little head-and-shoulders blobs (skin-tone circle over a
    // jersey-color rect) rather than flat color cells, so it actually reads
    // as rows of seated people from typical camera distances instead of a
    // plain mosaic pattern.
    static Texture2D GenerateCrowdTexture()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var rand = new System.Random(20240101);

        Color[] shirtPalette =
        {
            new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.3f, 0.8f), new Color(0.9f, 0.8f, 0.2f),
            new Color(0.2f, 0.7f, 0.3f), new Color(0.9f, 0.9f, 0.9f), new Color(0.6f, 0.2f, 0.7f),
            new Color(0.9f, 0.5f, 0.15f), new Color(0.3f, 0.3f, 0.35f)
        };
        Color[] skinPalette =
        {
            new Color(0.94f, 0.8f, 0.68f), new Color(0.76f, 0.57f, 0.42f), new Color(0.5f, 0.35f, 0.24f),
            new Color(0.32f, 0.22f, 0.16f), new Color(0.85f, 0.68f, 0.55f)
        };

        Color seatGap = new Color(0.1f, 0.14f, 0.2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, seatGap);
            }
        }

        int cell = 16;
        for (int cy = 0; cy < size; cy += cell)
        {
            for (int cx = 0; cx < size; cx += cell)
            {
                float jitterX = (float)(rand.NextDouble() - 0.5) * 4f;
                float baseX = cx + cell * 0.5f + jitterX;

                Color shirt = shirtPalette[rand.Next(shirtPalette.Length)];
                Color skin = skinPalette[rand.Next(skinPalette.Length)];

                float shoulderY = cy + cell * 0.32f;
                float headY = cy + cell * 0.74f;

                DrawRect(tex, baseX, shoulderY, cell * 0.8f, cell * 0.5f, shirt);
                DrawCircle(tex, baseX, headY, cell * 0.24f, skin);
            }
        }

        tex.Apply();
        return tex;
    }

    static Texture2D GenerateBallTexture()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        float phi = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] verts =
        {
            new Vector3(0, 1, phi), new Vector3(0, 1, -phi), new Vector3(0, -1, phi), new Vector3(0, -1, -phi),
            new Vector3(1, phi, 0), new Vector3(1, -phi, 0), new Vector3(-1, phi, 0), new Vector3(-1, -phi, 0),
            new Vector3(phi, 0, 1), new Vector3(phi, 0, -1), new Vector3(-phi, 0, 1), new Vector3(-phi, 0, -1)
        };
        for (int i = 0; i < verts.Length; i++) verts[i] = verts[i].normalized;

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            float lat = (v - 0.5f) * Mathf.PI;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float lon = (u - 0.5f) * 2f * Mathf.PI;
                Vector3 dir = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Sin(lat),
                    Mathf.Cos(lat) * Mathf.Sin(lon));

                float best = -2f;
                for (int i = 0; i < verts.Length; i++)
                {
                    float d = Vector3.Dot(dir, verts[i]);
                    if (d > best) best = d;
                }

                Color c = best > 0.86f ? Color.black : Color.white;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    static Texture2D GenerateFaceTexture()
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        FillTransparent(tex);

        Color eyeColor = new Color(0.15f, 0.12f, 0.12f, 1f);
        Color browColor = new Color(0.35f, 0.22f, 0.15f, 1f);
        Color mouthColor = new Color(0.6f, 0.3f, 0.28f, 1f);

        DrawCircle(tex, size * 0.34f, size * 0.55f, size * 0.075f, eyeColor);
        DrawCircle(tex, size * 0.66f, size * 0.55f, size * 0.075f, eyeColor);

        DrawRect(tex, size * 0.34f, size * 0.68f, size * 0.16f, size * 0.035f, browColor);
        DrawRect(tex, size * 0.66f, size * 0.68f, size * 0.16f, size * 0.035f, browColor);

        DrawSmile(tex, size * 0.5f, size * 0.32f, size * 0.22f, size * 0.03f, mouthColor);

        tex.Apply();
        return tex;
    }

    static Texture2D GenerateNumberTexture(int number)
    {
        int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        FillTransparent(tex);
        DrawDigit(tex, number, size * 0.5f, size * 0.5f, size * 0.42f, size * 0.72f, Color.white);
        tex.Apply();
        return tex;
    }

    static void FillTransparent(Texture2D tex)
    {
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                tex.SetPixel(x, y, clear);
            }
        }
    }

    static void DrawCircle(Texture2D tex, float cx, float cy, float r, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + r));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + r));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy <= r * r) tex.SetPixel(x, y, color);
            }
        }
    }

    static void DrawRect(Texture2D tex, float cx, float cy, float w, float h, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - w / 2f));
        int maxX = Mathf.Min(tex.width - 1, Mathf.CeilToInt(cx + w / 2f));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - h / 2f));
        int maxY = Mathf.Min(tex.height - 1, Mathf.CeilToInt(cy + h / 2f));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                tex.SetPixel(x, y, color);
            }
        }
    }

    static void DrawSmile(Texture2D tex, float cx, float cy, float width, float thickness, Color color)
    {
        int steps = 24;
        float halfW = width / 2f;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = cx - halfW + t * width;
            float dx = (x - cx) / halfW;
            float y = cy + dx * dx * (width * 0.22f);
            DrawCircle(tex, x, y, thickness, color);
        }
    }

    static void DrawDigit(Texture2D tex, int digit, float cx, float cy, float w, float h, Color color)
    {
        digit = Mathf.Clamp(digit, 0, 9);
        var seg = DigitSegments[digit];
        float t = w * 0.22f;
        float top = cy + h / 2f;
        float mid = cy;
        float bottom = cy - h / 2f;
        float left = cx - w / 2f;
        float right = cx + w / 2f;

        if (seg[0]) DrawRect(tex, cx, top, w, t, color);
        if (seg[1]) DrawRect(tex, right, (top + mid) / 2f, t, h / 2f, color);
        if (seg[2]) DrawRect(tex, right, (mid + bottom) / 2f, t, h / 2f, color);
        if (seg[3]) DrawRect(tex, cx, bottom, w, t, color);
        if (seg[4]) DrawRect(tex, left, (mid + bottom) / 2f, t, h / 2f, color);
        if (seg[5]) DrawRect(tex, left, (top + mid) / 2f, t, h / 2f, color);
        if (seg[6]) DrawRect(tex, cx, mid, w, t, color);
    }

    static Texture2D SaveAndImportTexture(Texture2D tex, string path, bool alphaTransparency)
    {
        byte[] bytes = tex.EncodeToPNG();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = alphaTransparency;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureTags()
    {
        AddTagIfMissing("GoalLine");
        AddTagIfMissing("Keeper");
    }

    static void AddTagIfMissing(string tag)
    {
        var tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var tagManager = new SerializedObject(tagManagerAsset);
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
        }

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
