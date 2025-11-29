using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MapEditor.Core;
using MapEditor.Data;

namespace MapEditor.UI
{
    public class MapEditorUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private MapEditorController controller;
        [SerializeField] private Texture2D defaultTileTexture;
        
        private VisualElement _root;
        
        // Toolbar
        private Button _btnBrush, _btnEraser, _btnEntity;
        private Button _btnZoomIn, _btnZoomOut;
        private Label _zoomLabel;
        private Toggle _toggleCollision;
        private Button _btnNew, _btnSave, _btnLoad;
        
        // Layers
        private Button _btnLayerBg, _btnLayerGround, _btnLayerFg;
        private Button _btnClearLayer;
        
        // Palette
        private DropdownField _categoryDropdown;
        private VisualElement _tileContainer;
        private VisualElement _entityContainer;
        
        // Status
        private Label _statusText, _positionText, _mapInfo;
        
        // Dialogs
        private VisualElement _newMapDialog, _loadDialog;
        private TextField _inputMapName;
        private IntegerField _inputWidth, _inputHeight;
        private VisualElement _fileList;
        private string _selectedFile;
        
        // Tracking
        private readonly List<Button> _tileButtons = new List<Button>();
        private readonly List<Button> _entityButtons = new List<Button>();
        
        private void OnEnable()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            
            _root = uiDocument.rootVisualElement;
            if (_root == null) return;
            
