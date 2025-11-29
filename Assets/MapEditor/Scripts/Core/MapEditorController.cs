using System;
using UnityEngine;
using MapEditor.Data;
using MapEditor.Services;

namespace MapEditor.Core
{
    public class MapEditorController : MonoBehaviour
    {
        [SerializeField] private TilePalette palette;
        [SerializeField] private int defaultWidth = 50;
        [SerializeField] private int defaultHeight = 30;
        
        public EditorState State { get; private set; }
        public TilePalette Palette => palette;
        
        private MapFileService _fileService;
        
        public event Action OnMapChanged;
        public event Action<string> OnError;
        public event Action OnTilePlaced;
        
        private void Awake()
        {
            State = new EditorState();
            _fileService = new MapFileService();
        }
        
        public void CreateMap(string name, int width = 0, int height = 0)
        {
            if (width <= 0) width = defaultWidth;
            if (height <= 0) height = defaultHeight;
            
            State.Map = MapData.Create(name, width, height);
            State.CurrentFileName = null;
            State.HasUnsavedChanges = false;
            
            // Center camera on map
            State.CameraOffset = new Vector2(width / 2f, height / 2f);
            
            // Select first tile if available
            if (palette != null && palette.tiles.Count > 0)
                State.SelectedTileId = palette.tiles[0].id;
            
            OnMapChanged?.Invoke();
            State.NotifyChanged();
            
            Debug.Log($"[MapEditor] Created map '{name}' ({width}x{height}), selected tile: {State.SelectedTileId}");
        }
        
        public void SaveMap(string fileName = null)
        {
            if (State.Map == null) { OnError?.Invoke("No map to save"); return; }
            
            fileName ??= State.CurrentFileName ?? State.Map.mapName;
            if (string.IsNullOrEmpty(fileName)) { OnError?.Invoke("No filename"); return; }
            
            try
            {
                _fileService.Save(State.Map, fileName);
                State.CurrentFileName = fileName;
                State.HasUnsavedChanges = false;
                State.NotifyChanged();
            }
            catch (Exception e) { OnError?.Invoke(e.Message); }
        }
        
        public void LoadMap(string fileName)
        {
            try
            {
                var map = _fileService.Load(fileName);
                if (map == null) { OnError?.Invoke("Failed to load"); return; }
                
                State.Map = map;
                State.CurrentFileName = fileName;
                State.HasUnsavedChanges = false;
                OnMapChanged?.Invoke();
                State.NotifyChanged();
            }
            catch (Exception e) { OnError?.Invoke(e.Message); }
        }
        
        public string[] GetMapList() => _fileService.GetMapList();
        
        public void SetTool(EditorTool tool)
        {
            State.ActiveTool = tool;
            State.NotifyChanged();
        }
        
        public void SetLayer(LayerType layer)
        {
            State.ActiveLayer = layer;
            State.NotifyChanged();
        }
        
        public void SelectTile(string tileId)
        {
            State.SelectedTileId = tileId;
            State.ActiveTool = EditorTool.Brush;
            State.NotifyChanged();
        }
        
        public void SelectEntity(string entityId)
        {
            State.SelectedEntityId = entityId;
            State.ActiveTool = EditorTool.Entity;
            State.NotifyChanged();
        }
        
        // Called when user clicks/drags on canvas
        public void ApplyToolAt(Vector2Int pos)
        {
            if (State.Map == null) return;
            
            var layer = State.Map.GetLayer(State.ActiveLayer);
            if (layer == null) return;
            
            switch (State.ActiveTool)
            {
                case EditorTool.Brush:
                    if (string.IsNullOrEmpty(State.SelectedTileId)) return;
                    var tileDef = palette?.GetTile(State.SelectedTileId);
                    layer.SetTile(new TileData
                    {
                        x = pos.x,
                        y = pos.y,
                        tileId = State.SelectedTileId,
                        hasCollision = tileDef?.hasCollision ?? false
                    });
                    State.HasUnsavedChanges = true;
                    OnTilePlaced?.Invoke();
                    break;
                    
                case EditorTool.Eraser:
                    if (layer.RemoveTile(pos))
                    {
                        State.HasUnsavedChanges = true;
                        OnTilePlaced?.Invoke();
                    }
                    break;
                    
                case EditorTool.Entity:
                    if (string.IsNullOrEmpty(State.SelectedEntityId)) return;
                    State.Map.entities.Add(new EntityData
                    {
                        entityId = $"{State.SelectedEntityId}_{DateTime.Now.Ticks}",
                        entityType = State.SelectedEntityId,
                        x = pos.x,
                        y = pos.y
                    });
                    State.HasUnsavedChanges = true;
                    OnTilePlaced?.Invoke();
                    break;
            }
        }
        
        public void SetHoveredTile(Vector2Int pos)
        {
            if (State.HoveredTile != pos)
            {
                State.HoveredTile = pos;
                State.NotifyChanged();
            }
        }
        
        public void Pan(Vector2 delta)
        {
            State.CameraOffset += delta;
            State.NotifyChanged();
        }
        
        public void Zoom(float delta)
        {
            State.SetZoom(State.Zoom + delta * 0.1f);
        }
        
        public void ClearLayer(LayerType layer)
        {
            State.Map?.GetLayer(layer)?.Clear();
            State.HasUnsavedChanges = true;
            OnTilePlaced?.Invoke();
        }
        
        public string GetMapsFolder() => _fileService.GetMapsFolder();
    }
}
