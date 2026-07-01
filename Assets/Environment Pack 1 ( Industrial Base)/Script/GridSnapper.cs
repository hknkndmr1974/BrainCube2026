using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EnvironmemtModularPack
{
    [ExecuteAlways]
    public class GridSnapper : MonoBehaviour
    {
        [Header("Grid Settings")]
        public float gridSize = 1f;
        public Vector3 offset = Vector3.zero;

        [Header("Binding settings")]
        [Tooltip("Snap while moving")]
        public bool snapWhileMoving = true;

        [Tooltip("Snap at the start of the scene")]
        public bool snapOnStart = false;

        [Tooltip("Delete the script when starting the game (saving resources)")]
        public bool destroyOnPlay = true;

        [Header("Rotation settings")]
        [Tooltip("Rotation pitch in degrees (usually 45 or 90)")]
        public float rotationStep = 90f;

        [Tooltip("Rotate clockwise (true) or counterclockwise (false)")]
        public bool rotateClockwise = true;

        private Vector3 lastPosition;

        void Start()
        {
            if (snapOnStart)
            {
                SnapToGrid();
            }

            if (destroyOnPlay && Application.isPlaying)
            {
                Destroy(this);
            }
        }

        void Update()
        {
            if (!Application.isPlaying && snapWhileMoving)
            {
                if (transform.position != lastPosition)
                {
                    SnapToGrid();
                    lastPosition = transform.position;
                }
            }
        }


        public void SnapToGrid()
        {
            Vector3 snapped = GetSnappedPosition(transform.position);
            transform.position = snapped;
        }

        public Vector3 GetSnappedPosition(Vector3 position)
        {
            Vector3 p = position - offset;

            p.x = Mathf.Round(p.x / gridSize) * gridSize;
            p.y = Mathf.Round(p.y / gridSize) * gridSize;
            p.z = Mathf.Round(p.z / gridSize) * gridSize;

            return p + offset;
        }

        public void RotateStep()
        {
            float angle = rotateClockwise ? -rotationStep : rotationStep;
            transform.Rotate(0, angle, 0, Space.World);

            if (snapWhileMoving)
            {
                SnapToGrid();
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!enabled) return;

            Vector3 snapPoint = GetSnappedPosition(transform.position);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(snapPoint, gridSize * 0.15f);

            if (Vector3.Distance(transform.position, snapPoint) > 0.01f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, snapPoint);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    Vector3 point = snapPoint + new Vector3(x * gridSize, 0, z * gridSize);
                    Gizmos.DrawWireCube(point, new Vector3(gridSize * 0.9f, 0.05f, gridSize * 0.9f));
                }
            }
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(GridSnapper))]
    public class GridSnapperEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        
            GridSnapper snapper = (GridSnapper)target;
        
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Fast action", EditorStyles.boldLabel);
        
            EditorGUILayout.BeginHorizontal();
        
            if (GUILayout.Button("↺ Rotate by " + snapper.rotationStep + "°", GUILayout.Height(30)))
            {
                Undo.RecordObject(snapper.transform, "Rotate Object");
                snapper.RotateStep();
                EditorUtility.SetDirty(snapper.transform);
            }
        
            EditorGUILayout.EndHorizontal();
        
        
        }
    }
    #endif
}