using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퍼즐 게임용 기본 맵(바닥/테두리)과 장애물을 빠르게 배치하는 설치 스크립트.
/// 빈 오브젝트에 붙인 뒤 Inspector에서 Generate Stage를 실행하세요.
/// </summary>
public class PuzzleStageInstaller : MonoBehaviour
{
    [Header("Grid")]
    [Min(3)] public int width = 10;
    [Min(3)] public int height = 10;
    [Min(0.1f)] public float cellSize = 1f;

    [Header("Prefabs (Optional)")]
    [Tooltip("비어있으면 Quad를 생성해 바닥으로 사용합니다.")]
    public GameObject floorPrefab;

    [Tooltip("비어있으면 Cube를 생성해 벽으로 사용합니다.")]
    public GameObject wallPrefab;

    [Tooltip("비어있으면 Cube를 생성해 장애물로 사용합니다.")]
    public GameObject obstaclePrefab;

    [Header("Obstacle Coordinates")]
    [Tooltip("장애물 배치 좌표 (0,0은 좌하단).")]
    public Vector2Int[] obstacleCells =
    {
        new Vector2Int(3, 3),
        new Vector2Int(4, 3),
        new Vector2Int(6, 6),
        new Vector2Int(2, 7)
    };

    [Header("Appearance")]
    public Color floorColor = new Color(0.16f, 0.18f, 0.22f);
    public Color wallColor = new Color(0.30f, 0.34f, 0.42f);
    public Color obstacleColor = new Color(0.73f, 0.26f, 0.26f);

    private const string RootName = "GeneratedPuzzleStage";

    [ContextMenu("Generate Stage")]
    public void GenerateStage()
    {
        ClearStage();

        Transform root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        Transform floorRoot = CreateChild(root, "Floor");
        Transform wallRoot = CreateChild(root, "Walls");
        Transform obstacleRoot = CreateChild(root, "Obstacles");

        BuildFloor(floorRoot);
        BuildBorderWalls(wallRoot);
        BuildObstacles(obstacleRoot);
    }

    [ContextMenu("Clear Generated Stage")]
    public void ClearStage()
    {
        Transform oldRoot = transform.Find(RootName);
        if (oldRoot == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(oldRoot.gameObject);
            return;
        }
#endif
        Destroy(oldRoot.gameObject);
    }

    private void BuildFloor(Transform parent)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 pos = CellToWorld(x, y);
                GameObject tile = CreateObject(floorPrefab, PrimitiveType.Quad, parent, "Floor", pos);
                tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                tile.transform.localScale = Vector3.one * cellSize;
                ApplyColor(tile, floorColor);
            }
        }
    }

    private void BuildBorderWalls(Transform parent)
    {
        for (int x = -1; x <= width; x++)
        {
            CreateWall(x, -1, parent);
            CreateWall(x, height, parent);
        }

        for (int y = 0; y < height; y++)
        {
            CreateWall(-1, y, parent);
            CreateWall(width, y, parent);
        }
    }

    private void BuildObstacles(Transform parent)
    {
        if (obstacleCells == null || obstacleCells.Length == 0) return;

        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();

        foreach (Vector2Int cell in obstacleCells)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height) continue;
            if (!uniqueCells.Add(cell)) continue;

            GameObject obstacle = CreateObject(obstaclePrefab, PrimitiveType.Cube, parent, "Obstacle", CellToWorld(cell.x, cell.y));
            obstacle.transform.localScale = new Vector3(cellSize * 0.8f, cellSize * 0.8f, cellSize * 0.8f);
            ApplyColor(obstacle, obstacleColor);

            if (obstacle.GetComponent<Collider>() == null)
            {
                obstacle.AddComponent<BoxCollider>();
            }
        }
    }

    private void CreateWall(int x, int y, Transform parent)
    {
        GameObject wall = CreateObject(wallPrefab, PrimitiveType.Cube, parent, "Wall", CellToWorld(x, y));
        wall.transform.localScale = Vector3.one * cellSize;
        ApplyColor(wall, wallColor);

        if (wall.GetComponent<Collider>() == null)
        {
            wall.AddComponent<BoxCollider>();
        }
    }

    private GameObject CreateObject(GameObject prefab, PrimitiveType primitiveType, Transform parent, string objectPrefix, Vector3 position)
    {
        GameObject created;
        if (prefab != null)
        {
            created = Instantiate(prefab, position, Quaternion.identity, parent);
            created.name = $"{objectPrefix}_{position.x:0}_{position.z:0}";
        }
        else
        {
            created = GameObject.CreatePrimitive(primitiveType);
            created.name = $"{objectPrefix}_{position.x:0}_{position.z:0}";
            created.transform.SetParent(parent, false);
            created.transform.position = position;
        }

        return created;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private Vector3 CellToWorld(int x, int y)
    {
        return transform.position + new Vector3(x * cellSize, cellSize * 0.5f, y * cellSize);
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(props);
        props.SetColor("_Color", color);
        renderer.SetPropertyBlock(props);
    }
}
