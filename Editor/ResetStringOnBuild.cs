#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using NitroNetwork.Core;

public class ResetStringOnBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;
    private static string backupPrivateKey;
    private static string scenePath;

    public void OnPreprocessBuild(BuildReport report)
    {
        NitroManager nitroManager = GameObject.FindFirstObjectByType<NitroManager>();
        if (nitroManager != null && nitroManager.Client && !nitroManager.Server)
        {
            backupPrivateKey = nitroManager.privateKey;
            nitroManager.privateKey = "";
            EditorUtility.SetDirty(nitroManager);

            // Salva o caminho da cena atual
            scenePath = nitroManager.gameObject.scene.path;
            EditorSceneManager.MarkSceneDirty(nitroManager.gameObject.scene);

            Debug.Log("privateKey removido antes do build.");
        }
    }

  public void OnPostprocessBuild(BuildReport report)
{
    if (!string.IsNullOrEmpty(backupPrivateKey) && !string.IsNullOrEmpty(scenePath))
    {
        EditorApplication.delayCall += () =>
        {
            // Reabre explicitamente a cena original
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            NitroManager nitroManager = GameObject.FindFirstObjectByType<NitroManager>();
            if (nitroManager != null)
            {
                nitroManager.privateKey = backupPrivateKey;

                EditorUtility.SetDirty(nitroManager);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveOpenScenes();

                Debug.Log("✅ privateKey restaurado e salvo com sucesso.");
            }
            else
            {
                Debug.LogWarning("⚠ NitroManager não encontrado na cena para restauração.");
            }

            // (Opcional) Recarrega a cena anterior do editor, se quiser restaurar o estado visual
            // EditorSceneManager.OpenScene("Assets/SuaCenaDeTrabalho.unity", OpenSceneMode.Single);
        };
    }
}
}
#endif
