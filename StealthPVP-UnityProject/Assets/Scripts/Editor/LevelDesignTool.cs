using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[System.Serializable]
public class PrefabData
{
    public GameObject prefab;
    public bool useCustomSize = false;
    public float customSizeX = 1f;
    public float customSizeZ = 1f;
}

public class LevelDesignTool : EditorWindow
{
    // Prefab lists for each category
    public List<PrefabData> wallPrefabs = new List<PrefabData>();
    public List<PrefabData> buildingPrefabs = new List<PrefabData>();
    public List<PrefabData> propPrefabs = new List<PrefabData>();
    public List<PrefabData> otherPrefabs = new List<PrefabData>();
    public List<PrefabData> gameplayPrefabs = new List<PrefabData>();

    // Current state
    private enum EditorMode { SelectMode, NewSection, EditSection }
    private EditorMode currentMode = EditorMode.SelectMode;

    private enum Category { None, Walls, Buildings, Props, Other, Gameplay, TerrainPaint, TerrainHeight }
    private Category currentCategory = Category.None;

    private GameObject currentSection;
    private string newSectionName = "New Section";
    private int selectedPrefabIndex = -1;
    private GameObject previewObject;
    private float currentRotation = 0f;
    private float currentYOffset = 0f;
    private float currentScale = 1f;
    private const float GRID_SIZE = 0.5f;
    
    // Terrain painting
    private Terrain activeTerrain;
    private int selectedTerrainLayer = -1;
    private float terrainPaintOpacity = 1f;
    private bool isPainting = false;
    private Vector3 paintStartPosition;
    private Vector3 paintCurrentPosition;
    
    // Terrain height
    private float targetTerrainHeight = 10f;
    
    // Undo data
    private float[,,] undoAlphamap;
    private int undoAlphamapX;
    private int undoAlphamapZ;
    private float[,] undoHeightmap;
    private int undoHeightmapX;
    private int undoHeightmapZ;

    private Vector2 scrollPosition;
    private Vector2 windowScrollPosition; // For main window scroll
    private GameObject sectionsParent;

    [MenuItem("Tools/Level Design Tool")]
    public static void ShowWindow()
    {
        LevelDesignTool window = GetWindow<LevelDesignTool>("Level Design");
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        FindOrCreateSectionsParent();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearPreview();
    }

