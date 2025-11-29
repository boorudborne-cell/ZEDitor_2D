using System;
using System.Collections.Generic;
using UnityEngine;

namespace MapEditor.Data
{
    /// <summary>
    /// Configuration for a single tile type in the palette
    /// </summary>
    [Serializable]
    public class TilePaletteEntry
    {
        public string tileId;
        public string displayName;
        public string category;
        public Sprite sprite;
        public bool defaultHasCollision;
        public Color previewColor; // Used when sprite is null
        
        public TilePaletteEntry()
        {
            previewColor = Color.white;
        }
        
        public TilePaletteEntry(string id, string name, string category, bool hasCollision = false)
        {
            tileId = id;
            displayName = name;
            this.category = category;
            defaultHasCollision = hasCollision;
            previewColor = Color.white;
        }
    }
    
    /// <summary>
    /// Configuration for an entity type
    /// </summary>
    [Serializable]
    public class EntityPaletteEntry
    {
        public string entityType;
        public string displayName;
        public string category;
        public Sprite icon;
        public Color gizmoColor;
        public Vector2 size;
        
        public EntityPaletteEntry()
        {
            gizmoColor = Color.yellow;
            size = Vector2.one;
        }
    }
    
    /// <summary>
    /// ScriptableObject containing all available tiles and entities for the editor
    /// </summary>
    [CreateAssetMenu(fileName = "TilePalette", menuName = "Map Editor/Tile Palette")]
    public class TilePalette : ScriptableObject
    {
        [Header("Tiles")]
        public List<TilePaletteEntry> tiles = new List<TilePaletteEntry>();
        
        [Header("Entities")]
        public List<EntityPaletteEntry> entities = new List<EntityPaletteEntry>();
        
        [Header("Categories")]
        public List<string> tileCategories = new List<string> { "Ground", "Decoration", "Hazard" };
        public List<string> entityCategories = new List<string> { "Player", "Enemy", "Collectible", "Trigger" };
        
        private Dictionary<string, TilePaletteEntry> _tileCache;
        private Dictionary<string, EntityPaletteEntry> _entityCache;
        
        /// <summary>
        /// Gets tile entry by ID with O(1) lookup
        /// </summary>
        public TilePaletteEntry GetTile(string tileId)
        {
            if (_tileCache == null)
                BuildCache();
                
            return _tileCache.TryGetValue(tileId, out var entry) ? entry : null;
        }
        
        /// <summary>
        /// Gets entity entry by type with O(1) lookup
        /// </summary>
        public EntityPaletteEntry GetEntity(string entityType)
        {
            if (_entityCache == null)
                BuildCache();
                
            return _entityCache.TryGetValue(entityType, out var entry) ? entry : null;
        }
        
        /// <summary>
        /// Gets all tiles in a category
        /// </summary>
        public List<TilePaletteEntry> GetTilesByCategory(string category)
        {
            return tiles.FindAll(t => t.category == category);
        }
        
        /// <summary>
        /// Gets all entities in a category
        /// </summary>
        public List<EntityPaletteEntry> GetEntitiesByCategory(string category)
        {
            return entities.FindAll(e => e.category == category);
        }
        
        private void BuildCache()
        {
            _tileCache = new Dictionary<string, TilePaletteEntry>();
            foreach (var tile in tiles)
            {
                _tileCache[tile.tileId] = tile;
            }
            
            _entityCache = new Dictionary<string, EntityPaletteEntry>();
            foreach (var entity in entities)
            {
                _entityCache[entity.entityType] = entity;
            }
        }
        
        private void OnEnable()
        {
            BuildCache();
        }
        
        private void OnValidate()
        {
            BuildCache();
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Creates a default palette with sample tiles (Editor only)
        /// </summary>
        [ContextMenu("Add Sample Tiles")]
        private void AddSampleTiles()
        {
            tiles.Clear();
            entities.Clear();
            
            // Sample ground tiles
            tiles.Add(new TilePaletteEntry("ground_dirt", "Dirt", "Ground", true) 
                { previewColor = new Color(0.5f, 0.3f, 0.1f) });
            tiles.Add(new TilePaletteEntry("ground_grass", "Grass", "Ground", true) 
                { previewColor = new Color(0.2f, 0.7f, 0.2f) });
            tiles.Add(new TilePaletteEntry("ground_stone", "Stone", "Ground", true) 
                { previewColor = Color.gray });
            
            // Sample decoration tiles
            tiles.Add(new TilePaletteEntry("deco_flower", "Flower", "Decoration", false) 
                { previewColor = Color.magenta });
            tiles.Add(new TilePaletteEntry("deco_bush", "Bush", "Decoration", false) 
                { previewColor = new Color(0.1f, 0.5f, 0.1f) });
            
            // Sample hazard tiles
            tiles.Add(new TilePaletteEntry("hazard_spike", "Spike", "Hazard", true) 
                { previewColor = Color.red });
            tiles.Add(new TilePaletteEntry("hazard_lava", "Lava", "Hazard", true) 
                { previewColor = new Color(1f, 0.3f, 0f) });
            
            // Sample entities
            entities.Add(new EntityPaletteEntry 
            { 
                entityType = "player_spawn", 
                displayName = "Player Spawn", 
                category = "Player",
                gizmoColor = Color.green 
            });
            entities.Add(new EntityPaletteEntry 
            { 
                entityType = "enemy_walker", 
                displayName = "Walking Enemy", 
                category = "Enemy",
                gizmoColor = Color.red 
            });
            entities.Add(new EntityPaletteEntry 
            { 
                entityType = "coin", 
                displayName = "Coin", 
                category = "Collectible",
                gizmoColor = Color.yellow 
            });
            entities.Add(new EntityPaletteEntry 
            { 
                entityType = "checkpoint", 
                displayName = "Checkpoint", 
                category = "Trigger",
                gizmoColor = Color.cyan 
            });
            
            BuildCache();
        }
#endif
    }
}
