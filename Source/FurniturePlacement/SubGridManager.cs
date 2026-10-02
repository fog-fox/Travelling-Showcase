using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubGridManager : MonoBehaviour
{
    public int gridWidth = 2;
    public int gridHeight = 2;
    public float cellSize = 0.5f;
    private Furniture currentFurniture;

    // 가구 위에는 한 개만 배치
    public bool CanUseGrid(Furniture furniture)
    {
        return currentFurniture == null || currentFurniture == furniture;
    }

    public bool GetPlacementPosition(Vector3 position, Furniture furniture, out Vector3 result)
    {
        result = position;
        if (!CanUseGrid(furniture) || cellSize <= 0f || gridWidth <= 0 || gridHeight <= 0) return false;

        Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector2 size = furniture.GetColliderSize(rotation);
        int width = Mathf.Max(1, Mathf.CeilToInt(size.x / cellSize - 0.001f));
        int height = Mathf.Max(1, Mathf.CeilToInt(size.y / cellSize - 0.001f));
        Vector3 localPosition = Quaternion.Inverse(rotation) * (position - transform.position);
        int startX = Mathf.RoundToInt(localPosition.x / cellSize + gridWidth * 0.5f - width * 0.5f);
        int startZ = Mathf.RoundToInt(localPosition.z / cellSize + gridHeight * 0.5f - height * 0.5f);

        if (width > gridWidth || height > gridHeight) return false;
        startX = Mathf.Clamp(startX, 0, gridWidth - width);
        startZ = Mathf.Clamp(startZ, 0, gridHeight - height);
        result = transform.position + rotation * new Vector3(
            (startX + width * 0.5f - gridWidth * 0.5f) * cellSize, 0f,
            (startZ + height * 0.5f - gridHeight * 0.5f) * cellSize);
        return true;
    }

    public bool IsPositionOccupied(Vector2 gridPosition)
    {
        return currentFurniture != null;
    }

    public void AddFurnitureToGrid(Vector2 gridPosition, Furniture furniture)
    {
        if (CanUseGrid(furniture)) currentFurniture = furniture;
    }

    public void RemoveFurnitureFromGrid(Vector2 gridPosition)
    {
        currentFurniture = null;
    }

    public void RemoveFurniture(Furniture furniture)
    {
        if (currentFurniture == furniture) currentFurniture = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (cellSize <= 0f) return;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(gridWidth * cellSize, 0.01f, gridHeight * cellSize));
        Gizmos.matrix = previousMatrix;
    }
}
