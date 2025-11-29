using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditor.Data
{
    /// <summary>
    /// Defines available layer types in the editor
    /// </summary>
    public enum LayerType
    {
        Background = 0,
        Ground = 1,
        Foreground = 2
    }
    
    /// <summary>
    /// Represents a single layer containing tiles
    /// </summary>
    [Serializable]
    public class LayerData
    {
        public string layerName;
        public int layerType;
        public int sortingOrder;
        public bool isVisible;
        public List<TileData> tiles;
        
        // Runtime dictionary for fast tile lookup - not serialized
        [NonSerialized]
        private Dictionary<Vector2Int, TileData> _tileCache;
        
        public LayerData()
        {
            tiles = new List<TileData>();
            isVisible = true;
        }
        
        public LayerData(LayerType type) : this()
        {
            layerType = (int)type;
            layerName = type.ToString();
            sortingOrder = (int)type;
        }
        
        public LayerType Type => (LayerType)layerType;
        
        /// <summary>
        /// Builds the runtime cache for O(1) tile lookups
        /// </summary>
        public void BuildCache()
        {
            _tileCache = new Dictionary<Vector2Int, TileData>(tiles.Count);
            foreach (var tile in tiles)
            {
                _tileCache[tile.Position] = tile;
            }
        }
        
        /// <summary>
        /// Gets tile at position with O(1) lookup
        /// </summary>
        public TileData GetTileAt(Vector2Int position)
        {
            if (_tileCache == null)
                BuildCache();
                
            return _tileCache.TryGetValue(position, out var tile) ? tile : null;
        }
        
        /// <summary>
        /// Sets or replaces tile at position
        /// </summary>
        public void SetTile(TileData tile)
        {
            if (_tileCache == null)
                BuildCache();
            
            var pos = tile.Position;
            
            if (_tileCache.TryGetValue(pos, out var existing))
            {
                tiles.Remove(existing);
            }
            
            tiles.Add(tile);
            _tileCache[pos] = tile;
        }
        
        /// <summary>
        /// Removes tile at position
        /// </summary>
        public bool RemoveTileAt(Vector2Int position)
        {
            if (_tileCache == null)
                BuildCache();
            
            if (_tileCache.TryGetValue(position, out var tile))
            {
                tiles.Remove(tile);
                _tileCache.Remove(position);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Clears all tiles from the layer
        /// </summary>
        public void Clear()
        {
            tiles.Clear();
            _tileCache?.Clear();
        }
    }
}
