using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera    cam;
    [SerializeField] private LayerMask placementLayermask;
                     private Vector3   lastPosition;
                     
                    
                     public event Action OnClicked, OnExit;
                     public GameObject BuildUI;
                     
                     public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();


     private void Start()
     {
         BuildUI.SetActive(false);
     }

     private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B)) BuildUI.SetActive(!BuildUI.activeSelf); // 토글 로직 간소화

        if (Input.GetMouseButtonDown(0)) OnClicked?.Invoke();;
        
        /*if (Input.GetMouseButtonDown(0))
        {
            OnClicked?.Invoke();
        }*/
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnExit?.Invoke();
            BuildUI.SetActive(false);
        }
    }

    public Vector3 GetSelectedMapPosition()
    {
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
