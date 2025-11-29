using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MapEditor.Core;
using MapEditor.Data;

namespace MapEditor.UI
{
    /// <summary>
    /// Main UI panel for the map editor
    /// Controls toolbar, layer selection, and status display
    /// </summary>
    public class EditorUIPanel : MonoBehaviour
    {
        [Header("Controller Reference")]
        [SerializeField] private MapEditorController editorController;
        
        [Header("Toolbar Buttons")]
        [SerializeField] private Button brushButton;
        [SerializeField] private Button eraserButton;
        [SerializeField] private Button entityButton;
        [SerializeField] private Button selectButton;
        
        [Header("Layer Buttons")]
        [SerializeField] private Button backgroundLayerButton;
        [SerializeField] private Button groundLayerButton;
        [SerializeField] private Button foregroundLayerButton;
        
        [Header("View Controls")]
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private Toggle gridToggle;
        [SerializeField] private Toggle collisionToggle;
        [SerializeField] private TMP_Text zoomLabel;
        
        [Header("File Controls")]
        [SerializeField] private Button newMapButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        
        [Header("Status Bar")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text positionText;
        [SerializeField] private TMP_Text mapInfoText;
        
        [Header("Visual Feedback")]
        [SerializeField] private Color activeButtonColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color inactiveButtonColor = Color.white;
        
        [Header("Dialogs")]
        [SerializeField] private NewMapDialog newMapDialog;
        [SerializeField] private FileDialog fileDialog;
        
        private void Awake()
        {
            SetupButtonListeners();
            SetupToggleListeners();
        }
        
        private void OnEnable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged += UpdateUI;
                editorController.OnMapCreated += OnMapCreated;
                editorController.OnMapLoaded += OnMapLoaded;
                editorController.OnMapSaved += OnMapSaved;
                editorController.OnError += OnError;
            }
        }
        
