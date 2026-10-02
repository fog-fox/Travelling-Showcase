using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum FurnitureType
{
    Default,
    Stackable,
    StackableSurface,
    Chair
}

public class Furniture : MonoBehaviour
{
    public FurnitureType furnitureType = FurnitureType.Default;
    public int furnitureWidth = 1; // 바닥에서 차지하는 가로 칸 수
    public int furnitureHeight = 1; // 바닥에서 차지하는 세로 칸 수
    public float placementRadius = 5f;
    public SubGridManager subGrid;
    public Transform chairPoint; // 의자 바닥 중심과 바라볼 방향

    public static Furniture MovingFurniture { get; private set; }
    public bool IsMoving => isMoving;
    public Furniture PlacementBase => isMoving ? previewBaseFurniture : baseFurniture;
    public bool IsAttachedChair => PlacementBase != null && furnitureType == FurnitureType.Chair;
    public bool IsOnFloor => PlacementBase == null || IsAttachedChair;

    private bool isMoving = false;
    private bool isNewFurniture = false;
    private bool hasPlacementPosition = false;
    private bool hasStarted = false;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Transform initialParent;
    private Transform floorParent;
    private Furniture baseFurniture;
    private Furniture currentStackedFurniture;
    private Furniture currentChair;
    private Furniture previewBaseFurniture;
    private RoomManager roomManager;
    private BoxCollider[] furnitureColliders;
    private int rotationCount;
    private int startFrame;
    private static int lastMovementFrame = -1;

