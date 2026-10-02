using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }
    private Dictionary<Vector2Int, List<Furniture>> furnitureGrid = new Dictionary<Vector2Int, List<Furniture>>();

    public GameObject floorObject;
    public GridSystem gridSystem;
    public Camera placementCamera; // 비어 있으면 MainCamera 사용
    public bool useCircularArea = false;
    public bool onlyInPlacementMode = true;
    public LayerMask obstacleLayer = ~0; // 벽과 다른 가구를 확인할 레이어
    public float collisionTolerance = 0.002f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        Initialize();
    }

    public void Initialize()
    {
        if (gridSystem == null) gridSystem = GetComponent<GridSystem>();
        if (gridSystem != null && gridSystem.gridCube == null) gridSystem.gridCube = floorObject;
    }

    public bool CanEditFurniture()
    {
        return !onlyInPlacementMode || UIManager.Instance == null || UIManager.Instance.IsInFurniturePlacementMode;
    }

    public Camera GetPlacementCamera()
    {
        if (placementCamera != null && placementCamera.isActiveAndEnabled) return placementCamera;
        return Camera.main;
    }

    public bool IsFloor(Collider target)
    {
        return floorObject != null && target != null && target.GetComponentInParent<Furniture>() == null &&
            (target.gameObject == floorObject || target.transform.IsChildOf(floorObject.transform));
    }

    public Vector3 GetRoomCenter()
    {
        if (gridSystem != null) return gridSystem.GetGridCenter();
        return floorObject != null ? floorObject.transform.position : transform.position;
    }

    public bool IsPositionOccupied(Vector2 gridPosition)
    {
        Vector2Int position = new Vector2Int(Mathf.RoundToInt(gridPosition.x), Mathf.RoundToInt(gridPosition.y));
        if (!furnitureGrid.TryGetValue(position, out List<Furniture> furniture)) return false;
        furniture.RemoveAll(item => item == null);
        return furniture.Count > 0;
    }

    public void AddFurnitureToGrid(Vector2 gridPosition, Furniture furniture)
    {
        // 위치 한 칸이 아닌 가구 전체의 영역을 등록
        RegisterFurniture(furniture);
    }

    public void RemoveFurnitureFromGrid(Vector2 gridPosition)
    {
        Vector2Int position = new Vector2Int(Mathf.RoundToInt(gridPosition.x), Mathf.RoundToInt(gridPosition.y));
        if (!furnitureGrid.TryGetValue(position, out List<Furniture> furniture)) return;
        foreach (Furniture item in new List<Furniture>(furniture)) RemoveFurniture(item);
    }

    public void RemoveFurniture(Furniture furniture)
    {
        List<Vector2Int> emptyPositions = new List<Vector2Int>();
        foreach (var item in furnitureGrid)
        {
            item.Value.RemoveAll(target => target == null || target == furniture);
            if (item.Value.Count == 0) emptyPositions.Add(item.Key);
        }
        foreach (Vector2Int position in emptyPositions) furnitureGrid.Remove(position);
    }

    public void RegisterFurniture(Furniture furniture)
    {
        if (furniture == null || gridSystem == null) return;
        RemoveFurniture(furniture);
        if (!furniture.IsOnFloor) return;

        List<Vector2Int> cells = new List<Vector2Int>();
        if (!gridSystem.GetOccupiedCells(furniture, cells))
        {
            Debug.LogWarning(furniture.name + ": 가구가 바닥 그리드 영역 밖에 있습니다.", furniture);
            return;
        }
        foreach (Vector2Int position in cells)
        {
            if (!furnitureGrid.TryGetValue(position, out List<Furniture> items))
            {
                items = new List<Furniture>();
                furnitureGrid.Add(position, items);
            }
            items.Add(furniture);
        }
    }

    public void RegisterFurnitureGroup(Furniture furniture)
    {
        foreach (Furniture item in furniture.GetComponentsInChildren<Furniture>()) RegisterFurniture(item);
    }

    // 이동 중에도 원래 칸을 유지하고, 확정할 때만 점유 갱신
    public bool CanPlaceFurniture(Furniture furniture)
    {
        if (gridSystem == null || floorObject == null) return false;
        foreach (Furniture item in furniture.GetComponentsInChildren<Furniture>())
        {
            if (!item.IsOnFloor) continue;
            List<Vector2Int> cells = new List<Vector2Int>();
            if (!gridSystem.GetOccupiedCells(item, cells) || !HasFloorBelow(item)) return false;

            if (useCircularArea)
            {
                Vector3 center = GetRoomCenter();
                Bounds bounds = item.GetFurnitureBounds();
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + new Vector3(bounds.extents.x * x, 0f, bounds.extents.z * z);
                        corner.y = center.y;
                        if (Vector3.Distance(center, corner) > item.placementRadius) return false;
                    }
                }
            }

            Furniture allowedBase = item.IsAttachedChair ? item.PlacementBase : null;
            foreach (Vector2Int position in cells)
            {
                if (!furnitureGrid.TryGetValue(position, out List<Furniture> occupants)) continue;
                foreach (Furniture other in occupants)
                {
                    if (other == null || other == allowedBase || other.transform.IsChildOf(furniture.transform)) continue;
                    return false;
                }
            }
        }
        return true;
    }

    private bool HasFloorBelow(Furniture furniture)
    {
        Bounds bounds = furniture.GetFurnitureBounds();
        Collider[] floorColliders = floorObject.GetComponentsInChildren<Collider>();
        float floorHeight = gridSystem.GetGridCenter().y;
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                Vector3 point = new Vector3(bounds.center.x + bounds.extents.x * x * 0.999f,
                    floorHeight + 0.1f, bounds.center.z + bounds.extents.z * z * 0.999f);
                Ray ray = new Ray(point, Vector3.down);
                bool foundFloor = false;
                foreach (Collider floorCollider in floorColliders)
                {
                    if (floorCollider.enabled && !floorCollider.isTrigger && IsFloor(floorCollider) &&
                        floorCollider.Raycast(ray, out RaycastHit hit, 0.2f))
                    {
                        foundFloor = true;
                        break;
                    }
                }
                if (!foundFloor) return false;
            }
        }
        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
