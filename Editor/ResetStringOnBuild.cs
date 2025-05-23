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
        // Itera por todas as cenas do projeto
        foreach (var scenePath in EditorBuildSettings.scenes)
        {
            if (!scenePath.enabled) continue; // Ignora cenas desativadas no build

            var scene = EditorSceneManager.OpenScene(scenePath.path, OpenSceneMode.Single);
            NitroManager nitroManager = GameObject.FindObjectOfType<NitroManager>();

            if (nitroManager != null && nitroManager.Client)
            {
                // Salva o privateKey no EditorPrefs para garantir persistência
                backupPrivateKey = nitroManager.privateKey;
                EditorPrefs.SetString("NitroManager_PrivateKey", backupPrivateKey);

                nitroManager.privateKey = "";
                EditorUtility.SetDirty(nitroManager);

                // Salva o caminho da cena onde o NitroManager foi encontrado
                ResetStringOnBuild.scenePath = scenePath.path;
                EditorSceneManager.MarkSceneDirty(scene);
                break; // Para após encontrar o NitroManager
            }
        }
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!string.IsNullOrEmpty(scenePath))
        {
            EditorApplication.delayCall += () =>
            {
                // Reabre a cena onde o NitroManager foi encontrado
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                NitroManager nitroManager = GameObject.FindObjectOfType<NitroManager>();
                if (nitroManager != null)
                {
                    // Restaura o privateKey do EditorPrefs
                    if (EditorPrefs.HasKey("NitroManager_PrivateKey"))
                    {
                        backupPrivateKey = EditorPrefs.GetString("NitroManager_PrivateKey");
                        nitroManager.privateKey = backupPrivateKey;
                        EditorPrefs.DeleteKey("NitroManager_PrivateKey"); // Remove o backup após restaurar
                    }

                    EditorUtility.SetDirty(nitroManager);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveOpenScenes();
                }
                else
                {
                    Debug.LogError("⚠ NitroManager not found in the scene for restoration.");
                }
            };
        }
    }
}
#endif
