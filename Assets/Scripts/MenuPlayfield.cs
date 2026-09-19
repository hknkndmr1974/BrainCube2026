using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main menu arka planı: "BRAIN CUBE" yazısı şeklinde platform + sabit rota ile dolaşan küpler.
/// </summary>
public class MenuPlayfield : MonoBehaviour
{
    [System.Serializable]
    public class CubeRoute
    {
        public string name = "Cube";

        [Tooltip("Play başlangıç X. 0=sol (B), 1=sağ (N/E). Edit'te küp yok — Scene'de renkli top kayar. Play veya sağ tık Rebuild Playfield.")]
        [Range(0f, 1f)] public float spawnX = 0.2f;

        [Tooltip("Play başlangıç Y. 0=üst (BRAIN), 1=alt (CUBE). Edit'te küp yok — Scene'de renkli top kayar.")]
        [Range(0f, 1f)] public float spawnY = 0.25f;

        [Tooltip("Hamle dizisi: F=ileri B=geri L=sol R=sağ. Örnek: R R R B B L L L F F. Döngüsel oynatılır.")]
        [TextArea(2, 4)]
        public string moves = "R R R B B B L L L F F F";
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject normalTilePrefab;
    [SerializeField] private GameObject playerCubePrefab;

    [Header("Placement")]
    [SerializeField] private Vector3 fieldOrigin = new Vector3(0f, 0f, -28f);
    [SerializeField] private float tumbleDuration = 0.38f;
    [SerializeField] private float moveDelay = 0.45f;
    [Tooltip("Açıkken rota bitince başa döner. Kapalıyken sadece yazdığın hamleler bir kez oynar.")]
    [SerializeField] private bool loopRoutes = false;
    [Tooltip("Her glyph pikseli NxN tile olur (yazı kalınlığı). 2 = ~2 tile stroke.")]
    [SerializeField] private int pixelScale = 2;
    [Tooltip("Yazıyı bir kez daha şişir. 0 bırak: stroke pixelScale kadar kalır.")]
    [SerializeField] private int dilatePasses = 0;
    [SerializeField] private int letterSpacing = 1;
    [SerializeField] private int lineGap = 3;
    [SerializeField] private float minSpawnDistance = 6f;

    [Header("Küp Rotaları (manuel)")]
    [Tooltip("Her eleman bir küp. moves alanına R L F B yaz; boşlukla ayır. Liste boşsa otomatik üretilir.")]
    [SerializeField] private CubeRoute[] cubeRoutes = new CubeRoute[]
    {
        new CubeRoute
        {
            name = "BrainLeft",
            spawnX = 0.12f, spawnY = 0.22f,
            // 3 yatay hamle → 3 dikey → 3 diğer yatay → 3 diğer dikey
            moves = "R R R B B B L L L F F F"
        },
        new CubeRoute
        {
            name = "BrainRight",
            spawnX = 0.88f, spawnY = 0.22f,
            moves = "L L L B B B R R R F F F"
        },
        new CubeRoute
        {
            name = "BrainMid",
            spawnX = 0.50f, spawnY = 0.18f,
            moves = "B B B R R R F F F L L L"
        },
        new CubeRoute
        {
            name = "CubeLeft",
            spawnX = 0.18f, spawnY = 0.78f,
            moves = "R R R F F F L L L B B B"
        },
        new CubeRoute
        {
            name = "CubeRight",
            spawnX = 0.82f, spawnY = 0.78f,
            moves = "L L L F F F R R R B B B"
        },
        new CubeRoute
        {
            name = "CubeMid",
            spawnX = 0.50f, spawnY = 0.82f,
            moves = "F F F L L L B B B R R R"
        },
        new CubeRoute
        {
            name = "RollWideA",
            spawnX = 0.28f, spawnY = 0.45f,
            // Yat: R ile uzan → B B B yatık yuvarlan → L ile kalk → F F dikey → tekrar
            moves = "R B B B L F F F L B B B R F F F"
        },
        new CubeRoute
        {
            name = "RollWideB",
            spawnX = 0.72f, spawnY = 0.55f,
            moves = "L B B B R F F F R B B B L F F F"
        },
    };

    [Header("Lighting")]
    [SerializeField] private bool ensureSunlight = true;
    [SerializeField] private float sunIntensity = 3.2f;
    [SerializeField] private float fillIntensity = 18f;
    [SerializeField] private float keyIntensity = 40f;
    [SerializeField] private float rimIntensity = 18f;

