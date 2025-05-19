#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NitroNetwork.Core;

/// <summary>
/// Custom editor for the NitroManager component.
/// Adds a button to the Inspector to generate new RSA keys directly from the Unity Editor,
/// facilitating key management during development and testing.
/// </summary>
[CustomEditor(typeof(NitroManager))]
public class NitroManagerEditor : Editor
{
    /// <summary>
    /// Overrides the default Inspector GUI to add custom controls.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Draws the default inspector fields for NitroManager
        DrawDefaultInspector();

        // Only show the button if a single object is selected
        if (!serializedObject.isEditingMultipleObjects)
        {
            NitroManager manager = (NitroManager)target;

            GUILayout.Space(10);
            // Button to generate new RSA keys for NitroManager
            if (GUILayout.Button("Generate new keys"))
            {
                NitroCriptografyRSA.GenerateKeys(out manager.publicKey, out manager.privateKey);
                EditorUtility.SetDirty(manager);
                Debug.Log("New keys generated!");
            }
        }
        else
        {
            // Show a help box if multiple objects are selected
            EditorGUILayout.HelpBox("Multi-object editing not supported for key generation.", MessageType.Info);
        }
    }
}
#endif