using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Core
{
    /// <summary>
    /// Available editor tools
    /// </summary>
    public enum EditorTool
    {
        None,
        Brush,
        Eraser,
        EntityPlace,
        EntitySelect
    }
    
    /// <summary>
    /// Editor state container
    /// </summary>
    public class EditorState
    {
        public MapData CurrentMap { get; set; }
        public LayerType ActiveLayer { get; set; } = LayerType.Ground;
        public EditorTool ActiveTool { get; set; } = EditorTool.Brush;
        public string SelectedTileId { get; set; }
        public string SelectedEntityType { get; set; }
        public bool ShowGrid { get; set; } = true;
        public bool ShowCollisions { get; set; } = true;
        public float Zoom { get; set; } = 1f;
        public Vector2 CameraPosition { get; set; }
        public bool HasUnsavedChanges { get; set; }
        public string CurrentFileName { get; set; }
        
        // Zoom constraints
        public const float MIN_ZOOM = 0.25f;
        public const float MAX_ZOOM = 4f;
        public const float ZOOM_STEP = 0.25f;
        
        public void ZoomIn()
        {
            Zoom = Mathf.Min(Zoom + ZOOM_STEP, MAX_ZOOM);
        }
        
        public void ZoomOut()
        {
            Zoom = Mathf.Max(Zoom - ZOOM_STEP, MIN_ZOOM);
        }
        
        public void Reset()
        {
            CurrentMap = null;
            ActiveLayer = LayerType.Ground;
            ActiveTool = EditorTool.Brush;
            SelectedTileId = null;
            SelectedEntityType = null;
            ShowGrid = true;
            ShowCollisions = true;
            Zoom = 1f;
            CameraPosition = Vector2.zero;
            HasUnsavedChanges = false;
            CurrentFileName = null;
        }
    }
    
    /// <summary>
    /// Interface for editor tools
    /// </summary>
    public interface IEditorTool
    {
        EditorTool ToolType { get; }
        string DisplayName { get; }
        
        void OnToolSelected(EditorState state);
        void OnToolDeselected(EditorState state);
        void OnPointerDown(Vector2Int tilePosition, EditorState state);
        void OnPointerDrag(Vector2Int tilePosition, EditorState state);
        void OnPointerUp(Vector2Int tilePosition, EditorState state);
    }
    
    /// <summary>
    /// Base class for tile-based tools
    /// </summary>
    public abstract class BaseTileTool : IEditorTool
    {
        public abstract EditorTool ToolType { get; }
        public abstract string DisplayName { get; }
        
        protected Vector2Int? _lastPosition;
        
        public virtual void OnToolSelected(EditorState state) { }
        public virtual void OnToolDeselected(EditorState state) 
        {
            _lastPosition = null;
        }
        
        public abstract void OnPointerDown(Vector2Int tilePosition, EditorState state);
        
        public virtual void OnPointerDrag(Vector2Int tilePosition, EditorState state)
        {
            // Skip if same position as last
            if (_lastPosition.HasValue && _lastPosition.Value == tilePosition)
                return;
            
            _lastPosition = tilePosition;
            ApplyTool(tilePosition, state);
        }
        
        public virtual void OnPointerUp(Vector2Int tilePosition, EditorState state)
        {
            _lastPosition = null;
        }
        
        protected abstract void ApplyTool(Vector2Int position, EditorState state);
    }
}
