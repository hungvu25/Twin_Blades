using Mirror;
using UnityEngine;

public class NetworkSceneTransition : MonoBehaviour
{
    public void StartTransition(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("StartTransition called with an empty scene name.");
            return;
        }

        if (!NetworkServer.active)
        {
            Debug.LogWarning("StartTransition must run on the server.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Scene '{sceneName}' is not in Build Settings.");
            return;
        }

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null)
        {
            Debug.LogWarning("No active NetworkManager found.");
            return;
        }

        manager.ServerChangeScene(sceneName);
    }
}
