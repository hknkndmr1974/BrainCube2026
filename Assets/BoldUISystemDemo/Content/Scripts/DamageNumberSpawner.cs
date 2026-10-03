using UnityEngine;
using UnityEngine.UI;

namespace uiuxforgedev.bolduisystemDEMO
{
    public class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] private DamageNumber prefab;


        /// <summary>
        /// Spawn a single damage number. Safe to call from code or UI Events.
        /// </summary>
        public void Spawn()
        {
            DamageNumber instance = Instantiate(prefab, transform.position, Quaternion.identity, transform);

            int value = Random.Range(1000, 10001);
            instance.Initialize(value);
        }
    }
}
