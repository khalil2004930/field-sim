#if UNITY_EDITOR
using FieldSim.Unity.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FieldSim.Unity.Editor
{
    public static class EnvironmentPrototypeMenu
    {
        [MenuItem("FieldSim/Prototype/Create Mountain Environment Scene")]
        public static void CreatePrototypeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject bootstrap = new GameObject("FieldSim Environment Prototype");
            bootstrap.AddComponent<EnvironmentPrototypeBuilder>();

            const string folder = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            string path = folder + "/EnvironmentPrototype.unity";
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = bootstrap;
            Debug.Log("Created " + path + ". Press Play to build the synthetic mountainous environment.");
        }
    }
}
#endif