    private readonly HashSet<Vector2Int> walkableSet = new HashSet<Vector2Int>();
    private readonly List<TumbleController> bots = new List<TumbleController>();
    private readonly List<Vector3> botSpawns = new List<Vector3>();
    private readonly List<Vector3[]> botPaths = new List<Vector3[]>();
    private readonly List<int> botPathIndex = new List<int>();

    private string[] layoutRows;
    private int gridCols;
    private int gridRows;
    private float originX;
    private float originZ;
    private Transform tilesRoot;
    private Transform botsRoot;
    private Transform lightRoot;
    private Coroutine driveRoutine;

    // 5x7 bitmap font (1 = dolu, . = boş)
    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['B'] = new[] { "1111.", "1...1", "1...1", "1111.", "1...1", "1...1", "1111." },
        ['R'] = new[] { "1111.", "1...1", "1...1", "1111.", "1.1..", "1..1.", "1...1" },
        ['A'] = new[] { ".111.", "1...1", "1...1", "11111", "1...1", "1...1", "1...1" },
        ['I'] = new[] { "11111", "..1..", "..1..", "..1..", "..1..", "..1..", "11111" },
        ['N'] = new[] { "1...1", "11..1", "1.1.1", "1..11", "1...1", "1...1", "1...1" },
        ['C'] = new[] { ".1111", "1....", "1....", "1....", "1....", "1....", ".1111" },
        ['U'] = new[] { "1...1", "1...1", "1...1", "1...1", "1...1", "1...1", ".111." },
        ['E'] = new[] { "11111", "1....", "1....", "1111.", "1....", "1....", "11111" },
        [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
    };

    private void Start()
    {
        ClearChildren();
        Build();
        EnsureLight();
        driveRoutine = StartCoroutine(DriveBots());
    }

    private void OnDisable()
    {
        if (driveRoutine != null)
        {
            StopCoroutine(driveRoutine);
            driveRoutine = null;
        }
    }

    [ContextMenu("Rebuild Playfield")]
    public void RebuildPlayfield()
    {
        if (driveRoutine != null)
        {
            StopCoroutine(driveRoutine);
            driveRoutine = null;
        }

        ClearChildren();
        Build();
        EnsureLight();

        if (Application.isPlaying)
            driveRoutine = StartCoroutine(DriveBots());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Edit'te küp instance'ı yok; gizmo anında güncellensin diye Scene yenilenir.
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnDrawGizmosSelected()
    {
        if (cubeRoutes == null || cubeRoutes.Length == 0)
        {
            return;
        }

        int cols;
        int rows;
        GetLetterGridSize(out cols, out rows);
        if (cols < 2 || rows < 2)
        {
            return;
        }

        Color[] colors =
        {
            new Color(0.2f, 0.9f, 1f),
            new Color(1f, 0.85f, 0.2f),
            new Color(1f, 0.4f, 0.3f),
            new Color(0.4f, 1f, 0.5f),
            new Color(0.85f, 0.5f, 1f),
            new Color(1f, 0.6f, 0.2f),
            new Color(0.5f, 0.8f, 1f),
            new Color(1f, 0.35f, 0.7f),
        };

        for (int i = 0; i < cubeRoutes.Length; i++)
        {
            CubeRoute route = cubeRoutes[i];
            if (route == null)
            {
                continue;
            }

            Vector3 world = SpawnToWorld(route.spawnX, route.spawnY, cols, rows) + Vector3.up * 0.85f;
            Gizmos.color = colors[i % colors.Length];
            Gizmos.DrawSphere(world, 0.45f);
            UnityEditor.Handles.color = Gizmos.color;
            string label = string.IsNullOrWhiteSpace(route.name) ? ("Küp " + i) : route.name;
            UnityEditor.Handles.Label(world + Vector3.up * 0.55f, label + "\n(" + route.spawnX.ToString("0.00") + ", " + route.spawnY.ToString("0.00") + ")");
        }
    }

    private void GetLetterGridSize(out int cols, out int rows)
    {
        int scale = Mathf.Max(1, pixelScale);
        int glyphH = 7 * scale;
        int glyphW = 5 * scale;
        int brainW = 5 * glyphW + 4 * letterSpacing;
        int cubeW = 4 * glyphW + 3 * letterSpacing;
        int maxLineWidth = Mathf.Max(brainW, cubeW);
        int pad = 2 + Mathf.Max(0, dilatePasses) * 2;
        cols = maxLineWidth + pad * 2;
        rows = 2 * glyphH + Mathf.Max(0, lineGap) + pad * 2;
    }

    private Vector3 SpawnToWorld(float spawnXNorm, float spawnYNorm, int cols, int rows)
    {
        float sx = Mathf.Clamp01(spawnXNorm);
        float sy = Mathf.Clamp01(spawnYNorm);
        float col = sx * (cols - 1);
        float row = sy * (rows - 1);
        float ox = fieldOrigin.x - (cols - 1) * 0.5f;
        float oz = fieldOrigin.z + (rows - 1) * 0.5f;
        return new Vector3(ox + col, 0f, oz - row);
    }
#endif

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (child.name.StartsWith("MenuPlayfield"))
                continue;
            DestroyImmediate(child);
        }

