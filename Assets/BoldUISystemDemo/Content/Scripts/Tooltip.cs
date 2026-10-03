using UnityEngine;
using System.Collections;

namespace uiuxforgedev.bolduisystemDEMO
{
    [RequireComponent(typeof(CanvasGroup))]
    public class Tooltip : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform target;


        [Header("Animation")]
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);


        private CanvasGroup canvasGroup;
        private Coroutine anim;
        private bool isVisible;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (target == null) target = transform as RectTransform;
            SetInstant(false);
        }


        /// <summary>
        /// Toggles between showing and hiding the tooltip. Safe to call from code or UI Events.
        /// </summary>
        public void Toggle(bool value)
        {
            isVisible = !isVisible;
            Play(isVisible);
        }

        private void Play(bool show)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Animate(show));
        }

        private IEnumerator Animate(bool show)
        {
            isVisible = show;

            gameObject.SetActive(true);

            float time = 0f;
            float start = show ? 0f : 1f;
            float end = show ? 1f : 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = time / duration;

                float eval = Mathf.Lerp(start, end, scaleCurve.Evaluate(t));
                if (show == true) target.localScale = Vector3.one * scaleCurve.Evaluate(t);
                else target.localScale = Vector3.one * eval;
                canvasGroup.alpha = eval;

                yield return null;
            }

            target.localScale = Vector3.one * end;
            canvasGroup.alpha = end;
        }

        private void SetInstant(bool show)
        {
            isVisible = show;
            target.localScale = show ? Vector3.one : Vector3.zero;
            canvasGroup.alpha = show ? 1f : 0f;
        }
    }
}