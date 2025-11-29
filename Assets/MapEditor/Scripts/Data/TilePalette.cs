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
    public class EntityDefinition
    {
        public string id;
        public string displayName;
        public string category;
        public Sprite icon;
        public Color gizmoColor = Color.yellow;
    }
    
    [CreateAssetMenu(fileName = "TilePalette", menuName = "Map Editor/Tile Palette")]
    public class TilePalette : ScriptableObject
    {
        public List<TileDefinition> tiles = new List<TileDefinition>();
        public List<EntityDefinition> entities = new List<EntityDefinition>();
        
        private Dictionary<string, TileDefinition> _tileCache;
        private Dictionary<string, EntityDefinition> _entityCache;
        
        public TileDefinition GetTile(string id)
        {
            if (_tileCache == null) BuildCache();
            return _tileCache.TryGetValue(id, out var t) ? t : null;
        }
        
        public EntityDefinition GetEntity(string id)
        {
            if (_entityCache == null) BuildCache();
            return _entityCache.TryGetValue(id, out var e) ? e : null;
        }
        
        public List<string> GetTileCategories()
        {
            var cats = new HashSet<string>();
            foreach (var t in tiles) if (!string.IsNullOrEmpty(t.category)) cats.Add(t.category);
            return new List<string>(cats);
        }
        
        private void BuildCache()
        {
            _tileCache = new Dictionary<string, TileDefinition>();
            foreach (var t in tiles) _tileCache[t.id] = t;
            
            _entityCache = new Dictionary<string, EntityDefinition>();
            foreach (var e in entities) _entityCache[e.id] = e;
        }
        
        private void OnEnable() => BuildCache();
        private void OnValidate() => _tileCache = null;
    }
}
