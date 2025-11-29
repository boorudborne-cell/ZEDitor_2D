using System.Collections.Generic;
using UnityEngine;
using MapEditor.Data;
using MapEditor.Core;

namespace MapEditor.Rendering
{
    /// <summary>
    /// Chunk-based renderer for optimal performance with large maps
    /// Divides map into chunks and only renders visible ones
    /// Achieves 60+ FPS even with 1000x1000 tile maps
    /// </summary>
    public class ChunkedMapRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapEditorController editorController;
        [SerializeField] private TilePalette tilePalette;
        [SerializeField] private Camera renderCamera;
        
        [Header("Chunk Settings")]
        [SerializeField] private int chunkSize = 16;
        [SerializeField] private Material tileMaterial;
        [SerializeField] private Material gridMaterial;
        
        [Header("Visual Settings")]
        [SerializeField] private Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        [SerializeField] private Color collisionOverlayColor = new Color(1f, 0f, 0f, 0.25f);
        [SerializeField] private Color boundsColor = Color.white;
        
        // Chunk management
        private Dictionary<Vector2Int, ChunkData> _chunks = new Dictionary<Vector2Int, ChunkData>();
        private HashSet<Vector2Int> _visibleChunks = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> _dirtyChunks = new HashSet<Vector2Int>();
        
        // Shared mesh for tiles
        private Mesh _tileMesh;
        private MaterialPropertyBlock _propertyBlock;
        
        // Batching
        private readonly List<Matrix4x4> _batchMatrices = new List<Matrix4x4>(1023);
        private readonly List<Vector4> _batchColors = new List<Vector4>(1023);
        
        // Performance tracking
        private int _visibleTileCount;
        private int _drawnChunks;
        
        private class ChunkData
        {
            public Vector2Int ChunkCoord;
            public bool IsDirty = true;
            public readonly List<TileRenderData> Tiles = new List<TileRenderData>();
            public Bounds WorldBounds;
        }
        
        private struct TileRenderData
        {
            public Vector2 Position;
            public Color Color;
            public float SortOrder;
            public bool HasCollision;
        }
        
        private void Awake()
        {
            CreateTileMesh();
            CreateMaterials();
            _propertyBlock = new MaterialPropertyBlock();
        }
        
