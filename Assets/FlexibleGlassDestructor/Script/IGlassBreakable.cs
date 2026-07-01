using UnityEngine;

namespace FlexibleGlassDestructor
{
    /// <summary>
    /// A common interface for objects that can be fractured or broken.
    /// Provides a standardized way to trigger impacts from external sources like Raycasts.
    /// </summary>
    public interface IGlassBreakable
    {
        /// <summary>
        /// Processes damage from an external source, updates health, and triggers fractures or physics.
        /// </summary>
        /// <param name="hitPoint">The exact world position where the impact occurred.</param>
        /// <param name="direction">The vector representing the direction of the impact force.</param>
        /// <param name="force">The magnitude of the impact to be applied.</param>
        public void TakeDamage(Vector3 hitPoint, Vector3 direction, float force);

        /// <summary>
        /// Creates localized cracks or minor fractures without necessarily causing complete destruction.
        /// Can be used as a precursor to total shattering.
        /// </summary>
        public void Fracture();

        /// <summary>
        /// Gets a value indicating whether the glass has reached its cracked state.
        /// Used by external systems to check the current structural integrity.
        /// </summary>
        //public bool IsCracked { get; }
    }
}