    void OnGUI()
    {
        windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
        
        GUILayout.Label("Level Design Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Mode Selection
        if (currentMode == EditorMode.SelectMode)
        {
            DrawModeSelection();
        }
        else if (currentMode == EditorMode.NewSection)
        {
            DrawNewSectionUI();
        }
        else if (currentMode == EditorMode.EditSection)
        {
            DrawEditSectionUI();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        // Prefab Lists Configuration
        DrawPrefabLists();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();
        
        // Reorganization Tool
        DrawReorganizationTool();
        
        EditorGUILayout.EndScrollView();
    }

    void DrawModeSelection()
    {
        GUILayout.Label("Select Mode:", EditorStyles.boldLabel);
        
        if (GUILayout.Button("New Section", GUILayout.Height(40)))
        {
            currentMode = EditorMode.NewSection;
            newSectionName = "Section_" + (sectionsParent != null ? sectionsParent.transform.childCount + 1 : 1);
        }

        if (GUILayout.Button("Edit Existing Section", GUILayout.Height(40)))
        {
            currentMode = EditorMode.EditSection;
        }
    }

    void DrawNewSectionUI()
    {
        GUILayout.Label("New Section", EditorStyles.boldLabel);
        
        newSectionName = EditorGUILayout.TextField("Section Name:", newSectionName);

        if (GUILayout.Button("Create Section", GUILayout.Height(30)))
        {
            CreateNewSection();
        }

        if (currentSection != null)
        {
            EditorGUILayout.HelpBox("Section created: " + currentSection.name, MessageType.Info);
            DrawCategorySelection();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Back", GUILayout.Height(25)))
        {
            ResetToModeSelection();
        }
    }

    void DrawEditSectionUI()
    {
        GUILayout.Label("Edit Existing Section", EditorStyles.boldLabel);

        if (sectionsParent != null && sectionsParent.transform.childCount > 0)
        {
            EditorGUILayout.LabelField("Select a section to edit:");
            
            for (int i = 0; i < sectionsParent.transform.childCount; i++)
            {
                GameObject section = sectionsParent.transform.GetChild(i).gameObject;
                if (GUILayout.Button(section.name, GUILayout.Height(30)))
                {
                    currentSection = section;
                    Selection.activeGameObject = section;
                }
            }

            if (currentSection != null)
            {
                EditorGUILayout.HelpBox("Editing: " + currentSection.name, MessageType.Info);
                DrawCategorySelection();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No sections found. Create a new section first.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Back", GUILayout.Height(25)))
        {
            ResetToModeSelection();
        }
    }

    void DrawCategorySelection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Select Category:", EditorStyles.boldLabel);

        if (GUILayout.Button("Walls", GUILayout.Height(35)))
        {
            SetCategory(Category.Walls);
        }
        if (GUILayout.Button("Buildings", GUILayout.Height(35)))
        {
            SetCategory(Category.Buildings);
        }
        if (GUILayout.Button("Props", GUILayout.Height(35)))
        {
            SetCategory(Category.Props);
        }
        if (GUILayout.Button("Other", GUILayout.Height(35)))
        {
            SetCategory(Category.Other);
        }
        if (GUILayout.Button("Gameplay", GUILayout.Height(35)))
        {
            SetCategory(Category.Gameplay);
        }
        if (GUILayout.Button("Terrain Paint", GUILayout.Height(35)))
        {
            SetCategory(Category.TerrainPaint);
        }
        if (GUILayout.Button("Terrain Height", GUILayout.Height(35)))
        {
            SetCategory(Category.TerrainHeight);
        }

        if (currentCategory != Category.None)
        {
            if (currentCategory == Category.TerrainPaint)
            {
                DrawTerrainPaintSelection();
            }
            else if (currentCategory == Category.TerrainHeight)
            {
                DrawTerrainHeightSelection();
            }
            else
            {
                DrawPrefabSelection();
            }
        }
    }

    void DrawTerrainPaintSelection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Terrain Paint", EditorStyles.boldLabel);

        // Terrain selection
        activeTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain:", activeTerrain, typeof(Terrain), true);

        if (activeTerrain == null)
        {
            EditorGUILayout.HelpBox("Please assign a Terrain to paint on.", MessageType.Warning);
            return;
        }

        TerrainData terrainData = activeTerrain.terrainData;
        if (terrainData == null || terrainData.terrainLayers.Length == 0)
        {
            EditorGUILayout.HelpBox("Terrain has no layers. Add terrain layers first.", MessageType.Warning);
            return;
        }

        // Opacity slider
        EditorGUILayout.Space();
        terrainPaintOpacity = EditorGUILayout.Slider("Opacity:", terrainPaintOpacity, 0f, 1f);

        EditorGUILayout.Space();
        GUILayout.Label("Select Layer:", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

        for (int i = 0; i < terrainData.terrainLayers.Length; i++)
        {
            TerrainLayer layer = terrainData.terrainLayers[i];
            if (layer == null) continue;

            bool isSelected = (selectedTerrainLayer == i);
            GUI.backgroundColor = isSelected ? Color.green : Color.white;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);

            // Draw layer texture preview
            if (layer.diffuseTexture != null)
            {
                GUILayout.Box(layer.diffuseTexture, GUILayout.Width(60), GUILayout.Height(60));
            }
            else
            {
                GUILayout.Box("No\nTexture", GUILayout.Width(60), GUILayout.Height(60));
            }

            if (GUILayout.Button(layer.name, GUILayout.Height(60), GUILayout.ExpandWidth(true)))
            {
                selectedTerrainLayer = i;
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();

        if (selectedTerrainLayer >= 0)
        {
            EditorGUILayout.HelpBox("Selected: " + terrainData.terrainLayers[selectedTerrainLayer].name +
                $"\nOpacity: {terrainPaintOpacity:P0}" +
                "\nClick & Drag: Paint rectangular area\n" +
                "ESC: Cancel", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Revert Last Paint", GUILayout.Height(25)))
            {
                RevertTerrainPaint();
            }
            if (GUILayout.Button("Clear Selection", GUILayout.Height(25)))
            {
                selectedTerrainLayer = -1;
                isPainting = false;
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawTerrainHeightSelection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Terrain Height", EditorStyles.boldLabel);

        // Terrain selection
        activeTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain:", activeTerrain, typeof(Terrain), true);

        if (activeTerrain == null)
        {
            EditorGUILayout.HelpBox("Please assign a Terrain to set height on.", MessageType.Warning);
            return;
        }

        TerrainData terrainData = activeTerrain.terrainData;
        if (terrainData == null)
        {
            EditorGUILayout.HelpBox("Terrain has no data.", MessageType.Warning);
            return;
        }

        // Terrain max height info and adjustment
        EditorGUILayout.Space();
        float maxHeight = terrainData.size.y;
        EditorGUILayout.LabelField("Terrain Max Height:", maxHeight.ToString("F1"));
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Max Height to 50", GUILayout.Height(25)))
        {
            Vector3 size = terrainData.size;
            terrainData.size = new Vector3(size.x, 50f, size.z);
            maxHeight = 50f;
        }
        if (GUILayout.Button("Set Max Height to 100", GUILayout.Height(25)))
        {
            Vector3 size = terrainData.size;
            terrainData.size = new Vector3(size.x, 100f, size.z);
            maxHeight = 100f;
        }
        if (GUILayout.Button("Set Max Height to 200", GUILayout.Height(25)))
        {
            Vector3 size = terrainData.size;
            terrainData.size = new Vector3(size.x, 200f, size.z);
            maxHeight = 200f;
        }
        EditorGUILayout.EndHorizontal();

        // Height slider
        EditorGUILayout.Space();
        targetTerrainHeight = EditorGUILayout.Slider("Target Height:", targetTerrainHeight, 0f, maxHeight);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox($"Target Height: {targetTerrainHeight:F1} units" +
            "\nClick & Drag: Set height in rectangular area\n" +
            "ESC: Cancel", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Revert Last Height Change", GUILayout.Height(25)))
        {
            RevertTerrainHeight();
        }
        if (GUILayout.Button("Back", GUILayout.Height(25)))
        {
            currentCategory = Category.None;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawPrefabSelection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Select Prefab: " + currentCategory.ToString(), EditorStyles.boldLabel);

        List<PrefabData> prefabList = GetCurrentPrefabList();

        if (prefabList.Count == 0)
        {
            EditorGUILayout.HelpBox("No prefabs configured for this category.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        
        for (int i = 0; i < prefabList.Count; i++)
        {
            if (prefabList[i].prefab == null) continue;

            bool isSelected = (selectedPrefabIndex == i);
            GUI.backgroundColor = isSelected ? Color.green : Color.white;

            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            
            // Draw preview thumbnail
            Texture2D preview = AssetPreview.GetAssetPreview(prefabList[i].prefab);
            if (preview != null)
            {
                GUILayout.Box(preview, GUILayout.Width(60), GUILayout.Height(60));
            }
            else
            {
                GUILayout.Box("No\nPreview", GUILayout.Width(60), GUILayout.Height(60));
            }

            if (GUILayout.Button(prefabList[i].prefab.name, GUILayout.Height(60), GUILayout.ExpandWidth(true)))
            {
                // Clear previous preview first
                ClearPreview();
                
                selectedPrefabIndex = i;
                currentRotation = 0f;
                currentYOffset = 0f;
                currentScale = 1f;
                SceneView.RepaintAll();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndScrollView();

        if (selectedPrefabIndex >= 0)
        {
            EditorGUILayout.HelpBox("Selected: " + prefabList[selectedPrefabIndex].prefab.name + 
                "\nLeft Click: Place | Left/Right: Rotate | Up/Down: Y Adjust\n" +
                "S: Cycle Scale (1x/1.5x/2x/0.5x) | ESC: Cancel", MessageType.Info);
            
            if (GUILayout.Button("Clear Selection", GUILayout.Height(25)))
            {
                ClearSelection();
            }
        }
    }

    void DrawPrefabLists()
    {
        GUILayout.Label("Prefab Configuration", EditorStyles.boldLabel);
        
        DrawPrefabListField("Walls", wallPrefabs);
        DrawPrefabListField("Buildings", buildingPrefabs);
        DrawPrefabListField("Props", propPrefabs);
        DrawPrefabListField("Other", otherPrefabs);
        DrawPrefabListField("Gameplay", gameplayPrefabs);
    }

    void DrawPrefabListField(string label, List<PrefabData> list)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        
        int count = EditorGUILayout.IntField("Count", list.Count);
        
        while (list.Count < count)
            list.Add(new PrefabData());
        while (list.Count > count)
            list.RemoveAt(list.Count - 1);

        for (int i = 0; i < list.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            list[i].prefab = (GameObject)EditorGUILayout.ObjectField($"Prefab {i + 1}:", list[i].prefab, typeof(GameObject), false);
            
            list[i].useCustomSize = EditorGUILayout.Toggle("Use Custom Size", list[i].useCustomSize);
            
            if (list[i].useCustomSize)
            {
                EditorGUI.indentLevel++;
                list[i].customSizeX = EditorGUILayout.FloatField("Size X", list[i].customSizeX);
                list[i].customSizeZ = EditorGUILayout.FloatField("Size Z", list[i].customSizeZ);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    void DrawReorganizationTool()
    {
        GUILayout.Label("Reorganize Selected Objects", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        // Show selection info
        GameObject[] selectedObjects = Selection.gameObjects;
        EditorGUILayout.LabelField($"Selected Objects: {selectedObjects.Length}");
        
        if (selectedObjects.Length == 0)
        {
            EditorGUILayout.HelpBox("Select objects in the scene to reorganize them.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.Space();
            
            // Move to new section
            EditorGUILayout.LabelField("Create New Section:", EditorStyles.boldLabel);
            string newSectionNameForMove = EditorGUILayout.TextField("Section Name:", "NewSection");
            
            if (GUILayout.Button("Move to New Section", GUILayout.Height(30)))
            {
                MoveToNewSection(selectedObjects, newSectionNameForMove);
            }
            
            EditorGUILayout.Space();
            
            // Move to existing section
            EditorGUILayout.LabelField("Move to Existing Section:", EditorStyles.boldLabel);
            
            FindOrCreateSectionsParent();
            
            if (sectionsParent != null && sectionsParent.transform.childCount > 0)
            {
                for (int i = 0; i < sectionsParent.transform.childCount; i++)
                {
                    GameObject section = sectionsParent.transform.GetChild(i).gameObject;
                    if (GUILayout.Button($"Move to: {section.name}", GUILayout.Height(25)))
                    {
                        MoveToExistingSection(selectedObjects, section);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No existing sections found.", MessageType.Info);
            }
        }
        
        EditorGUILayout.EndVertical();
    }

    void MoveToNewSection(GameObject[] objects, string sectionName)
    {
        if (objects.Length == 0) return;
        
        FindOrCreateSectionsParent();
        
        // Create new section
        GameObject newSection = new GameObject(sectionName);
        newSection.transform.SetParent(sectionsParent.transform);
        newSection.transform.localPosition = Vector3.zero;
        
        // Create category containers
        GameObject wallsContainer = CreateCategoryContainer(newSection, "Walls");
        GameObject buildingsContainer = CreateCategoryContainer(newSection, "Buildings");
        GameObject propsContainer = CreateCategoryContainer(newSection, "Props");
        GameObject otherContainer = CreateCategoryContainer(newSection, "Other");
        GameObject gameplayContainer = CreateCategoryContainer(newSection, "Gameplay");
        
        Undo.RegisterCreatedObjectUndo(newSection, "Create Section for Reorganization");
        
        // Categorize and move objects
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;
            
            // Determine category based on name or parent
            string category = DetermineCategory(obj);
            Transform targetContainer = null;
            
            switch (category)
            {
                case "Walls":
                    targetContainer = wallsContainer.transform;
                    break;
                case "Buildings":
                    targetContainer = buildingsContainer.transform;
                    break;
                case "Props":
                    targetContainer = propsContainer.transform;
                    break;
                case "Gameplay":
                    targetContainer = gameplayContainer.transform;
                    break;
                default:
                    targetContainer = otherContainer.transform;
                    break;
            }
            
            Undo.SetTransformParent(obj.transform, targetContainer, "Move Object to Section");
        }
        
        Selection.activeGameObject = newSection;
        EditorGUILayout.HelpBox($"Moved {objects.Length} objects to new section: {sectionName}", MessageType.Info);
    }

    void MoveToExistingSection(GameObject[] objects, GameObject targetSection)
    {
        if (objects.Length == 0 || targetSection == null) return;
        
        // Find or create category containers in target section
        Transform wallsContainer = FindOrCreateContainer(targetSection, "Walls");
        Transform buildingsContainer = FindOrCreateContainer(targetSection, "Buildings");
        Transform propsContainer = FindOrCreateContainer(targetSection, "Props");
        Transform otherContainer = FindOrCreateContainer(targetSection, "Other");
        Transform gameplayContainer = FindOrCreateContainer(targetSection, "Gameplay");
        
        // Categorize and move objects
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;
            
            // Don't move the sections parent or sections themselves
            if (obj == sectionsParent || obj.transform.parent == sectionsParent.transform)
                continue;
            
            // Determine category
            string category = DetermineCategory(obj);
            Transform targetContainer = null;
            
            switch (category)
            {
                case "Walls":
                    targetContainer = wallsContainer;
                    break;
                case "Buildings":
                    targetContainer = buildingsContainer;
                    break;
                case "Props":
                    targetContainer = propsContainer;
                    break;
                case "Gameplay":
                    targetContainer = gameplayContainer;
                    break;
                default:
                    targetContainer = otherContainer;
                    break;
            }
            
            Undo.SetTransformParent(obj.transform, targetContainer, "Move Object to Section");
        }
        
        Selection.activeGameObject = targetSection;
    }

    Transform FindOrCreateContainer(GameObject section, string containerName)
    {
        Transform container = section.transform.Find(containerName);
        if (container == null)
        {
            GameObject newContainer = CreateCategoryContainer(section, containerName);
            container = newContainer.transform;
        }
        return container;
    }

    string DetermineCategory(GameObject obj)
    {
        // Check if object is already in a category container
        if (obj.transform.parent != null)
        {
            string parentName = obj.transform.parent.name;
            if (parentName == "Walls" || parentName == "Buildings" || 
                parentName == "Props" || parentName == "Other" || parentName == "Gameplay")
            {
                return parentName;
            }
        }
        
        // Check object name for keywords
        string objName = obj.name.ToLower();
        
        if (objName.Contains("wall"))
            return "Walls";
        else if (objName.Contains("building") || objName.Contains("house") || objName.Contains("structure"))
            return "Buildings";
        else if (objName.Contains("prop") || objName.Contains("tree") || objName.Contains("rock") || 
                 objName.Contains("furniture") || objName.Contains("decoration"))
            return "Props";
        else if (objName.Contains("spike") || objName.Contains("trap") || objName.Contains("gameplay"))
            return "Gameplay";
        
        return "Other";
    }

    void OnSceneGUI(SceneView sceneView)
    {
        // Terrain painting mode
        if (currentCategory == Category.TerrainPaint && activeTerrain != null && selectedTerrainLayer >= 0)
        {
            HandleTerrainPainting(sceneView);
            return;
        }

        // Terrain height mode
        if (currentCategory == Category.TerrainHeight && activeTerrain != null)
        {
            HandleTerrainHeight(sceneView);
            return;
        }

        // Regular prefab placement mode
        if (currentSection == null || selectedPrefabIndex < 0 || currentCategory == Category.None || 
            currentCategory == Category.TerrainPaint || currentCategory == Category.TerrainHeight)
        {
            ClearPreview();
            return;
        }

        Event e = Event.current;
        
        // Get mouse position on ground plane (Y=10)
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float enter;
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, 10, 0)); // Ground at Y=10
        Vector3 targetPosition;
        
        if (groundPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            // Snap X and Z to grid, use 10 + currentYOffset for Y
            targetPosition = new Vector3(
                Mathf.Round(hitPoint.x / GRID_SIZE) * GRID_SIZE,
                10f + currentYOffset,
                Mathf.Round(hitPoint.z / GRID_SIZE) * GRID_SIZE
            );
        }
        else
        {
            ClearPreview();
            return;
        }

        // Handle rotation input with Shift + Mouse Wheel
        if (e.type == EventType.ScrollWheel && e.shift)
        {
            currentRotation += e.delta.y > 0 ? 90f : -90f;
            currentRotation = Mathf.Round(currentRotation / 90f) * 90f;
            e.Use();
            SceneView.RepaintAll();
        }
        // Handle keyboard input
        else if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftArrow)
            {
                currentRotation -= 90f;
                currentRotation = Mathf.Round(currentRotation / 90f) * 90f;
                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.keyCode == KeyCode.RightArrow)
            {
                currentRotation += 90f;
                currentRotation = Mathf.Round(currentRotation / 90f) * 90f;
                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                currentYOffset += GRID_SIZE;
                currentYOffset = Mathf.Round(currentYOffset / GRID_SIZE) * GRID_SIZE;
                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                currentYOffset -= GRID_SIZE;
                currentYOffset = Mathf.Round(currentYOffset / GRID_SIZE) * GRID_SIZE;
                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.keyCode == KeyCode.S)
            {
                // Cycle scale: 1 -> 1.5 -> 2 -> 0.5 -> 1
                if (Mathf.Approximately(currentScale, 1f))
                    currentScale = 1.5f;
                else if (Mathf.Approximately(currentScale, 1.5f))
                    currentScale = 2f;
                else if (Mathf.Approximately(currentScale, 2f))
                    currentScale = 0.5f;
                else
                    currentScale = 1f;
                
                e.Use();
                SceneView.RepaintAll();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                ClearSelection();
                e.Use();
                SceneView.RepaintAll();
            }
        }

        // Update preview
        UpdatePreview(targetPosition);

        // Handle placement
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            PlacePrefab(targetPosition);
            e.Use();
        }

        // Force repaint to update preview position smoothly
        if (e.type == EventType.MouseMove)
        {
            SceneView.RepaintAll();
        }

        // Get prefab bounds for visualization
        Bounds prefabBounds = GetPrefabBounds();
        DrawGridHelper(targetPosition, prefabBounds);

        // Draw UI overlay
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 400, 140));
        GUILayout.Box("Level Design Tool\n" +
                     $"Section: {currentSection.name}\n" +
                     $"Category: {currentCategory}\n" +
                     $"Rotation: {currentRotation}° | Y Offset: {currentYOffset:F1} | Scale: {currentScale:F1}x\n" +
                     "Left Click: Place | Left/Right: Rotate | Up/Down: Y\n" +
                     "S: Cycle Scale | ESC: Cancel",
                     GUILayout.Width(390));
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    Bounds GetPrefabBounds()
    {
        List<PrefabData> prefabList = GetCurrentPrefabList();
        if (prefabList.Count == 0 || selectedPrefabIndex >= prefabList.Count || prefabList[selectedPrefabIndex].prefab == null)
        {
            return new Bounds(Vector3.zero, Vector3.one * GRID_SIZE);
        }

        if (previewObject != null)
        {
            Bounds bounds = new Bounds(previewObject.transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in previewObject.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
                hasBounds = true;
            }

            if (hasBounds)
            {
                return bounds;
            }
        }

        return new Bounds(Vector3.zero, Vector3.one * GRID_SIZE);
    }

    void HandleTerrainPainting(SceneView sceneView)
    {
        Event e = Event.current;

        // Raycast to ground plane (Y=10) instead of just terrain
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float enter;
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, 10, 0));
        
        if (groundPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            
            // Snap to grid
            hitPoint.x = Mathf.Round(hitPoint.x / GRID_SIZE) * GRID_SIZE;
            hitPoint.z = Mathf.Round(hitPoint.z / GRID_SIZE) * GRID_SIZE;

            // Handle painting input
            if (e.type == EventType.MouseDown && e.button == 0 && !isPainting)
            {
                isPainting = true;
                paintStartPosition = hitPoint;
                paintCurrentPosition = hitPoint;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting)
            {
                paintCurrentPosition = hitPoint;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isPainting)
            {
                PaintTerrainRectangle();
                isPainting = false;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isPainting = false;
                e.Use();
                SceneView.RepaintAll();
            }

            // Draw rectangle preview
            if (isPainting)
            {
                DrawPaintRectangle();
            }
        }

        // Force repaint
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            SceneView.RepaintAll();
        }

        // Draw UI overlay
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 400, 100));
        string paintStatus = isPainting ? " [Painting...]" : "";
        GUILayout.Box("Terrain Paint Mode\n" +
                     $"Layer: {activeTerrain.terrainData.terrainLayers[selectedTerrainLayer].name}{paintStatus}\n" +
                     $"Opacity: {terrainPaintOpacity:P0}\n" +
                     "Click & Drag: Paint rectangular area\n" +
                     "ESC: Cancel" + (isPainting ? " drag" : " mode"),
                     GUILayout.Width(390));
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    void DrawPaintRectangle()
    {
        // Draw the paint rectangle
        Handles.color = new Color(1f, 0.5f, 0f, 0.5f);
        
        Vector3[] corners = new Vector3[4];
        corners[0] = new Vector3(paintStartPosition.x, paintStartPosition.y + 0.5f, paintStartPosition.z);
        corners[1] = new Vector3(paintCurrentPosition.x, paintStartPosition.y + 0.5f, paintStartPosition.z);
        corners[2] = new Vector3(paintCurrentPosition.x, paintCurrentPosition.y + 0.5f, paintCurrentPosition.z);
        corners[3] = new Vector3(paintStartPosition.x, paintCurrentPosition.y + 0.5f, paintCurrentPosition.z);
        
        Handles.DrawSolidRectangleWithOutline(corners, new Color(1, 0.5f, 0, 0.2f), new Color(1, 0.5f, 0, 0.9f));
    }

    void PaintTerrainRectangle()
    {
        if (activeTerrain == null || selectedTerrainLayer < 0) return;

        TerrainData terrainData = activeTerrain.terrainData;
        
        // Convert world positions to terrain alphamap coordinates
        Vector3 terrainPos = activeTerrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        
        // Calculate normalized positions
        float normStartX = (paintStartPosition.x - terrainPos.x) / terrainSize.x;
        float normStartZ = (paintStartPosition.z - terrainPos.z) / terrainSize.z;
        float normEndX = (paintCurrentPosition.x - terrainPos.x) / terrainSize.x;
        float normEndZ = (paintCurrentPosition.z - terrainPos.z) / terrainSize.z;
        
        // Clamp to 0-1 range
        normStartX = Mathf.Clamp01(normStartX);
        normStartZ = Mathf.Clamp01(normStartZ);
        normEndX = Mathf.Clamp01(normEndX);
        normEndZ = Mathf.Clamp01(normEndZ);
        
        // Get alphamap resolution
        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;
        
        // Convert to alphamap coordinates
        int minX = Mathf.RoundToInt(Mathf.Min(normStartX, normEndX) * alphamapWidth);
        int maxX = Mathf.RoundToInt(Mathf.Max(normStartX, normEndX) * alphamapWidth);
        int minZ = Mathf.RoundToInt(Mathf.Min(normStartZ, normEndZ) * alphamapHeight);
        int maxZ = Mathf.RoundToInt(Mathf.Max(normStartZ, normEndZ) * alphamapHeight);
        
        // Clamp to valid range
        minX = Mathf.Clamp(minX, 0, alphamapWidth - 1);
        maxX = Mathf.Clamp(maxX, 0, alphamapWidth - 1);
        minZ = Mathf.Clamp(minZ, 0, alphamapHeight - 1);
        maxZ = Mathf.Clamp(maxZ, 0, alphamapHeight - 1);
        
        int width = maxX - minX + 1;
        int height = maxZ - minZ + 1;
        
        // Save undo data
        undoAlphamap = terrainData.GetAlphamaps(minX, minZ, width, height);
        undoAlphamapX = minX;
        undoAlphamapZ = minZ;
        
        // Get current alphamap data
        float[,,] alphamap = (float[,,])undoAlphamap.Clone();
        int numLayers = terrainData.alphamapLayers;
        
        // Paint with specified opacity on selected layer
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Get current values
                float currentSelected = alphamap[z, x, selectedTerrainLayer];
                float totalOther = 0f;
                
                // Calculate target value with opacity
                float targetValue = Mathf.Lerp(currentSelected, 1f, terrainPaintOpacity);
                float diff = targetValue - currentSelected;
                
                // Update selected layer
                alphamap[z, x, selectedTerrainLayer] = targetValue;
                
                // Redistribute remaining weight to other layers
                for (int layer = 0; layer < numLayers; layer++)
                {
                    if (layer != selectedTerrainLayer)
                    {
                        totalOther += alphamap[z, x, layer];
                    }
                }
                
                if (totalOther > 0f)
                {
                    float remainingWeight = 1f - targetValue;
                    for (int layer = 0; layer < numLayers; layer++)
                    {
                        if (layer != selectedTerrainLayer)
                        {
                            alphamap[z, x, layer] = (alphamap[z, x, layer] / totalOther) * remainingWeight;
                        }
                    }
                }
                else
                {
                    // If no other layers, set them all to 0
                    for (int layer = 0; layer < numLayers; layer++)
                    {
                        if (layer != selectedTerrainLayer)
                        {
                            alphamap[z, x, layer] = 0f;
                        }
                    }
                }
            }
        }
        
        // Apply the changes
        terrainData.SetAlphamaps(minX, minZ, alphamap);
        
        SceneView.RepaintAll();
    }

    void RevertTerrainPaint()
    {
        if (activeTerrain == null || undoAlphamap == null) return;
        
        activeTerrain.terrainData.SetAlphamaps(undoAlphamapX, undoAlphamapZ, undoAlphamap);
        SceneView.RepaintAll();
    }

    void HandleTerrainHeight(SceneView sceneView)
    {
        Event e = Event.current;

        // Raycast to ground plane at Y=0 to allow any height
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float enter;
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        if (groundPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            
            // Snap to grid
            hitPoint.x = Mathf.Round(hitPoint.x / GRID_SIZE) * GRID_SIZE;
            hitPoint.z = Mathf.Round(hitPoint.z / GRID_SIZE) * GRID_SIZE;

            // Handle height setting input
            if (e.type == EventType.MouseDown && e.button == 0 && !isPainting)
            {
                isPainting = true;
                paintStartPosition = hitPoint;
                paintCurrentPosition = hitPoint;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && e.button == 0 && isPainting)
            {
                paintCurrentPosition = hitPoint;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && isPainting)
            {
                SetTerrainHeightRectangle();
                isPainting = false;
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isPainting = false;
                e.Use();
                SceneView.RepaintAll();
            }

            // Draw rectangle preview
            if (isPainting)
            {
                DrawHeightRectangle();
            }
        }

        // Force repaint
        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            SceneView.RepaintAll();
        }

        // Draw UI overlay
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 400, 100));
        string heightStatus = isPainting ? " [Setting Height...]" : "";
        GUILayout.Box("Terrain Height Mode\n" +
                     $"Target Height: {targetTerrainHeight:F1} units{heightStatus}\n" +
                     "Click & Drag: Set height in rectangular area\n" +
                     "ESC: Cancel" + (isPainting ? " drag" : " mode"),
                     GUILayout.Width(390));
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    void DrawHeightRectangle()
    {
        // Draw the height rectangle in blue
        Handles.color = new Color(0f, 0.5f, 1f, 0.5f);
        
        Vector3[] corners = new Vector3[4];
        corners[0] = new Vector3(paintStartPosition.x, targetTerrainHeight, paintStartPosition.z);
        corners[1] = new Vector3(paintCurrentPosition.x, targetTerrainHeight, paintStartPosition.z);
        corners[2] = new Vector3(paintCurrentPosition.x, targetTerrainHeight, paintCurrentPosition.z);
        corners[3] = new Vector3(paintStartPosition.x, targetTerrainHeight, paintCurrentPosition.z);
        
        Handles.DrawSolidRectangleWithOutline(corners, new Color(0, 0.5f, 1, 0.2f), new Color(0, 0.5f, 1, 0.9f));
    }

    void SetTerrainHeightRectangle()
    {
        if (activeTerrain == null) return;

        TerrainData terrainData = activeTerrain.terrainData;
        
        // Convert world positions to terrain heightmap coordinates
        Vector3 terrainPos = activeTerrain.transform.position;
        Vector3 terrainSize = terrainData.size;
        
        // Calculate normalized positions
        float normStartX = (paintStartPosition.x - terrainPos.x) / terrainSize.x;
        float normStartZ = (paintStartPosition.z - terrainPos.z) / terrainSize.z;
        float normEndX = (paintCurrentPosition.x - terrainPos.x) / terrainSize.x;
        float normEndZ = (paintCurrentPosition.z - terrainPos.z) / terrainSize.z;
        
        // Clamp to 0-1 range
        normStartX = Mathf.Clamp01(normStartX);
        normStartZ = Mathf.Clamp01(normStartZ);
        normEndX = Mathf.Clamp01(normEndX);
        normEndZ = Mathf.Clamp01(normEndZ);
        
        // Get heightmap resolution
        int heightmapWidth = terrainData.heightmapResolution;
        int heightmapHeight = terrainData.heightmapResolution;
        
        // Convert to heightmap coordinates
        int minX = Mathf.RoundToInt(Mathf.Min(normStartX, normEndX) * (heightmapWidth - 1));
        int maxX = Mathf.RoundToInt(Mathf.Max(normStartX, normEndX) * (heightmapWidth - 1));
        int minZ = Mathf.RoundToInt(Mathf.Min(normStartZ, normEndZ) * (heightmapHeight - 1));
        int maxZ = Mathf.RoundToInt(Mathf.Max(normStartZ, normEndZ) * (heightmapHeight - 1));
        
        // Clamp to valid range
        minX = Mathf.Clamp(minX, 0, heightmapWidth - 1);
        maxX = Mathf.Clamp(maxX, 0, heightmapWidth - 1);
        minZ = Mathf.Clamp(minZ, 0, heightmapHeight - 1);
        maxZ = Mathf.Clamp(maxZ, 0, heightmapHeight - 1);
        
        int width = maxX - minX + 1;
        int height = maxZ - minZ + 1;
        
        // Save undo data
        undoHeightmap = terrainData.GetHeights(minX, minZ, width, height);
        undoHeightmapX = minX;
        undoHeightmapZ = minZ;
        
        // Create new heightmap data
        float[,] heights = new float[height, width];
        float normalizedHeight = targetTerrainHeight / terrainData.size.y;
        
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                heights[z, x] = normalizedHeight;
            }
        }
        
        // Apply the changes
        terrainData.SetHeights(minX, minZ, heights);
        
        SceneView.RepaintAll();
    }

    void RevertTerrainHeight()
    {
        if (activeTerrain == null || undoHeightmap == null) return;
        
        activeTerrain.terrainData.SetHeights(undoHeightmapX, undoHeightmapZ, undoHeightmap);
        SceneView.RepaintAll();
    }

    Vector2 GetPrefabSize()
    {
        List<PrefabData> prefabList = GetCurrentPrefabList();
        if (prefabList.Count == 0 || selectedPrefabIndex >= prefabList.Count)
        {
            return new Vector2(GRID_SIZE, GRID_SIZE);
        }

        PrefabData data = prefabList[selectedPrefabIndex];
        
        if (data.useCustomSize)
        {
            return new Vector2(data.customSizeX, data.customSizeZ);
        }
        
        // Calculate actual bounds
        Bounds bounds = GetPrefabBounds();
        return new Vector2(bounds.size.x, bounds.size.z);
    }

    void UpdatePreview(Vector3 position)
    {
        List<PrefabData> prefabList = GetCurrentPrefabList();
        if (prefabList.Count == 0 || selectedPrefabIndex >= prefabList.Count) return;

        PrefabData data = prefabList[selectedPrefabIndex];
        if (data.prefab == null) return;

        if (previewObject == null)
        {
            previewObject = (GameObject)PrefabUtility.InstantiatePrefab(data.prefab);
            previewObject.hideFlags = HideFlags.HideAndDontSave;
            
            // Make preview semi-transparent
            foreach (Renderer renderer in previewObject.GetComponentsInChildren<Renderer>())
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        // Try _MainColor first, then fallback to _Color
                        if (mat.HasProperty("_MainColor"))
                        {
                            Color mainColor = mat.GetColor("_MainColor");
                            mat.SetColor("_MainColor", new Color(mainColor.r, mainColor.g, mainColor.b, 0.5f));
                        }
                        else if (mat.HasProperty("_Color"))
                        {
                            Color mainColor = mat.GetColor("_Color");
                            mat.SetColor("_Color", new Color(mainColor.r, mainColor.g, mainColor.b, 0.5f));
                        }
                    }
                }
            }
        }

