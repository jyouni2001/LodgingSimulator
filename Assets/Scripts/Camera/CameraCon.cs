using UnityEngine;
using Cinemachine;
using Unity.Cinemachine;

public class CameraCon : MonoBehaviour
{
    public CinemachineVirtualCamera CinemachineCam; // CinemachineCamera -> CinemachineVirtualCamera
    public float zoomSpeed = 2f;          // 줌 속도
    public float moveSpeed = 10f;         // 이동 속도 (WASD)
    public float rotationSpeed = 3f;      // 회전 속도 (마우스)
    public float minZoom = 2f;            // 최소 줌 거리
    public float maxZoom = 10f;           // 최대 줌 거리
    public Vector2 boundaryX = new Vector2(-10f, 10f); // X축 이동 제한
    public Vector2 boundaryZ = new Vector2(-10f, 10f); // Z축 이동 제한

    private CinemachineTransposer transposer; // CinemachinePositionComposer -> CinemachineTransposer
    private float targetZoom;
    private float yaw = 0f;              
    private float pitch = 45f;            // 초기 상하 회전값

    private void Start()
    {
        if (CinemachineCam != null)
        {
            // Body 스테이지에서 CinemachineTransposer 가져오기
            transposer = CinemachineCam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                targetZoom = transposer.m_FollowOffset.z; // 초기 줌 값 설정 (z축 거리)
            }
            else
            {
                Debug.LogError("CinemachineTransposer가 CinemachineVirtualCamera에 없습니다. Body 설정을 확인하세요.");
            }
        }
    }

    private void Update()
    {
        if (CinemachineCam == null || transposer == null) return;

        HandleZoom();
        HandleMovement();
        HandleRotation();
    }

    private void HandleZoom()
    {
        float scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            targetZoom -= scrollInput * zoomSpeed; // 줌 방향은 필요에 따라 반전 가능
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            Vector3 followOffset = transposer.m_FollowOffset;
            followOffset.z = Mathf.Lerp(followOffset.z, targetZoom, Time.deltaTime * 5f);
            transposer.m_FollowOffset = followOffset;
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        if (moveDirection.magnitude > 0)
        {
            Vector3 move = Quaternion.Euler(0, yaw, 0) * moveDirection * moveSpeed * Time.deltaTime;
            Vector3 newPosition = transform.position + move;

            // 이동 범위 제한
            newPosition.x = Mathf.Clamp(newPosition.x, boundaryX.x, boundaryX.y);
            newPosition.z = Mathf.Clamp(newPosition.z, boundaryZ.x, boundaryZ.y);
            
            transform.position = newPosition;
        }
    }

    private void HandleRotation()
    {
        if (Input.GetMouseButton(1)) // 마우스 우클릭 시 회전
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;

            pitch = Mathf.Clamp(pitch, 10f, 60f); // 상하 회전 제한

            CinemachineCam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}