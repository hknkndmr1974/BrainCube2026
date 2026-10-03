using TMPro;
using UnityEngine;

namespace uiuxforgedev.bolduisystemDEMO
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class DamageNumber : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float lifetime = 1.2f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float bounceMultiplier = 0.25f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private TextMeshProUGUI text;

        [Space(10)]

        [Header("Damage Settings")]
        [SerializeField] private int lowDamageThreshold = 2999;
        [SerializeField] private int normalDamageThreshold = 7000;

        [Header ("Normal Damage")]
        [SerializeField] private Color lowDamageColor = Color.white;
        [SerializeField] private float lowDamageSize = 0.7f;

        [Header("High Damage")]
        [SerializeField] private Color normalDamageColor = Color.yellow;
        [SerializeField] private float normalDamageSize = 1f;


        [Header("Critical Damage")]
        [SerializeField] private Color criticalDamageColor = Color.red;
        [SerializeField] private float criticalDamageSize = 1.5f;


        private float timer;
        private Vector3 startScale;


        public void Initialize(int damageAmount)
        {
            text.text = damageAmount.ToString();

            if (damageAmount <= lowDamageThreshold)
            {
                text.color = lowDamageColor;
                transform.localScale *= lowDamageSize;
            }
            else if (damageAmount <= normalDamageThreshold)
            {
                text.color = normalDamageColor;
                transform.localScale *= normalDamageSize;
            }
            else
            {
                text.color = criticalDamageColor;
                transform.localScale *= criticalDamageSize;
            }

            startScale = transform.localScale;

            float initialAlpha = fadeCurve.Evaluate(0f);
            Color c = text.color;
            c.a = initialAlpha;
            text.color = c;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        private void Update() => Animate();

        private void Animate()
        {
            timer += Time.deltaTime;
            float t = timer / lifetime;

            float bounceValue = scaleCurve.Evaluate(t);
            transform.localScale = startScale * (1 + bounceValue * bounceMultiplier);

            transform.position += Vector3.up * moveSpeed * Time.deltaTime;

            Color c = text.color;
            c.a = fadeCurve.Evaluate(t);
            text.color = c;

            if (timer >= lifetime)
                Destroy(gameObject);
        }
    }
}