        private void OnDisable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged -= UpdateUI;
                editorController.OnMapCreated -= OnMapCreated;
                editorController.OnMapLoaded -= OnMapLoaded;
                editorController.OnMapSaved -= OnMapSaved;
                editorController.OnError -= OnError;
            }
        }
        
        private void Start()
        {
            UpdateUI();
        }
        
        private void SetupButtonListeners()
        {
            // Tool buttons
            brushButton?.onClick.AddListener(() => editorController?.SetTool(EditorTool.Brush));
            eraserButton?.onClick.AddListener(() => editorController?.SetTool(EditorTool.Eraser));
            entityButton?.onClick.AddListener(() => editorController?.SetTool(EditorTool.EntityPlace));
            selectButton?.onClick.AddListener(() => editorController?.SetTool(EditorTool.EntitySelect));
            
            // Layer buttons
            backgroundLayerButton?.onClick.AddListener(() => editorController?.SetActiveLayer(LayerType.Background));
            groundLayerButton?.onClick.AddListener(() => editorController?.SetActiveLayer(LayerType.Ground));
            foregroundLayerButton?.onClick.AddListener(() => editorController?.SetActiveLayer(LayerType.Foreground));
            
            // View buttons
            zoomInButton?.onClick.AddListener(() => editorController?.ZoomIn());
            zoomOutButton?.onClick.AddListener(() => editorController?.ZoomOut());
            
            // File buttons
            newMapButton?.onClick.AddListener(OnNewMapClicked);
            saveButton?.onClick.AddListener(OnSaveClicked);
            loadButton?.onClick.AddListener(OnLoadClicked);
        }
        
        private void SetupToggleListeners()
        {
            gridToggle?.onValueChanged.AddListener(OnGridToggleChanged);
            collisionToggle?.onValueChanged.AddListener(OnCollisionToggleChanged);
        }
        
        private void OnGridToggleChanged(bool value)
        {
            if (editorController?.State != null)
            {
                editorController.State.ShowGrid = value;
                editorController.ToggleGrid(); // This will sync the state
            }
        }
        
        private void OnCollisionToggleChanged(bool value)
        {
            if (editorController?.State != null)
            {
                editorController.State.ShowCollisions = value;
                editorController.ToggleCollisions(); // This will sync the state
            }
        }
        
        private void UpdateUI()
        {
            if (editorController?.State == null)
                return;
            
            var state = editorController.State;
            
            // Update tool button states
            UpdateButtonState(brushButton, state.ActiveTool == EditorTool.Brush);
            UpdateButtonState(eraserButton, state.ActiveTool == EditorTool.Eraser);
            UpdateButtonState(entityButton, state.ActiveTool == EditorTool.EntityPlace);
            UpdateButtonState(selectButton, state.ActiveTool == EditorTool.EntitySelect);
            
            // Update layer button states
            UpdateButtonState(backgroundLayerButton, state.ActiveLayer == LayerType.Background);
            UpdateButtonState(groundLayerButton, state.ActiveLayer == LayerType.Ground);
            UpdateButtonState(foregroundLayerButton, state.ActiveLayer == LayerType.Foreground);
            
            // Update toggles
            if (gridToggle != null)
                gridToggle.SetIsOnWithoutNotify(state.ShowGrid);
            
            if (collisionToggle != null)
                collisionToggle.SetIsOnWithoutNotify(state.ShowCollisions);
            
            // Update zoom label
            if (zoomLabel != null)
                zoomLabel.text = $"{state.Zoom * 100:F0}%";
            
            // Update map info
            UpdateMapInfo();
            
            // Update save button interactability
            if (saveButton != null)
                saveButton.interactable = state.CurrentMap != null;
        }
        
        private void UpdateButtonState(Button button, bool isActive)
        {
            if (button == null)
                return;
            
            var colors = button.colors;
            colors.normalColor = isActive ? activeButtonColor : inactiveButtonColor;
            button.colors = colors;
        }
        
        private void UpdateMapInfo()
        {
            if (mapInfoText == null)
                return;
            
            var map = editorController?.State?.CurrentMap;
            if (map == null)
            {
                mapInfoText.text = "No map loaded";
                return;
            }
            
            string unsaved = editorController.State.HasUnsavedChanges ? "*" : "";
            mapInfoText.text = $"{map.mapName}{unsaved} ({map.width}x{map.height}) - {map.GetTotalTileCount()} tiles";
        }
        
        public void UpdateCursorPosition(Vector2Int tilePosition)
        {
            if (positionText != null)
            {
                positionText.text = $"Tile: ({tilePosition.x}, {tilePosition.y})";
            }
        }
        
        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
            
            Debug.Log($"[EditorUI] {message}");
        }
        
        #region Dialog Handlers
        
        private void OnNewMapClicked()
        {
            if (newMapDialog != null)
            {
                newMapDialog.Show(OnNewMapConfirmed);
            }
            else
            {
                // Default behavior without dialog
                editorController?.CreateNewMap("NewMap");
            }
        }
        
        private void OnNewMapConfirmed(string name, int width, int height)
        {
            editorController?.CreateNewMap(name, width, height);
        }
        
        private void OnSaveClicked()
        {
            if (editorController?.State?.CurrentMap == null)
                return;
            
            if (fileDialog != null)
            {
                fileDialog.ShowSave(
                    editorController.State.CurrentFileName ?? editorController.State.CurrentMap.mapName,
                    OnSaveConfirmed
                );
            }
            else
            {
                editorController.SaveMap();
            }
        }
        
        private void OnSaveConfirmed(string fileName)
        {
            editorController?.SaveMap(fileName);
        }
        
        private void OnLoadClicked()
        {
            if (fileDialog != null)
            {
                fileDialog.ShowLoad(editorController.GetAvailableMaps(), OnLoadConfirmed);
            }
        }
        
        private void OnLoadConfirmed(string fileName)
        {
            editorController?.LoadMap(fileName);
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnMapCreated()
        {
            SetStatus("New map created");
        }
        
        private void OnMapLoaded()
        {
            SetStatus($"Map loaded: {editorController.State.CurrentFileName}");
        }
        
        private void OnMapSaved()
        {
            SetStatus($"Map saved: {editorController.State.CurrentFileName}");
        }
        
        private void OnError(string message)
        {
            SetStatus($"Error: {message}");
        }
        
        #endregion
    }
}
