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
        
        // Simülasyon sırasında ses üretme.
        if (!TumbleController.isSimulating)
        {
            AudioClip sound = clickSound;
            if (sound == null)
            {
                sound = Resources.Load<AudioClip>("AudioClip/door-close");
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEvent(
                    AudioEventId.SwitchActivated,
                    transform.position,
                    sound);
            }
            else if (sound != null)
            {
                AudioSource.PlayClipAtPoint(sound, transform.position);
            }
        }

        // Find all bridge controllers in the scene and update their states
        BridgeController[] bridges = FindObjectsByType<BridgeController>(FindObjectsSortMode.None);
        bool bridgeChanged = false;
        bool bridgeOpened = false;
        Vector3 bridgeSoundPosition = transform.position;

        foreach (var bridge in bridges)
        {
            if (bridge.channel == this.channel)
            {
                bool wasActive = bridge.IsActive();

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

                if (!bridgeChanged && wasActive != bridge.IsActive())
                {
                    bridgeChanged = true;
                    bridgeOpened = bridge.IsActive();
                    bridgeSoundPosition = bridge.transform.position;
                }
            }
        }

        if (bridgeChanged && !TumbleController.isSimulating)
        {
            AudioManager.Instance?.PlayEvent(
                bridgeOpened ? AudioEventId.BridgeOpen : AudioEventId.BridgeClose,
                bridgeSoundPosition);
        }

        // Reset pressed flag after a short delay (or at start of next roll)
        StartCoroutine(ResetPress());
    }

    private System.Collections.IEnumerator ResetPress()
    {
        float delay = TumbleController.isSimulating ? 0.001f : 0.5f;
        yield return new WaitForSeconds(delay);
        isPressed = false;
    }
}
