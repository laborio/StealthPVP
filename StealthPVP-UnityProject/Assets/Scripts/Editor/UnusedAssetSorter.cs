using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class UnusedAssetSorter : EditorWindow
{
    private string sourceFolderPath = "Assets/";
    private string unusedFolderPath = "Assets/Unused/";
    private bool includeSubfolders = true;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Unused Asset Sorter")]
    public static void ShowWindow()
    {
        GetWindow<UnusedAssetSorter>("Unused Asset Sorter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unused Asset Sorter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool finds assets in the source folder that are not used in the current scene " +
            "and moves them to organized subfolders (Prefabs, Textures, Meshes, etc.)",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // Source folder selection
        EditorGUILayout.LabelField("Source Folder:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        sourceFolderPath = EditorGUILayout.TextField(sourceFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Source Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                sourceFolderPath = FileUtil.GetProjectRelativePath(selectedPath);
            }
        }
        EditorGUILayout.EndHorizontal();

        // Unused folder destination
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Unused Assets Destination:", EditorStyles.boldLabel);
        unusedFolderPath = EditorGUILayout.TextField(unusedFolderPath);

        EditorGUILayout.Space();
        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);

        EditorGUILayout.Space();

        if (GUILayout.Button("Sort Unused Assets", GUILayout.Height(40)))
        {
            SortUnusedAssets();
        }
    }

    private void SortUnusedAssets()
    {
        if (string.IsNullOrEmpty(sourceFolderPath) || !AssetDatabase.IsValidFolder(sourceFolderPath))
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid source folder.", "OK");
            return;
        }

        if (!EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorUtility.DisplayDialog("Error", "Please open a scene first.", "OK");
            return;
        }

        // Get all assets in source folder
        string[] allAssetPaths = GetAllAssetsInFolder(sourceFolderPath, includeSubfolders);
        
        // Get all assets used in the current scene
        HashSet<string> usedAssets = GetUsedAssetsInScene();

        // Find unused assets
        List<string> unusedAssets = new List<string>();
        foreach (string assetPath in allAssetPaths)
        {
            if (!usedAssets.Contains(assetPath))
            {
                unusedAssets.Add(assetPath);
            }
        }

        if (unusedAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("Complete", "No unused assets found in the selected folder.", "OK");
            return;
        }

        // Confirm with user
        bool confirm = EditorUtility.DisplayDialog(
            "Confirm Move",
            $"Found {unusedAssets.Count} unused assets. Move them to '{unusedFolderPath}'?",
            "Yes",
            "Cancel"
        );

        if (!confirm)
            return;

        // Create unused folder structure
        CreateUnusedFolderStructure();

        // Move assets
        int movedCount = 0;
        foreach (string assetPath in unusedAssets)
        {
            string assetType = GetAssetTypeFolder(assetPath);
            string destinationFolder = Path.Combine(unusedFolderPath, assetType).Replace("\\", "/");
            
            // Ensure destination folder exists
            if (!AssetDatabase.IsValidFolder(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
                AssetDatabase.Refresh();
            }

            string fileName = Path.GetFileName(assetPath);
            string destinationPath = Path.Combine(destinationFolder, fileName).Replace("\\", "/");

            // Handle duplicates
            destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);

            string error = AssetDatabase.MoveAsset(assetPath, destinationPath);
            if (string.IsNullOrEmpty(error))
            {
                movedCount++;
            }
            else
            {
                Debug.LogError($"Failed to move {assetPath}: {error}");
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Complete",
            $"Successfully moved {movedCount} unused assets to '{unusedFolderPath}'",
            "OK"
        );
    }

    private string[] GetAllAssetsInFolder(string folderPath, bool includeSubfolders)
    {
        SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
        List<string> assetPaths = new List<string>();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            
            // Skip folders and scripts in Editor folder
            if (AssetDatabase.IsValidFolder(assetPath))
                continue;
            
            // Only include if it's in the right directory level
            if (!includeSubfolders && Path.GetDirectoryName(assetPath).Replace("\\", "/") != folderPath.TrimEnd('/'))
                continue;

            assetPaths.Add(assetPath);
        }

        return assetPaths.ToArray();
    }

    private HashSet<string> GetUsedAssetsInScene()
    {
        HashSet<string> usedAssets = new HashSet<string>();

        // Get all game objects in the scene
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(obj => !EditorUtility.IsPersistent(obj.transform.root.gameObject) && 
                         !(obj.hideFlags == HideFlags.NotEditable || obj.hideFlags == HideFlags.HideAndDontSave))
            .ToArray();

        foreach (GameObject go in allObjects)
        {
            // Check if it's a prefab instance
            if (PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.NotAPrefab)
            {
                GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabRoot != null)
                {
                    string prefabPath = AssetDatabase.GetAssetPath(prefabRoot);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        usedAssets.Add(prefabPath);
                    }
                }
            }

            // Check all components
            Component[] components = go.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                SerializedObject so = new SerializedObject(comp);
                SerializedProperty sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference && sp.objectReferenceValue != null)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(sp.objectReferenceValue);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            usedAssets.Add(assetPath);
                            
                            // Also check for sub-assets (like materials in meshes)
                            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                            foreach (Object subAsset in subAssets)
                            {
                                string subPath = AssetDatabase.GetAssetPath(subAsset);
                                if (!string.IsNullOrEmpty(subPath))
                                {
                                    usedAssets.Add(subPath);
                                }
                            }
                        }
                    }
                }
            }
        }

        return usedAssets;
    }

    private void CreateUnusedFolderStructure()
    {
        // Create main unused folder
        if (!AssetDatabase.IsValidFolder(unusedFolderPath))
        {
            string parentFolder = Path.GetDirectoryName(unusedFolderPath).Replace("\\", "/");
            string folderName = Path.GetFileName(unusedFolderPath);
            AssetDatabase.CreateFolder(parentFolder, folderName);
        }
    }

    private string GetAssetTypeFolder(string assetPath)
    {
        Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        
        if (asset is GameObject)
        {
            // Check if it's a prefab
            if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.NotAPrefab)
                return "Prefabs";
            return "GameObjects";
        }
        else if (asset is Texture2D || asset is Texture)
            return "Textures";
        else if (asset is Mesh)
            return "Meshes";
        else if (asset is Material)
            return "Materials";
        else if (asset is AudioClip)
            return "Audio";
        else if (asset is AnimationClip)
            return "Animations";
        else if (asset is PhysicsMaterial || asset is PhysicsMaterial2D)
            return "PhysicMaterials";
        else if (asset is Shader)
            return "Shaders";
        else if (asset is ScriptableObject)
            return "ScriptableObjects";
        else if (asset is Sprite)
            return "Sprites";
        else if (asset is Font)
            return "Fonts";
        else if (assetPath.EndsWith(".cs"))
            return "Scripts";
        else if (assetPath.EndsWith(".asset"))
            return "Assets";
        else
            return "Other";
    }
}