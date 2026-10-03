using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps point-anchored direct children of the HUD canvas out of the notch / home indicator area.
/// Elements are only pushed inward as much as needed; stretched panels are left untouched.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class HudSafeArea : MonoBehaviour
{
    [SerializeField] private float padding = 16f;

    private Canvas canvas;
    private readonly Dictionary<RectTransform, Vector2> basePositions = new Dictionary<RectTransform, Vector2>();
    private readonly List<RectTransform> staleKeys = new List<RectTransform>();
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float lastScaleFactor = -1f;
    private int lastChildCount = -1;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
    }

    private void LateUpdate()
    {
        if (canvas == null)
            return;

        Rect safe = Screen.safeArea;
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        float scale = canvas.scaleFactor;

        if (safe == lastSafeArea && screen == lastScreenSize &&
            Mathf.Approximately(scale, lastScaleFactor) && transform.childCount == lastChildCount)
            return;

        lastSafeArea = safe;
        lastScreenSize = screen;
        lastScaleFactor = scale;
        lastChildCount = transform.childCount;
        Apply(safe, screen, scale);
    }

    private void Apply(Rect safe, Vector2Int screen, float scale)
    {
        if (scale <= 0f)
            return;

        float left = safe.xMin / scale;
        float right = (screen.x - safe.xMax) / scale;
        float bottom = safe.yMin / scale;
        float top = (screen.y - safe.yMax) / scale;

        RemoveDestroyedEntries();

        for (int i = 0; i < transform.childCount; i++)
        {
            RectTransform rt = transform.GetChild(i) as RectTransform;
            if (rt == null || rt.anchorMin != rt.anchorMax)
                continue;

            if (!basePositions.TryGetValue(rt, out Vector2 basePos))
            {
                basePos = rt.anchoredPosition;
                basePositions[rt] = basePos;
            }

            Vector2 size = rt.rect.size;
            Vector2 pivot = rt.pivot;
            Vector2 anchor = rt.anchorMin;
            Vector2 pos = basePos;

            if (left > 0f && Mathf.Approximately(anchor.x, 0f))
            {
                float gap = basePos.x - pivot.x * size.x;
                float need = left + padding;
                if (gap < need) pos.x += need - gap;
            }
            else if (right > 0f && Mathf.Approximately(anchor.x, 1f))
            {
                float gap = -(basePos.x + (1f - pivot.x) * size.x);
                float need = right + padding;
                if (gap < need) pos.x -= need - gap;
            }

            if (bottom > 0f && Mathf.Approximately(anchor.y, 0f))
            {
                float gap = basePos.y - pivot.y * size.y;
                float need = bottom + padding;
                if (gap < need) pos.y += need - gap;
            }
            else if (top > 0f && Mathf.Approximately(anchor.y, 1f))
            {
                float gap = -(basePos.y + (1f - pivot.y) * size.y);
                float need = top + padding;
                if (gap < need) pos.y -= need - gap;
            }

            rt.anchoredPosition = pos;
        }
    }

    private void RemoveDestroyedEntries()
    {
        staleKeys.Clear();
        foreach (RectTransform key in basePositions.Keys)
        {
            if (key == null || key.parent != transform)
                staleKeys.Add(key);
        }

        for (int i = 0; i < staleKeys.Count; i++)
            basePositions.Remove(staleKeys[i]);
    }
}