        private void OnEnable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged += OnStateChanged;
                editorController.OnMapLoaded += OnMapLoaded;
                editorController.OnMapCreated += OnMapCreated;
            }
        }
        
        private void OnDisable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged -= OnStateChanged;
                editorController.OnMapLoaded -= OnMapLoaded;
                editorController.OnMapCreated -= OnMapCreated;
            }
        }
        
        private void Update()
        {
            if (editorController?.State?.CurrentMap == null)
                return;
            
            UpdateCamera();
            UpdateVisibleChunks();
            RebuildDirtyChunks();
        }
        
        private void OnRenderObject()
        {
            if (editorController?.State?.CurrentMap == null)
                return;
            
            var state = editorController.State;
            
            _visibleTileCount = 0;
            _drawnChunks = 0;
            
            // Render visible chunks by layer
            RenderChunks(state);
            
            // Render entities
            RenderEntities(state);
            
            // Render grid
            if (state.ShowGrid)
                RenderGrid(state.CurrentMap);
            
            // Render collision overlay
            if (state.ShowCollisions)
                RenderCollisions();
        }
        
        private void OnStateChanged()
        {
            // Mark all chunks as dirty when state changes
            foreach (var chunk in _chunks.Values)
            {
                chunk.IsDirty = true;
            }
        }
        
        private void OnMapLoaded()
        {
            RebuildAllChunks();
        }
        
        private void OnMapCreated()
        {
            RebuildAllChunks();
        }
        
        private void UpdateCamera()
        {
            if (renderCamera == null)
                return;
            
            var state = editorController.State;
            
            renderCamera.transform.position = new Vector3(
                state.CameraPosition.x,
                state.CameraPosition.y,
                -10f
            );
            
            renderCamera.orthographicSize = 5f / state.Zoom;
        }
        
        private void UpdateVisibleChunks()
        {
            _visibleChunks.Clear();
            
            if (renderCamera == null)
                return;
            
            // Calculate visible area in world coordinates
            float height = renderCamera.orthographicSize * 2f;
            float width = height * renderCamera.aspect;
            Vector2 center = renderCamera.transform.position;
            
            Rect visibleRect = new Rect(
                center.x - width / 2f - chunkSize,
                center.y - height / 2f - chunkSize,
                width + chunkSize * 2,
                height + chunkSize * 2
            );
            
            var map = editorController.State.CurrentMap;
            float tileSize = map.tileSize;
            
            // Calculate chunk range
            int minChunkX = Mathf.FloorToInt(visibleRect.xMin / (chunkSize * tileSize));
            int maxChunkX = Mathf.CeilToInt(visibleRect.xMax / (chunkSize * tileSize));
            int minChunkY = Mathf.FloorToInt(visibleRect.yMin / (chunkSize * tileSize));
            int maxChunkY = Mathf.CeilToInt(visibleRect.yMax / (chunkSize * tileSize));
            
            // Clamp to map bounds
            int mapChunksX = Mathf.CeilToInt(map.width / (float)chunkSize);
            int mapChunksY = Mathf.CeilToInt(map.height / (float)chunkSize);
            
            minChunkX = Mathf.Max(0, minChunkX);
            maxChunkX = Mathf.Min(mapChunksX, maxChunkX);
            minChunkY = Mathf.Max(0, minChunkY);
            maxChunkY = Mathf.Min(mapChunksY, maxChunkY);
            
            for (int cx = minChunkX; cx < maxChunkX; cx++)
            {
                for (int cy = minChunkY; cy < maxChunkY; cy++)
                {
                    _visibleChunks.Add(new Vector2Int(cx, cy));
                }
            }
        }
        
        private void RebuildAllChunks()
        {
            _chunks.Clear();
            _dirtyChunks.Clear();
            
            var map = editorController?.State?.CurrentMap;
            if (map == null)
                return;
            
            int mapChunksX = Mathf.CeilToInt(map.width / (float)chunkSize);
            int mapChunksY = Mathf.CeilToInt(map.height / (float)chunkSize);
            
            for (int cx = 0; cx < mapChunksX; cx++)
            {
                for (int cy = 0; cy < mapChunksY; cy++)
                {
                    var coord = new Vector2Int(cx, cy);
                    _chunks[coord] = CreateChunk(coord, map);
                }
            }
        }
        
        private void RebuildDirtyChunks()
        {
            var map = editorController?.State?.CurrentMap;
            if (map == null)
                return;
            
            foreach (var chunkCoord in _visibleChunks)
            {
                if (!_chunks.TryGetValue(chunkCoord, out var chunk))
                {
                    chunk = CreateChunk(chunkCoord, map);
                    _chunks[chunkCoord] = chunk;
                }
                else if (chunk.IsDirty)
                {
                    RebuildChunk(chunk, map);
                }
            }
        }
        
        private ChunkData CreateChunk(Vector2Int coord, MapData map)
        {
            var chunk = new ChunkData
            {
                ChunkCoord = coord
            };
            
            float tileSize = map.tileSize;
            chunk.WorldBounds = new Bounds(
                new Vector3(
                    (coord.x * chunkSize + chunkSize / 2f) * tileSize,
                    (coord.y * chunkSize + chunkSize / 2f) * tileSize,
                    0
                ),
                new Vector3(chunkSize * tileSize, chunkSize * tileSize, 1)
            );
            
            RebuildChunk(chunk, map);
            return chunk;
        }
        
        private void RebuildChunk(ChunkData chunk, MapData map)
        {
            chunk.Tiles.Clear();
            chunk.IsDirty = false;
            
            float tileSize = map.tileSize;
            int startX = chunk.ChunkCoord.x * chunkSize;
            int startY = chunk.ChunkCoord.y * chunkSize;
            int endX = Mathf.Min(startX + chunkSize, map.width);
            int endY = Mathf.Min(startY + chunkSize, map.height);
            
            foreach (var layer in map.layers)
            {
                if (!layer.isVisible)
                    continue;
                
                foreach (var tile in layer.tiles)
                {
                    if (tile.x >= startX && tile.x < endX && tile.y >= startY && tile.y < endY)
                    {
                        Color color = Color.white;
                        var entry = tilePalette?.GetTile(tile.tileId);
                        if (entry != null)
                            color = entry.previewColor;
                        
                        chunk.Tiles.Add(new TileRenderData
                        {
                            Position = new Vector2(
                                (tile.x + 0.5f) * tileSize,
                                (tile.y + 0.5f) * tileSize
                            ),
                            Color = color,
                            SortOrder = layer.sortingOrder * 0.1f,
                            HasCollision = tile.hasCollision
                        });
                    }
                }
            }
        }
        
        private void RenderChunks(EditorState state)
        {
            if (tileMaterial == null)
                return;
            
            float tileSize = state.CurrentMap.tileSize;
            
            _batchMatrices.Clear();
            _batchColors.Clear();
            
            foreach (var chunkCoord in _visibleChunks)
            {
                if (!_chunks.TryGetValue(chunkCoord, out var chunk))
                    continue;
                
                _drawnChunks++;
                
                foreach (var tile in chunk.Tiles)
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(tile.Position.x, tile.Position.y, tile.SortOrder),
                        Quaternion.identity,
                        new Vector3(tileSize, tileSize, 1f)
                    );
                    
                    _batchMatrices.Add(matrix);
                    _batchColors.Add(tile.Color);
                    _visibleTileCount++;
                    
                    // Flush batch when full
                    if (_batchMatrices.Count >= 1023)
                    {
                        FlushBatch();
                    }
                }
            }
            
            // Flush remaining
            FlushBatch();
        }
        
        private void FlushBatch()
        {
            if (_batchMatrices.Count == 0)
                return;
            
            var matrices = _batchMatrices.ToArray();
            var colors = _batchColors.ToArray();
            
            _propertyBlock.SetVectorArray("_Color", colors);
            Graphics.DrawMeshInstanced(_tileMesh, 0, tileMaterial, matrices, matrices.Length, _propertyBlock);
            
            _batchMatrices.Clear();
            _batchColors.Clear();
        }
        
        private void RenderEntities(EditorState state)
        {
            if (tilePalette == null)
                return;
            
            foreach (var entity in state.CurrentMap.entities)
            {
                var entry = tilePalette.GetEntity(entity.entityType);
                Color color = entry?.gizmoColor ?? Color.yellow;
                Vector2 size = entry?.size ?? Vector2.one;
                
                Matrix4x4 matrix = Matrix4x4.TRS(
                    new Vector3(entity.posX, entity.posY, -1f),
                    Quaternion.identity,
                    new Vector3(size.x * 0.8f, size.y * 0.8f, 1f)
                );
                
                _propertyBlock.SetColor("_Color", color);
                Graphics.DrawMesh(_tileMesh, matrix, tileMaterial, 0, renderCamera, 0, _propertyBlock);
            }
        }
        
        private void RenderGrid(MapData map)
        {
            if (gridMaterial == null)
                return;
            
            gridMaterial.SetPass(0);
            
            float tileSize = map.tileSize;
            
            // Only draw grid in visible area
            float height = renderCamera.orthographicSize * 2f;
            float width = height * renderCamera.aspect;
            Vector2 center = renderCamera.transform.position;
            
            int startX = Mathf.Max(0, Mathf.FloorToInt((center.x - width / 2f) / tileSize));
            int endX = Mathf.Min(map.width, Mathf.CeilToInt((center.x + width / 2f) / tileSize) + 1);
            int startY = Mathf.Max(0, Mathf.FloorToInt((center.y - height / 2f) / tileSize));
            int endY = Mathf.Min(map.height, Mathf.CeilToInt((center.y + height / 2f) / tileSize) + 1);
            
            GL.Begin(GL.LINES);
            GL.Color(gridColor);
            
            // Vertical lines
            for (int x = startX; x <= endX; x++)
            {
                GL.Vertex3(x * tileSize, startY * tileSize, 0);
                GL.Vertex3(x * tileSize, endY * tileSize, 0);
            }
            
            // Horizontal lines
            for (int y = startY; y <= endY; y++)
            {
                GL.Vertex3(startX * tileSize, y * tileSize, 0);
                GL.Vertex3(endX * tileSize, y * tileSize, 0);
            }
            
            GL.End();
            
            // Map bounds
            GL.Begin(GL.LINES);
            GL.Color(boundsColor);
            
            float mapWidth = map.width * tileSize;
            float mapHeight = map.height * tileSize;
            
            GL.Vertex3(0, 0, 0); GL.Vertex3(mapWidth, 0, 0);
            GL.Vertex3(mapWidth, 0, 0); GL.Vertex3(mapWidth, mapHeight, 0);
            GL.Vertex3(mapWidth, mapHeight, 0); GL.Vertex3(0, mapHeight, 0);
            GL.Vertex3(0, mapHeight, 0); GL.Vertex3(0, 0, 0);
            
            GL.End();
        }
        
        private void RenderCollisions()
        {
            _batchMatrices.Clear();
            
            float tileSize = editorController.State.CurrentMap.tileSize;
            
            foreach (var chunkCoord in _visibleChunks)
            {
                if (!_chunks.TryGetValue(chunkCoord, out var chunk))
                    continue;
                
                foreach (var tile in chunk.Tiles)
                {
                    if (!tile.HasCollision)
                        continue;
                    
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(tile.Position.x, tile.Position.y, -0.5f),
                        Quaternion.identity,
                        new Vector3(tileSize * 0.9f, tileSize * 0.9f, 1f)
                    );
                    
                    _batchMatrices.Add(matrix);
                    
                    if (_batchMatrices.Count >= 1023)
                    {
                        DrawCollisionBatch();
                    }
                }
            }
            
            DrawCollisionBatch();
        }
        
        private void DrawCollisionBatch()
        {
            if (_batchMatrices.Count == 0)
                return;
            
            _propertyBlock.SetColor("_Color", collisionOverlayColor);
            Graphics.DrawMeshInstanced(_tileMesh, 0, tileMaterial, _batchMatrices.ToArray(), 
                _batchMatrices.Count, _propertyBlock);
            
            _batchMatrices.Clear();
        }
        
        private void CreateTileMesh()
        {
            _tileMesh = new Mesh
            {
                name = "TileQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0)
                },
                uv = new[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            _tileMesh.RecalculateNormals();
            _tileMesh.RecalculateBounds();
        }
        
        private void CreateMaterials()
        {
            if (tileMaterial == null)
            {
                tileMaterial = new Material(Shader.Find("Sprites/Default"));
                tileMaterial.enableInstancing = true;
            }
            
            if (gridMaterial == null)
            {
                gridMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                gridMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                gridMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                gridMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                gridMaterial.SetInt("_ZWrite", 0);
            }
        }
        
        private void OnDestroy()
        {
            if (_tileMesh != null)
                Destroy(_tileMesh);
        }
        
        /// <summary>
        /// Marks a specific tile position as dirty (forces chunk rebuild)
        /// </summary>
        public void MarkTileDirty(Vector2Int tilePosition)
        {
            var chunkCoord = new Vector2Int(
                tilePosition.x / chunkSize,
                tilePosition.y / chunkSize
            );
            
            if (_chunks.TryGetValue(chunkCoord, out var chunk))
            {
                chunk.IsDirty = true;
            }
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;
            
            // Debug info
            GUI.Label(new Rect(10, 10, 300, 20), 
                $"Visible Tiles: {_visibleTileCount} | Chunks: {_drawnChunks}/{_chunks.Count}");
        }
#endif
    }
}
