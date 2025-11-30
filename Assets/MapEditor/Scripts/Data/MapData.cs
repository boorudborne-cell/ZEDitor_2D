using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditor.Data
{
    [Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public string tileId;
        public bool hasCollision;
        
        public Vector2Int Position
        {
            get => new Vector2Int(x, y);
            set { x = value.x; y = value.y; }
        }
    }
    
    [Serializable]
    public class PrefabData
    {
        public string instanceId;
        public string prefabId;
        public int x;
        public int y;
        public float rotation;
        public Vector3 scale = Vector3.one;
        public string customData;
        
        public Vector2Int Position
        {
            get => new Vector2Int(x, y);
            set { x = value.x; y = value.y; }
        }
    }
    
    public enum LayerType { Background = 0, Ground = 1, Foreground = 2 }
    
    [Serializable]
    public class LayerData
    {
        public string name;
        public int layerType;
        public int sortingOrder;
        public bool isVisible = true;
        public List<TileData> tiles = new List<TileData>();
        
        [NonSerialized] private Dictionary<Vector2Int, TileData> _cache;
        
        public LayerType Type => (LayerType)layerType;
        
        public void BuildCache()
        {
            _cache = new Dictionary<Vector2Int, TileData>();
            foreach (var t in tiles) _cache[t.Position] = t;
        }
        
        public TileData GetTile(Vector2Int pos)
        {
            if (_cache == null) BuildCache();
            return _cache.TryGetValue(pos, out var t) ? t : null;
        }
        
        public void SetTile(TileData tile)
        {
            if (_cache == null) BuildCache();
            var existing = GetTile(tile.Position);
            if (existing != null) tiles.Remove(existing);
            tiles.Add(tile);
            _cache[tile.Position] = tile;
        }
        
        public bool RemoveTile(Vector2Int pos)
        {
            if (_cache == null) BuildCache();
            var tile = GetTile(pos);
            if (tile == null) return false;
            tiles.Remove(tile);
            _cache.Remove(pos);
            return true;
        }
        
        public void Clear()
        {
            tiles.Clear();
            _cache?.Clear();
        }
    }
    
    [Serializable]
    public class MapData
    {
        public string mapName;
        public string version = "1.0";
        public int width;
        public int height;
        public List<LayerData> layers = new List<LayerData>();
        public List<PrefabData> prefabs = new List<PrefabData>();
        
        public static MapData Create(string name, int w, int h)
        {
            var map = new MapData { mapName = name, width = w, height = h };
            map.layers.Add(new LayerData { name = "Background", layerType = 0, sortingOrder = -1 });
            map.layers.Add(new LayerData { name = "Ground", layerType = 1, sortingOrder = 1 });
            map.layers.Add(new LayerData { name = "Foreground", layerType = 2, sortingOrder = 2 });
            return map;
        }
        
        public LayerData GetLayer(LayerType type) => layers.Find(l => l.Type == type);
        
        public void BuildCaches() { foreach (var l in layers) l.BuildCache(); }
        
        public PrefabData GetPrefabAt(Vector2Int pos)
        {
            return prefabs.Find(p => p.x == pos.x && p.y == pos.y);
        }
        
        public void RemovePrefabAt(Vector2Int pos)
        {
            prefabs.RemoveAll(p => p.x == pos.x && p.y == pos.y);
        }
    }
}
