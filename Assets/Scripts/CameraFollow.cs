using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target to Follow")]
    public Transform target;

    [Header("Follow Settings")]
    [Tooltip("If true, automatically calculates the camera offset vector based on fixedRotation and distance to ensure the target is perfectly centered.")]
    public bool autoCalculateOffset = true;
    public float distance = 10f;
    [Tooltip("Manual offset of the camera relative to the tracking anchor (used if autoCalculateOffset is false).")]
    public Vector3 offset = new Vector3(4f, 7f, -5f);

    [Header("Rotation Settings")]
    [Tooltip("If true, uses fixed isometric rotation to avoid skewing/tilting perspective changes during movement.")]
    public bool useFixedRotation = true;
    public Vector3 fixedRotation = new Vector3(45f, 35f, 0f);

    [Header("Tracking Settings")]
    public float smoothTime = 0.35f;
    [Tooltip("The size of the box around the anchor where the player can move without moving the camera.")]
    public Vector2 deadZone = new Vector2(0.5f, 0.5f);

    [Header("Zoom Settings")]
    public float zoomSensitivity = 0.02f;
    public float minDistance = 5f;
    public float maxDistance = 25f;

    private Vector3 trackingAnchor;
    private Vector3 velocity = Vector3.zero;
    private bool initialized = false;

    private float lastPinchDistance = 0f;
    private bool isPinching = false;

    private void Start()
    {
        if (target != null)
        {
            InitializeAnchor();
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (!initialized)
        {
            InitializeAnchor();
        }

        HandlePinchToZoom();

        // Calculate differences between target and our tracking anchor
        float deltaX = target.position.x - trackingAnchor.x;
        float deltaZ = target.position.z - trackingAnchor.z;

        // Shift anchor only when target moves past the dead zone borders
        if (Mathf.Abs(deltaX) > deadZone.x)
        {
            trackingAnchor.x += deltaX - Mathf.Sign(deltaX) * deadZone.x;
        }

        if (Mathf.Abs(deltaZ) > deadZone.y)
        {
            trackingAnchor.z += deltaZ - Mathf.Sign(deltaZ) * deadZone.y;
        }

        // Keep anchor height locked at 0 (the floor level) to prevent zoom/height jitter 
        // when the block stands up/lies down, and to keep the camera from falling into the abyss.
        trackingAnchor.y = 0f;

        // Determine the offset to use
        Vector3 activeOffset = offset;
        if (autoCalculateOffset)
        {
            Vector3 dir = Quaternion.Euler(fixedRotation) * Vector3.back;
            activeOffset = dir * distance;
        }

        // Desired camera position
        Vector3 desiredPosition = trackingAnchor + activeOffset;

        // Smooth damp movement
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

        // Handle rotation
        if (useFixedRotation)
        {
            transform.rotation = Quaternion.Euler(fixedRotation);
        }
        else
        {
            transform.LookAt(target.position);
        }
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        InitializeAnchor();

        // Determine the offset to use
        Vector3 activeOffset = offset;
        if (autoCalculateOffset)
        {
            Vector3 dir = Quaternion.Euler(fixedRotation) * Vector3.back;
            activeOffset = dir * distance;
        }

        transform.position = trackingAnchor + activeOffset;

        if (useFixedRotation)
        {
            transform.rotation = Quaternion.Euler(fixedRotation);
        }
        else
        {
            transform.LookAt(target.position);
        }
    }

    private void InitializeAnchor()
    {
        trackingAnchor = target.position;
        trackingAnchor.y = 0f; // Keep anchor height locked at 0 (the floor level)
        initialized = true;
    }

    private void HandlePinchToZoom()
    {
        // Touchscreen pinch-to-zoom
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count >= 2)
        {
            var touch0 = touchscreen.touches[0];
            var touch1 = touchscreen.touches[1];

            if (touch0.press.isPressed && touch1.press.isPressed)
            {
                float currentDistance = Vector2.Distance(touch0.position.ReadValue(), touch1.position.ReadValue());
                if (!isPinching)
                {
                    lastPinchDistance = currentDistance;
                    isPinching = true;
                }
                else
                {
                    float deltaDistance = lastPinchDistance - currentDistance;
                    distance += deltaDistance * zoomSensitivity;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                    lastPinchDistance = currentDistance;
                }
                return;
            }
        }
        isPinching = false;

        // Mouse scroll wheel zoom fallback (for Editor testing)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                distance -= scroll * 0.005f;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }
    }
}
