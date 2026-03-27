using UnityEngine;

public class DontDestroyUI : MonoBehaviour
{
    private static DontDestroyUI instance;

    void Awake()
    {
        // Nếu đã có một cái Canvas này rồi thì xóa cái mới sinh ra đi
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        // Giữ cái Canvas này lại khi chuyển Scene
        DontDestroyOnLoad(gameObject);
    }
}