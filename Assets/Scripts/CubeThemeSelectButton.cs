using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edit Mode'da kendi yaptığın butona ekle. themeIndex = CubeThemeCatalog sırası.
/// Kod menü üretmez; sadece tıklanınca temayı seçer / uygular.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class CubeThemeSelectButton : MonoBehaviour
{
    [Tooltip("Assets/Resources/CubeThemeCatalog içindeki Themes dizisi indeksi (0 = Default).")]
    [SerializeField] private int themeIndex;

    [SerializeField] private Image highlightTarget;
    [SerializeField] private Color normalColor = new Color(0f, 0.35f, 0.55f, 0.85f);
    [SerializeField] private Color selectedColor = new Color(0f, 0.7f, 0.95f, 0.95f);

    public int ThemeIndex => Mathf.Max(0, themeIndex);

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (highlightTarget == null)
        {
            highlightTarget = GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnClicked);
        }

        CubeThemeManager.ThemeChanged += RefreshVisual;
        RefreshVisual();
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClicked);
        }

        CubeThemeManager.ThemeChanged -= RefreshVisual;
    }

    private void OnClicked()
    {
        if (CubeThemeManager.Instance != null)
        {
            CubeThemeManager.Instance.SelectTheme(themeIndex);
        }
    }

    private void RefreshVisual()
    {
        if (highlightTarget == null || CubeThemeManager.Instance == null)
        {
            return;
        }

        bool selected = CubeThemeManager.Instance.SelectedIndex == themeIndex;
        highlightTarget.color = selected ? selectedColor : normalColor;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        themeIndex = Mathf.Max(0, themeIndex);
        if (highlightTarget == null)
        {
            highlightTarget = GetComponent<Image>();
        }
    }
#endif
}
