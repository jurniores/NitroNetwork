#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NitroNetwork.Core;
[CustomEditor(typeof(NitroManager))]
public class NitroManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        // Só mostra o botão se apenas um objeto estiver selecionado
        if (!serializedObject.isEditingMultipleObjects)
        {
            NitroManager manager = (NitroManager)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Generate new keys"))
            {
                NitroCriptografyRSA.GenerateKeys(out manager.publicKey, out manager.privateKey);
                EditorUtility.SetDirty(manager);
                Debug.Log("New keys generated!");
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Multi-object editing not supported para geração de chaves.", MessageType.Info);
        }
    }
}
#endif