using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    public GameObject gridCube; // 그리드를 표시할 바닥
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSize = 1f;
    public bool showGrid = true;

    public Vector3 GetGridCenter()
    {
        if (gridCube == null) return transform.position;

        Collider floorCollider = gridCube.GetComponent<Collider>();
        if (floorCollider != null)
        {
            Bounds bounds = floorCollider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
        return gridCube.transform.position;
    }

    public Quaternion GetGridRotation()
    {
        Transform target = gridCube != null ? gridCube.transform : transform;
        return Quaternion.Euler(0f, target.eulerAngles.y, 0f);
    }

    // 가구 크기를 고려하여 칸의 중앙에 배치
    public bool GetPlacementPosition(Vector3 position, int width, int height, out Vector3 result)
    {
        result = position;
        if (cellSize <= 0f || gridWidth <= 0 || gridHeight <= 0) return false;

        Vector3 localPosition = Quaternion.Inverse(GetGridRotation()) * (position - GetGridCenter());
        int startX = Mathf.RoundToInt(localPosition.x / cellSize + gridWidth * 0.5f - width * 0.5f);
        int startZ = Mathf.RoundToInt(localPosition.z / cellSize + gridHeight * 0.5f - height * 0.5f);

        if (width <= 0 || height <= 0 || startX < 0 || startZ < 0 ||
            startX + width > gridWidth || startZ + height > gridHeight) return false;

        result = GetCellPosition(startX, startZ, width, height);
        return true;
    }

    public Vector3 GetCellPosition(int startX, int startZ, int width, int height)
    {
        Vector3 position = new Vector3(
            (startX + width * 0.5f - gridWidth * 0.5f) * cellSize,
            0f,
            (startZ + height * 0.5f - gridHeight * 0.5f) * cellSize
        );
        return GetGridCenter() + GetGridRotation() * position;
    }

    // 의자처럼 격자 밖의 위치에 놓인 가구도 차지하는 영역을 예약
    public bool GetOccupiedCells(Furniture furniture, List<Vector2Int> cells)
    {
        cells.Clear();
        if (cellSize <= 0f || gridWidth <= 0 || gridHeight <= 0) return false;

        Quaternion rotation = GetGridRotation();
        Vector3 center = Quaternion.Inverse(rotation) * (furniture.GetPlacementPosition() - GetGridCenter());
        Vector2 size = furniture.GetPlacementSize(rotation);
        float minX = (center.x - size.x * 0.5f) / cellSize + gridWidth * 0.5f;
        float maxX = (center.x + size.x * 0.5f) / cellSize + gridWidth * 0.5f;
        float minZ = (center.z - size.y * 0.5f) / cellSize + gridHeight * 0.5f;
        float maxZ = (center.z + size.y * 0.5f) / cellSize + gridHeight * 0.5f;

        const float tolerance = 0.001f;
        if (minX < -tolerance || minZ < -tolerance ||
            maxX > gridWidth + tolerance || maxZ > gridHeight + tolerance) return false;

        int startX = Mathf.Max(0, Mathf.FloorToInt(minX + tolerance));
        int endX = Mathf.Min(gridWidth - 1, Mathf.CeilToInt(maxX - tolerance) - 1);
        int startZ = Mathf.Max(0, Mathf.FloorToInt(minZ + tolerance));
        int endZ = Mathf.Min(gridHeight - 1, Mathf.CeilToInt(maxZ - tolerance) - 1);

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++) cells.Add(new Vector2Int(x, z));
        }
        return cells.Count > 0;
    }

    public bool CanPlaceFurnitureOnCube(int startX, int startZ, int furnitureWidth, int furnitureHeight)
    {
        if (RoomManager.Instance == null || furnitureWidth <= 0 || furnitureHeight <= 0) return false;
        for (int x = startX; x < startX + furnitureWidth; x++)
        {
            for (int z = startZ; z < startZ + furnitureHeight; z++)
            {
                if (x < 0 || z < 0 || x >= gridWidth || z >= gridHeight ||
                    RoomManager.Instance.IsPositionOccupied(new Vector2(x, z))) return false;
            }
        }
        return true;
    }

    public void PlaceFurnitureOnCube(GameObject furniture, int startX, int startZ, int furnitureWidth, int furnitureHeight)
    {
        Furniture target = furniture != null ? furniture.GetComponent<Furniture>() : null;
        if (target == null || !CanPlaceFurnitureOnCube(startX, startZ, furnitureWidth, furnitureHeight)) return;
        if (!target.BeginMovement(false)) return;

        target.PreviewPlacement(GetCellPosition(startX, startZ, furnitureWidth, furnitureHeight), null);
        if (!target.TryPlaceFurniture()) target.CancelMovement();
    }

    private void OnDrawGizmos()
    {
        if (!showGrid || cellSize <= 0f || gridWidth <= 0 || gridHeight <= 0) return;
        Gizmos.color = Color.gray;
        Vector3 center = GetGridCenter() + Vector3.up * 0.01f;
        Quaternion rotation = GetGridRotation();
        float width = gridWidth * cellSize;
        float height = gridHeight * cellSize;

        for (int x = 0; x <= gridWidth; x++)
        {
            float position = x * cellSize - width * 0.5f;
            Gizmos.DrawLine(center + rotation * new Vector3(position, 0f, -height * 0.5f),
                center + rotation * new Vector3(position, 0f, height * 0.5f));
        }
        for (int z = 0; z <= gridHeight; z++)
        {
            float position = z * cellSize - height * 0.5f;
            Gizmos.DrawLine(center + rotation * new Vector3(-width * 0.5f, 0f, position),
                center + rotation * new Vector3(width * 0.5f, 0f, position));
        }
    }
}
