using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Core
{
    /// <summary>
    /// Brush tool for placing tiles
    /// </summary>
    public class BrushTool : BaseTileTool
    {
        public override EditorTool ToolType => EditorTool.Brush;
        public override string DisplayName => "Brush";
        
        private readonly TilePalette _palette;
        
        public BrushTool(TilePalette palette)
        {
            _palette = palette;
        }
        
        public override void OnPointerDown(Vector2Int tilePosition, EditorState state)
        {
            _lastPosition = tilePosition;
            ApplyTool(tilePosition, state);
        }
        
        protected override void ApplyTool(Vector2Int position, EditorState state)
        {
            if (state.CurrentMap == null)
                return;
            
            if (string.IsNullOrEmpty(state.SelectedTileId))
                return;
            
            if (!state.CurrentMap.IsInBounds(position))
                return;
            
            var layer = state.CurrentMap.GetLayer(state.ActiveLayer);
            if (layer == null)
                return;
            
            // Get tile info from palette
            var paletteEntry = _palette?.GetTile(state.SelectedTileId);
            bool hasCollision = paletteEntry?.defaultHasCollision ?? false;
            
            // Create and place tile
            var tile = new TileData(position.x, position.y, state.SelectedTileId, hasCollision);
            layer.SetTile(tile);
            
            state.HasUnsavedChanges = true;
        }
    }
    
    /// <summary>
    /// Eraser tool for removing tiles
    /// </summary>
    public class EraserTool : BaseTileTool
    {
        public override EditorTool ToolType => EditorTool.Eraser;
        public override string DisplayName => "Eraser";
        
        public override void OnPointerDown(Vector2Int tilePosition, EditorState state)
        {
            _lastPosition = tilePosition;
            ApplyTool(tilePosition, state);
        }
        
        protected override void ApplyTool(Vector2Int position, EditorState state)
        {
            if (state.CurrentMap == null)
                return;
            
            if (!state.CurrentMap.IsInBounds(position))
                return;
            
            var layer = state.CurrentMap.GetLayer(state.ActiveLayer);
            if (layer == null)
                return;
            
            if (layer.RemoveTileAt(position))
            {
                state.HasUnsavedChanges = true;
            }
        }
    }
    
    /// <summary>
    /// Tool for placing entities
    /// </summary>
    public class EntityPlaceTool : IEditorTool
    {
        public EditorTool ToolType => EditorTool.EntityPlace;
        public string DisplayName => "Place Entity";
        
        private readonly TilePalette _palette;
        private int _entityCounter;
        
        public EntityPlaceTool(TilePalette palette)
        {
            _palette = palette;
        }
        
        public void OnToolSelected(EditorState state) { }
        public void OnToolDeselected(EditorState state) { }
        
        public void OnPointerDown(Vector2Int tilePosition, EditorState state)
        {
            if (state.CurrentMap == null)
                return;
            
            if (string.IsNullOrEmpty(state.SelectedEntityType))
                return;
            
            if (!state.CurrentMap.IsInBounds(tilePosition))
                return;
            
            // Generate unique ID
            string entityId = $"{state.SelectedEntityType}_{++_entityCounter}_{System.DateTime.Now.Ticks}";
            
            // Create entity at tile center
            Vector2 position = new Vector2(tilePosition.x + 0.5f, tilePosition.y + 0.5f);
            var entity = new EntityData(entityId, state.SelectedEntityType, position);
            
            state.CurrentMap.entities.Add(entity);
            state.HasUnsavedChanges = true;
        }
        
        public void OnPointerDrag(Vector2Int tilePosition, EditorState state) { }
        public void OnPointerUp(Vector2Int tilePosition, EditorState state) { }
    }
    
    /// <summary>
    /// Tool for selecting and manipulating entities
    /// </summary>
    public class EntitySelectTool : IEditorTool
    {
        public EditorTool ToolType => EditorTool.EntitySelect;
        public string DisplayName => "Select Entity";
        
        public EntityData SelectedEntity { get; private set; }
        
        private bool _isDragging;
        private Vector2 _dragOffset;
        
        public event System.Action<EntityData> OnEntitySelected;
        public event System.Action<EntityData> OnEntityMoved;
        
        public void OnToolSelected(EditorState state) { }
        
        public void OnToolDeselected(EditorState state)
        {
            SelectedEntity = null;
            _isDragging = false;
        }
        
        public void OnPointerDown(Vector2Int tilePosition, EditorState state)
        {
            if (state.CurrentMap == null)
                return;
            
            Vector2 worldPos = new Vector2(tilePosition.x + 0.5f, tilePosition.y + 0.5f);
            
            // Find entity at position
            EntityData hitEntity = null;
            float closestDistance = float.MaxValue;
            
            foreach (var entity in state.CurrentMap.entities)
            {
                float distance = Vector2.Distance(entity.Position, worldPos);
                if (distance < 0.75f && distance < closestDistance)
                {
                    closestDistance = distance;
                    hitEntity = entity;
                }
            }
            
            SelectedEntity = hitEntity;
            OnEntitySelected?.Invoke(SelectedEntity);
            
            if (SelectedEntity != null)
            {
                _isDragging = true;
                _dragOffset = SelectedEntity.Position - worldPos;
            }
        }
        
        public void OnPointerDrag(Vector2Int tilePosition, EditorState state)
        {
            if (!_isDragging || SelectedEntity == null)
                return;
            
            Vector2 worldPos = new Vector2(tilePosition.x + 0.5f, tilePosition.y + 0.5f);
            SelectedEntity.Position = worldPos + _dragOffset;
            
            state.HasUnsavedChanges = true;
            OnEntityMoved?.Invoke(SelectedEntity);
        }
        
        public void OnPointerUp(Vector2Int tilePosition, EditorState state)
        {
            _isDragging = false;
        }
        
        /// <summary>
        /// Deletes the currently selected entity
        /// </summary>
        public bool DeleteSelected(EditorState state)
        {
            if (SelectedEntity == null || state.CurrentMap == null)
                return false;
            
            bool removed = state.CurrentMap.entities.Remove(SelectedEntity);
            if (removed)
            {
                SelectedEntity = null;
                state.HasUnsavedChanges = true;
                OnEntitySelected?.Invoke(null);
            }
            
            return removed;
        }
    }
    
    /// <summary>
    /// Manages all editor tools
    /// </summary>
    public class ToolManager
    {
        private readonly System.Collections.Generic.Dictionary<EditorTool, IEditorTool> _tools;
        private IEditorTool _activeTool;
        private readonly EditorState _state;
        
        public IEditorTool ActiveTool => _activeTool;
        
        public event System.Action<EditorTool> OnToolChanged;
        
        public ToolManager(EditorState state, TilePalette palette)
        {
            _state = state;
            _tools = new System.Collections.Generic.Dictionary<EditorTool, IEditorTool>
            {
                { EditorTool.Brush, new BrushTool(palette) },
                { EditorTool.Eraser, new EraserTool() },
                { EditorTool.EntityPlace, new EntityPlaceTool(palette) },
                { EditorTool.EntitySelect, new EntitySelectTool() }
            };
        }
        
        public void SetTool(EditorTool tool)
        {
            if (_activeTool != null)
            {
                _activeTool.OnToolDeselected(_state);
            }
            
            _state.ActiveTool = tool;
            
            if (_tools.TryGetValue(tool, out var newTool))
            {
                _activeTool = newTool;
                _activeTool.OnToolSelected(_state);
            }
            else
            {
                _activeTool = null;
            }
            
            OnToolChanged?.Invoke(tool);
        }
        
        public T GetTool<T>() where T : class, IEditorTool
        {
            foreach (var tool in _tools.Values)
            {
                if (tool is T typedTool)
                    return typedTool;
            }
            return null;
        }
        
        public void OnPointerDown(Vector2Int position) => _activeTool?.OnPointerDown(position, _state);
        public void OnPointerDrag(Vector2Int position) => _activeTool?.OnPointerDrag(position, _state);
        public void OnPointerUp(Vector2Int position) => _activeTool?.OnPointerUp(position, _state);
    }
}
