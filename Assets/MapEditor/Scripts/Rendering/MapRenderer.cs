using System.Collections.Generic;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Rendering
{
    /// <summary>
    /// Renders the map using sprite batching for performance
    /// Optimized for 60+ FPS with large maps
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MapRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Core.MapEditorController editorController;
        [SerializeField] private TilePalette tilePalette;
        
        [Header("Grid Settings")]
        [SerializeField] private Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        [SerializeField] private Color collisionColor = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private Material gridMaterial;
        [SerializeField] private Material tileMaterial;
        
        [Header("Performance")]
        [SerializeField] private int batchSize = 1000;
        [SerializeField] private bool useGPUInstancing = true;
        
        private Camera _camera;
        private Mesh _quadMesh;
        private Mesh _gridMesh;
        
        // Batching data
        private readonly List<Matrix4x4> _tileMatrices = new List<Matrix4x4>();
        private readonly List<Vector4> _tileColors = new List<Vector4>();
        private MaterialPropertyBlock _propertyBlock;
        
        // Cached bounds for culling
        private Rect _visibleBounds;
        
        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _propertyBlock = new MaterialPropertyBlock();
            
            CreateQuadMesh();
            CreateDefaultMaterials();
        }
        
        private void OnEnable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged += MarkDirty;
                editorController.OnMapLoaded += MarkDirty;
                editorController.OnMapCreated += MarkDirty;
            }
        }
        
        private void OnDisable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged -= MarkDirty;
                editorController.OnMapLoaded -= MarkDirty;
                editorController.OnMapCreated -= MarkDirty;
            }
        }
        
        private void MarkDirty()
        {
            // Force redraw
        }
        
        private void Update()
        {
            if (editorController?.State?.CurrentMap == null)
                return;
            
            UpdateCamera();
            CalculateVisibleBounds();
        }
        
        private void OnRenderObject()
        {
            if (editorController?.State?.CurrentMap == null)
                return;
            
            var state = editorController.State;
            var map = state.CurrentMap;
            
            // Draw layers in order
            foreach (var layer in map.layers)
            {
                if (layer.isVisible)
                {
                    DrawLayer(layer, map.tileSize);
                }
            }
            
            // Draw entities
            DrawEntities(map);
            
            // Draw grid overlay
            if (state.ShowGrid)
            {
                DrawGrid(map);
            }
            
            // Draw collision overlay
            if (state.ShowCollisions)
            {
                DrawCollisions(map);
            }
        }
        
        private void UpdateCamera()
        {
            var state = editorController.State;
            
            // Update camera position
            transform.position = new Vector3(
                state.CameraPosition.x,
                state.CameraPosition.y,
                -10f
            );
            
            // Update camera zoom
            _camera.orthographicSize = 5f / state.Zoom;
        }
        
        private void CalculateVisibleBounds()
        {
            float height = _camera.orthographicSize * 2f;
            float width = height * _camera.aspect;
            
            Vector2 center = transform.position;
            
            _visibleBounds = new Rect(
                center.x - width / 2f,
                center.y - height / 2f,
                width,
                height
            );
        }
        
        private void DrawLayer(LayerData layer, float tileSize)
        {
            if (tileMaterial == null || _quadMesh == null)
                return;
            
            _tileMatrices.Clear();
            _tileColors.Clear();
            
            foreach (var tile in layer.tiles)
            {
                // Frustum culling
                Vector2 tilePos = new Vector2(tile.x * tileSize, tile.y * tileSize);
                if (!_visibleBounds.Overlaps(new Rect(tilePos, Vector2.one * tileSize)))
                    continue;
                
                // Get color from palette or use default
                Color color = Color.white;
                var paletteEntry = tilePalette?.GetTile(tile.tileId);
                if (paletteEntry != null)
                {
                    color = paletteEntry.previewColor;
                }
                
                Matrix4x4 matrix = Matrix4x4.TRS(
                    new Vector3(tilePos.x + tileSize / 2f, tilePos.y + tileSize / 2f, layer.sortingOrder * 0.1f),
                    Quaternion.identity,
                    new Vector3(tileSize, tileSize, 1f)
                );
                
                _tileMatrices.Add(matrix);
                _tileColors.Add(color);
                
                // Batch draw when full
                if (_tileMatrices.Count >= batchSize)
                {
                    FlushTileBatch();
                }
            }
            
            // Draw remaining
            if (_tileMatrices.Count > 0)
            {
                FlushTileBatch();
            }
        }
        
        private void FlushTileBatch()
        {
            if (_tileMatrices.Count == 0)
                return;
            
            if (useGPUInstancing)
            {
                // GPU instancing for better performance
                var matrices = _tileMatrices.ToArray();
                var colors = _tileColors.ToArray();
                
                _propertyBlock.SetVectorArray("_Color", colors);
                
                Graphics.DrawMeshInstanced(_quadMesh, 0, tileMaterial, matrices, matrices.Length, _propertyBlock);
            }
            else
            {
                // Fallback: individual draw calls
                for (int i = 0; i < _tileMatrices.Count; i++)
                {
                    _propertyBlock.SetColor("_Color", _tileColors[i]);
                    Graphics.DrawMesh(_quadMesh, _tileMatrices[i], tileMaterial, 0, _camera, 0, _propertyBlock);
                }
            }
            
            _tileMatrices.Clear();
            _tileColors.Clear();
        }
        
        private void DrawEntities(MapData map)
        {
            if (tilePalette == null)
                return;
            
            foreach (var entity in map.entities)
            {
                // Frustum culling
                if (!_visibleBounds.Contains(entity.Position))
                    continue;
                
                var paletteEntry = tilePalette.GetEntity(entity.entityType);
                Color color = paletteEntry?.gizmoColor ?? Color.yellow;
                Vector2 size = paletteEntry?.size ?? Vector2.one;
                
                // Draw entity gizmo
                DrawEntityGizmo(entity.Position, size, color);
            }
        }
        
        private void DrawEntityGizmo(Vector2 position, Vector2 size, Color color)
        {
            _propertyBlock.SetColor("_Color", color);
            
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(position.x, position.y, -1f),
                Quaternion.identity,
                new Vector3(size.x * 0.8f, size.y * 0.8f, 1f)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, tileMaterial, 0, _camera, 0, _propertyBlock);
        }
        
        private void DrawGrid(MapData map)
        {
            if (gridMaterial == null)
                return;
            
            gridMaterial.SetPass(0);
            
            float tileSize = map.tileSize;
            
            // Calculate visible tile range
            int startX = Mathf.Max(0, Mathf.FloorToInt(_visibleBounds.xMin / tileSize));
            int endX = Mathf.Min(map.width, Mathf.CeilToInt(_visibleBounds.xMax / tileSize));
            int startY = Mathf.Max(0, Mathf.FloorToInt(_visibleBounds.yMin / tileSize));
            int endY = Mathf.Min(map.height, Mathf.CeilToInt(_visibleBounds.yMax / tileSize));
            
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
            
            // Draw map bounds
            GL.Begin(GL.LINES);
            GL.Color(Color.white);
            
            float mapWidth = map.width * tileSize;
            float mapHeight = map.height * tileSize;
            
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(mapWidth, 0, 0);
            
            GL.Vertex3(mapWidth, 0, 0);
            GL.Vertex3(mapWidth, mapHeight, 0);
            
            GL.Vertex3(mapWidth, mapHeight, 0);
            GL.Vertex3(0, mapHeight, 0);
            
            GL.Vertex3(0, mapHeight, 0);
            GL.Vertex3(0, 0, 0);
            
            GL.End();
        }
        
        private void DrawCollisions(MapData map)
        {
            if (tileMaterial == null)
                return;
            
            float tileSize = map.tileSize;
            
            _tileMatrices.Clear();
            
            foreach (var layer in map.layers)
            {
                foreach (var tile in layer.tiles)
                {
                    if (!tile.hasCollision)
                        continue;
                    
                    // Frustum culling
                    Vector2 tilePos = new Vector2(tile.x * tileSize, tile.y * tileSize);
                    if (!_visibleBounds.Overlaps(new Rect(tilePos, Vector2.one * tileSize)))
                        continue;
                    
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(tilePos.x + tileSize / 2f, tilePos.y + tileSize / 2f, -0.5f),
                        Quaternion.identity,
                        new Vector3(tileSize * 0.9f, tileSize * 0.9f, 1f)
                    );
                    
                    _tileMatrices.Add(matrix);
                }
            }
            
            // Draw collision overlays
            _propertyBlock.SetColor("_Color", collisionColor);
            
            if (_tileMatrices.Count > 0)
            {
                var matrices = _tileMatrices.ToArray();
                Graphics.DrawMeshInstanced(_quadMesh, 0, tileMaterial, matrices, matrices.Length, _propertyBlock);
            }
            
            _tileMatrices.Clear();
        }
        
        private void CreateQuadMesh()
        {
            _quadMesh = new Mesh
            {
                name = "EditorQuad",
                vertices = new Vector3[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3(0.5f, -0.5f, 0),
                    new Vector3(0.5f, 0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0)
                },
                uv = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1)
                },
                triangles = new int[] { 0, 2, 1, 0, 3, 2 }
            };
            _quadMesh.RecalculateNormals();
            _quadMesh.RecalculateBounds();
        }
        
        private void CreateDefaultMaterials()
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
            if (_quadMesh != null)
                Destroy(_quadMesh);
        }
    }
}