        bots.Clear();
        botSpawns.Clear();
        botPaths.Clear();
        botPathIndex.Clear();
        walkableSet.Clear();
        layoutRows = null;
    }

    private void Build()
    {
        if (normalTilePrefab == null || playerCubePrefab == null)
        {
#if UNITY_EDITOR
            if (normalTilePrefab == null)
                normalTilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/NormalTile.prefab");
            if (playerCubePrefab == null)
                playerCubePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PlayerCube.prefab");
#endif
        }

        if (normalTilePrefab == null || playerCubePrefab == null)
        {
            Debug.LogError("MenuPlayfield: NormalTile veya PlayerCube prefab bulunamadı.");
            return;
        }

        BuildTextGrid(new[] { "BRAIN", "CUBE" });

        tilesRoot = new GameObject("Tiles").transform;
        tilesRoot.SetParent(transform, false);
        botsRoot = new GameObject("Bots").transform;
        botsRoot.SetParent(transform, false);

        originX = fieldOrigin.x - (gridCols - 1) * 0.5f;
        originZ = fieldOrigin.z + (gridRows - 1) * 0.5f;

        for (int r = 0; r < gridRows; r++)
        {
            string row = layoutRows[r];
            for (int c = 0; c < gridCols; c++)
            {
                if (row[c] != '1') continue;
                Vector3 pos = CellToWorld(c, r);
                SpawnTile(pos);
                walkableSet.Add(new Vector2Int(c, r));
            }
        }

        SpawnPatrolBots();

        if (ensureSunlight)
            EnsureLight();
    }

    private void BuildTextGrid(string[] lines)
    {
        int scale = Mathf.Max(1, pixelScale);
        int glyphH = 7 * scale;
        int glyphW = 5 * scale;

        int maxLineWidth = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            int w = lines[i].Length * glyphW + Mathf.Max(0, lines[i].Length - 1) * letterSpacing;
            if (w > maxLineWidth) maxLineWidth = w;
        }

        int pad = 2 + Mathf.Max(0, dilatePasses) * 2;
        gridCols = maxLineWidth + pad * 2;
        gridRows = lines.Length * glyphH + Mathf.Max(0, lines.Length - 1) * lineGap + pad * 2;

        var grid = new char[gridRows][];
        for (int r = 0; r < gridRows; r++)
        {
            grid[r] = new char[gridCols];
            for (int c = 0; c < gridCols; c++)
                grid[r][c] = '.';
        }

