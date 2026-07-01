using UnityEngine;

namespace FlexibleGlassDestructor
{
    /// <summary>
    /// Controls the physics and lifecycle of an individual fractured fragment.
    /// </summary>
    public class IndividualFragment : MonoBehaviour
    {
        private Rigidbody rb; // Cached Rigidbody component
        private bool isAwake = false; // Flag to prevent multiple physics activations

        public FlexibleGlass parentGlass; // Reference to the main parent glass system.

        /// <summary>
        /// Handles collision detection and reports significant impacts to the parent system.
        /// </summary>
        /// <param name="collision">Collision data provided by Unity's physics engine.</param>
        private void OnCollisionEnter(Collision collision)
        {
            // 1. Calculate the intensity of the impact
            Vector3 impactVel = collision.relativeVelocity;
            
            // 2. Report to the parent system if the impact exceeds the threshold
            // Only notify the parent if the squared magnitude of the velocity is greater than 1.0f
            if (collision.relativeVelocity.sqrMagnitude > 1.0f)
            {
                // parentGlass.OnImpact(collision.contacts[0].point, impactVel);
                this.parentGlass.OnCollisionEnter(collision);
            }
        }

        /// <summary>
        /// Standard Unity callback for initialization.
        /// </summary>
        private void Awake()
        {
            this.rb = GetComponent<Rigidbody>();
            
            // Ensure the fragment stays fixed upon instantiation
            this.rb.isKinematic = true;
        }

        /// <summary>
        /// Activates physics on the fragment using the provided impact velocity.
        /// </summary>
        /// <param name="impactVelocity">The initial velocity vector from the impact.</param>
        /// <param name="lifetime">The duration in seconds before the fragment is destroyed.</param>
        public void ActivatePhysics(Vector3 impactVelocity, float lifetime)
        {
            // if (this.isAwake) return;
            
            // this.isAwake = true;
            // this.rb.isKinematic = false;

            // // Moderate the force and apply a maximum magnitude limit
            // //Vector3 force = Vector3.ClampMagnitude(impactVelocity * 0.2f, 15f);
            // Vector3 force = Vector3.ClampMagnitude(impactVelocity * 0.1f, 5);
            
            // // Add random dispersion
            // force += Random.insideUnitSphere * 2f;

            // this.rb.AddForce(force, ForceMode.Impulse);
            
            // // Create a tumbling effect using torque
            // this.rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            
            // // Optimization: Clean up the object after lifetime seconds
            // Destroy(gameObject, lifetime);

            if (this.isAwake) return;
            
            this.isAwake = true;
            this.rb.isKinematic = false;
            this.rb.useGravity = true;

            // 1. 空気抵抗をコードから設定（これが飛びすぎ防止に一番効きます）
            // 0.5〜2.0程度にすると、飛んだあとに自然に失速して真下に落ちます
            // this.rb.linearDrag = 1.5f; 
            // this.rb.angularDrag = 2.0f; // 回転し続けるのを防ぐ

            // 2. 力の計算
            // 衝撃をマイルドにしつつ、最低限の「重み」を感じる制限にします
            Vector3 force = Vector3.ClampMagnitude(impactVelocity * 0.1f, 5f);
            
            // 3. ランダムな飛散（少し抑えめに）
            // directionに依存しない微細なバラつきを与えて、破片同士の重なりを防ぎます
            force += Random.insideUnitSphere * 0.5f;

            this.rb.AddForce(force, ForceMode.Impulse);
            
            // 4. 回転（トルク）の調整
            // 10fだとかなり高速回転するので、2f〜5f程度に抑えると「ボトッ」と落ちる質感になります
            this.rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
            
            // 5. 消滅処理（以前提案したコルーチンによるフェードアウトを推奨しますが、シンプルにDestroyでもOK）
            Destroy(gameObject, lifetime);
        }
    }
}
