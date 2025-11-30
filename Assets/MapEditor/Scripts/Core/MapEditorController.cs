using System;
using System.Collections.Generic;
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
        [SerializeField] private int maxUndoSteps = 50;
        
        public EditorState State { get; private set; }
        public TilePalette Palette => palette;
        
        private MapFileService _fileService;
        
        // Undo/Redo
        private readonly Stack<UndoAction> _undoStack = new Stack<UndoAction>();
        private readonly Stack<UndoAction> _redoStack = new Stack<UndoAction>();
        
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        
        // Events
        public event Action OnMapChanged;
        public event Action<string> OnError;
        public event Action OnTilePlaced;
        public event Action OnMapSaved;
        public event Action OnUndo;
        public event Action OnRedo;
        
        private void Awake()
        {
            State = new EditorState();
            _fileService = new MapFileService();
        }
        
        #region Map Operations
        
        public void CreateMap(string name, int width = 0, int height = 0)
        {
            if (width <= 0) width = defaultWidth;
            if (height <= 0) height = defaultHeight;
            
            State.Map = MapData.Create(name, width, height);
            State.CurrentFileName = null;
            State.HasUnsavedChanges = false;
            
            // Clear undo history
            _undoStack.Clear();
            _redoStack.Clear();
            
            // Center camera on map
            State.CameraOffset = new Vector2(width / 2f, height / 2f);
            
            // Select first tile if available
            if (palette != null && palette.tiles.Count > 0)
                State.SelectedTileId = palette.tiles[0].id;
            
            OnMapChanged?.Invoke();
            State.NotifyChanged();
            
            Debug.Log($"[MapEditor] Created map '{name}' ({width}x{height})");
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
                OnMapSaved?.Invoke();
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
                
                // Clear undo history
                _undoStack.Clear();
                _redoStack.Clear();
                
                OnMapChanged?.Invoke();
                State.NotifyChanged();
            }
            catch (Exception e) { OnError?.Invoke(e.Message); }
        }
        
        public string[] GetMapList() => _fileService.GetMapList();
        
        #endregion
        
        #region Tool Operations
        
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
        
        public void SelectPrefab(string prefabId)
        {
            State.SelectedPrefabId = prefabId;
            State.ActiveTool = EditorTool.Prefab;
            State.NotifyChanged();
        }
        
        public void ApplyToolAt(Vector2Int pos)
        {
            if (State.Map == null) return;
            
            // Check bounds
            if (pos.x < 0 || pos.x >= State.Map.width || pos.y < 0 || pos.y >= State.Map.height)
                return;
            
            var layer = State.Map.GetLayer(State.ActiveLayer);
            if (layer == null) return;
            
            switch (State.ActiveTool)
            {
                case EditorTool.Brush:
                    PlaceTile(layer, pos);
                    break;
                    
                case EditorTool.Eraser:
                    EraseTile(layer, pos);
                    ErasePrefab(pos);
                    break;
                    
                case EditorTool.Prefab:
                    PlacePrefab(pos);
                    break;
                    
                case EditorTool.Eyedropper:
                    PickTileOrPrefab(layer, pos);
                    break;
            }
        }
        
        private void PickTileOrPrefab(LayerData layer, Vector2Int pos)
        {
            // First check for prefab at position
            var prefab = State.Map.GetPrefabAt(pos);
            if (prefab != null)
            {
                State.SelectedPrefabId = prefab.prefabId;
                State.ActiveTool = EditorTool.Prefab;
                State.NotifyChanged();
                return;
            }
            
            // Then check for tile
            var tile = layer.GetTile(pos);
            if (tile != null)
            {
                State.SelectedTileId = tile.tileId;
                State.ActiveTool = EditorTool.Brush;
                State.NotifyChanged();
            }
        }
        
        private void PlaceTile(LayerData layer, Vector2Int pos)
        {
            if (string.IsNullOrEmpty(State.SelectedTileId)) return;
            
            var existingTile = layer.GetTile(pos);
            var tileDef = palette?.GetTile(State.SelectedTileId);
            
            // Create undo action
            var action = new TileUndoAction
            {
                Layer = layer,
                Position = pos,
                OldTile = existingTile != null ? CloneTile(existingTile) : null,
                NewTile = new TileData
                {
                    x = pos.x,
                    y = pos.y,
                    tileId = State.SelectedTileId,
                    hasCollision = tileDef?.hasCollision ?? false
                }
            };
            
            // Apply
            layer.SetTile(action.NewTile);
            State.HasUnsavedChanges = true;
            
            // Record undo
            RecordUndo(action);
            
            OnTilePlaced?.Invoke();
        }
        
        private void EraseTile(LayerData layer, Vector2Int pos)
        {
            var existingTile = layer.GetTile(pos);
            if (existingTile == null) return;
            
            // Create undo action
            var action = new TileUndoAction
            {
                Layer = layer,
                Position = pos,
                OldTile = CloneTile(existingTile),
                NewTile = null
            };
            
            // Apply
            layer.RemoveTile(pos);
            State.HasUnsavedChanges = true;
            
            // Record undo
            RecordUndo(action);
            
            OnTilePlaced?.Invoke();
        }
        
        private void ErasePrefab(Vector2Int pos)
        {
            var existingPrefab = State.Map.GetPrefabAt(pos);
            if (existingPrefab == null) return;
            
            var action = new PrefabUndoAction
            {
                Map = State.Map,
                Prefab = ClonePrefab(existingPrefab),
                WasAdded = false
            };
            
            State.Map.RemovePrefabAt(pos);
            State.HasUnsavedChanges = true;
            
            RecordUndo(action);
            OnTilePlaced?.Invoke();
        }
        
        private void PlacePrefab(Vector2Int pos)
        {
            if (string.IsNullOrEmpty(State.SelectedPrefabId)) return;
            
            // Remove existing prefab at position
            var existingPrefab = State.Map.GetPrefabAt(pos);
            if (existingPrefab != null)
            {
                State.Map.RemovePrefabAt(pos);
            }
            
            var prefab = new PrefabData
            {
                instanceId = $"{State.SelectedPrefabId}_{DateTime.Now.Ticks}",
                prefabId = State.SelectedPrefabId,
                x = pos.x,
                y = pos.y,
                rotation = 0f,
                scale = Vector3.one
            };
            
            var action = new PrefabUndoAction
            {
                Map = State.Map,
                Prefab = prefab,
                OldPrefab = existingPrefab != null ? ClonePrefab(existingPrefab) : null,
                WasAdded = true
            };
            
            State.Map.prefabs.Add(prefab);
            State.HasUnsavedChanges = true;
            
            RecordUndo(action);
            
            OnTilePlaced?.Invoke();
        }
        
        #endregion
        
        #region Layer Operations
        
        public void ClearLayer(LayerType layerType)
        {
            var layer = State.Map?.GetLayer(layerType);
            if (layer == null || layer.tiles.Count == 0) return;
            
            // Create undo action with all tiles
            var action = new LayerClearUndoAction
            {
                Layer = layer,
                Tiles = new List<TileData>()
            };
            
            foreach (var tile in layer.tiles)
            {
                action.Tiles.Add(CloneTile(tile));
            }
            
            layer.Clear();
            State.HasUnsavedChanges = true;
            
            RecordUndo(action);
            
            OnTilePlaced?.Invoke();
        }
        
        public void FillLayer(LayerType layerType, string tileId)
        {
            var layer = State.Map?.GetLayer(layerType);
            if (layer == null || string.IsNullOrEmpty(tileId)) return;
            
            var tileDef = palette?.GetTile(tileId);
            bool hasCollision = tileDef?.hasCollision ?? false;
            
            // Create undo action
            var action = new LayerFillUndoAction
            {
                Layer = layer,
                OldTiles = new List<TileData>(),
                NewTileId = tileId,
                Width = State.Map.width,
                Height = State.Map.height
            };
            
            foreach (var tile in layer.tiles)
            {
                action.OldTiles.Add(CloneTile(tile));
            }
            
            // Fill
            layer.Clear();
            for (int x = 0; x < State.Map.width; x++)
            {
                for (int y = 0; y < State.Map.height; y++)
                {
                    layer.SetTile(new TileData
                    {
                        x = x,
                        y = y,
                        tileId = tileId,
                        hasCollision = hasCollision
                    });
                }
            }
            
            State.HasUnsavedChanges = true;
            RecordUndo(action);
            OnTilePlaced?.Invoke();
        }
        
        public void ClearPrefabs()
        {
            if (State.Map == null || State.Map.prefabs.Count == 0) return;
            
            var action = new PrefabsClearUndoAction
            {
                Map = State.Map,
                Prefabs = new List<PrefabData>()
            };
            
            foreach (var prefab in State.Map.prefabs)
            {
                action.Prefabs.Add(ClonePrefab(prefab));
            }
            
            State.Map.prefabs.Clear();
            State.HasUnsavedChanges = true;
            
            RecordUndo(action);
            OnTilePlaced?.Invoke();
        }
        
        #endregion
        
        #region Undo/Redo
        
        private void RecordUndo(UndoAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear();
            
            // Limit undo stack size
            while (_undoStack.Count > maxUndoSteps)
            {
                // Remove oldest (this is inefficient but simple)
                var temp = new Stack<UndoAction>();
                while (_undoStack.Count > 1)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack.Pop(); // Remove oldest
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }
            
            State.NotifyChanged();
        }
        
        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            
            var action = _undoStack.Pop();
            action.Undo();
            _redoStack.Push(action);
            
            State.HasUnsavedChanges = true;
            OnUndo?.Invoke();
            OnTilePlaced?.Invoke();
            State.NotifyChanged();
        }
        
        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            
            var action = _redoStack.Pop();
            action.Redo();
            _undoStack.Push(action);
            
            State.HasUnsavedChanges = true;
            OnRedo?.Invoke();
            OnTilePlaced?.Invoke();
            State.NotifyChanged();
        }
        
        private TileData CloneTile(TileData tile)
        {
            return new TileData
            {
                x = tile.x,
                y = tile.y,
                tileId = tile.tileId,
                hasCollision = tile.hasCollision
            };
        }
        
        private PrefabData ClonePrefab(PrefabData prefab)
        {
            return new PrefabData
            {
                instanceId = prefab.instanceId,
                prefabId = prefab.prefabId,
                x = prefab.x,
                y = prefab.y,
                rotation = prefab.rotation,
                scale = prefab.scale,
                customData = prefab.customData
            };
        }
        
        #endregion
        
        #region View
        
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
        
        public string GetMapsFolder() => _fileService.GetMapsFolder();
        
        #endregion
    }
    
    #region Undo Actions
    
    public abstract class UndoAction
    {
        public abstract void Undo();
        public abstract void Redo();
    }
    
    public class TileUndoAction : UndoAction
    {
        public LayerData Layer;
        public Vector2Int Position;
        public TileData OldTile;
        public TileData NewTile;
        
        public override void Undo()
        {
            if (OldTile != null)
                Layer.SetTile(OldTile);
            else
                Layer.RemoveTile(Position);
        }
        
        public override void Redo()
        {
            if (NewTile != null)
                Layer.SetTile(NewTile);
            else
                Layer.RemoveTile(Position);
        }
    }
    
    public class PrefabUndoAction : UndoAction
    {
        public MapData Map;
        public PrefabData Prefab;
        public PrefabData OldPrefab;
        public bool WasAdded;
        
        public override void Undo()
        {
            if (WasAdded)
            {
                Map.prefabs.Remove(Prefab);
                if (OldPrefab != null)
                    Map.prefabs.Add(OldPrefab);
            }
            else
            {
                Map.prefabs.Add(Prefab);
            }
        }
        
        public override void Redo()
        {
            if (WasAdded)
            {
                if (OldPrefab != null)
                    Map.prefabs.Remove(OldPrefab);
                Map.prefabs.Add(Prefab);
            }
            else
            {
                Map.prefabs.Remove(Prefab);
            }
        }
    }
    
    public class PrefabsClearUndoAction : UndoAction
    {
        public MapData Map;
        public List<PrefabData> Prefabs;
        
        public override void Undo()
        {
            foreach (var prefab in Prefabs)
            {
                Map.prefabs.Add(prefab);
            }
        }
        
        public override void Redo()
        {
            Map.prefabs.Clear();
        }
    }
    
    public class LayerClearUndoAction : UndoAction
    {
        public LayerData Layer;
        public List<TileData> Tiles;
        
        public override void Undo()
        {
            foreach (var tile in Tiles)
            {
                Layer.SetTile(tile);
            }
        }
        
        public override void Redo()
        {
            Layer.Clear();
        }
    }
    
    public class LayerFillUndoAction : UndoAction
    {
        public LayerData Layer;
        public List<TileData> OldTiles;
        public string NewTileId;
        public int Width, Height;
        
        public override void Undo()
        {
            Layer.Clear();
            foreach (var tile in OldTiles)
            {
                Layer.SetTile(tile);
            }
        }
        
        public override void Redo()
        {
            Layer.Clear();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Layer.SetTile(new TileData
                    {
                        x = x,
                        y = y,
                        tileId = NewTileId,
                        hasCollision = false
                    });
                }
            }
        }
    }
    
    #endregion
}
