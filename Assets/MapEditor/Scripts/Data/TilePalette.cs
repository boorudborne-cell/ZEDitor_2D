using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditor.Data
{
    [Serializable]
    public class TileDefinition
    {
        public string id;
        public string displayName;
        public string category;
        public Sprite sprite;
        public bool hasCollision;
    }
    
    [Serializable]
    public class PrefabDefinition
    {
        public string id;
        public string displayName;
        public string category;
        public GameObject prefab;
        public Sprite icon;
        public Color gizmoColor = Color.cyan;
        public bool snapToGrid = true;
    }
    
    [CreateAssetMenu(fileName = "TilePalette", menuName = "Map Editor/Tile Palette")]
    public class TilePalette : ScriptableObject
    {
        public List<TileDefinition> tiles = new List<TileDefinition>();
        public List<PrefabDefinition> prefabs = new List<PrefabDefinition>();
        
        private Dictionary<string, TileDefinition> _tileCache;
        private Dictionary<string, PrefabDefinition> _prefabCache;
        
        public TileDefinition GetTile(string id)
        {
            if (_tileCache == null) BuildCache();
            return _tileCache.TryGetValue(id, out var t) ? t : null;
        }
        
        public PrefabDefinition GetPrefab(string id)
        {
            if (_prefabCache == null) BuildCache();
            return _prefabCache.TryGetValue(id, out var p) ? p : null;
        }
        
        public List<string> GetTileCategories()
        {
            var cats = new HashSet<string>();
            foreach (var t in tiles) if (!string.IsNullOrEmpty(t.category)) cats.Add(t.category);
            return new List<string>(cats);
        }
        
        public List<string> GetPrefabCategories()
        {
            var cats = new HashSet<string>();
            foreach (var p in prefabs) if (!string.IsNullOrEmpty(p.category)) cats.Add(p.category);
            return new List<string>(cats);
        }
        
        private void BuildCache()
        {
            _tileCache = new Dictionary<string, TileDefinition>();
            foreach (var t in tiles) _tileCache[t.id] = t;
            
            _prefabCache = new Dictionary<string, PrefabDefinition>();
            foreach (var p in prefabs) _prefabCache[p.id] = p;
        }
        
        private void OnEnable() => BuildCache();
        private void OnValidate() { _tileCache = null; _prefabCache = null; }
    }
}
