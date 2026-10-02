using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FurniturePlacer : MonoBehaviour
{
    public LayerMask floorLayer; // 기존 Inspector 값 보존
    public LayerMask furnitureLayer;
    public Transform floorCenter;
    public float floorRadius = 5f;

    private GameObject selectedFurniture;
    private bool isPlacing = false;

    void Update()
    {
        if (isPlacing)
        {
            Furniture furniture = selectedFurniture != null ? selectedFurniture.GetComponent<Furniture>() : null;
            if (furniture != null && furniture.IsMoving) return;
            selectedFurniture = null;
            isPlacing = false;
            return;
        }

        // 자식 Collider를 클릭한 경우에도 가구 루트 선택
        if (Furniture.MovingFurniture != null || Furniture.IsPointerOverUI() || !Input.GetMouseButtonDown(0)) return;
        RoomManager room = RoomManager.Instance;
        if (room == null || !room.CanEditFurniture()) return;
        Camera targetCamera = room.GetPlacementCamera();
        if (targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            Furniture furniture = hit.collider.GetComponentInParent<Furniture>();
            if (furniture != null) furniture.BeginMovement(false);
        }
    }

    public void SelectFurniture(GameObject furniturePrefab)
    {
        if (furniturePrefab == null || furniturePrefab.GetComponent<Furniture>() == null)
        {
            Debug.LogWarning("Furniture가 붙은 가구 프리팹을 연결해주세요.");
            return;
        }
        if (Furniture.MovingFurniture != null) Furniture.MovingFurniture.CancelMovement();

        selectedFurniture = Instantiate(furniturePrefab);
        selectedFurniture.SetActive(true);
        Furniture furniture = selectedFurniture.GetComponent<Furniture>();
        if (!furniture.BeginMovement(true))
        {
            Destroy(selectedFurniture);
            selectedFurniture = null;
            Debug.LogWarning("RoomManager, GridSystem, 배치 모드와 가구 BoxCollider를 확인해주세요.");
            return;
        }
        isPlacing = true;
    }

    public void CancelPlacement()
    {
        if (isPlacing && selectedFurniture != null)
        {
            Furniture furniture = selectedFurniture.GetComponent<Furniture>();
            if (furniture != null) furniture.CancelMovement();
        }
        selectedFurniture = null;
        isPlacing = false;
    }
}
