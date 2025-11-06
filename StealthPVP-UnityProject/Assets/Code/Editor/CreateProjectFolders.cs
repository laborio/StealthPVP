using UnityEditor;
using UnityEngine;
using System.IO;

public static class CreateProjectFolders
{
    [MenuItem("Tools/Project/Scaffold Folders")]
    public static void Scaffold()
    {
        string[] paths = new[]
        {
            "Assets/_Project/_Settings",
            "Assets/_Project/_Shared/Fonts",
            "Assets/_Project/_Shared/Shaders",
            "Assets/_Project/_Shared/VFX",
            "Assets/_Project/_Shared/Materials_Library",
            "Assets/_Project/Code/Runtime/Core",
            "Assets/_Project/Code/Runtime/UI",
            "Assets/_Project/Code/Editor",
            "Assets/_Project/Code/Tests",
            "Assets/_Project/Features/Characters",
            "Assets/_Project/Features/Environments",
            "Assets/_Project/Features/Items",
            "Assets/_Project/Features/Systems",
            "Assets/_Project/Scenes/Additive",
            "Assets/_ThirdParty",
            "Assets/Plugins",
            "Assets/StreamingAssets"
        };

        foreach (var p in paths)
        {
            if (!Directory.Exists(p))
                Directory.CreateDirectory(p);
        }

        AssetDatabase.Refresh();
        Debug.Log("Project folders scaffolded.");
    }
}