            BindToolbar();
            BindLayers();
            BindPalette();
            BindStatus();
            BindDialogs();
        }
        
        private void Start()
        {
            // Subscribe after all Awake methods have run
            if (controller != null)
            {
                if (controller.State != null)
                    controller.State.OnChanged += UpdateUI;
                controller.OnMapChanged += OnMapChanged;
                controller.OnError += ShowError;
            }
            
            UpdateUI();
        }
        
        private void OnDisable()
        {
            if (controller == null) return;
            
            if (controller.State != null)
                controller.State.OnChanged -= UpdateUI;
            controller.OnMapChanged -= OnMapChanged;
            controller.OnError -= ShowError;
        }
        
        private void BindToolbar()
        {
            _btnBrush = _root.Q<Button>("btn-brush");
            _btnEraser = _root.Q<Button>("btn-eraser");
            _btnEntity = _root.Q<Button>("btn-entity");
            
            _btnBrush?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Brush));
            _btnEraser?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Eraser));
            _btnEntity?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Entity));
            
            _btnZoomIn = _root.Q<Button>("btn-zoom-in");
            _btnZoomOut = _root.Q<Button>("btn-zoom-out");
            _zoomLabel = _root.Q<Label>("zoom-label");
            
            _btnZoomIn?.RegisterCallback<ClickEvent>(_ => controller?.Zoom(1f));
            _btnZoomOut?.RegisterCallback<ClickEvent>(_ => controller?.Zoom(-1f));
            
            _toggleCollision = _root.Q<Toggle>("toggle-collision");
            _toggleCollision?.RegisterValueChangedCallback(evt =>
            {
                if (controller?.State != null)
                {
                    controller.State.ShowCollisions = evt.newValue;
                    controller.State.NotifyChanged();
                }
            });
            
            _btnNew = _root.Q<Button>("btn-new");
            _btnSave = _root.Q<Button>("btn-save");
            _btnLoad = _root.Q<Button>("btn-load");
            
            _btnNew?.RegisterCallback<ClickEvent>(_ => ShowNewMapDialog());
            _btnSave?.RegisterCallback<ClickEvent>(_ => controller?.SaveMap());
            _btnLoad?.RegisterCallback<ClickEvent>(_ => ShowLoadDialog());
        }
        
        private void BindLayers()
        {
            _btnLayerBg = _root.Q<Button>("btn-layer-bg");
            _btnLayerGround = _root.Q<Button>("btn-layer-ground");
            _btnLayerFg = _root.Q<Button>("btn-layer-fg");
            _btnClearLayer = _root.Q<Button>("btn-clear-layer");
            
            _btnLayerBg?.RegisterCallback<ClickEvent>(_ => controller?.SetLayer(LayerType.Background));
            _btnLayerGround?.RegisterCallback<ClickEvent>(_ => controller?.SetLayer(LayerType.Ground));
            _btnLayerFg?.RegisterCallback<ClickEvent>(_ => controller?.SetLayer(LayerType.Foreground));
            _btnClearLayer?.RegisterCallback<ClickEvent>(_ => 
            {
                if (controller?.State != null)
                    controller.ClearLayer(controller.State.ActiveLayer);
            });
        }
        
        private void BindPalette()
        {
            _categoryDropdown = _root.Q<DropdownField>("category-dropdown");
            _tileContainer = _root.Q<VisualElement>("tile-container");
            _entityContainer = _root.Q<VisualElement>("entity-container");
            
            _categoryDropdown?.RegisterValueChangedCallback(evt => FilterTiles(evt.newValue));
        }
        
        private void BindStatus()
        {
            _statusText = _root.Q<Label>("status-text");
            _positionText = _root.Q<Label>("position-text");
            _mapInfo = _root.Q<Label>("map-info");
        }
        
        private void BindDialogs()
        {
            _newMapDialog = _root.Q<VisualElement>("new-map-dialog");
            _loadDialog = _root.Q<VisualElement>("load-dialog");
            
            _inputMapName = _root.Q<TextField>("input-map-name");
            _inputWidth = _root.Q<IntegerField>("input-width");
            _inputHeight = _root.Q<IntegerField>("input-height");
            _fileList = _root.Q<VisualElement>("file-list");
            
            var btnDialogCancel = _root.Q<Button>("btn-dialog-cancel");
            var btnDialogCreate = _root.Q<Button>("btn-dialog-create");
            var btnLoadCancel = _root.Q<Button>("btn-load-cancel");
            var btnLoadConfirm = _root.Q<Button>("btn-load-confirm");
            
            btnDialogCancel?.RegisterCallback<ClickEvent>(_ => HideDialogs());
            btnDialogCreate?.RegisterCallback<ClickEvent>(_ => CreateNewMap());
            btnLoadCancel?.RegisterCallback<ClickEvent>(_ => HideDialogs());
            btnLoadConfirm?.RegisterCallback<ClickEvent>(_ => LoadSelectedMap());
        }
        
        private void UpdateUI()
        {
            if (controller?.State == null) return;
            var state = controller.State;
            
            // Tool buttons
            SetSelected(_btnBrush, state.ActiveTool == EditorTool.Brush);
            SetSelected(_btnEraser, state.ActiveTool == EditorTool.Eraser);
            SetSelected(_btnEntity, state.ActiveTool == EditorTool.Entity);
            
            // Layer buttons
            SetSelected(_btnLayerBg, state.ActiveLayer == LayerType.Background);
            SetSelected(_btnLayerGround, state.ActiveLayer == LayerType.Ground);
            SetSelected(_btnLayerFg, state.ActiveLayer == LayerType.Foreground);
            
            // Zoom
            if (_zoomLabel != null)
                _zoomLabel.text = $"{Mathf.RoundToInt(state.Zoom * 100)}%";
            
            // Collision toggle
            if (_toggleCollision != null)
                _toggleCollision.SetValueWithoutNotify(state.ShowCollisions);
            
            // Tile selection
            foreach (var btn in _tileButtons)
            {
                bool selected = btn.userData as string == state.SelectedTileId;
                SetSelected(btn, selected);
            }
            
            // Entity selection
            foreach (var btn in _entityButtons)
            {
                bool selected = btn.userData as string == state.SelectedEntityId;
                SetSelected(btn, selected);
            }
            
            // Position
            if (_positionText != null)
            {
                var pos = state.HoveredTile;
                _positionText.text = $"Tile: {pos.x}, {pos.y}";
            }
            
            // Map info
            UpdateMapInfo();
        }
        
        private void UpdateMapInfo()
        {
            if (_mapInfo == null) return;
            
            var map = controller?.State?.Map;
            if (map == null)
            {
                _mapInfo.text = "No map";
                return;
            }
            
            string unsaved = controller.State.HasUnsavedChanges ? "*" : "";
            int tileCount = 0;
            foreach (var layer in map.layers) tileCount += layer.tiles.Count;
            
            _mapInfo.text = $"{map.mapName}{unsaved} ({map.width}x{map.height}) - {tileCount} tiles";
        }
        
        private void OnMapChanged()
        {
            PopulatePalette();
            UpdateUI();
            SetStatus("Map loaded");
        }
        
        private void PopulatePalette()
        {
            if (controller?.Palette == null) return;
            
            var palette = controller.Palette;
            
            // Clear
            _tileContainer?.Clear();
            _entityContainer?.Clear();
            _tileButtons.Clear();
            _entityButtons.Clear();
            
            // Categories
            var categories = new List<string> { "All" };
            categories.AddRange(palette.GetTileCategories());
            _categoryDropdown?.SetValueWithoutNotify("All");
            if (_categoryDropdown != null)
                _categoryDropdown.choices = categories;
            
            // Tiles
            foreach (var tile in palette.tiles)
            {
                var btn = CreateTileButton(tile.id, tile.displayName, tile.sprite, false);
                btn.RegisterCallback<ClickEvent>(_ => controller.SelectTile(tile.id));
                btn.userData = tile.id;
                btn.AddToClassList(tile.category);
                _tileContainer?.Add(btn);
                _tileButtons.Add(btn);
            }
            
            // Entities
            foreach (var entity in palette.entities)
            {
                var btn = CreateTileButton(entity.id, entity.displayName, entity.icon, true);
                btn.RegisterCallback<ClickEvent>(_ => controller.SelectEntity(entity.id));
                btn.userData = entity.id;
                _entityContainer?.Add(btn);
                _entityButtons.Add(btn);
            }
            
            UpdateUI();
        }
        
        private Button CreateTileButton(string id, string name, Sprite sprite, bool isEntity)
        {
            var btn = new Button();
            btn.AddToClassList("tile-button");
            btn.tooltip = name;
            
            var img = new VisualElement();
            img.AddToClassList("tile-image");
            
            if (sprite != null)
            {
                img.style.backgroundImage = new StyleBackground(sprite);
            }
            else if (defaultTileTexture != null)
            {
                img.style.backgroundImage = new StyleBackground(defaultTileTexture);
            }
            else
            {
                img.style.backgroundColor = isEntity ? 
                    new StyleColor(Color.yellow) : 
                    new StyleColor(Color.gray);
            }
            
            btn.Add(img);
            
            var label = new Label(name);
            label.AddToClassList("tile-label");
            btn.Add(label);
            
            return btn;
        }
        
        private void FilterTiles(string category)
        {
            foreach (var btn in _tileButtons)
            {
                bool show = category == "All" || btn.ClassListContains(category);
                btn.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        private void SetSelected(Button btn, bool selected)
        {
            if (btn == null) return;
            
            if (selected)
                btn.AddToClassList("selected");
            else
                btn.RemoveFromClassList("selected");
        }
        
        private void ShowNewMapDialog()
        {
            _inputMapName?.SetValueWithoutNotify("NewMap");
            _inputWidth?.SetValueWithoutNotify(50);
            _inputHeight?.SetValueWithoutNotify(30);
            _newMapDialog?.RemoveFromClassList("hidden");
        }
        
        private void CreateNewMap()
        {
            string name = _inputMapName?.value ?? "NewMap";
            int width = _inputWidth?.value ?? 50;
            int height = _inputHeight?.value ?? 30;
            
            controller?.CreateMap(name, width, height);
            HideDialogs();
            SetStatus($"Created new map: {name}");
        }
        
        private void ShowLoadDialog()
        {
            _selectedFile = null;
            _fileList?.Clear();
            
            var files = controller?.GetMapList();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var item = new Button { text = file };
                    item.AddToClassList("file-item");
                    item.RegisterCallback<ClickEvent>(_ => SelectFile(item, file));
                    _fileList?.Add(item);
                }
            }
            
            _loadDialog?.RemoveFromClassList("hidden");
        }
        
        private void SelectFile(Button btn, string file)
        {
            // Deselect all
            foreach (var child in _fileList.Children())
            {
                if (child is Button b) b.RemoveFromClassList("selected");
            }
            
            btn.AddToClassList("selected");
            _selectedFile = file;
        }
        
        private void LoadSelectedMap()
        {
            if (string.IsNullOrEmpty(_selectedFile)) return;
            
            controller?.LoadMap(_selectedFile);
            HideDialogs();
        }
        
        private void HideDialogs()
        {
            _newMapDialog?.AddToClassList("hidden");
            _loadDialog?.AddToClassList("hidden");
        }
        
        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }
        
        private void ShowError(string error)
        {
            SetStatus($"Error: {error}");
            Debug.LogError($"[MapEditor] {error}");
        }
    }
}
