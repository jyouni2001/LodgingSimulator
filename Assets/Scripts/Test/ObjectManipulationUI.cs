using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 오브젝트 주변에 조작 UI를 표시하는 클래스
/// </summary>
public class ObjectManipulationUI : MonoBehaviour
{
    [Header("UI 요소")]
    [SerializeField] private GameObject manipulationUIPanel;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button deleteButton;
    
    [Header("참조")]
    [SerializeField] private ObjectSelector objectSelector;
    [SerializeField] private ObjectRotator objectRotator;
    [SerializeField] private ObjectMover objectMover;
    [SerializeField] private ObjectRemover objectRemover;
    [SerializeField] private Camera mainCamera;
    
    private GameObject selectedObject;
    private Canvas uiCanvas;
    private RectTransform uiRectTransform;
    
    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        uiCanvas = GetComponent<Canvas>();
        uiRectTransform = manipulationUIPanel.GetComponent<RectTransform>();
        
        // UI 초기 상태 설정
        manipulationUIPanel.SetActive(false);
    }
    
    private void Start()
    {
        // 이벤트 리스너 등록
        if (objectSelector != null)
        {
            objectSelector.OnObjectSelected += ShowUI;
            objectSelector.OnObjectDeselected += HideUI;
        }
        
        // 버튼 이벤트 설정
        if (rotateButton != null)
            rotateButton.onClick.AddListener(OnRotateButtonClicked);
            
        if (moveButton != null)
            moveButton.onClick.AddListener(OnMoveButtonClicked);
            
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
    }
    
    private void OnDestroy()
    {
        // 이벤트 리스너 해제
        if (objectSelector != null)
        {
            objectSelector.OnObjectSelected -= ShowUI;
            objectSelector.OnObjectDeselected -= HideUI;
        }
        
        // 버튼 이벤트 해제
        if (rotateButton != null)
            rotateButton.onClick.RemoveListener(OnRotateButtonClicked);
            
        if (moveButton != null)
            moveButton.onClick.RemoveListener(OnMoveButtonClicked);
            
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteButtonClicked);
    }
    
    private void Update()
    {
        // 선택된 오브젝트가 있을 때 UI 위치 업데이트
        if (selectedObject != null && manipulationUIPanel.activeSelf)
        {
            UpdateUIPosition();
        }
    }
    
    private void ShowUI(GameObject obj)
    {
        selectedObject = obj;
        manipulationUIPanel.SetActive(true);
        UpdateUIPosition();
    }
    
    private void HideUI()
    {
        selectedObject = null;
        manipulationUIPanel.SetActive(false);
    }
    
    private void UpdateUIPosition()
    {
        if (selectedObject == null || mainCamera == null)
            return;
            
        // 오브젝트 위치를 스크린 좌표로 변환
        Vector3 screenPos = mainCamera.WorldToScreenPoint(selectedObject.transform.position);
        
        // UI가 화면 밖으로 나가지 않도록 조정
        screenPos.x = Mathf.Clamp(screenPos.x, uiRectTransform.rect.width / 2, Screen.width - uiRectTransform.rect.width / 2);
        screenPos.y = Mathf.Clamp(screenPos.y, uiRectTransform.rect.height / 2, Screen.height - uiRectTransform.rect.height / 2);
        
        // UI 위치 설정 (오브젝트 위에 표시)
        uiRectTransform.position = new Vector3(screenPos.x, screenPos.y + 50f, 0);
    }
    
    private void OnRotateButtonClicked()
    {
        if (objectRotator != null && selectedObject != null)
        {
            objectRotator.StartRotating(selectedObject);
        }
    }
    
    private void OnMoveButtonClicked()
    {
        if (objectMover != null && selectedObject != null)
        {
            objectMover.StartMoving(selectedObject);
        }
    }
    
    private void OnDeleteButtonClicked()
    {
        if (objectRemover != null && selectedObject != null)
        {
            objectRemover.RemoveObject(selectedObject);
            HideUI();
        }
    }
}
