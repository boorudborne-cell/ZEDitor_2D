using UnityEngine;
using MapEditor.Data;
using UnityEngine.UI;
using TMPro;

namespace MapEditor.Setup
{
    /// <summary>
    /// Helper component to create the map editor UI hierarchy at runtime
    /// Attach to empty GameObject and call Setup() or enable Auto Setup
    /// </summary>
    public class MapEditorSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool autoSetup = true;
        [SerializeField] private TilePalette tilePalette;
        
        [Header("Created References")]
        public Core.MapEditorController Controller { get; private set; }
        public Rendering.ChunkedMapRenderer Renderer { get; private set; }
        public Camera EditorCamera { get; private set; }
        public Canvas UICanvas { get; private set; }
        
        private void Awake()
        {
            if (autoSetup)
            {
                Setup();
            }
        }
        
        /// <summary>
        /// Creates the complete map editor hierarchy
        /// </summary>
        public void Setup()
        {
            CreateEditorCamera();
            CreateController();
            CreateRenderer();
            CreateUI();
            
            Debug.Log("[MapEditorSetup] Map editor setup complete!");
        }
        
        private void CreateEditorCamera()
        {
            var camObj = new GameObject("EditorCamera");
            camObj.transform.SetParent(transform);
            
            EditorCamera = camObj.AddComponent<Camera>();
            EditorCamera.orthographic = true;
            EditorCamera.orthographicSize = 5f;
            EditorCamera.nearClipPlane = -100f;
            EditorCamera.farClipPlane = 100f;
            EditorCamera.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            EditorCamera.clearFlags = CameraClearFlags.SolidColor;
            
            camObj.transform.position = new Vector3(0, 0, -10);
        }
        
        private void CreateController()
        {
            var controllerObj = new GameObject("MapEditorController");
            controllerObj.transform.SetParent(transform);
            
            Controller = controllerObj.AddComponent<Core.MapEditorController>();
            
            // Set palette through reflection or serialization workaround
            var paletteField = typeof(Core.MapEditorController).GetField("tilePalette", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            paletteField?.SetValue(Controller, tilePalette);
        }
        
        private void CreateRenderer()
        {
            if (EditorCamera == null)
                return;
            
            Renderer = EditorCamera.gameObject.AddComponent<Rendering.ChunkedMapRenderer>();
            
            // Set references through reflection
            var type = typeof(Rendering.ChunkedMapRenderer);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            type.GetField("editorController", flags)?.SetValue(Renderer, Controller);
            type.GetField("tilePalette", flags)?.SetValue(Renderer, tilePalette);
            type.GetField("renderCamera", flags)?.SetValue(Renderer, EditorCamera);
        }
        
        private void CreateUI()
        {
            // Create Canvas
            var canvasObj = new GameObject("EditorCanvas");
            canvasObj.transform.SetParent(transform);
            
            UICanvas = canvasObj.AddComponent<Canvas>();
            UICanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UICanvas.sortingOrder = 100;
            
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Create toolbar
            CreateToolbar(canvasObj.transform);
            
            // Create side panel
            CreateSidePanel(canvasObj.transform);
            
            // Create status bar
            CreateStatusBar(canvasObj.transform);
            
            // Create canvas input handler
            CreateInputHandler(canvasObj);
        }
        
        private void CreateToolbar(Transform parent)
        {
            var toolbar = CreatePanel(parent, "Toolbar", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -30), new Vector2(400, 50));
            
            var hlg = toolbar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            
            // Tool buttons
            CreateButton(toolbar.transform, "Brush", 60, 40);
            CreateButton(toolbar.transform, "Eraser", 60, 40);
            CreateButton(toolbar.transform, "Entity", 60, 40);
            CreateButton(toolbar.transform, "Select", 60, 40);
            
            // Separator
            CreateSeparator(toolbar.transform);
            
            // View buttons
            CreateButton(toolbar.transform, "+", 30, 40);
            CreateButton(toolbar.transform, "-", 30, 40);
            
            // Toggles
            CreateToggle(toolbar.transform, "Grid", true);
            CreateToggle(toolbar.transform, "Collision", true);
        }
        