        int rowCursor = pad;
        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];
            int lineWidth = line.Length * glyphW + Mathf.Max(0, line.Length - 1) * letterSpacing;
            int colCursor = pad + (maxLineWidth - lineWidth) / 2; // satırı ortala

            for (int li = 0; li < line.Length; li++)
            {
                char ch = line[li];
                if (!Glyphs.TryGetValue(ch, out string[] glyph))
                    glyph = Glyphs[' '];

                StampGlyph(grid, glyph, colCursor, rowCursor, scale);
                colCursor += glyphW + letterSpacing;
            }

            rowCursor += glyphH + lineGap;
        }

        layoutRows = new string[gridRows];
        for (int r = 0; r < gridRows; r++)
            layoutRows[r] = new string(grid[r]);

        // Hareket alanı için yazıyı şişir
        for (int p = 0; p < Mathf.Max(0, dilatePasses); p++)
            DilateLayout();
    }

    private void DilateLayout()
    {
        var next = new char[gridRows][];
        for (int r = 0; r < gridRows; r++)
        {
            next[r] = layoutRows[r].ToCharArray();
            for (int c = 0; c < gridCols; c++)
            {
                if (layoutRows[r][c] == '1') continue;
                bool near = false;
                for (int dr = -1; dr <= 1 && !near; dr++)
                {
                    for (int dc = -1; dc <= 1 && !near; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int rr = r + dr;
                        int cc = c + dc;
                        if (rr < 0 || rr >= gridRows || cc < 0 || cc >= gridCols) continue;
                        if (layoutRows[rr][cc] == '1') near = true;
                    }
                }
                if (near) next[r][c] = '1';
            }
        }

        for (int r = 0; r < gridRows; r++)
            layoutRows[r] = new string(next[r]);
    }

    private static void StampGlyph(char[][] grid, string[] glyph, int startCol, int startRow, int scale)
    {
        for (int gr = 0; gr < glyph.Length; gr++)
        {
            string gRow = glyph[gr];
            for (int gc = 0; gc < gRow.Length; gc++)
            {
                if (gRow[gc] != '1') continue;
                for (int dy = 0; dy < scale; dy++)
                {
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int c = startCol + gc * scale + dx;
                        int r = startRow + gr * scale + dy;
                        if (r >= 0 && r < grid.Length && c >= 0 && c < grid[r].Length)
                            grid[r][c] = '1';
                    }
                }
            }
        }
    }

    private void SpawnPatrolBots()
    {
        // Manuel cubeRoutes doluysa onu kullan; değilse eski otomatik üretim
        if (cubeRoutes != null && cubeRoutes.Length > 0)
        {
            SpawnFromManualRoutes();
            return;
        }

        var specs = new List<PatrolSpec>();
        AddHorizontalPatrols(specs, "BrainH", 0, gridRows / 2, 2);
        AddHorizontalPatrols(specs, "CubeH", gridRows / 2, gridRows, 2);
        AddVerticalPatrols(specs, "Vert", 0, gridCols, 2);
        SpawnFromPatrolSpecs(specs);
    }

    private void SpawnFromManualRoutes()
    {
        for (int i = 0; i < cubeRoutes.Length; i++)
        {
            var route = cubeRoutes[i];
            if (route == null)
            {
                continue;
            }

            string label = string.IsNullOrWhiteSpace(route.name) ? ("Route" + i) : route.name;
            int col = Mathf.RoundToInt(Mathf.Clamp01(route.spawnX) * (gridCols - 1));
            int row = Mathf.RoundToInt(Mathf.Clamp01(route.spawnY) * (gridRows - 1));
            Vector2Int spawn = FindNearestFreeWalkable(col, row);
            if (spawn.x < 0)
            {
                Debug.LogWarning("MenuPlayfield: '" + label + "' için walkable tile yok.", this);
                continue;
            }

            Vector3 tilePos = CellToWorld(spawn.x, spawn.y);
            Vector3[] path = ParseMoves(route.moves);
            if (!SpawnBot(tilePos, path, label))
            {
                Debug.LogWarning("MenuPlayfield: '" + label + "' spawn edilemedi.", this);
            }
        }
    }

    private bool IsTooCloseToExistingSpawn(Vector3 tilePos)
    {
        for (int s = 0; s < botSpawns.Count; s++)
        {
            if (Vector3.Distance(botSpawns[s], tilePos) < minSpawnDistance)
                return true;
        }
        return false;
    }

    private Vector2Int FindFarthestWalkableFromSpawns(int preferCol, int preferRow)
    {
        Vector2Int best = new Vector2Int(-1, -1);
        float bestScore = float.NegativeInfinity;
        foreach (var cell in walkableSet)
        {
            Vector3 world = CellToWorld(cell.x, cell.y);
            float minDist = float.MaxValue;
            for (int s = 0; s < botSpawns.Count; s++)
                minDist = Mathf.Min(minDist, Vector3.Distance(botSpawns[s], world));
            if (botSpawns.Count == 0) minDist = 100f;

            float prefer = -Mathf.Abs(cell.x - preferCol) - Mathf.Abs(cell.y - preferRow);
            float score = minDist * 10f + prefer;
            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
            }
        }
        return best;
    }

    private void SpawnFromPatrolSpecs(List<PatrolSpec> specs)
    {
        for (int i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            Vector2Int spawn = FindNearestWalkable(spec.spawnCol, spec.spawnRow);
            if (spawn.x < 0) continue;

            Vector3 tilePos = CellToWorld(spawn.x, spawn.y);
            if (IsTooCloseToExistingSpawn(tilePos)) continue;

            Vector3[] path = ParseMoves(spec.moves);
            if (path.Length == 0) continue;

            SpawnBot(tilePos, path, spec.name);
        }
    }

    [System.Serializable]
    private class PatrolSpec
    {
        public string name;
        public int spawnCol;
        public int spawnRow;
        public string moves;
    }

    private void AddHorizontalPatrols(List<PatrolSpec> specs, string prefix, int rowMin, int rowMax, int maxCount)
    {
        var runs = FindHorizontalRuns(rowMin, rowMax, 5);
        runs.Sort((a, b) => b.len.CompareTo(a.len));
        int added = 0;
        for (int i = 0; i < runs.Count && added < maxCount; i++)
        {
            var run = runs[i];
            int steps = Mathf.Max(4, run.len - 2);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < steps; k++) sb.Append("R ");
            for (int k = 0; k < steps; k++) sb.Append("L ");

            specs.Add(new PatrolSpec
            {
                name = prefix + added,
                spawnCol = run.start + Mathf.Min(2, run.len / 2),
                spawnRow = run.row,
                moves = sb.ToString().Trim()
            });
            added++;
        }
    }

    private void AddVerticalPatrols(List<PatrolSpec> specs, string prefix, int colMin, int colMax, int maxCount)
    {
        var runs = FindVerticalRuns(colMin, colMax, 5);
        runs.Sort((a, b) => b.len.CompareTo(a.len));
        int added = 0;
        for (int i = 0; i < runs.Count && added < maxCount; i++)
        {
            var run = runs[i];
            int steps = Mathf.Max(4, run.len - 2);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < steps; k++) sb.Append("B ");
            for (int k = 0; k < steps; k++) sb.Append("F ");

            specs.Add(new PatrolSpec
            {
                name = prefix + added,
                spawnCol = run.col,
                spawnRow = run.start + Mathf.Min(2, run.len / 2),
                moves = sb.ToString().Trim()
            });
            added++;
        }
    }

    /// <summary>
    /// Yatık küp: önce R ile yatay uzanır, sonra B/F ile yatık halde yuvarlanır, L ile kalkar.
    /// </summary>
    private void AddRollerPatrols(List<PatrolSpec> specs, int maxCount)
    {
        // Kalın bölgeler: 3x3 dolu hücreler
        var hubs = new List<Vector2Int>();
        foreach (var cell in walkableSet)
        {
            bool thick = true;
            for (int dr = -1; dr <= 1 && thick; dr++)
            for (int dc = -1; dc <= 1 && thick; dc++)
            {
                if (!IsWalkable(cell.x + dc, cell.y + dr))
                    thick = false;
            }
            if (thick) hubs.Add(cell);
        }

        if (hubs.Count == 0) return;

        hubs.Sort((a, b) => (a.x + a.y * 31).CompareTo(b.x + b.y * 31));
        int step = Mathf.Max(1, hubs.Count / (maxCount + 1));
        int added = 0;
        for (int i = step; i < hubs.Count && added < maxCount; i += step)
        {
            var hub = hubs[i];
            // R: dik → yatık (X), B/F: yatık yuvarlanma, L: tekrar dik
            const string roll = "R B B B B F F F F L L B B B B F F F F R R";
            specs.Add(new PatrolSpec
            {
                name = "Roll" + added,
                spawnCol = hub.x,
                spawnRow = hub.y,
                moves = roll
            });
            added++;
        }
    }

    private struct GridRun
    {
        public int row;
        public int col;
        public int start;
        public int len;
    }

    private List<GridRun> FindHorizontalRuns(int rowMin, int rowMax, int minLen)
    {
        var list = new List<GridRun>();
        rowMin = Mathf.Clamp(rowMin, 0, gridRows);
        rowMax = Mathf.Clamp(rowMax, 0, gridRows);
        for (int r = rowMin; r < rowMax; r++)
        {
            int c = 0;
            while (c < gridCols)
            {
                while (c < gridCols && layoutRows[r][c] != '1') c++;
                int start = c;
                while (c < gridCols && layoutRows[r][c] == '1') c++;
                int len = c - start;
                if (len >= minLen)
                    list.Add(new GridRun { row = r, start = start, len = len });
            }
        }
        return list;
    }

    private List<GridRun> FindVerticalRuns(int colMin, int colMax, int minLen)
    {
        var list = new List<GridRun>();
        colMin = Mathf.Clamp(colMin, 0, gridCols);
        colMax = Mathf.Clamp(colMax, 0, gridCols);
        for (int c = colMin; c < colMax; c++)
        {
            int r = 0;
            while (r < gridRows)
            {
                while (r < gridRows && layoutRows[r][c] != '1') r++;
                int start = r;
                while (r < gridRows && layoutRows[r][c] == '1') r++;
                int len = r - start;
                if (len >= minLen)
                    list.Add(new GridRun { col = c, start = start, len = len });
            }
        }
        return list;
    }

    // Eski tek-koşu yardımcıları (artık Add* kullanılıyor; geride kalan çağrı olmasın diye tutulabilir)
    private bool TryBuildHorizontalPatrol(string name, int rowMin, int rowMax, out PatrolSpec spec)
    {
        spec = null;
        var runs = FindHorizontalRuns(rowMin, rowMax, 4);
        if (runs.Count == 0) return false;
        runs.Sort((a, b) => b.len.CompareTo(a.len));
        var run = runs[0];
        int steps = Mathf.Max(3, run.len - 2);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < steps; i++) sb.Append("R ");
        for (int i = 0; i < steps; i++) sb.Append("L ");
        spec = new PatrolSpec
        {
            name = name,
            spawnCol = run.start + Mathf.Min(2, run.len / 2),
            spawnRow = run.row,
            moves = sb.ToString().Trim()
        };
        return true;
    }

    private bool TryBuildVerticalPatrol(string name, int colMin, int colMax, out PatrolSpec spec)
    {
        spec = null;
        var runs = FindVerticalRuns(colMin, colMax, 4);
        if (runs.Count == 0) return false;
        runs.Sort((a, b) => b.len.CompareTo(a.len));
        var run = runs[0];
        int steps = Mathf.Max(3, run.len - 2);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < steps; i++) sb.Append("B ");
        for (int i = 0; i < steps; i++) sb.Append("F ");
        spec = new PatrolSpec
        {
            name = name,
            spawnCol = run.col,
            spawnRow = run.start + Mathf.Min(2, run.len / 2),
            moves = sb.ToString().Trim()
        };
        return true;
    }

    private Vector2Int FindNearestFreeWalkable(int col, int row)
    {
        Vector2Int best = new Vector2Int(-1, -1);
        int bestDist = int.MaxValue;
        foreach (var cell in walkableSet)
        {
            Vector3 world = CellToWorld(cell.x, cell.y);
            bool occupied = false;
            for (int s = 0; s < botSpawns.Count; s++)
            {
                if (Vector3.Distance(botSpawns[s], world) < 0.4f)
                {
                    occupied = true;
                    break;
                }
            }

            int d = Mathf.Abs(cell.x - col) + Mathf.Abs(cell.y - row);
            if (occupied)
            {
                d += 1000;
            }

            if (d < bestDist)
            {
                bestDist = d;
                best = cell;
            }
        }

        return best;
    }

    private Vector2Int FindNearestWalkable(int col, int row)
    {
        if (IsWalkable(col, row)) return new Vector2Int(col, row);

        int bestDist = int.MaxValue;
        Vector2Int best = new Vector2Int(-1, -1);
        foreach (var cell in walkableSet)
        {
            int d = Mathf.Abs(cell.x - col) + Mathf.Abs(cell.y - row);
            if (d < bestDist)
            {
                bestDist = d;
                best = cell;
            }
        }
        return best;
    }

    private static Vector3[] ParseMoves(string moves)
    {
        if (string.IsNullOrWhiteSpace(moves)) return System.Array.Empty<Vector3>();
        string[] tokens = moves.Split(new[] { ' ', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        var list = new List<Vector3>(tokens.Length);
        for (int i = 0; i < tokens.Length; i++)
        {
            switch (tokens[i].Trim().ToUpperInvariant())
            {
                case "F": case "N": list.Add(Vector3.forward); break;
                case "B": case "S": list.Add(Vector3.back); break;
                case "L": case "W": list.Add(Vector3.left); break;
                case "R": case "E": list.Add(Vector3.right); break;
            }
        }
        return list.ToArray();
    }

    private Vector3 CellToWorld(int col, int row)
    {
        return new Vector3(originX + col, 0f, originZ - row);
    }

    private bool IsWalkable(int c, int r)
    {
        return walkableSet.Contains(new Vector2Int(c, r));
    }

    private void SpawnTile(Vector3 pos)
    {
        var tile = Instantiate(normalTilePrefab, pos, normalTilePrefab.transform.rotation, tilesRoot);
        tile.name = "MenuTile";

        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        Vector3 size = combinedBounds.size;
        Vector3 currentScale = tile.transform.localScale;
        float scaleRatio = (size.x > 0.01f) ? (1f / size.x) : 1f;
        tile.transform.localScale = currentScale * scaleRatio;

        combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        float topY = combinedBounds.max.y;
        tile.transform.position = new Vector3(pos.x, pos.y - topY, pos.z);
    }

    private bool SpawnBot(Vector3 tilePos, Vector3[] path, string label)
    {
        var go = Instantiate(playerCubePrefab);
        go.name = "MenuBot_" + label;
        go.tag = "Untagged";
        go.SetActive(false);
        go.transform.SetParent(botsRoot, true);

        FitPlayerScale(go, tilePos);
        Vector3 spawnPos = go.transform.position;
        Quaternion spawnRot = Quaternion.identity;
        go.transform.rotation = spawnRot;

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            SetLayerRecursive(go, playerLayer);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var tc = go.GetComponent<TumbleController>();
        if (tc == null) tc = go.AddComponent<TumbleController>();
        tc.ConfigureDecorative(spawnPos, spawnRot);
        tc.tumblingDuration = tumbleDuration;
        RestoreSharedMaterials(go);
        if (CubeThemeManager.Instance != null)
            CubeThemeManager.Instance.ApplyTo(go);

        go.SetActive(true);
        bots.Add(tc);
        botSpawns.Add(tilePos);
        botPaths.Add(path);
        botPathIndex.Add(0);
        return true;
    }

    private void RestoreSharedMaterials(GameObject go)
    {
        if (playerCubePrefab == null) return;
        var src = playerCubePrefab.GetComponentInChildren<Renderer>();
        if (src == null || src.sharedMaterial == null) return;

        var renderers = go.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterial = src.sharedMaterial;
    }

    private static void FitPlayerScale(GameObject playerInstance, Vector3 tilePos)
    {
        Vector3 spawnPos = tilePos + Vector3.up * 1f;
        playerInstance.transform.position = spawnPos;

        Renderer[] renderers = playerInstance.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        Vector3 size = combinedBounds.size;
        Vector3 currentScale = playerInstance.transform.localScale;
        float scaleX = (size.x > 0.01f) ? (0.9f / size.x) * currentScale.x : currentScale.x;
        float scaleY = (size.y > 0.01f) ? (1.8f / size.y) * currentScale.y : currentScale.y;
        float scaleZ = (size.z > 0.01f) ? (0.9f / size.z) * currentScale.z : currentScale.z;
        playerInstance.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

        combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        float minY = combinedBounds.min.y;
        playerInstance.transform.position = new Vector3(spawnPos.x, spawnPos.y - minY, spawnPos.z);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void EnsureLight()
    {
        if (lightRoot == null)
        {
            var existing = GameObject.Find("MenuPlayfieldLights");
            if (existing == null)
            {
                existing = new GameObject("MenuPlayfieldLights");
                existing.transform.SetParent(null);
            }
            lightRoot = existing.transform;
        }

        var sun = GetOrCreateLight("MenuPlayfieldSun", LightType.Directional, fieldOrigin);
        sun.transform.rotation = Quaternion.Euler(40f, -55f, 0f);
        sun.intensity = sunIntensity;
        sun.color = new Color(0.95f, 0.97f, 1f);
        sun.shadows = LightShadows.None;

        var fill = GetOrCreateLight("MenuPlayfieldFill", LightType.Point, fieldOrigin + new Vector3(0f, 14f, 0f));
        fill.intensity = fillIntensity;
        fill.range = 90f;
        fill.color = new Color(0.9f, 0.93f, 1f);

        var key = GetOrCreateLight("MenuPlayfieldKey", LightType.Spot, fieldOrigin + new Vector3(0f, 12f, -18f));
        key.transform.LookAt(fieldOrigin + Vector3.up * 0.8f);
        key.intensity = keyIntensity;
        key.range = 60f;
        key.spotAngle = 100f;
        key.innerSpotAngle = 55f;
        key.color = new Color(1f, 0.98f, 0.95f);

        var rim = GetOrCreateLight("MenuPlayfieldRim", LightType.Spot, fieldOrigin + new Vector3(16f, 8f, 2f));
        rim.transform.LookAt(fieldOrigin + Vector3.up * 0.5f);
        rim.intensity = rimIntensity;
        rim.range = 55f;
        rim.spotAngle = 95f;
        rim.innerSpotAngle = 50f;
        rim.color = new Color(0.7f, 0.85f, 1f);
    }

    private Light GetOrCreateLight(string name, LightType type, Vector3 worldPos)
    {
        Transform t = lightRoot != null ? lightRoot.Find(name) : null;
        if (t == null)
        {
            var legacy = transform.Find(name);
            if (legacy != null) t = legacy;
        }

        GameObject go;
        if (t == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(lightRoot != null ? lightRoot : transform, false);
            go.AddComponent<Light>();
        }
        else
        {
            go = t.gameObject;
            if (lightRoot != null && go.transform.parent != lightRoot)
                go.transform.SetParent(lightRoot, true);
        }

        go.transform.position = worldPos;
        var light = go.GetComponent<Light>();
        light.type = type;
        light.enabled = true;
        return light;
    }

    private IEnumerator DriveBots()
    {
        yield return null;
        EnsureLight();
        yield return new WaitForFixedUpdate();

        var delays = new float[bots.Count];
        for (int i = 0; i < delays.Length; i++)
            delays[i] = i * 0.2f;

        while (true)
        {
            for (int i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot == null) continue;
                if (bot.IsMoving) continue;
                if (botPaths[i] == null || botPaths[i].Length == 0) continue;

                delays[i] -= Time.deltaTime;
                if (delays[i] > 0f) continue;
                delays[i] = moveDelay;

                Vector3 chosen = Vector3.zero;
                if (loopRoutes)
                {
                    int safety = botPaths[i].Length;
                    while (safety-- > 0)
                    {
                        int idx = botPathIndex[i] % botPaths[i].Length;
                        botPathIndex[i] = idx + 1;
                        Vector3 dir = botPaths[i][idx];
                        if (IsMoveSafe(bot.transform, dir))
                        {
                            chosen = dir;
                            break;
                        }
                    }
                }
                else
                {
                    if (botPathIndex[i] >= botPaths[i].Length)
                    {
                        continue;
                    }

                    Vector3 dir = botPaths[i][botPathIndex[i]];
                    botPathIndex[i]++;
                    if (IsMoveSafe(bot.transform, dir))
                    {
                        chosen = dir;
                    }
                }

                if (chosen != Vector3.zero)
                    bot.TryMoveExternal(chosen);
            }

            yield return null;
        }
    }

    private static bool IsMoveSafe(Transform t, Vector3 direction)
    {
        float verticalExtent = t.localScale.x * 0.5f;
        if (Mathf.Abs(Vector3.Dot(t.up, Vector3.up)) > 0.9f)
            verticalExtent = t.localScale.y * 0.5f;

        float rollExtent = 0.5f;
        if (Mathf.Abs(Vector3.Dot(t.up, direction)) > 0.9f)
            rollExtent = 1.0f;

        Vector3 pivot = t.position - Vector3.up * verticalExtent + direction * rollExtent;
        Vector3 rotAxis = Vector3.Cross(Vector3.up, direction);
        Quaternion q = Quaternion.AngleAxis(90f, rotAxis);

        Vector3 newPos = pivot + q * (t.position - pivot);
        Vector3 newUp = q * t.up;

        newPos.x = Mathf.Round(newPos.x * 2f) / 2f;
        newPos.z = Mathf.Round(newPos.z * 2f) / 2f;

        bool standing = Mathf.Abs(Vector3.Dot(newUp, Vector3.up)) > 0.9f;
        if (standing)
            return HasTileAt(newPos);

        Vector3 longAxis = newUp;
        longAxis.y = 0f;
        if (longAxis.sqrMagnitude < 0.001f)
            return HasTileAt(newPos);

        longAxis.Normalize();
        return HasTileAt(newPos + longAxis * 0.5f) && HasTileAt(newPos - longAxis * 0.5f);
    }

    private static bool HasTileAt(Vector3 position)
    {
        Vector3 origin = new Vector3(position.x, 0.5f, position.z);
        int layerMask = ~LayerMask.GetMask("Player");
        return Physics.Raycast(origin, Vector3.down, 1.5f, layerMask, QueryTriggerInteraction.Collide);
    }
}
