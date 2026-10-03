using UnityEngine;

public class BridgeController : MonoBehaviour
{
    public int channel;
    public bool startsActive = false;

    private bool isActive = false;
    private Renderer tileRenderer;
    private Collider tileCollider;

    /// <summary>CubeThemeManager atar: true iken orijinal görseller gizli kalır, yerine SkinMesh gösterilir.</summary>
    public bool UsesSkinMesh { get; set; }

    private void Awake()
    {
        tileRenderer = GetComponent<Renderer>();
        tileCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        SetActiveState(startsActive);
    }

    public void ToggleActive()
    {
        SetActiveState(!isActive);
    }

    public void SetActiveState(bool activeState)
    {
        isActive = activeState;

        // Enable or disable renderer and collider based on active state
        if (tileRenderer != null)
        {
            tileRenderer.enabled = isActive && !UsesSkinMesh;
        }

        if (tileCollider != null)
        {
            tileCollider.enabled = isActive;
        }

        // Toggle all child GameObjects (like the Quad) active/inactive
        foreach (Transform child in transform)
        {
            bool isSkin = child.name == CubeThemeManager.SkinMeshName;
            child.gameObject.SetActive(isActive && (!isSkin || UsesSkinMesh));
        }
    }

    public bool IsActive()
    {
        return isActive;
    }
}
