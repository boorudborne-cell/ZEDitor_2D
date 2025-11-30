using System;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Core
{
    public enum EditorTool { Brush, Eraser, Prefab, Eyedropper }
    
    public class EditorState
    {
        public MapData Map { get; set; }
        public LayerType ActiveLayer { get; set; } = LayerType.Ground;
        public EditorTool ActiveTool { get; set; } = EditorTool.Brush;
        public string SelectedTileId { get; set; }
        public string SelectedPrefabId { get; set; }
        public bool HasUnsavedChanges { get; set; }
        public string CurrentFileName { get; set; }
        
        // Play Mode
        public bool IsPlayMode { get; set; }
        
        // View
        public float Zoom { get; set; } = 1f;
        public Vector2 CameraOffset { get; set; }
        public bool ShowCollisions { get; set; } = true;
        public bool ShowGrid { get; set; } = true;
        
        // Preview
        public Vector2Int HoveredTile { get; set; }
        
        public const float MinZoom = 0.25f;
        public const float MaxZoom = 4f;
        
        public event Action OnChanged;
        
        public void NotifyChanged() => OnChanged?.Invoke();
        
        public void SetZoom(float z)
        {
            Zoom = Mathf.Clamp(z, MinZoom, MaxZoom);
            NotifyChanged();
        }
    }
}
