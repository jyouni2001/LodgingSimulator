using UnityEngine;

/// <summary>
/// 오브젝트 조작 관련 클래스들을 관리하는 매니저
/// </summary>
public class ObjectManipulationManager : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ObjectSelector objectSelector;
    [SerializeField] private ObjectManipulationUI manipulationUI;
    [SerializeField] private ObjectRotator objectRotator;
    [SerializeField] private ObjectMover objectMover;
    [SerializeField] private ObjectRemover objectRemover;
    [SerializeField] private InputManager inputManager;
    
    private void Start()
    {
        // 필요한 컴포넌트 자동 찾기
        if (objectSelector == null)
            objectSelector = GetComponentInChildren<ObjectSelector>() ?? FindFirstObjectByType<ObjectSelector>();
            
        if (manipulationUI == null)
            manipulationUI = GetComponentInChildren<ObjectManipulationUI>() ?? FindFirstObjectByType<ObjectManipulationUI>();
            
        if (objectRotator == null)
            objectRotator = GetComponentInChildren<ObjectRotator>() ?? FindFirstObjectByType<ObjectRotator>();
            
        if (objectMover == null)
            objectMover = GetComponentInChildren<ObjectMover>() ?? FindFirstObjectByType<ObjectMover>();
            
        if (objectRemover == null)
            objectRemover = GetComponentInChildren<ObjectRemover>() ?? FindFirstObjectByType<ObjectRemover>();
            
        if (inputManager == null)
            inputManager = FindFirstObjectByType<InputManager>();
            
        // 이벤트 구독
        SetupEventListeners();
    }
    
    private void OnDestroy()
    {
        // 이벤트 구독 해제
        RemoveEventListeners();
    }
    
    private void SetupEventListeners()
    {
        // 회전 시작 시 UI 숨기기
        if (objectRotator != null && manipulationUI != null)
        {
            objectRotator.OnRotationStarted += _ => manipulationUI.gameObject.SetActive(false);
            objectRotator.OnRotationCompleted += (obj, _) => manipulationUI.gameObject.SetActive(true);
        }
        
        // 이동 시작 시 UI 숨기기
        if (objectMover != null && manipulationUI != null)
        {
            objectMover.OnMoveStarted += _ => manipulationUI.gameObject.SetActive(false);
            objectMover.OnMoveCompleted += (obj, _) => manipulationUI.gameObject.SetActive(true);
        }
        
        // 삭제 시 선택 해제
        if (objectRemover != null && objectSelector != null)
        {
            objectRemover.OnObjectRemoved += _ => objectSelector.DeselectObject();
        }
    }
    
    private void RemoveEventListeners()
    {
        if (objectRotator != null && manipulationUI != null)
        {
            objectRotator.OnRotationStarted -= _ => manipulationUI.gameObject.SetActive(false);
            objectRotator.OnRotationCompleted -= (obj, _) => manipulationUI.gameObject.SetActive(true);
        }
        
        if (objectMover != null && manipulationUI != null)
        {
            objectMover.OnMoveStarted -= _ => manipulationUI.gameObject.SetActive(false);
            objectMover.OnMoveCompleted -= (obj, _) => manipulationUI.gameObject.SetActive(true);
        }
        
        if (objectRemover != null && objectSelector != null)
        {
            objectRemover.OnObjectRemoved += _ => objectSelector.DeselectObject();
        }
    }
}