        previewObject.transform.position = position;
        previewObject.transform.rotation = Quaternion.Euler(0, currentRotation, 0);
        previewObject.transform.localScale = Vector3.one * currentScale;
    }

    void PlacePrefab(Vector3 position)
    {
        List<PrefabData> prefabList = GetCurrentPrefabList();
        if (prefabList.Count == 0 || selectedPrefabIndex >= prefabList.Count) return;

        PrefabData data = prefabList[selectedPrefabIndex];
        if (data.prefab == null) return;

        // Get or create category container
        Transform categoryContainer = GetOrCreateCategoryContainer();

        // Instantiate prefab
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(data.prefab, categoryContainer);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0, currentRotation, 0);
        instance.transform.localScale = Vector3.one * currentScale;

        Undo.RegisterCreatedObjectUndo(instance, "Place Prefab");
        
        SceneView.RepaintAll();
    }

    void DrawGridHelper(Vector3 center, Bounds prefabBounds)
    {
        int gridCount = 20;
        float gridExtent = gridCount * GRID_SIZE;

        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // Draw grid lines at Y=10
        for (int i = -gridCount; i <= gridCount; i++)
        {
            float offset = i * GRID_SIZE;
            Handles.DrawLine(
                new Vector3(center.x - gridExtent, 10, center.z + offset),
                new Vector3(center.x + gridExtent, 10, center.z + offset)
            );
            Handles.DrawLine(
                new Vector3(center.x + offset, 10, center.z - gridExtent),
                new Vector3(center.x + offset, 10, center.z + gridExtent)
            );
        }

        // Highlight prefab footprint using actual bounds (project to Y=10)
        Handles.color = new Color(0f, 1f, 0f, 0.4f);
        Vector3 size = prefabBounds.size;
        Vector3 boundsCenter = prefabBounds.center;
        
        // Project bounds to ground plane (Y = 10)
        float halfWidth = size.x / 2f;
        float halfDepth = size.z / 2f;
        
        Vector3[] cellCorners = new Vector3[4];
        cellCorners[0] = new Vector3(boundsCenter.x - halfWidth, 10, boundsCenter.z - halfDepth);
        cellCorners[1] = new Vector3(boundsCenter.x + halfWidth, 10, boundsCenter.z - halfDepth);
        cellCorners[2] = new Vector3(boundsCenter.x + halfWidth, 10, boundsCenter.z + halfDepth);
        cellCorners[3] = new Vector3(boundsCenter.x - halfWidth, 10, boundsCenter.z + halfDepth);
        
        Handles.DrawSolidRectangleWithOutline(cellCorners, new Color(0, 1, 0, 0.1f), Color.green);
    }

    void CreateNewSection()
    {
        FindOrCreateSectionsParent();

        currentSection = new GameObject(newSectionName);
        currentSection.transform.SetParent(sectionsParent.transform);
        currentSection.transform.localPosition = Vector3.zero;

        // Create category containers
        CreateCategoryContainer(currentSection, "Walls");
        CreateCategoryContainer(currentSection, "Buildings");
        CreateCategoryContainer(currentSection, "Props");
        CreateCategoryContainer(currentSection, "Other");
        CreateCategoryContainer(currentSection, "Gameplay");

        Undo.RegisterCreatedObjectUndo(currentSection, "Create Section");
        Selection.activeGameObject = currentSection;
    }

    void FindOrCreateSectionsParent()
    {
        sectionsParent = GameObject.Find("Sections");
        
        if (sectionsParent == null)
        {
            sectionsParent = new GameObject("Sections");
            Undo.RegisterCreatedObjectUndo(sectionsParent, "Create Sections Parent");
        }
    }

    GameObject CreateCategoryContainer(GameObject parent, string categoryName)
    {
        GameObject container = new GameObject(categoryName);
        container.transform.SetParent(parent.transform);
        container.transform.localPosition = Vector3.zero;
        return container;
    }

    Transform GetOrCreateCategoryContainer()
    {
        if (currentSection == null) return null;

        string categoryName = currentCategory.ToString();
        Transform container = currentSection.transform.Find(categoryName);

        if (container == null)
        {
            GameObject newContainer = CreateCategoryContainer(currentSection, categoryName);
            container = newContainer.transform;
        }

        return container;
    }

    List<PrefabData> GetCurrentPrefabList()
    {
        switch (currentCategory)
        {
            case Category.Walls: return wallPrefabs;
            case Category.Buildings: return buildingPrefabs;
            case Category.Props: return propPrefabs;
            case Category.Other: return otherPrefabs;
            case Category.Gameplay: return gameplayPrefabs;
            default: return new List<PrefabData>();
        }
    }

    void SetCategory(Category category)
    {
        currentCategory = category;
        selectedPrefabIndex = -1;
        currentRotation = 0f;
        currentYOffset = 0f;
        currentScale = 1f;
        ClearPreview();
    }

    void ClearSelection()
    {
        selectedPrefabIndex = -1;
        currentRotation = 0f;
        currentYOffset = 0f;
        currentScale = 1f;
        selectedTerrainLayer = -1;
        isPainting = false;
        currentCategory = Category.None;
        ClearPreview();
        SceneView.RepaintAll();
    }

    void ClearPreview()
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
            previewObject = null;
        }
    }

    void ResetToModeSelection()
    {
        currentMode = EditorMode.SelectMode;
        currentSection = null;
        currentCategory = Category.None;
        ClearSelection();
    }
}
