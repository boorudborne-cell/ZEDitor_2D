using System;
using System.Collections;
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
        [SerializeField] private PlayModeController playModeController;
        [SerializeField] private Texture2D defaultTileTexture;
        
        [Header("Toast Settings")]
        [SerializeField] private float toastDuration = 3f;
        
        private VisualElement _root;
        
        // Toolbar
        private Button _btnUndo, _btnRedo;
        private Button _btnBrush, _btnEraser, _btnPrefab, _btnEyedropper;
        private Button _btnZoomIn, _btnZoomOut;
        private Label _zoomLabel;
        private Toggle _toggleCollision, _toggleGrid;
        private Button _btnNew, _btnSave, _btnLoad;
        private Button _btnPlay;
        private Button _btnShortcuts;
        private VisualElement _unsavedIndicator;
        
        // Layers
        private Button _btnLayerBg, _btnLayerGround, _btnLayerFg;
        private Button _btnClearLayer, _btnFillLayer;
        
        // Palette
        private DropdownField _categoryDropdown;
        private VisualElement _tileContainer;
        private VisualElement _prefabContainer;
        
        // Status
        private Label _statusText, _positionText, _layerText, _mapInfo;
        
        // Toast
        private VisualElement _toastContainer;
        
        // Dialogs
        private VisualElement _newMapDialog, _loadDialog, _confirmDialog;
        private TextField _inputMapName;
        private IntegerField _inputWidth, _inputHeight;
        private VisualElement _fileList;
        private string _selectedFile;
        
        // Confirm dialog
        private Label _confirmTitle, _confirmMessage;
        private Button _btnConfirmCancel, _btnConfirmDiscard, _btnConfirmSave;
        private Action _pendingAction;
        
        // Shortcuts panel
        private VisualElement _shortcutsPanel;
        
        // Tracking
        private readonly List<Button> _tileButtons = new List<Button>();
        private readonly List<Button> _prefabButtons = new List<Button>();
        private readonly List<Coroutine> _activeToasts = new List<Coroutine>();
        
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
            BindToast();
            BindDialogs();
            BindShortcuts();
        }
        
        private void Start()
        {
            if (controller != null)
            {
                if (controller.State != null)
                    controller.State.OnChanged += UpdateUI;
                controller.OnMapChanged += OnMapChanged;
                controller.OnError += ShowError;
                controller.OnTilePlaced += OnTilePlaced;
                controller.OnMapSaved += OnMapSaved;
                controller.OnUndo += () => ShowToast("Undo", ToastType.Info);
                controller.OnRedo += () => ShowToast("Redo", ToastType.Info);
            }
            
            if (playModeController != null)
            {
                playModeController.OnPlayModeStarted += OnPlayModeStarted;
                playModeController.OnPlayModeStopped += OnPlayModeStopped;
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
            controller.OnTilePlaced -= OnTilePlaced;
            controller.OnMapSaved -= OnMapSaved;
            
            if (playModeController != null)
            {
                playModeController.OnPlayModeStarted -= OnPlayModeStarted;
                playModeController.OnPlayModeStopped -= OnPlayModeStopped;
            }
        }
        
        #region Binding
        
        private void BindToolbar()
        {
            // Undo/Redo
            _btnUndo = _root.Q<Button>("btn-undo");
            _btnRedo = _root.Q<Button>("btn-redo");
            _btnUndo?.RegisterCallback<ClickEvent>(_ => controller?.Undo());
            _btnRedo?.RegisterCallback<ClickEvent>(_ => controller?.Redo());
            
            // Tools
            _btnBrush = _root.Q<Button>("btn-brush");
            _btnEraser = _root.Q<Button>("btn-eraser");
            _btnPrefab = _root.Q<Button>("btn-prefab");
            _btnEyedropper = _root.Q<Button>("btn-eyedropper");
            
            _btnBrush?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Brush));
            _btnEraser?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Eraser));
            _btnPrefab?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Prefab));
            _btnEyedropper?.RegisterCallback<ClickEvent>(_ => controller?.SetTool(EditorTool.Eyedropper));
            
            // Zoom
            _btnZoomIn = _root.Q<Button>("btn-zoom-in");
            _btnZoomOut = _root.Q<Button>("btn-zoom-out");
            _zoomLabel = _root.Q<Label>("zoom-label");
            
            _btnZoomIn?.RegisterCallback<ClickEvent>(_ => controller?.Zoom(1f));
            _btnZoomOut?.RegisterCallback<ClickEvent>(_ => controller?.Zoom(-1f));
            
            // Toggles
            _toggleCollision = _root.Q<Toggle>("toggle-collision");
            _toggleGrid = _root.Q<Toggle>("toggle-grid");
            
            _toggleCollision?.RegisterValueChangedCallback(evt =>
            {
                if (controller?.State != null)
                {
                    controller.State.ShowCollisions = evt.newValue;
                    controller.State.NotifyChanged();
                }
            });
            
            _toggleGrid?.RegisterValueChangedCallback(evt =>
            {
                if (controller?.State != null)
                {
                    controller.State.ShowGrid = evt.newValue;
                    controller.State.NotifyChanged();
                }
            });
            
            // File operations
            _btnNew = _root.Q<Button>("btn-new");
            _btnSave = _root.Q<Button>("btn-save");
            _btnLoad = _root.Q<Button>("btn-load");
            
            _btnNew?.RegisterCallback<ClickEvent>(_ => TryNewMap());
            _btnSave?.RegisterCallback<ClickEvent>(_ => controller?.SaveMap());
            _btnLoad?.RegisterCallback<ClickEvent>(_ => TryLoadMap());
            
            // Unsaved indicator
            _unsavedIndicator = _root.Q<VisualElement>("unsaved-indicator");
            
            // Play button
            _btnPlay = _root.Q<Button>("btn-play");
            _btnPlay?.RegisterCallback<ClickEvent>(_ => playModeController?.TogglePlayMode());
            
            // Shortcuts button
            _btnShortcuts = _root.Q<Button>("btn-shortcuts");
            _btnShortcuts?.RegisterCallback<ClickEvent>(_ => ToggleShortcutsPanel());
        }
        
        private void BindLayers()
        {
            _btnLayerBg = _root.Q<Button>("btn-layer-bg");
            _btnLayerGround = _root.Q<Button>("btn-layer-ground");
            _btnLayerFg = _root.Q<Button>("btn-layer-fg");
            _btnClearLayer = _root.Q<Button>("btn-clear-layer");
            _btnFillLayer = _root.Q<Button>("btn-fill-layer");
            
            _btnLayerBg?.RegisterCallback<ClickEvent>(_ => controller?.SetLayer(LayerType.Background));
            _btnLayerGround?.RegisterCallback<ClickEvent>(_ => controller?.SetLayer(LayerType.Ground));
            _btnLayerFg?.RegisterCallback<ClickEvent>(_ => controller?.SetLayer(LayerType.Foreground));
            
            _btnClearLayer?.RegisterCallback<ClickEvent>(_ => 
            {
                if (controller?.State != null)
                {
                    controller.ClearLayer(controller.State.ActiveLayer);
                    ShowToast($"Cleared {controller.State.ActiveLayer} layer", ToastType.Info);
                }
            });
            
            _btnFillLayer?.RegisterCallback<ClickEvent>(_ =>
            {
                if (controller?.State != null && !string.IsNullOrEmpty(controller.State.SelectedTileId))
                {
                    controller.FillLayer(controller.State.ActiveLayer, controller.State.SelectedTileId);
                    ShowToast($"Filled {controller.State.ActiveLayer} layer", ToastType.Info);
                }
                else
                {
                    ShowToast("Select a tile first", ToastType.Warning);
                }
            });
        }
        
        private void BindPalette()
        {
            _categoryDropdown = _root.Q<DropdownField>("category-dropdown");
            _tileContainer = _root.Q<VisualElement>("tile-container");
            _prefabContainer = _root.Q<VisualElement>("prefab-container");
            
            _categoryDropdown?.RegisterValueChangedCallback(evt => FilterTiles(evt.newValue));
        }
        
        private void BindStatus()
        {
            _statusText = _root.Q<Label>("status-text");
            _positionText = _root.Q<Label>("position-text");
            _layerText = _root.Q<Label>("layer-text");
            _mapInfo = _root.Q<Label>("map-info");
        }
        
        private void BindToast()
        {
            _toastContainer = _root.Q<VisualElement>("toast-container");
        }
        
        private void BindDialogs()
        {
            // New map dialog
            _newMapDialog = _root.Q<VisualElement>("new-map-dialog");
            _inputMapName = _root.Q<TextField>("input-map-name");
            _inputWidth = _root.Q<IntegerField>("input-width");
            _inputHeight = _root.Q<IntegerField>("input-height");
            
            var btnDialogCancel = _root.Q<Button>("btn-dialog-cancel");
            var btnDialogCreate = _root.Q<Button>("btn-dialog-create");
            
            btnDialogCancel?.RegisterCallback<ClickEvent>(_ => HideDialogs());
            btnDialogCreate?.RegisterCallback<ClickEvent>(_ => CreateNewMap());
            
            // Load dialog
            _loadDialog = _root.Q<VisualElement>("load-dialog");
            _fileList = _root.Q<VisualElement>("file-list");
            
            var btnLoadCancel = _root.Q<Button>("btn-load-cancel");
            var btnLoadConfirm = _root.Q<Button>("btn-load-confirm");
            
            btnLoadCancel?.RegisterCallback<ClickEvent>(_ => HideDialogs());
            btnLoadConfirm?.RegisterCallback<ClickEvent>(_ => LoadSelectedMap());
            
            // Confirm dialog
            _confirmDialog = _root.Q<VisualElement>("confirm-dialog");
            _confirmTitle = _root.Q<Label>("confirm-title");
            _confirmMessage = _root.Q<Label>("confirm-message");
            _btnConfirmCancel = _root.Q<Button>("btn-confirm-cancel");
            _btnConfirmDiscard = _root.Q<Button>("btn-confirm-discard");
            _btnConfirmSave = _root.Q<Button>("btn-confirm-save");
            
            _btnConfirmCancel?.RegisterCallback<ClickEvent>(_ => 
            {
                _pendingAction = null;
                HideDialogs();
            });
            
            _btnConfirmDiscard?.RegisterCallback<ClickEvent>(_ =>
            {
                HideDialogs();
                _pendingAction?.Invoke();
                _pendingAction = null;
            });
            
            _btnConfirmSave?.RegisterCallback<ClickEvent>(_ =>
            {
                controller?.SaveMap();
                HideDialogs();
                _pendingAction?.Invoke();
                _pendingAction = null;
            });
        }
        
        private void BindShortcuts()
        {
            _shortcutsPanel = _root.Q<VisualElement>("shortcuts-panel");
            
            // Close when clicking outside
            _root.RegisterCallback<ClickEvent>(evt =>
            {
                if (_shortcutsPanel != null && 
                    !_shortcutsPanel.ClassListContains("hidden") &&
                    !_shortcutsPanel.worldBound.Contains(evt.position) &&
                    _btnShortcuts != null &&
                    !_btnShortcuts.worldBound.Contains(evt.position))
                {
                    _shortcutsPanel.AddToClassList("hidden");
                }
            });
        }
        
        #endregion
        
        #region UI Updates
        
        private void UpdateUI()
        {
            if (controller?.State == null) return;
            var state = controller.State;
            
            // Undo/Redo buttons
            UpdateButtonEnabled(_btnUndo, controller.CanUndo);
            UpdateButtonEnabled(_btnRedo, controller.CanRedo);
            
            // Tool buttons
            SetSelected(_btnBrush, state.ActiveTool == EditorTool.Brush);
            SetSelected(_btnEraser, state.ActiveTool == EditorTool.Eraser);
            SetSelected(_btnPrefab, state.ActiveTool == EditorTool.Prefab);
            SetSelected(_btnEyedropper, state.ActiveTool == EditorTool.Eyedropper);
            
            // Layer buttons
            SetSelected(_btnLayerBg, state.ActiveLayer == LayerType.Background);
            SetSelected(_btnLayerGround, state.ActiveLayer == LayerType.Ground);
            SetSelected(_btnLayerFg, state.ActiveLayer == LayerType.Foreground);
            
            // Zoom
            if (_zoomLabel != null)
                _zoomLabel.text = $"{Mathf.RoundToInt(state.Zoom * 100)}%";
            
            // Toggles
            if (_toggleCollision != null)
                _toggleCollision.SetValueWithoutNotify(state.ShowCollisions);
            if (_toggleGrid != null)
                _toggleGrid.SetValueWithoutNotify(state.ShowGrid);
            
            // Unsaved indicator
            if (_unsavedIndicator != null)
            {
                if (state.HasUnsavedChanges)
                    _unsavedIndicator.RemoveFromClassList("hidden");
                else
                    _unsavedIndicator.AddToClassList("hidden");
            }
            
            // Tile selection
            foreach (var btn in _tileButtons)
            {
                bool selected = btn.userData as string == state.SelectedTileId;
                SetSelected(btn, selected);
            }
            
            // Prefab selection
            foreach (var btn in _prefabButtons)
            {
                bool selected = btn.userData as string == state.SelectedPrefabId;
                SetSelected(btn, selected);
            }
            
            // Position
            if (_positionText != null)
            {
                var pos = state.HoveredTile;
                _positionText.text = $"Tile: {pos.x}, {pos.y}";
            }
            
            // Layer text
            if (_layerText != null)
                _layerText.text = $"Layer: {state.ActiveLayer}";
            
            // Map info
            UpdateMapInfo();
        }
        
        private void UpdateMapInfo()
        {
            if (_mapInfo == null) return;
            
            var map = controller?.State?.Map;
            if (map == null)
            {
                _mapInfo.text = "No map loaded";
                return;
            }
            
            int tileCount = 0;
            foreach (var layer in map.layers) tileCount += layer.tiles.Count;
            
            _mapInfo.text = $"{map.mapName} ({map.width}×{map.height}) • {tileCount} tiles • {map.prefabs.Count} prefabs";
        }
        
        private void UpdateButtonEnabled(Button btn, bool enabled)
        {
            if (btn == null) return;
            btn.SetEnabled(enabled);
        }
        
        private void SetSelected(Button btn, bool selected)
        {
            if (btn == null) return;
            
            if (selected)
                btn.AddToClassList("selected");
            else
                btn.RemoveFromClassList("selected");
        }
        
        #endregion
        
        #region Events
        
        private void OnMapChanged()
        {
            PopulatePalette();
            UpdateUI();
            ShowToast("Map loaded", ToastType.Success);
        }
        
        private void OnTilePlaced()
        {
            UpdateMapInfo();
        }
        
        private void OnMapSaved()
        {
            ShowToast("Map saved", ToastType.Success);
            UpdateUI();
        }
        
        private void OnPlayModeStarted()
        {
            if (controller?.State != null)
                controller.State.IsPlayMode = true;
            
            UpdatePlayButton(true);
            SetEditorUIEnabled(false);
            ShowToast("Play Mode started (F5 or Esc to stop)", ToastType.Info);
        }
        
        private void OnPlayModeStopped()
        {
            if (controller?.State != null)
                controller.State.IsPlayMode = false;
            
            UpdatePlayButton(false);
            SetEditorUIEnabled(true);
            ShowToast("Play Mode stopped", ToastType.Info);
        }
        
        private void UpdatePlayButton(bool isPlaying)
        {
            if (_btnPlay == null) return;
            
            if (isPlaying)
            {
                _btnPlay.text = "■ Stop";
                _btnPlay.AddToClassList("playing");
            }
            else
            {
                _btnPlay.text = "▶ Play";
                _btnPlay.RemoveFromClassList("playing");
            }
        }
        
        private void SetEditorUIEnabled(bool enabled)
        {
            // Disable/enable editor controls during play mode
            _btnUndo?.SetEnabled(enabled);
            _btnRedo?.SetEnabled(enabled);
            _btnBrush?.SetEnabled(enabled);
            _btnEraser?.SetEnabled(enabled);
            _btnPrefab?.SetEnabled(enabled);
            _btnEyedropper?.SetEnabled(enabled);
            _btnNew?.SetEnabled(enabled);
            _btnSave?.SetEnabled(enabled);
            _btnLoad?.SetEnabled(enabled);
            _btnLayerBg?.SetEnabled(enabled);
            _btnLayerGround?.SetEnabled(enabled);
            _btnLayerFg?.SetEnabled(enabled);
            _btnClearLayer?.SetEnabled(enabled);
            _btnFillLayer?.SetEnabled(enabled);
            _toggleCollision?.SetEnabled(enabled);
            _toggleGrid?.SetEnabled(enabled);
            
            // Disable palette interaction
            if (_tileContainer != null)
                _tileContainer.SetEnabled(enabled);
            if (_prefabContainer != null)
                _prefabContainer.SetEnabled(enabled);
        }
        
        #endregion
        
        #region Palette
        
        private void PopulatePalette()
        {
            if (controller?.Palette == null) return;
            
            var palette = controller.Palette;
            
            _tileContainer?.Clear();
            _prefabContainer?.Clear();
            _tileButtons.Clear();
            _prefabButtons.Clear();
            
            // Categories
            var categories = new List<string> { "All" };
            categories.AddRange(palette.GetTileCategories());
            _categoryDropdown?.SetValueWithoutNotify("All");
            if (_categoryDropdown != null)
                _categoryDropdown.choices = categories;
            
            // Tiles
            foreach (var tile in palette.tiles)
            {
                var btn = CreatePaletteButton(tile.id, tile.displayName, tile.sprite, false);
                btn.RegisterCallback<ClickEvent>(_ => controller.SelectTile(tile.id));
                btn.userData = tile.id;
                btn.AddToClassList(tile.category);
                _tileContainer?.Add(btn);
                _tileButtons.Add(btn);
            }
            
            // Prefabs
            foreach (var prefab in palette.prefabs)
            {
                var btn = CreatePaletteButton(prefab.id, prefab.displayName, prefab.icon, true);
                btn.RegisterCallback<ClickEvent>(_ => controller.SelectPrefab(prefab.id));
                btn.userData = prefab.id;
                _prefabContainer?.Add(btn);
                _prefabButtons.Add(btn);
            }
            
            UpdateUI();
        }
        
        private Button CreatePaletteButton(string id, string name, Sprite sprite, bool isPrefab)
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
                img.style.backgroundColor = isPrefab ? 
                    new StyleColor(new Color(0.2f, 0.8f, 0.9f)) : 
                    new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            }
            
            btn.Add(img);
            
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
        
        #endregion
        
        #region Dialogs
        
        private void TryNewMap()
        {
            if (controller?.State?.HasUnsavedChanges == true)
            {
                ShowConfirmDialog(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save before creating a new map?",
                    ShowNewMapDialog
                );
            }
            else
            {
                ShowNewMapDialog();
            }
        }
        
        private void TryLoadMap()
        {
            if (controller?.State?.HasUnsavedChanges == true)
            {
                ShowConfirmDialog(
                    "Unsaved Changes",
                    "You have unsaved changes. Do you want to save before loading another map?",
                    ShowLoadDialog
                );
            }
            else
            {
                ShowLoadDialog();
            }
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
            int width = Mathf.Clamp(_inputWidth?.value ?? 50, 1, 500);
            int height = Mathf.Clamp(_inputHeight?.value ?? 30, 1, 500);
            
            controller?.CreateMap(name, width, height);
            HideDialogs();
            ShowToast($"Created map: {name}", ToastType.Success);
        }
        
        private void ShowLoadDialog()
        {
            _selectedFile = null;
            _fileList?.Clear();
            
            var files = controller?.GetMapList();
            if (files != null && files.Length > 0)
            {
                foreach (var file in files)
                {
                    var item = new Button { text = file };
                    item.AddToClassList("file-item");
                    item.RegisterCallback<ClickEvent>(_ => SelectFile(item, file));
                    item.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (evt.clickCount == 2)
                        {
                            LoadSelectedMap();
                        }
                    });
                    _fileList?.Add(item);
                }
            }
            else
            {
                var noFiles = new Label("No saved maps found");
                noFiles.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                noFiles.style.unityTextAlign = TextAnchor.MiddleCenter;
                noFiles.style.paddingTop = 20;
                _fileList?.Add(noFiles);
            }
            
            _loadDialog?.RemoveFromClassList("hidden");
        }
        
        private void SelectFile(Button btn, string file)
        {
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
        
        private void ShowConfirmDialog(string title, string message, Action onConfirm)
        {
            if (_confirmTitle != null) _confirmTitle.text = title;
            if (_confirmMessage != null) _confirmMessage.text = message;
            _pendingAction = onConfirm;
            _confirmDialog?.RemoveFromClassList("hidden");
        }
        
        private void HideDialogs()
        {
            _newMapDialog?.AddToClassList("hidden");
            _loadDialog?.AddToClassList("hidden");
            _confirmDialog?.AddToClassList("hidden");
        }
        
        private void ToggleShortcutsPanel()
        {
            if (_shortcutsPanel == null) return;
            
            if (_shortcutsPanel.ClassListContains("hidden"))
                _shortcutsPanel.RemoveFromClassList("hidden");
            else
                _shortcutsPanel.AddToClassList("hidden");
        }
        
        #endregion
        
        #region Toast Notifications
        
        public enum ToastType { Info, Success, Warning, Error }
        
        public void ShowToast(string message, ToastType type = ToastType.Info)
        {
            if (_toastContainer == null) return;
            
            var toast = new VisualElement();
            toast.AddToClassList("toast");
            toast.AddToClassList("hidden");
            
            switch (type)
            {
                case ToastType.Success: toast.AddToClassList("success"); break;
                case ToastType.Warning: toast.AddToClassList("warning"); break;
                case ToastType.Error: toast.AddToClassList("error"); break;
            }
            
            var text = new Label(message);
            text.AddToClassList("toast-text");
            toast.Add(text);
            
            _toastContainer.Add(toast);
            
            // Animate in
            toast.schedule.Execute(() => toast.RemoveFromClassList("hidden")).StartingIn(10);
            
            // Remove after duration
            StartCoroutine(RemoveToastAfterDelay(toast, toastDuration));
        }
        
        private IEnumerator RemoveToastAfterDelay(VisualElement toast, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            toast.AddToClassList("hidden");
            
            yield return new WaitForSeconds(0.3f);
            
            toast.RemoveFromHierarchy();
        }
        
        private void ShowError(string error)
        {
            ShowToast($"Error: {error}", ToastType.Error);
            Debug.LogError($"[MapEditor] {error}");
        }
        
        #endregion
    }
}