    private Collider[] movingColliders;
    private bool[] colliderStates;
    private Rigidbody[] movingRigidbodies;
    private bool[] rigidbodyStates;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        hasStarted = true;
        CreateSubGrid();
        if (!isMoving && roomManager != null) roomManager.RegisterFurniture(this);
    }

    public void Initialize()
    {
        if (roomManager == null) roomManager = RoomManager.Instance;
        if (roomManager == null) roomManager = FindObjectOfType<RoomManager>();
        if (furnitureColliders != null) return;

        floorParent = transform.parent;
        List<BoxCollider> colliders = new List<BoxCollider>();
        foreach (BoxCollider target in GetComponentsInChildren<BoxCollider>())
        {
            if (target.enabled && !target.isTrigger && target.GetComponentInParent<Furniture>() == this)
                colliders.Add(target);
        }
        furnitureColliders = colliders.ToArray();
    }

    private void Update()
    {
        if (isMoving) HandleDragging();
    }

    private void OnMouseDown()
    {
        if (!IsPointerOverUI() && MovingFurniture == null) BeginMovement(false);
    }

    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public bool BeginMovement(bool newFurniture)
    {
        Initialize();
        if (!isActiveAndEnabled || (Application.isPlaying && !newFurniture && Time.frameCount == lastMovementFrame) ||
            MovingFurniture != null || roomManager == null || roomManager.gridSystem == null ||
            !roomManager.CanEditFurniture() || furnitureColliders.Length == 0)
        {
            return false;
        }

        // 이동 취소를 위해 부모 기준 위치와 회전 보관
        initialParent = transform.parent;
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        isNewFurniture = newFurniture;
        isMoving = true;
        hasPlacementPosition = false;
        previewBaseFurniture = baseFurniture;
        MovingFurniture = this;
        startFrame = Time.frameCount;
        float floorAngle = roomManager.gridSystem.GetGridRotation().eulerAngles.y;
        rotationCount = Mathf.RoundToInt(Mathf.DeltaAngle(floorAngle, transform.eulerAngles.y) / 90f);
        SetMovingState();
        return true;
    }

    private void SetMovingState()
    {
        // 미리보기 중에는 다른 가구를 물리적으로 밀지 않도록 처리
        foreach (Furniture furniture in GetComponentsInChildren<Furniture>()) furniture.Initialize();
        movingColliders = GetComponentsInChildren<Collider>();
        colliderStates = new bool[movingColliders.Length];
        for (int i = 0; i < movingColliders.Length; i++)
        {
            colliderStates[i] = movingColliders[i].enabled;
            movingColliders[i].enabled = false;
        }
        movingRigidbodies = GetComponentsInChildren<Rigidbody>();
        rigidbodyStates = new bool[movingRigidbodies.Length];
        for (int i = 0; i < movingRigidbodies.Length; i++)
        {
            rigidbodyStates[i] = movingRigidbodies[i].isKinematic;
            movingRigidbodies[i].isKinematic = true;
        }
    }

    private void RestoreMovingState()
    {
        if (movingColliders != null)
        {
            for (int i = 0; i < movingColliders.Length; i++)
                if (movingColliders[i] != null) movingColliders[i].enabled = colliderStates[i];
        }
        if (movingRigidbodies != null)
        {
            for (int i = 0; i < movingRigidbodies.Length; i++)
                if (movingRigidbodies[i] != null) movingRigidbodies[i].isKinematic = rigidbodyStates[i];
        }
        movingColliders = null;
        movingRigidbodies = null;
        Physics.SyncTransforms();
    }

    private void HandleDragging()
    {
        if (!roomManager.CanEditFurniture() || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMovement();
            return;
        }
        if (Time.frameCount == startFrame) return;

        bool pointerOverUI = IsPointerOverUI();
        if (!pointerOverUI)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) RotateFurniture(scroll > 0f ? 1 : -1);
            FollowMouse();
        }
        else hasPlacementPosition = false;

        bool confirm = isNewFurniture ? Input.GetMouseButtonDown(0) : Input.GetMouseButtonUp(0);
        if (confirm && !TryPlaceFurniture())
        {
            if (!isNewFurniture) CancelMovement();
            else if (!pointerOverUI) Debug.Log("해당 위치에는 가구를 배치할 수 없습니다.");
        }
    }

    public void RotateFurniture(int direction)
    {
        if (isMoving) rotationCount += direction;
    }

    public void FollowMouse()
    {
        hasPlacementPosition = false;
        Camera targetCamera = roomManager.GetPlacementCamera();
        if (targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            Furniture target = hit.collider.GetComponentInParent<Furniture>();
            if (target != null)
            {
                PreviewPlacement(hit.point, target);
                return;
            }
            if (roomManager.IsFloor(hit.collider)) PreviewPlacement(hit.point, null);
            return;
        }
    }

    public void PreviewPlacement(Vector3 position, Furniture target)
    {
        if (!isMoving) return;
        hasPlacementPosition = false;
        previewBaseFurniture = target;
        if (target != null && (target == this || target.transform.IsChildOf(transform))) return;

        Vector3 placementPosition;
        if (target == null)
        {
            GridSystem grid = roomManager.gridSystem;
            if (grid.cellSize <= 0f) return;
            transform.rotation = grid.GetGridRotation() * Quaternion.Euler(0f, rotationCount * 90f, 0f);
            Vector2 size = GetPlacementSize(grid.GetGridRotation());
            int width = Mathf.Max(1, Mathf.CeilToInt(size.x / grid.cellSize - 0.001f));
            int height = Mathf.Max(1, Mathf.CeilToInt(size.y / grid.cellSize - 0.001f));
            if (!grid.GetPlacementPosition(position, width, height, out placementPosition)) return;
        }
        else if (furnitureType == FurnitureType.Chair)
        {
            if (!target.CanAttachChair(this)) return;
            // 책상의 의자 연결 지점은 바닥 격자와 관계없이 사용
            transform.rotation = Quaternion.Euler(0f, target.chairPoint.eulerAngles.y, 0f);
            placementPosition = target.chairPoint.position;
            placementPosition.y = roomManager.gridSystem.GetGridCenter().y;
        }
        else
        {
            if (furnitureType != FurnitureType.Stackable || !target.CanStackFurniture(this)) return;
            transform.rotation = Quaternion.Euler(0f, target.subGrid.transform.eulerAngles.y + rotationCount * 90f, 0f);
            if (!target.subGrid.GetPlacementPosition(position, this, out placementPosition)) return;
        }

        transform.position += placementPosition - GetPlacementPosition();
        hasPlacementPosition = true;
        Physics.SyncTransforms();
    }

    public bool CanPlace()
    {
        if (!isMoving || !hasPlacementPosition || roomManager == null) return false;
        if (previewBaseFurniture != null)
        {
            if (furnitureType == FurnitureType.Chair)
            {
                if (!previewBaseFurniture.CanAttachChair(this)) return false;
            }
            else if (!previewBaseFurniture.CanStackFurniture(this)) return false;
        }
        return roomManager.CanPlaceFurniture(this) && !IsCollidingWithOtherFurniture();
    }

    public bool TryPlaceFurniture()
    {
        if (!CanPlace()) return false;

        ReleaseBaseFurniture();
        baseFurniture = previewBaseFurniture;
        if (baseFurniture != null)
        {
            if (furnitureType == FurnitureType.Chair) baseFurniture.currentChair = this;
            else
            {
                baseFurniture.currentStackedFurniture = this;
                baseFurniture.subGrid.AddFurnitureToGrid(Vector2.zero, this);
            }
            transform.SetParent(baseFurniture.transform, true);
        }
        else transform.SetParent(floorParent, true);

        isMoving = false;
        isNewFurniture = false;
        MovingFurniture = null;
        lastMovementFrame = Time.frameCount;
        RestoreMovingState();
        roomManager.RegisterFurnitureGroup(this);
        return true;
    }

    public void CancelMovement()
    {
        if (!isMoving) return;
        bool removeFurniture = isNewFurniture;
        transform.SetParent(initialParent, false);
        transform.localPosition = initialPosition;
        transform.localRotation = initialRotation;
        isMoving = false;
        isNewFurniture = false;
        previewBaseFurniture = null;
        MovingFurniture = null;
        lastMovementFrame = Time.frameCount;
        RestoreMovingState();

        // 기존 가구의 관계와 점유는 확정 전까지 변경하지 않았으므로 그대로 유지
        if (removeFurniture)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    private void ReleaseBaseFurniture()
    {
        if (baseFurniture == null) return;
        if (baseFurniture.currentChair == this) baseFurniture.currentChair = null;
        if (baseFurniture.currentStackedFurniture == this) baseFurniture.currentStackedFurniture = null;
        if (baseFurniture.subGrid != null) baseFurniture.subGrid.RemoveFurniture(this);
        baseFurniture = null;
    }

    private bool CanAttachChair(Furniture furniture)
    {
        return furnitureType == FurnitureType.StackableSurface && chairPoint != null &&
            (currentChair == null || currentChair == furniture);
    }

    private bool CanStackFurniture(Furniture furniture)
    {
        if (furnitureType != FurnitureType.StackableSurface) return false;
        CreateSubGrid();
        return subGrid != null && subGrid.CanUseGrid(furniture) &&
            (currentStackedFurniture == null || currentStackedFurniture == furniture);
    }

    private void CreateSubGrid()
    {
        if (furnitureType != FurnitureType.StackableSurface || subGrid != null) return;
        Initialize();
        foreach (SubGridManager grid in GetComponentsInChildren<SubGridManager>())
        {
            if (grid.GetComponentInParent<Furniture>() == this)
            {
                subGrid = grid;
                return;
            }
        }

        Bounds bounds = GetFurnitureBounds();
        GameObject gridObject = new GameObject("SubGrid");
        gridObject.transform.SetParent(transform);
        gridObject.transform.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        gridObject.transform.localRotation = Quaternion.identity;
        subGrid = gridObject.AddComponent<SubGridManager>();
        Vector2 size = GetColliderSize(Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        subGrid.gridWidth = Mathf.Max(1, Mathf.FloorToInt(size.x / subGrid.cellSize));
        subGrid.gridHeight = Mathf.Max(1, Mathf.FloorToInt(size.y / subGrid.cellSize));
    }

    // Collider를 끈 미리보기에서도 사용할 수 있도록 Box의 모서리로 범위 계산
    private Bounds GetBounds(Quaternion rotation)
    {
        Initialize();
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        Bounds bounds = new Bounds(inverseRotation * transform.position, Vector3.zero);
        bool firstPoint = true;
        foreach (BoxCollider target in furnitureColliders)
        {
            if (target == null) continue;
            Vector3 halfSize = target.size * 0.5f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = target.transform.TransformPoint(target.center + Vector3.Scale(halfSize, new Vector3(x, y, z)));
                        point = inverseRotation * point;
                        if (firstPoint)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            firstPoint = false;
                        }
                        else bounds.Encapsulate(point);
                    }
                }
            }
        }
        return bounds;
    }

    public Bounds GetFurnitureBounds()
    {
        return GetBounds(Quaternion.identity);
    }

    public Vector3 GetPlacementPosition()
    {
        Bounds bounds = GetFurnitureBounds();
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    public Vector2 GetColliderSize(Quaternion rotation)
    {
        Bounds bounds = GetBounds(rotation);
        Vector3 center = Quaternion.Inverse(rotation) * GetPlacementPosition();
        return new Vector2(
            Mathf.Max(Mathf.Abs(bounds.min.x - center.x), Mathf.Abs(bounds.max.x - center.x)) * 2f,
            Mathf.Max(Mathf.Abs(bounds.min.z - center.z), Mathf.Abs(bounds.max.z - center.z)) * 2f);
    }

    public Vector2 GetPlacementSize(Quaternion rotation)
    {
        Vector2 size = GetColliderSize(rotation);
        float cellSize = roomManager != null && roomManager.gridSystem != null ? roomManager.gridSystem.cellSize : 1f;
        float angle = Mathf.DeltaAngle(rotation.eulerAngles.y, transform.eulerAngles.y) * Mathf.Deg2Rad;
        float width = Mathf.Abs(Mathf.Cos(angle)) * Mathf.Max(1, furnitureWidth) + Mathf.Abs(Mathf.Sin(angle)) * Mathf.Max(1, furnitureHeight);
        float height = Mathf.Abs(Mathf.Sin(angle)) * Mathf.Max(1, furnitureWidth) + Mathf.Abs(Mathf.Cos(angle)) * Mathf.Max(1, furnitureHeight);
        return new Vector2(Mathf.Max(size.x, width * cellSize), Mathf.Max(size.y, height * cellSize));
    }

    private bool IsCollidingWithOtherFurniture()
    {
        Physics.SyncTransforms();
        foreach (Furniture item in GetComponentsInChildren<Furniture>())
        {
            item.Initialize();
            if (item.furnitureColliders.Length == 0) return true;
            Bounds bounds = item.GetFurnitureBounds();
            Collider[] overlaps = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                roomManager.obstacleLayer, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps)
            {
                if (other.transform.IsChildOf(transform) || roomManager.IsFloor(other)) continue;
                Furniture otherFurniture = other.GetComponentInParent<Furniture>();
                // 책상 전체를 감싼 BoxCollider 안으로 의자가 들어가는 배치도 허용
                if (otherFurniture != null && otherFurniture == item.PlacementBase) continue;
                foreach (BoxCollider own in item.furnitureColliders)
                {
                    if (own != null && Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance) &&
                        distance > roomManager.collisionTolerance) return true;
                }
            }
        }
        return false;
    }

    private void OnDisable()
    {
        if (isMoving) CancelMovement();
        if (roomManager != null) roomManager.RemoveFurniture(this);
    }

    private void OnEnable()
    {
        if (hasStarted && roomManager != null) roomManager.RegisterFurniture(this);
    }

    private void OnDestroy()
    {
        if (MovingFurniture == this) MovingFurniture = null;
        ReleaseBaseFurniture();
        if (roomManager != null) roomManager.RemoveFurniture(this);
    }
}
