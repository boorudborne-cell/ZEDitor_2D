using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MapEditor.Data;
using MapEditor.Services;

namespace MapEditor.Core
{
    /// <summary>
    /// Main controller for the map editor
    /// Coordinates between UI, tools, and services
    /// </summary>
    public class MapEditorController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TilePalette tilePalette;
        [SerializeField] private int defaultMapWidth = 50;
        [SerializeField] private int defaultMapHeight = 30;
        
        [Header("Performance Settings")]
        [SerializeField] private bool useAsyncFileOperations = true;
        
        // State and services
        public EditorState State { get; private set; }
        public ToolManager Tools { get; private set; }
        public TilePalette Palette => tilePalette;
        
        private MapFileService _fileService;
        private CancellationTokenSource _currentOperation;
        
        // Events
        public event Action OnMapCreated;
        public event Action OnMapLoaded;
        public event Action OnMapSaved;
        public event Action<string> OnError;
        public event Action OnStateChanged;
        public event Action<float> OnLoadProgress;
        
        private void Awake()
        {
            State = new EditorState();
            _fileService = new MapFileService();
            
            if (tilePalette != null)
            {
                Tools = new ToolManager(State, tilePalette);
                Tools.SetTool(EditorTool.Brush);
            }
            else
            {
                Debug.LogWarning("[MapEditorController] No TilePalette assigned!");
            }
        }
        
        private void OnDestroy()
        {
            _currentOperation?.Cancel();
            _currentOperation?.Dispose();
        }
        
        #region Map Operations
        
        /// <summary>
        /// Creates a new empty map
        /// </summary>
        public void CreateNewMap(string mapName, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                OnError?.Invoke("Invalid map dimensions");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(mapName))
                mapName = "Untitled";
            
            // Cancel any pending operations
            _currentOperation?.Cancel();
            
            State.Reset();
            State.CurrentMap = MapData.CreateNew(mapName, width, height);
            State.HasUnsavedChanges = false;
            
            Debug.Log($"[MapEditorController] Created new map: {mapName} ({width}x{height})");
            
            OnMapCreated?.Invoke();
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Creates a new map with default dimensions
        /// </summary>
        public void CreateNewMap(string mapName)
        {
            CreateNewMap(mapName, defaultMapWidth, defaultMapHeight);
        }
        
        /// <summary>
        /// Saves the current map
        /// </summary>
        public async void SaveMap(string fileName = null)
        {
            if (State.CurrentMap == null)
            {
                OnError?.Invoke("No map to save");
                return;
            }
            
            fileName = fileName ?? State.CurrentFileName ?? State.CurrentMap.mapName;
            
            if (string.IsNullOrWhiteSpace(fileName))
            {
                OnError?.Invoke("Please specify a file name");
                return;
            }
            
            CancelCurrentOperation();
            
            FileOperationResult result;
            
            if (useAsyncFileOperations)
            {
                _currentOperation = new CancellationTokenSource();
                result = await _fileService.SaveMapAsync(State.CurrentMap, fileName, _currentOperation.Token);
            }
            else
            {
                result = _fileService.SaveMap(State.CurrentMap, fileName);
            }
            
            if (result.Success)
            {
                State.CurrentFileName = fileName;
                State.HasUnsavedChanges = false;
                OnMapSaved?.Invoke();
                OnStateChanged?.Invoke();
            }
            else
            {
                OnError?.Invoke(result.ErrorMessage);
            }
        }
        
        /// <summary>
        /// Loads a map from file
        /// </summary>
        public async void LoadMap(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                OnError?.Invoke("Please specify a file name");
                return;
            }
            
            CancelCurrentOperation();
            
            OnLoadProgress?.Invoke(0f);
            
            FileOperationResult result;
            
            if (useAsyncFileOperations)
            {
                _currentOperation = new CancellationTokenSource();
                result = await _fileService.LoadMapAsync(fileName, _currentOperation.Token);
            }
            else
            {
                result = _fileService.LoadMap(fileName);
            }
            
            OnLoadProgress?.Invoke(1f);
            
