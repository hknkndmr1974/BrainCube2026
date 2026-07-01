using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FlexibleGlassDestructor
{
    [ExecuteInEditMode] // Instantly update changes in the editor
    public class FlexibleGlass : MonoBehaviour, IGlassBreakable
    {
        [Header("Settings")]
        public Vector2 glassSize = new Vector2(1f, 1f); // Width and height dimensions of the glass pane
        [Range(0.001f, 0.5f)]
        public float thickness = 0.05f;                 // Thickness of the glass pane
        public Material glassMaterial; // Material applied to the main glass surfaces
        public Material glassCrossSectionMaterial; // Material applied to the fractured cross-sections

        [Header("Durability Settings")]
        [Tooltip("Determines if the glass can be broken by physical collisions")]
        public bool isBreakableByPhysics = true; // Toggle for enabling/disabling physics-based destruction
        [SerializeField] private float healthPhysics = 10f; // Maximum durability against physical collisions
        private float currentHealthPhysics; // Current remaining health for physical impacts

        [Tooltip("Determines if the glass can be broken by raycast-based damage")]
        public bool isBreakableByRaycast = true; // Toggle for enabling/disabling weapon-based destruction
        [SerializeField] private float healthRaycast = 10f; // Maximum durability against raycast-based attacks
        private float currentHealthRaycast; // Current remaining health for raycast/damage system impacts

        public float baseRippleRadius = 0.2f;   // Minimum fracture radius
        public float forceMultiplier = 0.05f;  // How much the impact force scales the radius
        public float maxRippleRadius = 2.0f;   // Maximum allowed radius to prevent excessive spread

        // Internal state to track the current preview display mode
        [SerializeField, HideInInspector] private bool previewShattered = false; // Flag for editor-side preview state

        [Header("Fracture Settings")]
        [Tooltip("Number of divisions in the X direction")]
        [Range(1, 100)] // Limited by slider for performance
        public int XAxisDivisionCount = 10; // Subdivisions along the horizontal axis

        [Tooltip("Number of divisions in the Y direction")]
        [Range(1, 100)]
        public int YAxisDivisionCount = 10; // Subdivisions along the vertical axis

        [Tooltip("Intensity of the fracture tilt")]
        [Range(0, 10)]
        public float tiltIntensity = 5.0f; // Multiplier for the variation in fragment angles

        [Tooltip("Time (seconds) from when the fragment awakens until it disappears")]
        [Range(1f, 60f)]
        public float fragmentLifetime = 3.0f;

        [Header("Sound Settings")]
        public AudioSource audioSource; // Component for playing sound effects
        public AudioClip shatterSound;  // Audio clip for complete glass destruction
        public AudioClip fractureSound;  // Audio clip for complete glass destruction
        [Range(0f, 2f)]
        public float audioVolume = 1.0f; // Volume

        [Range(0f, 2f)]
        public float pitchRandomness = 0.2f; // Amount of random variation in sound pitch

        [Header("Cinematic Settings")]
        [Tooltip("Enables stylized floating effects for shattered fragments")]
        public bool cinematicMode = false; // Toggle for cinematic visual effects
        
        [Tooltip("How long the fragments remain floating in the air")]
        public float floatDuration = 3.0f; // Duration of the float effect
        
        [Tooltip("Air resistance applied while floating to slow down movement")]  

        [Header("References")]
        [SerializeField, HideInInspector]
        private GameObject visualMesh; // Reference to the primary visual mesh object

        // Instance of the fractured model currently present in the scene
        [SerializeField, HideInInspector]
        private GameObject fracturedInstance; // Cached reference to the instantiated shattered fragments

        // State flag indicating if the glass is currently cracked
        private bool isCracked = false; // True if the damage has exceeded the crack threshold
        
        /// <summary>
        /// Standard Unity callback for initialization.
        /// </summary>
        private void Start()
        {
            this.InitializeGlass();
            SetupAudioSource();
        }

        /// <summary>
        /// Initializes or resets the glass state, durability, and visual/physics components.
        /// </summary>
        private void InitializeGlass()
        {
            // Initialize current health to maximum
            this.currentHealthPhysics = this.healthPhysics;
            this.currentHealthRaycast = this.healthRaycast;

            // Always dynamically resolve child references at runtime to prevent prefab reference mismatches
            Transform solidChild = this.transform.Find("Glass_Solid");
            if (solidChild != null)
            {
                this.visualMesh = solidChild.gameObject;
            }
            else
            {
                this.visualMesh = null;
            }

            Transform fracturedChild = this.transform.Find("Glass_Fractured");
            if (fracturedChild != null)
            {
                this.fracturedInstance = fracturedChild.gameObject;
            }
            else
            {
                this.fracturedInstance = null;
            }

            // Ensure visual mesh is created
            if (this.visualMesh == null)
            {
                this.UpdateGlass();
            }

            // Force reset the visual state (overrides editor preview states)
            if (this.visualMesh != null)
            {
                this.visualMesh.SetActive(true);
            }

            // Always dynamically regenerate the pieces at runtime to avoid missing/invalid mesh references from prefabs
            if (Application.isPlaying || this.fracturedInstance == null)
            {
                Debug.Log("[FlexibleGlass] Always regenerating pieces at runtime/playmode to ensure valid mesh references...");
                this.CreateFracturedBodyInScene();
            }

            Debug.Log($"[FlexibleGlass] InitializeGlass. Parent: {gameObject.name}, scale: {transform.localScale}, visualMesh: {visualMesh != null}, fracturedInstance: {fracturedInstance != null}, glassMaterial: {glassMaterial != null}, crossSectionMaterial: {glassCrossSectionMaterial != null}, layer: {gameObject.layer}");

            if (this.fracturedInstance != null)
            {
                this.fracturedInstance.SetActive(false);
                Debug.Log($"[FlexibleGlass] fracturedInstance has {fracturedInstance.transform.childCount} children. Deactivating container.");

                // Set all fragments' physics state to "Static"
                Rigidbody[] rbs = this.fracturedInstance.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in rbs)
                {
                    rb.isKinematic = true;
                }
            }

            // Final check for necessary components on the parent object
            Rigidbody myRb = GetComponent<Rigidbody>();
            if (myRb != null)
            {
                myRb.isKinematic = true;
            }
        }

        /// <summary>
        /// Configures the AudioSource component with default spatial settings.
        /// Automatically adds or retrieves the component if not assigned.
        /// </summary>
        private void SetupAudioSource()
        {
            // Retrieve the AudioSource if missing, or add one if it doesn't exist
            if (this.audioSource == null)
            {
                this.audioSource = this.GetComponent<AudioSource>();
                if (this.audioSource == null)
                {
                    this.audioSource = this.gameObject.AddComponent<AudioSource>();
                }
            }

            // Apply default configuration values via code
            this.audioSource.playOnAwake = false;
            this.audioSource.spatialBlend = 1.0f; // Fully 3D spatialized sound
            this.audioSource.minDistance = 1.0f;
            this.audioSource.maxDistance = 20.0f;
            this.audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            
        }

        /// <summary>
        /// Standard Unity callback called when script values are modified in the Inspector.
        /// </summary>
        private void OnValidate()
        {
            this.UpdateGlass();
        }

        /// <summary>
        /// Updates the glass visual representation based on current settings.
        /// Creates a visual mesh if one does not exist.
        /// </summary>
        public void UpdateGlass()
        {
            // Search by name and reuse existing objects even if they are no longer referenced (prevent proliferation)
            if (this.visualMesh == null)
            {
                Transform existing = this.transform.Find("Glass_Solid");
                if (existing != null) this.visualMesh = existing.gameObject;
            }

            // Create a visual representation object if it doesn't exist
            if (this.visualMesh == null)
            {
                // Create a primitive Cube named "Glass_Solid" as a child object
                this.visualMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                this.visualMesh.name = "Glass_Solid";
                this.visualMesh.transform.SetParent(this.transform);
                this.visualMesh.transform.localPosition = Vector3.zero;
            }

            // Update the dimensions based on Inspector values
            this.visualMesh.transform.localScale = new Vector3(this.glassSize.x, this.glassSize.y, this.thickness);

            // Apply the assigned material to the mesh
            if (this.glassMaterial != null)
            {
                MeshRenderer renderer = this.visualMesh.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // Use sharedMaterial for editor-time updates
                    renderer.sharedMaterial = this.glassMaterial; 
                }
            }
        }

        /// <summary>
        /// Generates the complete fractured body structure within the scene.
        /// Orchestrates cleanup, container setup, and fragment generation.
        /// </summary>
        public void CreateFracturedBodyInScene()
        {
            // 1. Cleanup existing objects
            this.CleanupOldFractures();

            // 2. Create the container object
            this.SetupFractureContainer();

            // 3. Generate individual fragments based on the map
            this.GeneratePiecesFromMap();

            // 4. Hide the fractured model immediately after generation
            this.fracturedInstance.SetActive(false);
            
        }

        /// <summary>
        /// Removes the existing fractured model from the scene to prevent duplication.
        /// </summary>
        private void CleanupOldFractures()
        {
            if (this.fracturedInstance != null)
            {
                // In play mode, using DestroyImmediate is safer during initialization
                // to prevent duplicate active objects (and their colliders) in the same frame.
                DestroyImmediate(this.fracturedInstance);
            }
        }

        /// <summary>
        /// Creates and configures the parent container object for all generated fragments.
        /// </summary>
        private void SetupFractureContainer()
        {
            this.fracturedInstance = new GameObject("Glass_Fractured");
            this.fracturedInstance.transform.SetParent(this.transform);
            this.fracturedInstance.transform.localPosition = Vector3.zero;
            this.fracturedInstance.transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Creates an individual triangular fragment with depth, assigned materials, and physics components.
        /// </summary>
        /// <param name="v0">First vertex of the triangle face.</param>
        /// <param name="v1">Second vertex of the triangle face.</param>
        /// <param name="v2">Third vertex of the triangle face.</param>
        /// <param name="name">Name to assign to the fragment GameObject.</param>
        /// <param name="pieceThickness">Depth/thickness of the fragment.</param>
        private void CreateTrianglePiece(Vector3 v0, Vector3 v1, Vector3 v2, string name, float pieceThickness)
        {
            GameObject piece = new GameObject(name);
            piece.transform.SetParent(this.fracturedInstance.transform, false);

            // Match the layer of the parent to ensure camera renders it correctly
            piece.layer = this.gameObject.layer;

            Vector3 center = (v0 + v1 + v2) / 3f; // Calculate geometric center
            piece.transform.localPosition = center;

            // Set local scale to 1 so it inherits the parent's scale correctly without double scaling
            piece.transform.localScale = Vector3.one;

            // 1. Apply random tilt for visual variety
            Quaternion randomRotation = Quaternion.Euler(
                Random.Range(-this.tiltIntensity, this.tiltIntensity),
                Random.Range(-this.tiltIntensity, this.tiltIntensity),
                Random.Range(-this.tiltIntensity * 0.5f, this.tiltIntensity * 0.5f)
            );
            piece.transform.localRotation = randomRotation;

            MeshFilter mf = piece.AddComponent<MeshFilter>();
            MeshRenderer mr = piece.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh();

            // 2. Vertex creation (3 Front-face vertices + 3 Back-face vertices)
            Vector3[] vertices = new Vector3[6];
            float halfThickness = pieceThickness * 0.5f;
            vertices[0] = (v0 - center) + Vector3.back * halfThickness;
            vertices[1] = (v1 - center) + Vector3.back * halfThickness;
            vertices[2] = (v2 - center) + Vector3.back * halfThickness;
            vertices[3] = (v0 - center) + Vector3.forward * halfThickness;
            vertices[4] = (v1 - center) + Vector3.forward * halfThickness;
            vertices[5] = (v2 - center) + Vector3.forward * halfThickness;

            // 3. Define indices for the main faces (Front/Back) and the side edges
            int[] mainTris = new int[] {
                0, 1, 2,        // Front face
                5, 4, 3         // Back face
            };

            int[] sideTris = new int[] {
                0, 3, 4, 0, 4, 1, // Side 1
                1, 4, 5, 1, 5, 2, // Side 2
                2, 5, 3, 2, 3, 0  // Side 3
            };

            // Prepare arrays for flattened vertices and UVs based on total triangle count
            int totalTrisLength = mainTris.Length + sideTris.Length;
            Vector3[] flatVertices = new Vector3[totalTrisLength];
            int[] flatMainTris = new int[mainTris.Length];
            int[] flatSideTris = new int[sideTris.Length];
            Vector2[] uvs = new Vector2[totalTrisLength];

            // Populate main face vertices and UVs
            for (int i = 0; i < mainTris.Length; i++) {
                flatVertices[i] = vertices[mainTris[i]];
                flatMainTris[i] = i;
                uvs[i] = new Vector2(flatVertices[i].x, flatVertices[i].y);
            }

            // Pre-calculate side edge lengths for UV scaling
            float side1Len = Vector3.Distance(v0, v1);
            float side2Len = Vector3.Distance(v1, v2);
            float side3Len = Vector3.Distance(v2, v0);

            // Populate side vertices and generate UVs (starts after main face vertices)
            for (int i = 0; i < sideTris.Length; i++) {
                int vertIdx = mainTris.Length + i;
                flatVertices[vertIdx] = vertices[sideTris[i]];
                flatSideTris[i] = vertIdx;

                float currentSideLen = (i < 6) ? side1Len : (i < 12) ? side2Len : side3Len;

                float uBase = (float)(i % 6) / 5f; 
                float u = uBase * currentSideLen * 100f;
                float v = (flatVertices[vertIdx].z > 0) ? 1f : 0f;
                uvs[vertIdx] = new Vector2(u, v);
            }

            mesh.vertices = flatVertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2; // Configure two sub-meshes for different materials
            mesh.SetIndices(flatMainTris, MeshTopology.Triangles, 0); // SubMesh 0: Front/Back faces
            mesh.SetIndices(flatSideTris, MeshTopology.Triangles, 1); // SubMesh 1: Cross-section sides

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            mf.mesh = mesh;

            // 4. Assign materials (Order must match sub-mesh indices)
            mr.sharedMaterials = new Material[] { this.glassMaterial, this.glassCrossSectionMaterial };

            // 5. Physics and collision setup
            MeshCollider mc = piece.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = mesh;

            piece.AddComponent<Rigidbody>().isKinematic = true;

            // Attach fragment-specific behavior script
            IndividualFragment fragment = piece.AddComponent<IndividualFragment>();
            fragment.parentGlass = this;
        }

        /// <summary>
        /// Generates a 2D array of vertex positions for the glass grid, applying random jitter to internal points.
        /// </summary>
        /// <returns>A 2D array of Vector3 positions representing the grid vertices.</returns>
        private Vector3[,] GenerateVertexMap()
        {
            Vector3[,] grid;

            grid = new Vector3[this.XAxisDivisionCount + 1, this.YAxisDivisionCount + 1];
            float pieceWidth = this.glassSize.x / this.XAxisDivisionCount;
            float pieceHeight = this.glassSize.y / this.YAxisDivisionCount;
            float jitterAmount = 0.4f; // Caution: Exceeding 0.5 may cause overlapping with adjacent vertices

            for (int x = 0; x <= this.XAxisDivisionCount; x++)
            {
                for (int y = 0; y <= this.YAxisDivisionCount; y++)
                {
                    // Calculate base grid point centered at origin
                    float posX = x * pieceWidth - (this.glassSize.x * 0.5f);
                    float posY = y * pieceHeight - (this.glassSize.y * 0.5f);
                    
                    Vector3 pos = new Vector3(posX, posY, 0);

                    // Apply random displacement to internal vertices only (excluding edges)
                    if (x > 0 && x < this.XAxisDivisionCount && y > 0 && y < this.YAxisDivisionCount)
                    {
                        pos.x += Random.Range(-pieceWidth * jitterAmount, pieceWidth * jitterAmount);
                        pos.y += Random.Range(-pieceHeight * jitterAmount, pieceHeight * jitterAmount);
                    }

                    grid[x, y] = pos;
                }
            }

            return grid;
        }

        /// <summary>
        /// Generates individual triangular glass fragments based on the vertex map.
        /// </summary>
        private void GeneratePiecesFromMap()
        {
            Vector3[,] grid = this.GenerateVertexMap();

            for (int x = 0; x < this.XAxisDivisionCount; x++)
            {
                for (int y = 0; y < this.YAxisDivisionCount; y++)
                {
                    // Retrieve the four vertices forming a quad from the grid
                    Vector3 v0 = grid[x, y];
                    Vector3 v1 = grid[x, y + 1];
                    Vector3 v2 = grid[x + 1, y + 1];
                    Vector3 v3 = grid[x + 1, y];

                    // Randomly choose the diagonal to split the quad into two triangles (A and B)
                    if (Random.Range(0, 2) % 2 == 0)
                    {
                        this.CreateTrianglePiece(v0, v1, v2, $"Piece_{x}_{y}_A", this.thickness);
                        this.CreateTrianglePiece(v0, v2, v3, $"Piece_{x}_{y}_B", this.thickness);
                    }
                    else
                    {
                        this.CreateTrianglePiece(v0, v1, v3, $"Piece_{x}_{y}_A", this.thickness);
                        this.CreateTrianglePiece(v1, v2, v3, $"Piece_{x}_{y}_B", this.thickness);
                    }
                }
            }
        }

        /// <summary>
        /// Toggles the editor preview between the solid glass mesh and the fractured fragment model.
        /// </summary>
        public void TogglePreview()
        {
            this.previewShattered = !this.previewShattered;

            // Switch visibility of the solid mesh
            if (this.visualMesh != null)
            {
                this.visualMesh.SetActive(!this.previewShattered);
            }

            // Switch visibility of the fractured instance
            if (this.fracturedInstance != null)
            {
                this.fracturedInstance.SetActive(this.previewShattered);
            }
            
        }

        /// <summary>
        /// Detects nearby fragments within a specific radius and activates their physics simulations.
        /// </summary>
        /// <param name="center">The center point of the impact or explosion.</param>
        /// <param name="radius">The radius within which fragments will be affected.</param>
        /// <param name="force">The velocity vector to apply to the detected fragments.</param>
        public void WakeNeighbors(Vector3 center, float radius, Vector3 force)
        {
            Debug.Log($"[FlexibleGlass] WakeNeighbors called. Center: {center}, Radius: {radius}, Force: {force}");

            // Ensure physics engine is synchronized with recent active states
            Physics.SyncTransforms();

            // Find surrounding fragments using a physical overlap sphere check
            Collider[] nearby = Physics.OverlapSphere(center, radius);
            Debug.Log($"[FlexibleGlass] OverlapSphere found {nearby.Length} colliders.");

            List<Rigidbody> activatedRigidbodies = new List<Rigidbody>();
            
            foreach (var col in nearby)
            {
                if (col.TryGetComponent<IndividualFragment>(out var fragment))
                {
                    Debug.Log($"[FlexibleGlass] OverlapSphere matched fragment: {fragment.name}");
                    // Trigger physics activation on the fragment
                    fragment.ActivatePhysics(force, fragmentLifetime);

                    // 2. 起動した破片のRigidbodyをリストに追加
                    if (fragment.TryGetComponent<Rigidbody>(out var rb))
                    {
                        activatedRigidbodies.Add(rb);
                    }
                }
            }

            // FALLBACK: If OverlapSphere returned nothing (due to same-frame latency),
            // manually search for fragments within the radius using mathematical distance.
            if (activatedRigidbodies.Count == 0 && this.fracturedInstance != null)
            {
                Debug.Log("[FlexibleGlass] OverlapSphere found no fragments. Running distance fallback...");
                IndividualFragment[] allFragments = this.fracturedInstance.GetComponentsInChildren<IndividualFragment>(true);
                Debug.Log($"[FlexibleGlass] Total fragments in container: {allFragments.Length}");
                foreach (var fragment in allFragments)
                {
                    float dist = Vector3.Distance(fragment.transform.position, center);
                    if (dist <= radius)
                    {
                        Debug.Log($"[FlexibleGlass] Distance fallback matched fragment: {fragment.name} (dist: {dist}), World Pos: {fragment.transform.position}, Local Scale: {fragment.transform.localScale}, World Scale: {fragment.transform.lossyScale}");
                        fragment.ActivatePhysics(force, this.fragmentLifetime);
                        if (fragment.TryGetComponent<Rigidbody>(out var rb))
                        {
                            activatedRigidbodies.Add(rb);
                        }
                    }
                }
            }

            Debug.Log($"[FlexibleGlass] Woke up {activatedRigidbodies.Count} fragments.");

            // Cinematic mode effects
            if (cinematicMode && activatedRigidbodies.Count > 0)
            {
                ApplyCinematicFloat(activatedRigidbodies);
            }

        }

        public float airDragDuringFloat = 2.0f; // Drag coefficient during the float phase

        /// <summary>
        /// Initiates a cinematic float effect for a list of fragment rigidbodies.
        /// </summary>
        /// <param name="fragments">List of Rigidbodies to be affected by the float logic.</param>
        public void ApplyCinematicFloat(List<Rigidbody> fragments)
        {
            this.StartCoroutine(this.FloatRoutine(fragments));
        }

        /// <summary>
        /// Coroutine that handles the transition between floating and falling for fragments.
        /// Manages gravity and damping properties over time.
        /// </summary>
        /// <param name="fragments">The fragments to manipulate during the sequence.</param>
        private IEnumerator FloatRoutine(List<Rigidbody> fragments)
        {
            // Transition to float state: disable gravity and apply high drag
            foreach (var rb in fragments)
            {
                if (rb == null) continue;
                
                rb.useGravity = false;
                
                // Apply linear damping to act as air resistance for a "soft" movement
                rb.linearDamping = this.airDragDuringFloat;
                
                // Apply slight angular damping for a more polished rotational slow-down
                rb.angularDamping = 1.0f; 
                
                // Add a subtle random spin for visual variety
                rb.AddTorque(Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
            }

            yield return new WaitForSeconds(this.floatDuration);

            // Resume natural physics: restore gravity and reset damping values
            foreach (var rb in fragments)
            {
                if (rb == null) continue;
                
                rb.useGravity = true;
                
                // Restore standard physical damping constants
                rb.linearDamping = 0.05f; 
                rb.angularDamping = 0.05f;
            }
        }

        /// <summary>
        /// Handles physical collisions, calculates damage based on impact velocity, 
        /// and triggers cracking or structural awakening.
        /// </summary>
        /// <param name="collision">The collision data from Unity's physics engine.</param>
        public void OnCollisionEnter(Collision collision)
        {
            if (!isBreakableByPhysics) return;  // Process only during physics calculation

            // Ignore physical impacts from the Player block.
            // Glass should only break when standing upright on it via TumbleController.
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<TumbleController>() != null)
            {
                return;
            }

            // Check if the colliding object is another glass fragment using CollisionProxy
            if (collision.gameObject.GetComponent<IndividualFragment>() != null)
            {
                return; // Ignore collisions between glass fragments
            }

            // Calculate the impact magnitude based on relative velocity
            float force = collision.relativeVelocity.magnitude;
            this.currentHealthPhysics -= force;

            // Cracks
            if (this.currentHealthPhysics < 1)
            {
                this.ShowCracks();
            }

            if (this.isCracked)
            {
                // 1. Calculate impact intensity for propagation
                Vector3 impactVel = collision.relativeVelocity;
                //float rippleRadius = 0.3f; // Radius for spreading the physical impact
                
                // Wake up neighboring fragments at the collision contact point
                this.WakeNeighbors(collision.contacts[0].point, calcDynamicRadius(force), impactVel);

                // Sound
                PlaySound(shatterSound, audioVolume);
            }
        }

        public void TakeDamage(Vector3 hitPoint, Vector3 direction, float force)
        {
            if (!isBreakableByRaycast) return;  // Process only during raycast calculation

            // Calculate the impact magnitude based on relative velocity
            //float force = collision.relativeVelocity.magnitude;
            this.currentHealthRaycast -= force;

            // // Cracks
            if (this.currentHealthRaycast < 1)
            {
                this.ShowCracks();
            }

            if (this.isCracked)
            {
                // 1. 弾丸の向きと力から、物理ベクトルを合成
                Vector3 impactVel = direction.normalized * force;
                //float rippleRadius = 0.3f; // Radius for spreading the physical impact

                // Wake up neighboring fragments at the collision contact point
                this.WakeNeighbors(hitPoint, calcDynamicRadius(force), impactVel);

                // Sound
                PlaySound(shatterSound, audioVolume);
            }
        }

        private float calcDynamicRadius(float force)
        {
                // 1. Calculate the dynamic radius based on force
                // Formula: Base Radius + (Force * Multiplier), capped at Max Radius
                float dynamicRadius = this.baseRippleRadius + (force * this.forceMultiplier);
                dynamicRadius = Mathf.Min(dynamicRadius, this.maxRippleRadius);
                return dynamicRadius;
        }

        /// <summary>
        /// Plays the specified audio clip with a randomized pitch to add auditory variety.
        /// </summary>
        /// <param name="clip">The AudioClip to be played.</param>
        private void PlaySound(AudioClip clip,float volume)
        {
            if (clip == null || this.audioSource == null) return;

            // Apply random pitch variation to avoid repetitive sound patterns
            this.audioSource.pitch = 1.0f + Random.Range(-this.pitchRandomness, this.pitchRandomness);

            // PlayOneShot allows multiple sounds to overlap without cutting off the previous one
            this.audioSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Switches the visual state from a solid mesh to the pre-generated fractured fragments.
        /// </summary>
        public void ShowCracks()
        {
            if (this.isCracked) return;
            
            this.isCracked = true;

            // 1. Hide the standard solid mesh
            if (this.visualMesh != null)
            {
                this.visualMesh.SetActive(false);
            }

            // 2. Enable the fractured model (fragments are assumed to be kinematic at this stage)
            if (this.fracturedInstance != null)
            {
                this.fracturedInstance.SetActive(true);

                // Unparent to allow independent physics simulation without being constrained by the parent's kinematic Rigidbody
                this.fracturedInstance.transform.SetParent(null, true);

                // Destroy the empty container shortly after the fragments are destroyed to prevent leaks
                Destroy(this.fracturedInstance, this.fragmentLifetime + 1f);
            }
            
        }

        /// <summary>
        /// Triggers the visual fracture state and plays the associated sound effect.
        /// This represents the initial cracking phase without complete structural failure.
        /// </summary>
        public void Fracture()
        {
            // Early exit if the glass is already in a cracked state
            if (this.isCracked) return;

            // Switch the visual representation to the cracked version
            this.ShowCracks();

            // Play the subtle sound of glass fracturing
            this.PlaySound(this.fractureSound, this.audioVolume);
        }
    }
}

