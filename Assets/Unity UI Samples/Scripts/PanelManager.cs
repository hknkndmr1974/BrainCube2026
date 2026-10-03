using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class PanelManager : MonoBehaviour
{
	public Animator initiallyOpen;

	private int m_OpenParameterId;
	private Animator m_Open;
	private GameObject m_PreviouslySelected;
	private Coroutine m_TransitionRoutine;

	const string k_OpenTransitionName = "Open";
	const string k_ClosedStateName = "Closed";

	public void OnEnable()
	{
		m_OpenParameterId = Animator.StringToHash(k_OpenTransitionName);

		if (initiallyOpen == null)
			return;

		OpenPanel(initiallyOpen);
	}

	public void OpenPanel(Animator anim)
	{
		if (anim == null || m_Open == anim)
			return;

		if (m_TransitionRoutine != null)
			StopCoroutine(m_TransitionRoutine);

		m_TransitionRoutine = StartCoroutine(OpenPanelRoutine(anim));
	}

	IEnumerator OpenPanelRoutine(Animator anim)
	{
		GameObject newPreviouslySelected = EventSystem.current != null
			? EventSystem.current.currentSelectedGameObject
			: null;

		// Wait for the current panel to finish closing before showing the next one.
		Animator closing = m_Open;
		if (closing != null)
		{
			AudioManager.Instance?.PlayUiEvent(AudioEventId.MenuTransition);
			closing.SetBool(m_OpenParameterId, false);
			SetSelected(m_PreviouslySelected);
			m_Open = null;

			yield return WaitForClosedState(closing);

			if (closing != null)
				closing.gameObject.SetActive(false);
		}

		// Start the next panel from its Closed pose, then open it.
		anim.gameObject.SetActive(true);
		anim.transform.SetAsLastSibling();
		anim.SetBool(m_OpenParameterId, false);
		anim.Play(k_ClosedStateName, 0, 0f);
		anim.Update(0f);

		yield return null;

		m_PreviouslySelected = newPreviouslySelected;
		m_Open = anim;
		m_Open.SetBool(m_OpenParameterId, true);

		GameObject go = FindFirstEnabledSelectable(anim.gameObject);
		SetSelected(go);

		m_TransitionRoutine = null;
	}

	static GameObject FindFirstEnabledSelectable(GameObject gameObject)
	{
		GameObject go = null;
		var selectables = gameObject.GetComponentsInChildren<Selectable>(true);
		foreach (var selectable in selectables)
		{
			if (selectable.IsActive() && selectable.IsInteractable())
			{
				go = selectable.gameObject;
				break;
			}
		}
		return go;
	}

	public void CloseCurrent()
	{
		if (m_Open == null)
			return;

		if (m_TransitionRoutine != null)
		{
			StopCoroutine(m_TransitionRoutine);
			m_TransitionRoutine = null;
		}

		Animator closing = m_Open;
		m_TransitionRoutine = StartCoroutine(CloseCurrentRoutine(closing));
	}

	IEnumerator CloseCurrentRoutine(Animator closing)
	{
		if (closing == null)
		{
			m_TransitionRoutine = null;
			yield break;
		}

		AudioManager.Instance?.PlayUiEvent(AudioEventId.MenuTransition);
		closing.SetBool(m_OpenParameterId, false);
		SetSelected(m_PreviouslySelected);
		if (m_Open == closing)
			m_Open = null;

		yield return WaitForClosedState(closing);

		if (closing != null)
			closing.gameObject.SetActive(false);

		m_TransitionRoutine = null;
	}

	IEnumerator WaitForClosedState(Animator anim)
	{
		if (anim == null)
			yield break;

		// Let the Close transition begin.
		yield return null;

		float timeout = 2f;
		float elapsed = 0f;
		while (anim != null && anim.isActiveAndEnabled && elapsed < timeout)
		{
			if (!anim.IsInTransition(0) &&
			    anim.GetCurrentAnimatorStateInfo(0).IsName(k_ClosedStateName))
			{
				yield break;
			}

			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}
	}

	private void SetSelected(GameObject go)
	{
		if (EventSystem.current != null)
			EventSystem.current.SetSelectedGameObject(go);
	}
}
