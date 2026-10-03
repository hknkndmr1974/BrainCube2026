using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace uiuxforgedev.bolduisystemDEMO
{
    public class TabSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            public Button tabButton;
            public GameObject contentPanel;
            public GameObject activeTab;
            public GameObject inactiveTab;

            [HideInInspector] public Coroutine anim;
        }

        [Header("Tab Configuration")]
        [SerializeField] private List<Tab> tabs = new List<Tab>();

        [Header("Settings")]
        [SerializeField] private int defaultTabIndex = 0;

        private int currentTabIndex = -1;


        private void Start()
        {
            InitializeTabs();
        }


        private void InitializeTabs()
        {
            if (tabs == null || tabs.Count == 0)
            {
                Debug.LogWarning("[TabSwitcher] No tabs assigned.", this);
                return;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                Tab tab = tabs[i];

                if (tab.tabButton != null)
                    tab.tabButton.onClick.AddListener(() => SwitchToTab(index));
                else
                    Debug.LogWarning($"[TabSwitcher] Tab at index {i} has no Button assigned.", this);
            }

            int startIndex = Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1);
            SwitchToTab(startIndex);
        }


        /// <summary>
        /// Switches to the tab at the given index. Safe to call from code or UI Events.
        /// </summary>
        public void SwitchToTab(int index)
        {
            if (index < 0 || index >= tabs.Count)
            {
                Debug.LogWarning($"[TabSwitcher] Tab index {index} is out of range.", this);
                return;
            }

            if (index == currentTabIndex) return;

            for (int i = 0; i < tabs.Count; i++)
            {
                bool isActive = (i == index);
                SetTabState(tabs[i], isActive);
            }

            currentTabIndex = index;
        }


        private void SetTabState(Tab tab, bool isActive)
        {
            if (tab.contentPanel != null)
                tab.contentPanel.SetActive(isActive);

            if (tab.tabButton != null)
            {
                if (tab.activeTab != null) tab.activeTab.SetActive(isActive);
                if (tab.inactiveTab != null) tab.inactiveTab.SetActive(!isActive);
            }

            if (tab.tabButton != null)
                Play(isActive, tab.tabButton.gameObject, ref tab.anim);
        }


        [Header("Animation")]
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);


        private void Play(bool show, GameObject obj, ref Coroutine anim)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Animate(show, obj));
        }

        private IEnumerator Animate(bool show, GameObject obj)
        {
            gameObject.SetActive(true);

            float time = 0f;
            Vector3 start = obj.transform.localScale;
            Vector3 end = show ? Vector3.one * 1.15f : Vector3.one;

            while (time < duration)
            {
                time += Time.deltaTime;
                float t = scaleCurve.Evaluate(time / duration);
                obj.transform.localScale = Vector3.Lerp(start, end, t);
                yield return null;
            }

            obj.transform.localScale = end;
        }
    }
}