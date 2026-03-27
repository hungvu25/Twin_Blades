using UnityEngine;
using Mirror;
using Unity.Cinemachine;

public class PlayerCameraSetup : NetworkBehaviour
{
    [SerializeField] private bool autoCenterOnSprite = true;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        CinemachineCamera cam = FindFirstObjectByType<CinemachineCamera>();

        if (cam == null)
        {
            Debug.LogWarning("Không tìm thấy CinemachineCamera trong scene!");
            return;
        }

        cam.Follow = transform;
        cam.LookAt = transform;

        if (!autoCenterOnSprite) return;

        CinemachinePositionComposer composer = cam.GetComponent<CinemachinePositionComposer>();
        if (composer == null) return;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        // Nếu pivot nhân vật nằm ở chân, dùng tâm sprite để camera bám đúng trọng tâm hiển thị.
        Vector3 worldCenter = sr.bounds.center;
        Vector3 localOffset = transform.InverseTransformPoint(worldCenter);
        composer.TargetOffset = new Vector3(0f, localOffset.y, 0f);
    }
}