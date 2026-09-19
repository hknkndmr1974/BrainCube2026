using UnityEngine;

public class TiltWindow : MonoBehaviour
{
	public Vector2 range = new Vector2(5f, 3f);

	Transform mTrans;
	Quaternion mStart;
	Vector2 mRot = Vector2.zero;

	void Start ()
	{
		mTrans = transform;
		mStart = mTrans.localRotation;
	}

	void Update ()
	{
		Vector3 pos = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
		if (UnityEngine.InputSystem.Mouse.current != null)
		{
			pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
		}
		else if (UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
		{
			pos = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
		}
#else
		pos = Input.mousePosition;
#endif

		float halfWidth = Screen.width * 0.5f;
		float halfHeight = Screen.height * 0.5f;
		float x = Mathf.Clamp((pos.x - halfWidth) / halfWidth, -1f, 1f);
		float y = Mathf.Clamp((pos.y - halfHeight) / halfHeight, -1f, 1f);
		mRot = Vector2.Lerp(mRot, new Vector2(x, y), Time.deltaTime * 5f);

		mTrans.localRotation = mStart * Quaternion.Euler(-mRot.y * range.y, mRot.x * range.x, 0f);
	}
}
