using System;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening; // DOTween 사용을 위해 추가

public class InputManager : MonoBehaviour
{
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask placementLayermask;
    private Vector3 lastPosition;
    
    public event Action OnClicked, OnExit;
    public GameObject BuildUI;
    public RaycastHit hit;
    public RaycastHit hit2; 
    [SerializeField] private LayerMask batchedLayer;
    
    public bool isBuildMode = false;
    private Vector3 uiShowPosition; // BuildUI가 보이는 위치
    private Vector3 uiHidePosition; // BuildUI가 숨겨진 위치
    private Tween uiTween; // 현재 실행 중인 트윈 저장

    public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

    private void Start()
    {
        // BuildUI의 초기 위치 설정
        if (BuildUI != null)
        {
            // BuildUI의 RectTransform 사용
            RectTransform uiRect = BuildUI.GetComponent<RectTransform>();
            if (uiRect != null)
            {
                // 현재 위치를 보이는 위치로 설정
                uiShowPosition = uiRect.anchoredPosition;

                // 숨겨진 위치는 Y축을 아래로 이동 (화면 아래로)
                uiHidePosition = uiShowPosition + new Vector3(0, -Screen.height, 0); // 화면 높이만큼 아래로

                // 초기 상태: BuildUI 숨김
                uiRect.anchoredPosition = uiHidePosition;
                BuildUI.SetActive(true); // 비활성화 대신 위치로 제어
            }
            else
            {
                Debug.LogError("BuildUI에 RectTransform이 없습니다!");
            }
        }
        else
        {
            Debug.LogError("BuildUI가 할당되지 않았습니다!");
        }
    }

    private void Update()
    {
        // B키로 건설 상태 토글
        if (Input.GetKeyDown(KeyCode.B))
        {
            isBuildMode = !isBuildMode;
            if (isBuildMode)
            {
                ShowBuildUI(); // BuildUI 애니메이션 실행
            }
            else
            {
                HideBuildUI(); // BuildUI 애니메이션 실행
                
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            OnClicked?.Invoke();
        }

        // ESC 키로 건설 상태 종료
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isBuildMode)
            {
                isBuildMode = false;
                placementSystem.ExitBuildMode();
                HideBuildUI();
            }
            OnExit?.Invoke();
        }
    }

    // BuildUI를 위로 올리는 애니메이션
    private void ShowBuildUI()
    {
        if (BuildUI == null) return;

        // 기존 트윈이 있으면 종료
        if (uiTween != null)
        {
            uiTween.Kill();
        }

        RectTransform uiRect = BuildUI.GetComponent<RectTransform>();
        if (uiRect != null)
        {
            placementSystem.EnterBuildMode();
            // DOTween으로 Y축 이동 애니메이션
            uiTween = uiRect.DOAnchorPosY(uiShowPosition.y, 0.5f) // 0.5초 동안 이동
                .SetEase(Ease.OutQuad) // 부드러운 이징
                .OnComplete(() => uiTween = null); // 완료 시 트윈 변수 초기화
        }
    }

    // BuildUI를 아래로 내리는 애니메이션
    private void HideBuildUI()
    {
        if (BuildUI == null) return;

        // 기존 트윈이 있으면 종료
        if (uiTween != null)
        {
            uiTween.Kill();
        }

        RectTransform uiRect = BuildUI.GetComponent<RectTransform>();
        if (uiRect != null)
        {
            // DOTween으로 Y축 이동 애니메이션
            uiTween = uiRect.DOAnchorPosY(uiHidePosition.y, 0.5f) // 0.5초 동안 이동
                .SetEase(Ease.InQuad) // 부드러운 이징
                .OnComplete(() =>
                {
                    uiTween = null;
                    placementSystem.ExitBuildMode();
                }); // 완료 시 트윈 변수 초기화
        }
    }

    public Vector3 GetSelectedMapPosition()
    {
        if (cam is null)
        {
            Debug.LogError("Camera가 할당되지 않았습니다!");
            return Vector3.zero;
        }
        
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = cam.nearClipPlane;
        
        Ray ray = cam.ScreenPointToRay(mousePos);
        //RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 50, placementLayermask))
        {
            lastPosition = hit.point;
        }

        if (Physics.Raycast(ray, out hit2, 50, batchedLayer))
        {
            Debug.Log(hit2.collider.name);
        }
        
        return lastPosition;
    }
}


/*
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private Camera    cam;
    [SerializeField] private LayerMask placementLayermask;
                     private Vector3   lastPosition;

                     private bool isBuildMode = false;
                     
                    
                     public event Action OnClicked, OnExit;
                     public GameObject BuildUI;
                     
                     public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();


     private void Start()
     {
         BuildUI.SetActive(false);
     }

     private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.B)) BuildUI.SetActive(!BuildUI.activeSelf); // 토글 로직 간소화

        // B키로 건설 상태 토글
        if (Input.GetKeyDown(KeyCode.B))
        {
            isBuildMode = !isBuildMode; // 건설 상태 토글
            if (isBuildMode)
            {
                placementSystem.EnterBuildMode(); // 건설 상태 진입
            }
            else
            {
                placementSystem.ExitBuildMode(); // 건설 상태 종료
            }
        }
        
        if (Input.GetMouseButtonDown(0)) OnClicked?.Invoke();;
        
        /*if (Input.GetMouseButtonDown(0))
        {
            OnClicked?.Invoke();
        }#1#
        
        // ESC 키로 건설 상태 종료
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isBuildMode)
            {
                isBuildMode = false;
                placementSystem.ExitBuildMode();
            }
            OnExit?.Invoke();
        }
        /*if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnExit?.Invoke();
            BuildUI.SetActive(false);
        }#1#
    }

    public Vector3 GetSelectedMapPosition()
    {
        if (cam is null)
        {
            Debug.LogError("Camera가 할당되지 않았습니다!");
            return Vector3.zero;
        }
        
        #region 마우스 위치
        Vector3 mousePos = Input.mousePosition;     // 마우스커서 위치 변수에 할당
        mousePos.z = cam.nearClipPlane;             // 마우스 z 값 최소 클리핑 고정
        #endregion
        
        #region Ray
        Ray ray = cam.ScreenPointToRay(mousePos);   // Ray를 마우스에서 발사
        RaycastHit hit;
        if(Physics.Raycast(ray, out hit, 100, placementLayermask)) lastPosition = hit.point; // 100거리 만큼 Layermask에만 ray 발사  
        #endregion
        
        return lastPosition;
    }
}
*/
