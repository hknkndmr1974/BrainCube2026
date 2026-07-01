using System.Collections.Generic;
using UnityEngine;

namespace FlexibleGlassDestructor
{
    /// <summary>
    /// Manages the demo scene environment, including first-person camera controls, 
    /// projectile launching mechanics, and basic scene lifecycle management.
    /// </summary>
    public class DemoSceneManager : MonoBehaviour
    {
        [Header("Ball Settings")]
        public GameObject ballPrefab; // Reference to the ball prefab for instantiation
        public float launchForce = 20f; // Initial impulse magnitude when launched
        public float ballScale = 0.5f; // Local scale multiplier for the ball

        [Header("Spawn Position")]
        public Transform spawnPoint; // Origin point where the ball will be created

        [Header("Explode Position")]
        public Transform explodePoint; // Explode Position
        public float explodeRadius = 10f; // Explode Radius
        
        public float explodeDamege = 100f; // Explode Damege

        [Header("Camera Control Settings")]
        public float moveSpeed = 5f; // Horizontal and vertical travel speed
        public float lookSensitivity = 2f; // Sensitivity for mouse-based rotation
        private float rotationX = 0f; // Current horizontal rotation value
        private float rotationY = 0f; // Current vertical rotation value

        /// <summary>
        /// Standard Unity callback for initialization.
        /// </summary>
        private void Start()
        {
            // Lock and hide the mouse cursor during execution (press ESC to release)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Standard Unity callback for frame-by-frame updates.
        /// </summary>
        private void Update()
        {
            // 1. Camera rotation (Mouse input)
            this.HandleRotation();

            // 2. Camera movement (WASD input)
            this.HandleMovement();

            // 3. Launch ball (Space key)
            if (Input.GetKeyDown(KeyCode.Space))
            {
                this.LaunchBall();
            }

            // 4. Reload the current scene
            if (Input.GetKeyDown(KeyCode.F1))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }

            // Feature to release the cursor via ESC key (for debugging)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Left Click
            if (Input.GetMouseButtonDown(0))
            {
                this.FireWeapon();
            }

            // Right Click
            if (Input.GetMouseButtonDown(1))
            {
                this.Explode(explodePoint.position, explodeRadius, explodeDamege);
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                this.Fracture(explodePoint.position, explodeRadius);
            }
        }

        /// <summary>
        /// Processes camera rotation based on mouse input.
        /// </summary>
        private void HandleRotation()
        {
            this.rotationX += Input.GetAxis("Mouse X") * this.lookSensitivity;
            this.rotationY -= Input.GetAxis("Mouse Y") * this.lookSensitivity;
            
            // Clamp vertical rotation to prevent flipping at the poles
            this.rotationY = Mathf.Clamp(this.rotationY, -90f, 90f);

            this.transform.localRotation = Quaternion.Euler(this.rotationY, this.rotationX, 0);
        }

        /// <summary>
        /// Processes camera movement based on horizontal and vertical input axes.
        /// </summary>
        private void HandleMovement()
        {
            float moveX = Input.GetAxis("Horizontal"); // A, D keys
            float moveZ = Input.GetAxis("Vertical");   // W, S keys

            Vector3 move = this.transform.right * moveX + this.transform.forward * moveZ;
            this.transform.position += move * this.moveSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Spawns and launches a ball object from the defined spawn point.
        /// </summary>
        private void LaunchBall()
        {
            GameObject ball;
            if (this.ballPrefab != null)
            {
                ball = Instantiate(this.ballPrefab);
            }
            else
            {
                ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                
                // Reduce the default sphere size (e.g., 10cm diameter)
                ball.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                ball.AddComponent<Rigidbody>();
            }

            // Determine launch origin (defaults to camera position if spawnPoint is null)
            Transform origin = this.spawnPoint != null ? this.spawnPoint : this.transform;
            ball.transform.position = origin.position;
            ball.transform.localScale = Vector3.one * this.ballScale;

            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Launch the ball directly forward from the camera's orientation
                rb.AddForce(this.transform.forward * this.launchForce, ForceMode.Impulse);
            }

            // Clean up the ball after 3 seconds
            Destroy(ball, 3f);
        }

        /// <summary>
        /// Executes a raycast from the center of the screen and applies damage to any IGlassBreakable target.
        /// Typically called from an input handler or weapon controller.
        /// </summary>
        private void FireWeapon()
        {
            // Generate a ray from the center of the viewport (crosshair position)
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                hit.collider.GetComponentInParent<IGlassBreakable>()?.TakeDamage(hit.point, ray.direction, 10f);

                // Display a sphere at the raycast position
                GameObject viewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                viewSphere.transform.position = hit.point;
                viewSphere.transform.localScale = Vector3.one * 0.05f; // Size
                Renderer sphereRenderer = viewSphere.GetComponent<Renderer>();
                if (sphereRenderer != null)
                {
                    sphereRenderer.material.color = Color.red;
                }
                Destroy(viewSphere, 0.2f); // Erase in 0.2 seconds
            }
        }

        /// <summary>
        /// Triggers an explosion that deals radial damage to all nearby glass objects.
        /// Damage decreases non-linearly (squared falloff) based on the distance from the explosion center.
        /// </summary>
        /// <param name="explosionPos">The world position of the explosion center.</param>
        /// <param name="radius">The maximum range of the explosion's effect.</param>
        /// <param name="maxDamage">The damage applied at the exact center of the explosion.</param>
        private void Explode(Vector3 explosionPos, float radius, float maxDamage)
        {
            // Retrieve all colliders within the explosion's spherical radius
            Collider[] colliders = Physics.OverlapSphere(explosionPos, radius);

            // Store unique systems to prevent redundant calculations per impact
            HashSet<IGlassBreakable> processedSystems = new HashSet<IGlassBreakable>();

            foreach (Collider hit in colliders)
            {
                // Search for the breakable interface in the hierarchy
                var glass = hit.GetComponentInParent<IGlassBreakable>();
                
                if (glass != null)
                {
                    // Skip if this glass system has already been processed by this explosion
                    if (processedSystems.Contains(glass)) continue;
                    
                    // Mark as processed
                    processedSystems.Add(glass);

                    // Calculate direction and relative offset from the explosion center
                    Vector3 diff = hit.transform.position - explosionPos;
                    Vector3 direction = diff.normalized;
                    
                    // Calculate damage attenuation using a squared falloff for more realistic dissipation
                    float proximity = (radius - diff.magnitude) / radius;
                    float damage = (proximity * proximity) * maxDamage;

                    // Guard against negative damage values
                    if (damage <= 0) continue;

                    // Apply damage using the closest point on the collider surface for realistic impact
                    glass.TakeDamage(hit.ClosestPoint(explosionPos), direction, damage);
                }
            }
        }

        /// <summary>
        /// Identifies all breakable glass objects within a spherical radius and triggers their fracture state.
        /// Useful for non-destructive impacts or the initial phase of an explosion.
        /// </summary>
        /// <param name="explosionPos">The origin point of the fracture force.</param>
        /// <param name="radius">The spherical range within which objects will be affected.</param>
        private void Fracture(Vector3 explosionPos, float radius)
        {
            // Detect all colliders within the specified influence range
            Collider[] colliders = Physics.OverlapSphere(explosionPos, radius);

            foreach (Collider hit in colliders)
            {
                // Search for the breakable interface in the hit object or its parent hierarchy
                var glass = hit.GetComponentInParent<IGlassBreakable>();
                
                if (glass != null)
                {
                    // Trigger the fracture visual/logic without necessarily destroying the object
                    glass.Fracture();
                }
            }
        }
        
    }
}