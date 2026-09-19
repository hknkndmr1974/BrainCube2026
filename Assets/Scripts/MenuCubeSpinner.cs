using UnityEngine;

/// <summary>
/// Main menu arka plan küpü için yavaş ambient dönüş.
/// </summary>
public class MenuCubeSpinner : MonoBehaviour
{
    [SerializeField] private Vector3 degreesPerSecond = new Vector3(8f, 22f, 6f);
    [SerializeField] private bool useUnscaledTime = true;

    public void SetDegreesPerSecond(Vector3 value) => degreesPerSecond = value;

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(degreesPerSecond * dt, Space.World);
    }
}
