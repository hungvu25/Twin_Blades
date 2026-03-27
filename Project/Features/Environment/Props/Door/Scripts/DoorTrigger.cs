using UnityEngine;
using Mirror; // Thêm thư viện Mirror
using UnityEngine.InputSystem;
public class DoorTrigger : NetworkBehaviour
{
    public string nextSceneName;
    public GameObject interactUI;
    private bool isPlayerNearby = false;

    void Update()
    {
        if (!isClient) return;

        if (isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            CmdRequestSceneChange();
        }
    }

    [Command(requiresAuthority = false)] // Client gửi yêu cầu lên Server
    void CmdRequestSceneChange()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log("Server đang chuyển sang scene: " + nextSceneName);
            // Lệnh quan trọng nhất của Mirror để chuyển map cho toàn bộ room
            NetworkManager.singleton.ServerChangeScene(nextSceneName);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra xem object chạm vào có phải là Local Player không (tránh hiện UI lung tung)
        NetworkIdentity ni = collision.GetComponent<NetworkIdentity>();
        if (ni != null && ni.isLocalPlayer)
        {
            isPlayerNearby = true;
            if (interactUI != null) interactUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        NetworkIdentity ni = collision.GetComponent<NetworkIdentity>();
        if (ni != null && ni.isLocalPlayer)
        {
            isPlayerNearby = false;
            if (interactUI != null) interactUI.SetActive(false);
        }
    }
}