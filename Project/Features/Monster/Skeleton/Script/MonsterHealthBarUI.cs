using UnityEngine;
using UnityEngine.UI;

public class MonsterHealthBarUI : MonoBehaviour
{
    [Header("World Space")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.25f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(1.2f, 0.18f);
    [SerializeField] private int sortingOrder = 500;

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color fillColor = new Color(0.2f, 0.95f, 0.25f, 1f);

    private Transform uiRoot;
    private RectTransform fillRect;
    private bool isInitialized;

    private static Sprite uiSprite;

    private void Awake()
    {
        EnsureUi();
    }

    private void LateUpdate()
    {
        if (uiRoot == null) return;
        uiRoot.position = transform.position + worldOffset;
    }

    public void Initialize(int currentHealth, int maxHealth)
    {
        EnsureUi();
        isInitialized = true;
        SetHealth(currentHealth, maxHealth);
    }

    public void SetHealth(int currentHealth, int maxHealth)
    {
        EnsureUi();

        float safeMax = Mathf.Max(1f, maxHealth);
        float ratio = Mathf.Clamp01(currentHealth / safeMax);

        if (fillRect != null)
            fillRect.anchorMax = new Vector2(ratio, 1f);

        if (uiRoot != null)
            uiRoot.gameObject.SetActive(currentHealth > 0 && maxHealth > 0);
    }

    public void SetVisible(bool visible)
    {
        if (uiRoot == null)
            EnsureUi();

        if (uiRoot != null)
            uiRoot.gameObject.SetActive(visible);
    }

    private void EnsureUi()
    {
        if (uiRoot != null && fillRect != null)
            return;

        if (uiSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            uiSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
        }

        Transform existing = transform.Find("MonsterHealthBarUI");
        GameObject rootObject;
        if (existing != null)
        {
            uiRoot = existing;
            rootObject = existing.gameObject;
        }
        else
        {
            rootObject = new GameObject("MonsterHealthBarUI");
            uiRoot = rootObject.transform;
            uiRoot.SetParent(transform, false);
            uiRoot.localPosition = worldOffset;
        }

        Canvas canvas = rootObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = rootObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        RectTransform canvasRect = rootObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = barSize;

        CanvasScaler scaler = rootObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = rootObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 25f;

        GraphicRaycaster raycaster = rootObject.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = rootObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        RectTransform bgRect = EnsureChildImage(rootObject.transform, "Bg", backgroundColor, out _);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        RectTransform fillBgRect = EnsureChildImage(rootObject.transform, "FillBg", new Color(0f, 0f, 0f, 0.35f), out _);
        fillBgRect.anchorMin = new Vector2(0.04f, 0.22f);
        fillBgRect.anchorMax = new Vector2(0.96f, 0.78f);
        fillBgRect.offsetMin = Vector2.zero;
        fillBgRect.offsetMax = Vector2.zero;

        RectTransform fillTransform = EnsureChildImage(fillBgRect, "Fill", fillColor, out Image fillImage);
        fillTransform.anchorMin = new Vector2(0f, 0f);
        fillTransform.anchorMax = new Vector2(1f, 1f);
        fillTransform.offsetMin = Vector2.zero;
        fillTransform.offsetMax = Vector2.zero;

        fillRect = fillTransform;

        if (!isInitialized)
            SetVisible(false);
    }

    private RectTransform EnsureChildImage(Transform parent, string objectName, Color color, out Image image)
    {
        Transform child = parent.Find(objectName);
        GameObject childObject;
        if (child != null)
        {
            childObject = child.gameObject;
        }
        else
        {
            childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
        }

        image = childObject.GetComponent<Image>();
        if (image == null)
            image = childObject.AddComponent<Image>();

        image.sprite = uiSprite;
        image.type = Image.Type.Simple;
        image.color = color;

        RectTransform rectTransform = childObject.GetComponent<RectTransform>();
        return rectTransform;
    }
}
