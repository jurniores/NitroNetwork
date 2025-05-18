#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NitroNetwork.Core.NitroManager))]
public class NitroManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        // Só mostra o botão se apenas um objeto estiver selecionado
        if (!serializedObject.isEditingMultipleObjects)
        {
            NitroNetwork.Core.NitroManager manager = (NitroNetwork.Core.NitroManager)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Gerar novas chaves"))
            {
                NitroCriptografy.GenerateKeys(out manager.publicKey, out manager.privateKey);
                EditorUtility.SetDirty(manager);
                Debug.Log("Novas chaves geradas!");
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Multi-object editing not supported para geração de chaves.", MessageType.Info);
        }
    }
}
#endif