            if (result.Success)
            {
                State.Reset();
                State.CurrentMap = result.Data;
                State.CurrentFileName = fileName;
                State.HasUnsavedChanges = false;
                
                Debug.Log($"[MapEditorController] Loaded map in {result.LoadTime:F3}s");
                
                OnMapLoaded?.Invoke();
                OnStateChanged?.Invoke();
            }
            else
            {
                OnError?.Invoke(result.ErrorMessage);
            }
        }
        
        /// <summary>
        /// Gets list of available map files
        /// </summary>
        public string[] GetAvailableMaps()
        {
            return _fileService.GetAvailableMaps();
        }
        
        /// <summary>
        /// Deletes a map file
        /// </summary>
        public bool DeleteMap(string fileName)
        {
            if (_fileService.DeleteMap(fileName))
            {
                // If deleted current map, reset
                if (State.CurrentFileName == fileName)
                {
                    State.Reset();
                    OnStateChanged?.Invoke();
                }
                return true;
            }
            return false;
        }
        
        #endregion
        
        #region Layer Operations
        
        /// <summary>
        /// Sets the active layer for editing
        /// </summary>
        public void SetActiveLayer(LayerType layer)
        {
            State.ActiveLayer = layer;
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Toggles layer visibility
        /// </summary>
        public void SetLayerVisibility(LayerType layer, bool visible)
        {
            if (State.CurrentMap == null)
                return;
            
            var layerData = State.CurrentMap.GetLayer(layer);
            if (layerData != null)
            {
                layerData.isVisible = visible;
                OnStateChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Clears all tiles from a layer
        /// </summary>
        public void ClearLayer(LayerType layer)
        {
            if (State.CurrentMap == null)
                return;
            
            var layerData = State.CurrentMap.GetLayer(layer);
            if (layerData != null)
            {
                layerData.Clear();
                State.HasUnsavedChanges = true;
                OnStateChanged?.Invoke();
            }
        }
        
        #endregion
        
        #region View Operations
        
        /// <summary>
        /// Zooms in the editor view
        /// </summary>
        public void ZoomIn()
        {
            State.ZoomIn();
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Zooms out the editor view
        /// </summary>
        public void ZoomOut()
        {
            State.ZoomOut();
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Sets zoom level directly
        /// </summary>
        public void SetZoom(float zoom)
        {
            State.Zoom = Mathf.Clamp(zoom, EditorState.MIN_ZOOM, EditorState.MAX_ZOOM);
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Toggles grid visibility
        /// </summary>
        public void ToggleGrid()
        {
            State.ShowGrid = !State.ShowGrid;
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Toggles collision visualization
        /// </summary>
        public void ToggleCollisions()
        {
            State.ShowCollisions = !State.ShowCollisions;
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Sets camera position
        /// </summary>
        public void SetCameraPosition(Vector2 position)
        {
            State.CameraPosition = position;
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Centers view on map
        /// </summary>
        public void CenterView()
        {
            if (State.CurrentMap == null)
                return;
            
            State.CameraPosition = new Vector2(
                State.CurrentMap.width * State.CurrentMap.tileSize / 2f,
                State.CurrentMap.height * State.CurrentMap.tileSize / 2f
            );
            OnStateChanged?.Invoke();
        }
        
        #endregion
        
        #region Tool Operations
        
        /// <summary>
        /// Selects a tile from the palette
        /// </summary>
        public void SelectTile(string tileId)
        {
            State.SelectedTileId = tileId;
            State.SelectedEntityType = null;
            
            if (State.ActiveTool == EditorTool.EntityPlace || State.ActiveTool == EditorTool.EntitySelect)
            {
                Tools.SetTool(EditorTool.Brush);
            }
            
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Selects an entity type for placement
        /// </summary>
        public void SelectEntity(string entityType)
        {
            State.SelectedEntityType = entityType;
            State.SelectedTileId = null;
            Tools.SetTool(EditorTool.EntityPlace);
            OnStateChanged?.Invoke();
        }
        
        /// <summary>
        /// Sets the active tool
        /// </summary>
        public void SetTool(EditorTool tool)
        {
            Tools.SetTool(tool);
            OnStateChanged?.Invoke();
        }
        
        #endregion
        
        #region Input Handling
        
        /// <summary>
        /// Converts screen position to tile coordinates
        /// </summary>
        public Vector2Int ScreenToTile(Vector2 screenPosition, Camera camera)
        {
            if (State.CurrentMap == null || camera == null)
                return Vector2Int.zero;
            
            Vector3 worldPos = camera.ScreenToWorldPoint(screenPosition);
            float tileSize = State.CurrentMap.tileSize;
            
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / tileSize),
                Mathf.FloorToInt(worldPos.y / tileSize)
            );
        }
        
        /// <summary>
        /// Handles pointer down on canvas
        /// </summary>
        public void HandlePointerDown(Vector2Int tilePosition)
        {
            Tools?.OnPointerDown(tilePosition);
        }
        
        /// <summary>
        /// Handles pointer drag on canvas
        /// </summary>
        public void HandlePointerDrag(Vector2Int tilePosition)
        {
            Tools?.OnPointerDrag(tilePosition);
        }
        
        /// <summary>
        /// Handles pointer up on canvas
        /// </summary>
        public void HandlePointerUp(Vector2Int tilePosition)
        {
            Tools?.OnPointerUp(tilePosition);
        }
        
        #endregion
        
        #region Utility
        
        private void CancelCurrentOperation()
        {
            if (_currentOperation != null)
            {
                _currentOperation.Cancel();
                _currentOperation.Dispose();
                _currentOperation = null;
            }
        }
        
        /// <summary>
        /// Gets the maps directory path
        /// </summary>
        public string GetMapsDirectory()
        {
            return _fileService.GetMapsDirectory();
        }
        
        #endregion
    }
}
