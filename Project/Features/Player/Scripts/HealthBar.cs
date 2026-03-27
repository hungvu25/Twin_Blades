using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image fillImage; // Kéo cái Image "Fill" vào đây

    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        // Tính tỷ lệ máu (từ 0 đến 1)
        float healthRatio = (float)currentHealth / maxHealth;
        
        // Cập nhật thanh Fill
        fillImage.fillAmount = healthRatio;
    }
}