        private void CreateSidePanel(Transform parent)
        {
            var panel = CreatePanel(parent, "SidePanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-120, 0), new Vector2(220, 500));
            
            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            
            // Layer section
            var layerLabel = CreateLabel(panel.transform, "Layers", 20);
            CreateButton(panel.transform, "Background", -1, 35);
            CreateButton(panel.transform, "Ground", -1, 35);
            CreateButton(panel.transform, "Foreground", -1, 35);
            
            // Palette section header
            CreateLabel(panel.transform, "Tiles", 20);
        }
        
        private void CreateStatusBar(Transform parent)
        {
            var statusBar = CreatePanel(parent, "StatusBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 15), new Vector2(800, 30));
            
            var hlg = statusBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            
            CreateLabel(statusBar.transform, "No map loaded", 14);
            CreateLabel(statusBar.transform, "Tile: (0, 0)", 14);
        }
        
        private void CreateInputHandler(GameObject canvas)
        {
            // Create a full-screen invisible panel for input
            var inputPanel = CreatePanel(canvas.transform, "InputPanel", 
                new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            
            var rt = inputPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            // Make it receive raycasts but be invisible
            var img = inputPanel.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;
            
            // Add input handler
            var inputHandler = inputPanel.AddComponent<UI.CanvasInputHandler>();
            
            // Set references through reflection
            var type = typeof(UI.CanvasInputHandler);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            type.GetField("editorController", flags)?.SetValue(inputHandler, Controller);
            type.GetField("editorCamera", flags)?.SetValue(inputHandler, EditorCamera);
            
            // Move to back so other UI elements receive input first
            inputPanel.transform.SetAsFirstSibling();
        }
        
        #region UI Factory Methods
        
        private GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, 
            Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            
            return panel;
        }
        
        private Button CreateButton(Transform parent, string text, float width, float height)
        {
            var btnObj = new GameObject(text + "Button");
            btnObj.transform.SetParent(parent, false);
            
            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f);
            
            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f);
            colors.pressedColor = new Color(0.25f, 0.25f, 0.25f);
            btn.colors = colors;
            
            var rt = btnObj.GetComponent<RectTransform>();
            if (width > 0) rt.sizeDelta = new Vector2(width, height);
            
            // Add text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            
            return btn;
        }
        
        private Toggle CreateToggle(Transform parent, string text, bool defaultValue)
        {
            var toggleObj = new GameObject(text + "Toggle");
            toggleObj.transform.SetParent(parent, false);
            
            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = defaultValue;
            
            var rt = toggleObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80, 40);
            
            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(toggleObj.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.25f, 0.25f, 0.25f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            
            // Checkmark
            var check = new GameObject("Checkmark");
            check.transform.SetParent(bg.transform, false);
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.6f, 1f);
            var checkRt = check.GetComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0, 0);
            checkRt.anchorMax = new Vector2(0.3f, 1);
            checkRt.sizeDelta = Vector2.zero;
            
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            
            // Label
            var label = new GameObject("Label");
            label.transform.SetParent(toggleObj.transform, false);
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 12;
            tmp.alignment = TextAlignmentOptions.Center;
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;
            
            return toggle;
        }
        
        private TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize)
        {
            var labelObj = new GameObject(text + "Label");
            labelObj.transform.SetParent(parent, false);
            
            var tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;
            
            var rt = labelObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, fontSize + 10);
            
            return tmp;
        }
        
        private void CreateSeparator(Transform parent)
        {
            var sep = new GameObject("Separator");
            sep.transform.SetParent(parent, false);
            
            var img = sep.AddComponent<Image>();
            img.color = new Color(0.4f, 0.4f, 0.4f);
            
            var rt = sep.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2, 30);
        }
        
        #endregion
    }
}
