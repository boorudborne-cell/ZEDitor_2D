using System;
using UnityEngine;

namespace MapEditor.Data
{
    /// <summary>
    /// Represents a single tile in the map
    /// </summary>
    [Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public string tileId;
        public bool hasCollision;
        
        public TileData() { }
        
        public TileData(int x, int y, string tileId, bool hasCollision = false)
        {
            this.x = x;
            this.y = y;
            this.tileId = tileId;
            this.hasCollision = hasCollision;
        }
        
        public Vector2Int Position => new Vector2Int(x, y);
    }
    
    /// <summary>
    /// Represents a game entity (player spawn, enemy, collectible, etc.)
    /// </summary>
    [Serializable]
    public class EntityData
    {
        public string entityId;
        public string entityType;
        public float posX;
        public float posY;
        public string customData; // JSON string for entity-specific properties
        
        public EntityData() { }
        
        public EntityData(string entityId, string entityType, Vector2 position)
        {
            this.entityId = entityId;
            this.entityType = entityType;
            this.posX = position.x;
            this.posY = position.y;
            this.customData = "{}";
        }
        
        public Vector2 Position
        {
            get => new Vector2(posX, posY);
            set { posX = value.x; posY = value.y; }
        }
    }
}
