using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance;

    [Header("Level Settings")]
    public int worldIndex = 1;
    public int levelIndex = 1;

    [Header("Tile Prefabs (Optional - falls back to Primitives)")]
    public GameObject normalTilePrefab;
    public GameObject goalTilePrefab;
    public GameObject weakTilePrefab;
    public GameObject bridgeTilePrefab;
    public GameObject softSwitchPrefab;
    public GameObject hardSwitchPrefab;
    public GameObject splitSwitchPrefab;
    public GameObject teleportTilePrefab;
    public GameObject conveyorTilePrefab;

    [Header("Player Settings")]
    public GameObject playerBlockPrefab;

    // Track active tiles in the grid: Key is (column, row)
    private Dictionary<Vector2Int, GameObject> spawnedTiles = new Dictionary<Vector2Int, GameObject>();
    private GameObject playerInstance;

    /// <summary>Şu an aktif olan levelın tüm verileri (JSON'dan okunan)</summary>
    public LevelData CurrentLevelData { get; private set; }

    /// <summary>Her level yüklendiğinde (restart veya yeni level) tetiklenir.</summary>
    public static event System.Action OnLevelLoaded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        LoadLevel(worldIndex, levelIndex);
    }

    [ContextMenu("Load Level")]
    public void LoadLevelFromInspector()
    {
        LoadLevel(worldIndex, levelIndex);
    }

    public void LoadLevel(int world, int level)
    {
        worldIndex = world;
        levelIndex = level;

        ClearLevel();

        // JSON dosyasını yükle
        string filename = $"LevelsJSON/w{world}l{level}";
        TextAsset levelAsset = Resources.Load<TextAsset>(filename);

        if (levelAsset == null)
        {
            Debug.LogError($"Level JSON file '{filename}' not found in Resources!");
            return;
        }

        try
        {
            CurrentLevelData = JsonUtility.FromJson<LevelData>(levelAsset.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse level JSON '{filename}': {ex.Message}");
            return;
        }

        if (CurrentLevelData == null || CurrentLevelData.layout == null)
        {
            Debug.LogError($"Level layout is empty or invalid in '{filename}'");
            return;
        }

        string[] lines = CurrentLevelData.layout;

        for (int r = 0; r < lines.Length; r++)
        {
            string[] tokens = lines[r].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int c = 0; c < tokens.Length; c++)
            {
                string token = tokens[c].Trim();
                if (token == CellMapType.NOTHING) continue;

                Vector3 position = new Vector3(c, 0, -r);
                Vector2Int gridCoord = new Vector2Int(c, r);

                // Spawn the appropriate tile based on token type
                GameObject tile = SpawnTile(token, position);
                if (tile != null)
                {
                    spawnedTiles[gridCoord] = tile;
                }

                // If it is the START tile, spawn the player block
                if (token == CellMapType.START)
                {
                    SpawnPlayer(position);
                }
            }
        }

        Debug.Log($"Level {world}-{level} loaded successfully with {spawnedTiles.Count} tiles.");
        OnLevelLoaded?.Invoke();
    }

    private void ParseToken(string token, out string type, out int channel)
    {
        type = "";
        channel = 0;

        if (string.IsNullOrEmpty(token)) return;

        // If the token starts with a digit, it is a static/numeric tile type (e.g., "1", "8", "9")
        if (char.IsDigit(token[0]))
        {
            type = token;
            channel = 0;
            return;
        }

        int i = 0;
        while (i < token.Length && !char.IsDigit(token[i]))
        {
            type += token[i];
            i++;
        }

        if (i < token.Length)
        {
            int.TryParse(token.Substring(i), out channel);
        }
    }

    private GameObject SpawnTile(string token, Vector3 pos)
    {
        string[] subTokens = token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        string baseToken = subTokens.Length > 0 ? subTokens[0] : token;

        string type;
        int channel;
        ParseToken(baseToken, out type, out channel);

        GameObject tilePrefab = null;
        Color fallbackColor = Color.white;
        string tileName = "Tile";

        switch (type)
        {
            case CellMapType.BRICK:
            case CellMapType.START:
                tilePrefab = normalTilePrefab;
                fallbackColor = new Color(0.7f, 0.7f, 0.7f); // gray
                tileName = (token == CellMapType.START) ? "StartTile" : "NormalTile";
                break;
            case CellMapType.HOME:
                tilePrefab = goalTilePrefab;
                fallbackColor = Color.black;
                tileName = "GoalTile";
                break;
            case CellMapType.WEAK:
                tilePrefab = weakTilePrefab;
                fallbackColor = new Color(0.8f, 0.5f, 0.3f); // wood/orange
                tileName = "WeakTile";
                break;
            case "b":
            case "B":
                tilePrefab = bridgeTilePrefab;
                fallbackColor = Color.cyan;
                tileName = "BridgeTile_" + channel;
                break;
            case "h":
            case "ho":
            case "hc":
                tilePrefab = hardSwitchPrefab;
                fallbackColor = new Color(0.9f, 0.2f, 0.2f); // Reddish fallback for hard switches
                tileName = "HardSwitchTile_" + token;
                break;
            case "s":
            case "so":
            case "sc":
                tilePrefab = softSwitchPrefab;
                fallbackColor = new Color(0.9f, 0.9f, 0.2f); // Yellowish fallback for soft switches
                tileName = "SoftSwitchTile_" + token;
                break;
            case CellMapType.SPLIT:
                tilePrefab = splitSwitchPrefab;
                fallbackColor = Color.cyan;
                tileName = "SplitSwitchTile_" + baseToken;
                break;
            case CellMapType.MOVE_NEXT:
                // MOVE_NEXT tile: ok (konveyör) tile — oyuncuyu otomatik yönlendirir
                tilePrefab = conveyorTilePrefab != null ? conveyorTilePrefab : normalTilePrefab;
                fallbackColor = new Color(1.0f, 0.55f, 0.1f); // turuncu
                tileName = "MoveNextTile_" + baseToken;
                break;
            case CellMapType.TELEPORT:
                tilePrefab = teleportTilePrefab;
                fallbackColor = Color.magenta;
                tileName = "TeleportTile_" + token;
                break;
            case CellMapType.APPEAR:
                tilePrefab = normalTilePrefab;
                fallbackColor = new Color(0.7f, 0.7f, 0.7f); // gray
                tileName = "AppearTile_" + token;
                break;
            default:
                fallbackColor = Color.red; // default error indicator
                break;
        }

        GameObject spawnedObj;

        if (tilePrefab != null)
        {
            spawnedObj = Instantiate(tilePrefab, pos, tilePrefab.transform.rotation, transform);
            spawnedObj.name = tileName;

            // Auto-scale prefab uniformly to fit exactly 1x1 in world X/Z dimensions
            Renderer[] renderers = spawnedObj.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                Vector3 size = combinedBounds.size;
                Vector3 currentScale = spawnedObj.transform.localScale;

                float targetX = 1f;

                // Calculate uniform scaling ratio based on X dimension to preserve original model proportions
                float scaleRatio = (size.x > 0.01f) ? (targetX / size.x) : 1f;

                float scaleX = scaleRatio * currentScale.x;
                float scaleY = scaleRatio * currentScale.y;
                float scaleZ = scaleRatio * currentScale.z;

                spawnedObj.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                // Recalculate bounds after scaling to get the final world-space height
                combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                // Shift the tile down so its top surface sits exactly at Y = 0
                float topY = combinedBounds.max.y;
                spawnedObj.transform.position = new Vector3(pos.x, pos.y - topY, pos.z);
            }
        }
        else
        {
            // Fallback to primitive Cube if no prefab is configured
            spawnedObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawnedObj.name = tileName;
            // Shift the fallback cube down so its top surface sits exactly at Y = 0 (thickness is 0.15, so shift by 0.075)
            spawnedObj.transform.position = new Vector3(pos.x, pos.y - 0.075f, pos.z);
            spawnedObj.transform.localScale = new Vector3(1f, 0.15f, 1f); // Flat tile representation
            spawnedObj.transform.SetParent(transform);

            // Apply color to material
            Renderer renderer = spawnedObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Create a clean instance of material so we don't leak to default assets
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = fallbackColor;
                renderer.material = material;
            }
        }

        // Add a collider if missing, Tag it accordingly for player interaction detection
        if (spawnedObj.GetComponent<Collider>() == null)
        {
            spawnedObj.AddComponent<BoxCollider>();
        }

        if (type == CellMapType.HOME)
        {
            spawnedObj.AddComponent<GoalTile>();
        }
        else if (type == CellMapType.WEAK)
        {
            spawnedObj.AddComponent<FragileTile>();
        }
        else if (type == CellMapType.SPLIT)
        {
            SplitTile split = spawnedObj.AddComponent<SplitTile>();
            if (subTokens.Length >= 5)
            {
                int t1_x, t1_z, t2_x, t2_z;
                if (int.TryParse(subTokens[1], out t1_x) &&
                    int.TryParse(subTokens[2], out t1_z) &&
                    int.TryParse(subTokens[3], out t2_x) &&
                    int.TryParse(subTokens[4], out t2_z))
                {
                    split.hasTargets = true;
                    // Z coordinate in world space is -r
                    split.target1 = new Vector3(t1_x, 0.45f, -t1_z);
                    split.target2 = new Vector3(t2_x, 0.45f, -t2_z);
                }
            }
        }
        else if (type == CellMapType.MOVE_NEXT)
        {
            // yön haritası: m0=yukarı(+Z), m1=sağ(+X), m2=aşağı(-Z), m3=sol(-X)
            // tile Y rotasyonu: m0=0°, m1=90°, m2=180°, m3=270°
            Vector3 convDir;
            float convRotY;
            switch (channel)
            {
                case 0:  convDir = Vector3.forward; convRotY = 0f;   break; // yukarı
                case 1:  convDir = Vector3.right;   convRotY = 90f;  break; // sağ
                case 2:  convDir = Vector3.back;    convRotY = 180f; break; // aşağı
                case 3:  convDir = Vector3.left;    convRotY = 270f; break; // sol
                default: convDir = Vector3.forward; convRotY = 0f;   break;
            }
            ConveyorTile conveyor = spawnedObj.AddComponent<ConveyorTile>();
            conveyor.direction = convDir;

            // Prefab'ı ok yönüne göre döndür
            Vector3 euler = spawnedObj.transform.eulerAngles;
            spawnedObj.transform.eulerAngles = new Vector3(euler.x, convRotY, euler.z);
        }

        // Attach specific behavior controllers
        if (type == "b" || type == "B")
        {
            BridgeController bridge = spawnedObj.AddComponent<BridgeController>();
            bridge.channel = channel;
            bridge.startsActive = (type == "B"); // B starts active, b starts inactive
        }
        else
        {
            foreach (var subToken in subTokens)
            {
                string subType;
                int subChannel;
                ParseToken(subToken, out subType, out subChannel);
                if (subType == "h" || subType == "ho" || subType == "hc" || subType == "s" || subType == "so" || subType == "sc")
                {
                    SwitchController sw = spawnedObj.AddComponent<SwitchController>();
                    sw.switchType = subType;
                    sw.channel = subChannel;
                }
            }
        }

        return spawnedObj;
    }

    private void SpawnPlayer(Vector3 tilePos)
    {
        if (playerInstance != null)
        {
            Destroy(playerInstance);
        }

        Vector3 spawnPos = tilePos + Vector3.up * 1f; // Block stands upright, so Y is shifted up by half its height (2.0f / 2 = 1.0f)

        if (playerBlockPrefab != null)
        {
            playerInstance = Instantiate(playerBlockPrefab, spawnPos, Quaternion.identity);

            // Auto-scale player prefab to fit exactly 1x2x1 in world dimensions
            Renderer[] renderers = playerInstance.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                Vector3 size = combinedBounds.size;
                Vector3 currentScale = playerInstance.transform.localScale;

                float targetX = 0.9f;
                float targetY = 1.8f;
                float targetZ = 0.9f;

                float scaleX = (size.x > 0.01f) ? (targetX / size.x) * currentScale.x : currentScale.x;
                float scaleY = (size.y > 0.01f) ? (targetY / size.y) * currentScale.y : currentScale.y;
                float scaleZ = (size.z > 0.01f) ? (targetZ / size.z) * currentScale.z : currentScale.z;

                playerInstance.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                // Recalculate bounds after scaling to find the bottom of the player block
                combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                // Shift the player block so its bottom surface sits exactly at Y = 0
                float minY = combinedBounds.min.y;
                playerInstance.transform.position = new Vector3(spawnPos.x, spawnPos.y - minY, spawnPos.z);
            }

            // Set up components for physics/rolling on the prefab instance
            Rigidbody rb = playerInstance.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = playerInstance.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;

            playerInstance.tag = "Player";

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                SetLayerRecursive(playerInstance, playerLayer);
            }
        }
        else
        {
            // Fallback to primitive Box representing 1x2x1 block
            playerInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            playerInstance.name = "PlayerBlock";
            // Place its bottom face exactly at Y = 0 (since height is 1.8, center Y should be 0.9f)
            playerInstance.transform.position = new Vector3(spawnPos.x, 0.9f, spawnPos.z);
            playerInstance.transform.localScale = new Vector3(0.9f, 1.8f, 0.9f);

            Renderer renderer = playerInstance.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(0.2f, 0.6f, 0.9f); // Blue
                renderer.material = material;
            }

            // Set up components for physics/rolling
            Rigidbody rb = playerInstance.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = playerInstance.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;

            playerInstance.tag = "Player";
        }

        // Ensure TumbleController is attached
        if (playerInstance != null && playerInstance.GetComponent<TumbleController>() == null)
        {
            playerInstance.AddComponent<TumbleController>();
        }

        // Find CameraFollow in the scene and assign its follow target
        CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = playerInstance.transform;
            cameraFollow.SnapToTarget();
        }
    }

    public void ClearLevel()
    {
        spawnedTiles.Clear();

        // Destroy all children to avoid leaks in Edit Mode & Play Mode
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (Application.isPlaying)
            {
                Destroy(p);
            }
            else
            {
                DestroyImmediate(p);
            }
        }
    }

    public GameObject GetTileAt(int col, int row)
    {
        Vector2Int key = new Vector2Int(col, row);
        if (spawnedTiles.TryGetValue(key, out GameObject tile))
        {
            return tile;
        }
        return null;
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Don't execute if not playing to prevent Editor GUI System crashes
        if (!Application.isPlaying)
            return;

        UnityEditor.EditorApplication.delayCall -= OnValidateLoad;
        UnityEditor.EditorApplication.delayCall += OnValidateLoad;
    }

    private void OnValidateLoad()
    {
        if (this == null || !Application.isPlaying) return;
        LoadLevel(worldIndex, levelIndex);
    }
#endif
}
