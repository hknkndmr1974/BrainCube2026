using UnityEditor;
using UnityEngine;

namespace FlexibleGlassDestructor
{
    /// <summary>
    /// Provides a custom inspector and scene view handles for the GlassSystem component.
    /// Enables intuitive resizing and quick access to generation tools.
    /// </summary>
    [CustomEditor(typeof(FlexibleGlass))]
    public class FlexibleGlassEditor: Editor
    {
        /// <summary>
        /// Draws interactive handles in the Scene view for resizing the glass dimensions.
        /// </summary>
        private void OnSceneGUI()
        {
            // Retrieve the target GlassSystem component
            FlexibleGlass glass = (FlexibleGlass)this.target;

            // --- Width Adjustment Handle (Red) ---
            Handles.color = Color.red;

            // Calculate the world position of the right-center edge
            Vector3 rightPos = glass.transform.TransformPoint(new Vector3(glass.glassSize.x / 2, 0, 0));

            EditorGUI.BeginChangeCheck();
            
            // Render a slider handle along the transform's right axis
            Vector3 newRightPos = Handles.Slider(
                rightPos, 
                glass.transform.right,   // Movement direction (Right)
                0.3f,
                Handles.ArrowHandleCap,
                0.1f
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(glass, "Resize Glass Width");
                
                // Convert back to local space and recalculate the full width
                float newWidth = glass.transform.InverseTransformPoint(newRightPos).x * 2f;
                glass.glassSize.x = Mathf.Max(0.1f, newWidth); // Enforce minimum size
                glass.UpdateGlass();
            }

            // --- Height Adjustment Handle (Green) ---
            Handles.color = Color.green;
            Vector3 topPos = glass.transform.TransformPoint(new Vector3(0, glass.glassSize.y / 2, 0));

            EditorGUI.BeginChangeCheck();
            
            // Render a slider handle along the transform's up axis
            Vector3 newTopPos = Handles.Slider(
                topPos, 
                glass.transform.up,      // Movement direction (Up)
                0.3f,
                Handles.ArrowHandleCap, 
                0.1f
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(glass, "Resize Glass Height");
                
                // Convert back to local space and recalculate the full height
                float newHeight = glass.transform.InverseTransformPoint(newTopPos).y * 2f;
                glass.glassSize.y = Mathf.Max(0.1f, newHeight);
                glass.UpdateGlass();
            }
        }

        /// <summary>
        /// Customizes the Inspector window with utility buttons and organized headers.
        /// </summary>
        public override void OnInspectorGUI()
        {
            // Display default script properties
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);

            FlexibleGlass glass = (FlexibleGlass)this.target;

            // Arrange buttons horizontally for a cleaner layout
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Fractured"))
            {
                glass.CreateFracturedBodyInScene();
            }

            // Preview toggle button with distinct background color
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Toggle Preview"))
            {
                glass.TogglePreview();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }
}

