using UnityEngine;
using System;

/// <summary>
/// 배치된 오브젝트를 선택하는 기능을 담당하는 클래스
/// </summary>
public class ObjectSelector : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask selectableObjectsLayer;
    
    private GameObject selectedObject;
    
    // 오브젝트 선택/선택 해제 이벤트
    public event Action<GameObject> OnObjectSelected;
    public event Action OnObjectDeselected;
    
    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        if (inputManager == null)
            inputManager = FindFirstObjectByType<InputManager>();
    }
    
    private void Update()
    {
        // 건설 모드나 삭제 모드일 때는 선택 기능 비활성화
        if (/*inputManager.isBuildMode ||*/ inputManager.isDeleteMode)
        {
            if (selectedObject != null)
            {
                DeselectObject();
            }
            return;
        }
        
        // 마우스 왼쪽 버튼 클릭 시 오브젝트 선택
        if (Input.GetMouseButtonDown(0) && !inputManager.IsPointerOverUI())
        {
            TrySelectObject();
        }
        
        // ESC 키 누르면 선택 해제
        if (Input.GetKeyDown(KeyCode.Escape) && selectedObject != null)
        {
            DeselectObject();
        }
    }
    
    private void TrySelectObject()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 100f, selectableObjectsLayer))
        {
            GameObject hitObject = hit.transform.root.gameObject;
            
            // 이미 선택된 오브젝트를 다시 클릭하면 선택 유지
            if (selectedObject == hitObject)
                return;
                
            // 다른 오브젝트가 선택되어 있었다면 선택 해제
            if (selectedObject != null)
            {
                DeselectObject();
            }
            
            // 새 오브젝트 선택
            selectedObject = hitObject;
            OnObjectSelected?.Invoke(selectedObject);
            Debug.Log($"오브젝트 선택됨: {selectedObject.name}");
        }
        else
        {
            // 빈 공간 클릭 시 선택 해제
            if (selectedObject != null)
            {
                DeselectObject();
            }
        }
    }
    
    public void DeselectObject()
    {
        // 이벤트 발생
        OnObjectDeselected?.Invoke();
        selectedObject = null;
        Debug.Log("오브젝트 선택 해제됨");
    }
    
    public GameObject GetSelectedObject()
    {
        return selectedObject;
    }
}
