using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour
{
    [SerializeField] private Camera    cam;
    [SerializeField] private LayerMask placementLayermask;
                     private Vector3   lastPosition;

                     public event Action OnClicked, OnExit;
                     
                     public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) OnClicked?.Invoke();
        if (Input.GetKeyDown(KeyCode.Escape)) OnExit?.Invoke();
    }

    public Vector3 GetSelectedMapPosition()
    {
        Vector3 mousePos = Input.mousePosition;     // 마우스커서 위치 변수에 할당
        mousePos.z = cam.nearClipPlane;             // 마우스 z 값 최소 클리핑 고정
        Ray ray = cam.ScreenPointToRay(mousePos);   // Ray를 마우스에서 발사
        RaycastHit hit;

        if(Physics.Raycast(ray, out hit, 100, placementLayermask)) lastPosition = hit.point; // 100거리 만큼 Layermask에만 ray 발사  

        return lastPosition;
    }
}
