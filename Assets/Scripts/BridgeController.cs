using UnityEngine;

public class BridgeController : MonoBehaviour
{
    public int channel;
    public bool startsActive = false;

    private bool isActive = false;
    private Renderer tileRenderer;
    private Collider tileCollider;

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
            tileRenderer.enabled = isActive;
        }

        if (tileCollider != null)
        {
            tileCollider.enabled = isActive;
        }

        // Toggle all child GameObjects (like the Quad) active/inactive
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(isActive);
        }
    }

    public bool IsActive()
    {
        return isActive;
    }
}
