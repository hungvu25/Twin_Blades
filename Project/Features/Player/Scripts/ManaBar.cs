using UnityEngine;
using UnityEngine.UI;

public class ManaBar : MonoBehaviour
{
    public Image fillImage;

    public void UpdateMana(float currentMana, float maxMana)
    {
        if (fillImage == null) return;

        float safeMax = Mathf.Max(0.0001f, maxMana);
        float manaRatio = Mathf.Clamp01(currentMana / safeMax);
        fillImage.fillAmount = manaRatio;
    }
}
