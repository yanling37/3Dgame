using UnityEngine;

namespace Game.Jianglin
{
    /// <summary>
    /// Builds the independent "降临吧" 3D block at runtime.
    /// The scene only contains this GameObject; everything else (light, camera,
    /// ground, trees, player and HUD) is created here so it cannot interfere
    /// with the legacy 俯瞰 simulation.
    /// </summary>
    public class JianglinBootstrap : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] float groundSize = 48f;
        [SerializeField] int treeCount = 24;
        [SerializeField] int randomSeed = 20260825;

        [Header("Player")]
        [SerializeField] Vector3 playerStart = new Vector3(0f, 0.08f, 0f);
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float jumpHeight = 1.35f;
        [SerializeField] float gravity = -24f;

        [Header("Camera")]
        [SerializeField] Vector3 thirdPersonOffset = new Vector3(0f, 2.4f, -6f);
        [SerializeField] float eyeHeight = 1.72f;
        [SerializeField] float mouseSensitivity = 2.2f;
        [SerializeField] bool startInFirstPerson = false;

        [Header("Player Model")]
        [Tooltip("导入 Blender FBX 后，从 Project 窗口把模型/预制体拖到这里，即可替换默认灰盒玩家。")]
        [SerializeField] GameObject playerModelPrefab;
        [SerializeField] float playerModelScale = 1f;
        [SerializeField] Vector3 playerModelOffset = Vector3.zero;
        [SerializeField] Vector3 playerModelRotation = Vector3.zero;
        [SerializeField] bool repairModelMaterials = true;
        [SerializeField] Color playerModelFallbackColor = new Color(0.86f, 0.76f, 0.66f);
        [Tooltip("grace.fbx albedo. Assign Assets/Art/Characters/Textures/Image_0.png (must be a real PNG, not WebP).")]
        [SerializeField] Texture2D playerFallbackTexture;
        [SerializeField] bool useFallbackTextureWhenMissing = true;

        void Awake()
        {
            EnsureLight();
            var camera = EnsureCamera();

            var worldRoot = new GameObject("Jianglin_World");
            CreateGround(worldRoot.transform);
            CreateTrees(worldRoot.transform);
            var player = CreatePlayer(worldRoot.transform, camera, out var cameraControl);
            CreateMonsters(worldRoot.transform, player.transform);

            CreateHud(camera, player, cameraControl);

            Debug.Log("[降临吧] 独立3D场景已创建：玩家 / 怪物 / 索敌 / 配方施法。");
        }

        void EnsureLight()
        {
            if (FindObjectOfType<Light>() != null)
            {
                return;
            }

            var lightGo = new GameObject("Jianglin_DirectionalLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        }

        Camera EnsureCamera()
        {
            var existing = Camera.main;
            if (existing != null)
            {
                return existing;
            }

            var camGo = new GameObject("Jianglin_MainCamera");
            camGo.tag = "MainCamera";
            var camera = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.52f, 0.74f, 0.92f);
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 500f;
            return camera;
        }

        void CreateGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Jianglin_Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = Vector3.zero;
            ground.transform.localScale = new Vector3(groundSize / 10f, 1f, groundSize / 10f);

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMaterial(new Color(0.28f, 0.46f, 0.22f), "地面");
            }

            // Keep the built-in MeshCollider; it makes the ground walkable.
        }

        void CreateTrees(Transform parent)
        {
            var random = new System.Random(randomSeed);
            float half = groundSize * 0.5f - 2.5f;

            for (int i = 0; i < treeCount; i++)
            {
                float x = (float)(random.NextDouble() * 2.0 - 1.0) * half;
                float z = (float)(random.NextDouble() * 2.0 - 1.0) * half;

                // Keep the spawn point a little away from the player.
                if (Mathf.Abs(x) < 2.5f && Mathf.Abs(z) < 2.5f)
                {
                    x += Mathf.Sign(x) * 2.5f;
                    z += Mathf.Sign(z) * 2.5f;
                }

                CreateTree(parent, new Vector3(x, 0f, z), random);
            }
        }

        void CreateTree(Transform parent, Vector3 position, System.Random random)
        {
            var root = new GameObject("Jianglin_Tree");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            float trunkHeight = 1.9f + (float)random.NextDouble() * 0.8f;
            float trunkRadius = 0.13f + (float)random.NextDouble() * 0.08f;
            float foliageRadius = 0.85f + (float)random.NextDouble() * 0.55f;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
            var trunkRenderer = trunk.GetComponent<Renderer>();
            if (trunkRenderer != null)
            {
                trunkRenderer.sharedMaterial = CreateMaterial(new Color(0.40f, 0.24f, 0.12f), "树干");
            }

            var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(root.transform, false);
            foliage.transform.localPosition = new Vector3(0f, trunkHeight + foliageRadius * 0.45f, 0f);
            foliage.transform.localScale = Vector3.one * (foliageRadius * 2f);
            var foliageRenderer = foliage.GetComponent<Renderer>();
            if (foliageRenderer != null)
            {
                foliageRenderer.sharedMaterial = CreateMaterial(
                    new Color(0.20f, 0.48f, 0.18f),
                    "树冠");
            }

            // The trunk collider is enough to block the player.
            Destroy(foliage.GetComponent<Collider>());
        }

        JianglinPlayerController CreatePlayer(Transform parent, Camera camera, out JianglinCameraController cameraControl)
        {
            var playerGo = new GameObject("Jianglin_Player");
            playerGo.transform.SetParent(parent, false);
            playerGo.transform.position = playerStart;

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 0.95f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.05f;

            Transform facing = CreatePlayerVisual(playerGo.transform);

            // Camera is a sibling of the player so model yaw never inherits into the rig.
            var cameraRig = new GameObject("Jianglin_CameraRig");
            cameraRig.transform.SetParent(parent, false);
            cameraRig.transform.localPosition = Vector3.zero;

            cameraControl = cameraRig.AddComponent<JianglinCameraController>();
            cameraControl.Configure(
                camera,
                playerGo.transform,
                cameraRig.transform,
                thirdPersonOffset,
                eyeHeight,
                mouseSensitivity,
                startInFirstPerson);

            var health = playerGo.AddComponent<JianglinHealth>();
            health.Configure(100f, true);

            var player = playerGo.AddComponent<JianglinPlayerController>();
            player.Configure(cameraControl, facing, moveSpeed, jumpHeight, gravity);

            return player;
        }

        Transform CreatePlayerVisual(Transform playerRoot)
        {
            var facing = new GameObject("Jianglin_Facing").transform;
            facing.SetParent(playerRoot, false);

            var stand = new GameObject("Jianglin_Stand").transform;
            stand.SetParent(facing, false);

            if (playerModelPrefab != null)
            {
                // grace.fbx vertices are Blender Z-up / -Y forward. Stand converts that
                // to Unity Y-up / +Z forward. Imported node rotations are cleared so
                // they cannot stack with this conversion.
                stand.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                var visual = Instantiate(playerModelPrefab, stand, false);
                visual.name = "Jianglin_PlayerModel";
                StripImportedCamerasAndLights(visual);
                FlattenImportedRotation(visual.transform);
                visual.transform.localPosition = playerModelOffset;
                if (playerModelRotation != Vector3.zero)
                {
                    visual.transform.localRotation *= Quaternion.Euler(playerModelRotation);
                }
                visual.transform.localScale = Vector3.one * Mathf.Max(0.001f, playerModelScale);
                RemoveVisualColliders(visual.transform);
                AlignModelToFeet(visual.transform, playerRoot);
                if (repairModelMaterials)
                {
                    RepairModelMaterials(visual.transform);
                }

                return facing;
            }

            stand.localRotation = Quaternion.identity;
            CreateGreyboxVisual(stand);
            return facing;
        }

        void CreateGreyboxVisual(Transform stand)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Jianglin_Body";
            body.transform.SetParent(stand, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            body.transform.localScale = new Vector3(0.72f, 0.82f, 0.72f);
            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = CreateMaterial(new Color(0.16f, 0.42f, 0.78f), "玩家");
            }
            Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Jianglin_Head";
            head.transform.SetParent(stand, false);
            head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            head.transform.localScale = Vector3.one * 0.48f;
            var headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.sharedMaterial = CreateMaterial(new Color(0.91f, 0.70f, 0.51f), "头部");
            }
            Destroy(head.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Jianglin_FaceDirection";
            nose.transform.SetParent(head.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.03f, 0.25f);
            nose.transform.localScale = new Vector3(0.10f, 0.08f, 0.08f);
            var noseRenderer = nose.GetComponent<Renderer>();
            if (noseRenderer != null)
            {
                noseRenderer.sharedMaterial = CreateMaterial(new Color(0.28f, 0.16f, 0.10f), "朝向");
            }
            Destroy(nose.GetComponent<Collider>());
        }

        static void StripImportedCamerasAndLights(GameObject visual)
        {
            foreach (var extra in visual.GetComponentsInChildren<Camera>(true))
            {
                if (extra != null)
                {
                    Destroy(extra.gameObject);
                }
            }

            foreach (var extra in visual.GetComponentsInChildren<Light>(true))
            {
                if (extra != null)
                {
                    Destroy(extra.gameObject);
                }
            }

            foreach (var extra in visual.GetComponentsInChildren<AudioListener>(true))
            {
                if (extra != null)
                {
                    Destroy(extra);
                }
            }
        }

        static void FlattenImportedRotation(Transform root)
        {
            root.localRotation = Quaternion.identity;
            for (int i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).localRotation = Quaternion.identity;
            }
        }

        static void RemoveVisualColliders(Transform root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(rigidbody);
            }
        }

        static void AlignModelToFeet(Transform visual, Transform playerRoot)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float targetBottom = playerRoot.position.y;
            float offsetY = targetBottom - bounds.min.y;
            visual.position += Vector3.up * offsetY;
        }

        void RepairModelMaterials(Transform root)
        {
            Texture2D albedo = ResolvePlayerAlbedo();

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (NeedsStandardRepair(original, albedo))
                    {
                        materials[i] = CreateTexturedStandardMaterial(original, albedo);
                        changed = true;
                    }
                }

                if (changed)
                {
                    // Instance materials so we do not mutate the imported FBX asset.
                    renderer.materials = materials;
                }
            }
        }

        bool NeedsStandardRepair(Material original, Texture2D albedo)
        {
            if (original == null || original.shader == null || !original.shader.isSupported)
            {
                return true;
            }

            if (!IsBuiltInStandard(original.shader))
            {
                return true;
            }

            if (useFallbackTextureWhenMissing && albedo != null && !HasUsableAlbedo(original))
            {
                return true;
            }

            return false;
        }

        Material CreateTexturedStandardMaterial(Material source, Texture2D albedo)
        {
            var material = CreateMaterial(Color.white, "模型标准材质");
            ConfigureOpaqueStandard(material);

            Texture2D main = null;
            if (source != null)
            {
                CopyMaterialTexture(source, material, "_MainTex", "_MainTex", "_BaseMap", "_BaseColorMap");
                CopyMaterialTexture(source, material, "_BumpMap", "_BumpMap", "_BumpMap", "_NormalMap");
                CopyMaterialTexture(source, material, "_MetallicGlossMap", "_MetallicGlossMap", "_MetallicGlossMap");
                CopyMaterialTexture(source, material, "_OcclusionMap", "_OcclusionMap", "_OcclusionMap");
                main = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") as Texture2D : null;
            }

            if ((main == null || !IsUsableTexture(main)) && albedo != null && material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", albedo);
                main = albedo;
            }

            if (material.HasProperty("_Color"))
            {
                // Keep albedo unbiased when a real texture is present. Skin-tint * white
                // missing-tex is what produced the foamy look.
                if (IsUsableTexture(main))
                {
                    material.color = Color.white;
                }
                else if (source != null && source.HasProperty("_BaseColor"))
                {
                    material.color = source.GetColor("_BaseColor");
                }
                else if (source != null && source.HasProperty("_Color"))
                {
                    material.color = source.color;
                }
                else
                {
                    material.color = playerModelFallbackColor;
                }
            }

            return material;
        }

        Texture2D ResolvePlayerAlbedo()
        {
            Texture2D source = LoadAlbedoPngFromDisk();
            if (!IsUsableTexture(source) && IsUsableTexture(playerFallbackTexture))
            {
                source = playerFallbackTexture;
            }

#if UNITY_EDITOR
            if (!IsUsableTexture(source))
            {
                source = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Art/Characters/Textures/Image_0.png");
            }
#endif
            return BoostAlbedo(source);
        }

        Texture2D BoostAlbedo(Texture2D source)
        {
            if (!IsUsableTexture(source))
            {
                return source;
            }

            Texture2D readable = source;
            if (!source.isReadable)
            {
                readable = LoadAlbedoPngFromDisk();
                if (!IsUsableTexture(readable) || !readable.isReadable)
                {
                    return source;
                }
            }

            int width = readable.width;
            int height = readable.height;
            var boosted = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "Jianglin_GraceAlbedoBoosted",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = readable.GetPixels();
            const float saturation = 1.85f;
            const float contrast = 1.55f;
            const float brightness = 0.72f;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float grey = c.grayscale;
                c.r = Mathf.Lerp(grey, c.r, saturation);
                c.g = Mathf.Lerp(grey, c.g, saturation);
                c.b = Mathf.Lerp(grey, c.b, saturation);
                c.r = Mathf.Clamp01((c.r - 0.5f) * contrast + 0.5f) * brightness;
                c.g = Mathf.Clamp01((c.g - 0.5f) * contrast + 0.5f) * brightness;
                c.b = Mathf.Clamp01((c.b - 0.5f) * contrast + 0.5f) * brightness;
                pixels[i] = c;
            }

            boosted.SetPixels(pixels);
            boosted.Apply(true, true);
            if (readable != source && readable != playerFallbackTexture)
            {
                Destroy(readable);
            }
            else if (source != null && source.name == "Jianglin_GraceAlbedo" && source != playerFallbackTexture)
            {
                Destroy(source);
            }

            return boosted;
        }

        static Texture2D LoadAlbedoPngFromDisk()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "Art/Characters/Textures/Image_0.png");
            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            byte[] data = System.IO.File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = "Jianglin_GraceAlbedo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            if (!tex.LoadImage(data))
            {
                Destroy(tex);
                return null;
            }

            return tex;
        }

        static void ConfigureOpaqueStandard(Material material)
        {
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.12f);
            }

            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
            }

            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 0f);
            }

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }

        static bool IsBuiltInStandard(Shader shader)
        {
            if (shader == null)
            {
                return false;
            }

            string name = shader.name;
            return name == "Standard" || name == "Legacy Shaders/Diffuse" || name == "Diffuse";
        }

        static bool HasUsableAlbedo(Material material)
        {
            if (material == null)
            {
                return false;
            }

            Texture tex = null;
            if (material.HasProperty("_MainTex"))
            {
                tex = material.GetTexture("_MainTex");
            }

            if (!IsUsableTexture(tex) && material.HasProperty("_BaseMap"))
            {
                tex = material.GetTexture("_BaseMap");
            }

            return IsUsableTexture(tex);
        }

        static bool IsUsableTexture(Texture texture)
        {
            return texture != null && texture.width >= 256 && texture.height >= 256;
        }

        static void CopyMaterialTexture(Material source, Material target, string targetProperty, params string[] sourceProperties)
        {
            if (!target.HasProperty(targetProperty))
            {
                return;
            }

            foreach (var sourceProperty in sourceProperties)
            {
                if (!source.HasProperty(sourceProperty))
                {
                    continue;
                }

                var texture = source.GetTexture(sourceProperty);
                if (texture != null)
                {
                    target.SetTexture(targetProperty, texture);
                    return;
                }
            }
        }

        void CreateHud(Camera camera, JianglinPlayerController player, JianglinCameraController cameraControl)
        {
            var hudGo = new GameObject("Jianglin_Hud");
            var hud = hudGo.AddComponent<JianglinHud>();
            hud.Bind(camera, player);

            var magicGo = new GameObject("Jianglin_Magic");
            var targeting = magicGo.AddComponent<JianglinTargeting>();
            targeting.Bind(player.transform, camera);
            var magic = magicGo.AddComponent<JianglinMagicController>();
            magic.Bind(
                cameraControl,
                player.GetComponent<CharacterController>(),
                player,
                targeting);
            var magicHud = magicGo.AddComponent<JianglinMagicHud>();
            magicHud.Bind(magic);
        }

        void CreateMonsters(Transform parent, Transform player)
        {
            var random = new System.Random(randomSeed + 77);
            float half = groundSize * 0.42f;
            for (int i = 0; i < 6; i++)
            {
                SpawnMonster(parent, player, JianglinMonsterKind.Grunt, MonsterSpawnPoint(random, half), i);
            }

            for (int i = 0; i < 2; i++)
            {
                SpawnMonster(parent, player, JianglinMonsterKind.Brute, MonsterSpawnPoint(random, half), i + 10);
            }
        }

        static Vector3 MonsterSpawnPoint(System.Random random, float half)
        {
            for (int n = 0; n < 12; n++)
            {
                float x = (float)(random.NextDouble() * 2.0 - 1.0) * half;
                float z = (float)(random.NextDouble() * 2.0 - 1.0) * half;
                if (x * x + z * z > 36f)
                {
                    return new Vector3(x, 0.08f, z);
                }
            }

            return new Vector3(half * 0.6f, 0.08f, half * 0.4f);
        }

        void SpawnMonster(
            Transform parent,
            Transform player,
            JianglinMonsterKind kind,
            Vector3 position,
            int index)
        {
            bool brute = kind == JianglinMonsterKind.Brute;
            var go = new GameObject(brute ? "Jianglin_Brute_" + index : "Jianglin_Grunt_" + index);
            go.transform.SetParent(parent, false);

            var controller = go.AddComponent<CharacterController>();
            controller.height = brute ? 2.15f : 1.7f;
            controller.radius = brute ? 0.48f : 0.36f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;

            go.AddComponent<JianglinHealth>();

            Color bodyColor = brute
                ? new Color(0.28f, 0.16f, 0.38f)
                : new Color(0.72f, 0.18f, 0.14f);
            Color headColor = brute
                ? new Color(0.42f, 0.22f, 0.5f)
                : new Color(0.85f, 0.32f, 0.22f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, controller.height * 0.5f, 0f);
            body.transform.localScale = brute
                ? new Vector3(1.15f, 1.05f, 1.15f)
                : new Vector3(0.9f, 0.85f, 0.9f);
            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = CreateMaterial(bodyColor, brute ? "蛮兵" : "杂兵");
            }

            Destroy(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, controller.height - 0.12f, 0.08f);
            head.transform.localScale = Vector3.one * (brute ? 0.62f : 0.48f);
            var headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                headRenderer.sharedMaterial = CreateMaterial(headColor, "怪物头");
            }

            Destroy(head.GetComponent<Collider>());

            var monster = go.AddComponent<JianglinMonster>();
            monster.Configure(player, kind, position);
        }

        static Material CreateMaterial(Color color, string label)
        {
            // This project currently has no Scriptable Render Pipeline asset assigned
            // (GraphicsSettings.m_CustomRenderPipeline is empty), so it renders with the
            // built-in pipeline. Prefer Standard; the old simulation markers use the same
            // primitive material.color path and display correctly.
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            var material = new Material(shader)
            {
                name = "Jianglin_" + label
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.color = color;
            }

            return material;
        }
    }
}
