using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditor.Data
{
    /// <summary>
    /// Main map data container - serialized to JSON
    /// </summary>
    [Serializable]
    public class MapData
    {
        public string mapName;
        public string version;
        public int width;
        public int height;
        public float tileSize;
        public long createdAt;
        public long modifiedAt;
        public List<LayerData> layers;
        public List<EntityData> entities;
        public MapMetadata metadata;
        
        public MapData()
        {
            version = "1.0";
            tileSize = 1f;
            layers = new List<LayerData>();
            entities = new List<EntityData>();
            metadata = new MapMetadata();
        }
        
        /// <summary>
        /// Creates a new map with specified dimensions
        /// </summary>
        public static MapData CreateNew(string name, int width, int height, float tileSize = 1f)
        {
            var map = new MapData
            {
                mapName = name,
                width = width,
                height = height,
                tileSize = tileSize,
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                modifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            
            // Create default layers
            map.layers.Add(new LayerData(LayerType.Background));
            map.layers.Add(new LayerData(LayerType.Ground));
            map.layers.Add(new LayerData(LayerType.Foreground));
            
            return map;
        }
        
        /// <summary>
        /// Gets layer by type
        /// </summary>
        public LayerData GetLayer(LayerType type)
        {
            return layers.Find(l => l.Type == type);
        }
        
        /// <summary>
        /// Builds all layer caches for fast lookups
        /// </summary>
        public void BuildAllCaches()
        {
            foreach (var layer in layers)
            {
                layer.BuildCache();
            }
        }
        
        /// <summary>
        /// Marks the map as modified
        /// </summary>
        public void MarkModified()
        {
            modifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        
        /// <summary>
        /// Checks if position is within map bounds
        /// </summary>
        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
        
        /// <summary>
        /// Checks if position is within map bounds
        /// </summary>
        public bool IsInBounds(Vector2Int position)
        {
            return IsInBounds(position.x, position.y);
        }
        
        /// <summary>
        /// Gets total tile count across all layers
        /// </summary>
        public int GetTotalTileCount()
        {
            int count = 0;
            foreach (var layer in layers)
            {
                count += layer.tiles.Count;
            }
            return count;
        }
    }
    
    /// <summary>
    /// Additional map metadata
    /// </summary>
    [Serializable]
    public class MapMetadata
    {
        public string author;
        public string description;
        public string difficulty;
        public int estimatedPlayTime; // in seconds
        public List<string> tags;
        
        public MapMetadata()
        {
            tags = new List<string>();
        }
    }
}
