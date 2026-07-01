using UnityEngine;

public class SwitchController : MonoBehaviour
{
    public string switchType; // "h", "s", "ho", "so", "hc", "sc"
    public int channel;
    public AudioClip clickSound;

    private bool isPressed = false;

    public void TryPress(bool isStanding)
    {
        // Hard switches (h, ho, hc) can only be pressed if standing upright
        bool canPress = true;
        if (switchType.StartsWith("h"))
        {
            canPress = isStanding;
        }

        if (canPress)
        {
            Press();
        }
    }

    private void Press()
    {
        if (isPressed) return; // Prevent double pressing on the same turn

        isPressed = true;
        
        // Play click sound
        if (clickSound != null)
        {
            AudioSource.PlayClipAtPoint(clickSound, transform.position);
        }
        else
        {
            // Try loading default door close sound from Resources if no sound set
            AudioClip defaultSound = Resources.Load<AudioClip>("AudioClip/door-close");
            if (defaultSound != null)
            {
                AudioSource.PlayClipAtPoint(defaultSound, transform.position);
            }
        }

        // Find all bridge controllers in the scene and update their states
        BridgeController[] bridges = FindObjectsOfType<BridgeController>();
        foreach (var bridge in bridges)
        {
            if (bridge.channel == this.channel)
            {
                if (switchType.EndsWith("o")) // "ho", "so" -> Open
                {
                    bridge.SetActiveState(true);
                }
                else if (switchType.EndsWith("c")) // "hc", "sc" -> Close
                {
                    bridge.SetActiveState(false);
                }
                else // "h", "s" -> Toggle
                {
                    bridge.ToggleActive();
                }
            }
        }

        // Reset pressed flag after a short delay (or at start of next roll)
        StartCoroutine(ResetPress());
    }

    private System.Collections.IEnumerator ResetPress()
    {
        yield return new WaitForSeconds(0.5f);
        isPressed = false;
    }
}
