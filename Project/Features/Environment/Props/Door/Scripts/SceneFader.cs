using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
    public Image fadeImage;
    public float fadeDuration = 1f;

    public IEnumerator FadeOut()
    {
        float t = 0f;
        Color currentColor = fadeImage.color;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            currentColor.a = Mathf.Clamp01(t / fadeDuration);
            fadeImage.color = currentColor;
            yield return null;
        }
    }
    public IEnumerator FadeIn()
    {
        float t = 0f;
        Color currentColor = fadeImage.color;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            currentColor.a = 1f - Mathf.Clamp01(t / fadeDuration);
            fadeImage.color = currentColor;
            yield return null;
        }
    }
}
