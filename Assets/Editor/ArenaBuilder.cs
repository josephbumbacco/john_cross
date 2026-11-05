using System.IO;
using System.Linq;
using JohnCross;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JohnCross.EditorTools
{
    // Generates the placeholder sprites, Player/Bullet prefabs and the Arena scene.
    // Re-running overwrites them. Menu: Tools > john_cross > Build Arena.
    // Headless: -batchmode -quit -executeMethod JohnCross.EditorTools.ArenaBuilder.BuildAll
    public static class ArenaBuilder
    {
        const string ArtDir = "Assets/Art";
        const string PrefabDir = "Assets/Prefabs";
        const string ScenePath = "Assets/Scenes/Arena.unity";
        const string PrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        const int PixelsPerUnit = 32;

        [MenuItem("Tools/john_cross/Build Arena")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(PrefabDir);

            var square = MakeSprite("Square", 32, (x, y, s) => true);
            var ship = MakeSprite("Ship", 32, (x, y, s) =>
            {
                // Upward-pointing triangle.
                float fy = y / (float)(s - 1);
                float halfWidth = (1f - fy) * 0.5f;
                return Mathf.Abs(x / (float)(s - 1) - 0.5f) <= halfWidth;
            });

            var bullet = BuildBulletPrefab(square);
            var player = BuildPlayerPrefab(ship, bullet);
            RegisterNetworkPrefabs(bullet);
            BuildScene(square, player);

            AssetDatabase.SaveAssets();
            Debug.Log("[ArenaBuilder] Arena built.");
        }

        static Sprite MakeSprite(string name, int size, System.Func<int, int, int, bool> filled)
        {
            string path = $"{ArtDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, filled(x, y, size) ? Color.white : Color.clear);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Bullet BuildBulletPrefab(Sprite sprite)
        {
            var go = new GameObject("Bullet");
            go.transform.localScale = new Vector3(0.25f, 0.5f, 1f);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            Make2D(nt);
            go.AddComponent<Bullet>();
            return SavePrefab(go, "Bullet").GetComponent<Bullet>();
        }

        static GameObject BuildPlayerPrefab(Sprite sprite, Bullet bullet)
        {
            var go = new GameObject("Player");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 1;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            Make2D(nt);
            go.AddComponent<Health>();
            var pc = go.AddComponent<PlayerController>();
            var so = new SerializedObject(pc);
            so.FindProperty("bulletPrefab").objectReferenceValue = bullet;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "Player");
        }

        static void Make2D(NetworkTransform nt)
        {
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void RegisterNetworkPrefabs(Bullet bullet)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabsListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabsListPath);
            }
            if (!list.PrefabList.Any(p => p.Prefab == bullet.gameObject))
                list.Add(new NetworkPrefab { Prefab = bullet.gameObject });
            EditorUtility.SetDirty(list);
        }

        static void BuildScene(Sprite square, GameObject player)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<CameraFollow>();

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            var size = Arena.HalfSize * 2f;
            MakeBlock("Floor", square, Vector2.zero, size, new Color(0.16f, 0.17f, 0.2f), -10);
            const float t = 0.3f;
            var wall = new Color(0.45f, 0.47f, 0.55f);
            MakeBlock("Wall N", square, new Vector2(0, Arena.HalfSize.y), new Vector2(size.x + t, t), wall, -5);
            MakeBlock("Wall S", square, new Vector2(0, -Arena.HalfSize.y), new Vector2(size.x + t, t), wall, -5);
            MakeBlock("Wall E", square, new Vector2(Arena.HalfSize.x, 0), new Vector2(t, size.y + t), wall, -5);
            MakeBlock("Wall W", square, new Vector2(-Arena.HalfSize.x, 0), new Vector2(t, size.y + t), wall, -5);

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = player,
                EnableSceneManagement = true,
            };
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabsListPath));

            var ui = new GameObject("Game");
            ui.AddComponent<SessionManager>();
            ui.AddComponent<GameUI>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void MakeBlock(string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }
    }
}
