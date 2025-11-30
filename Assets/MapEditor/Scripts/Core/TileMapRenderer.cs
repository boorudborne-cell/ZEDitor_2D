using System.Collections.Generic;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Core
{
    /// <summary>
    /// Renders tiles as sprites with preview ghost under cursor and optional grid.
    /// Optimized for incremental updates instead of full rebuilds.
    /// </summary>
    public class TileMapRenderer : MonoBehaviour
    {
        [SerializeField] private MapEditorController controller;
        [SerializeField] private Transform tilesParent;
        [SerializeField] private GameObject tilePrefab;
        
        [Header("Preview")]
        [SerializeField] private Color previewColor = new Color(1, 1, 1, 0.5f);
        [SerializeField] private Color collisionTint = new Color(1, 0.5f, 0.5f, 0.3f);
        
        [Header("Bounds")]
        [SerializeField] private Color boundsColor = new Color(0.3f, 0.6f, 1f, 0.8f);
        [SerializeField] private float boundsLineWidth = 0.08f;
        
        [Header("Grid")]
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private float gridLineWidth = 0.02f;
        
        [Header("Performance")]
        [SerializeField] private bool usePooling = true;
        [SerializeField] private int initialPoolSize = 100;
        
        // Tile renderers indexed by "x_y_layer"
        private Dictionary<string, SpriteRenderer> _tileRenderers = new Dictionary<string, SpriteRenderer>();
        // Prefab renderers indexed by instanceId
        private Dictionary<string, SpriteRenderer> _prefabRenderers = new Dictionary<string, SpriteRenderer>();
        // Object pool for tile GameObjects
        private Queue<GameObject> _tilePool = new Queue<GameObject>();
        
        private SpriteRenderer _previewRenderer;
        private SpriteRenderer _collisionOverlay;
        private LineRenderer _boundsLine;
        private List<LineRenderer> _gridLines = new List<LineRenderer>();
        private Transform _gridParent;
        private Camera _cam;
        
        private bool _lastShowGrid;
        private bool _lastShowCollisions;
        private int _lastMapWidth, _lastMapHeight;
        private bool _isPlayMode;
        
        // Track last known state for dirty checking
        private MapData _lastMap;
        private Dictionary<string, string> _lastTileStates = new Dictionary<string, string>();
        private HashSet<string> _lastPrefabIds = new HashSet<string>();
        
        private void Awake()
        {
            _cam = Camera.main;
            
            if (tilesParent == null)
            {
                var go = new GameObject("TilesParent");
                go.transform.SetParent(transform);
                tilesParent = go.transform;
            }
            
            CreatePreviewRenderer();
            CreateBoundsRenderer();
            CreateGridParent();
            
            if (usePooling)
                InitializePool();
        }
        
        private void InitializePool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                var go = CreateTileGameObject();
                go.SetActive(false);
                _tilePool.Enqueue(go);
            }
        }
        
        private GameObject CreateTileGameObject()
        {
            GameObject go;
            if (tilePrefab != null)
            {
                go = Instantiate(tilePrefab, tilesParent);
            }
            else
            {
                go = new GameObject("PooledTile");
                go.transform.SetParent(tilesParent);
                go.AddComponent<SpriteRenderer>();
            }
            return go;
        }
        
        private GameObject GetFromPool()
        {
            if (_tilePool.Count > 0)
            {
                var go = _tilePool.Dequeue();
                go.SetActive(true);
                return go;
            }
            return CreateTileGameObject();
        }
        
        private void ReturnToPool(GameObject go)
        {
            if (go == null) return;
            
            // Clean up collision overlay children
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(go.transform.GetChild(i).gameObject);
            }
            
            go.SetActive(false);
            _tilePool.Enqueue(go);
        }
        
        private void OnEnable()
        {
            if (controller == null) return;
            
            controller.OnMapChanged += OnMapChanged;
            controller.OnTilePlaced += OnTilePlaced;
            if (controller.State != null)
                controller.State.OnChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            
            controller.OnMapChanged -= OnMapChanged;
            controller.OnTilePlaced -= OnTilePlaced;
            if (controller.State != null)
                controller.State.OnChanged -= OnStateChanged;
        }
        
        private void Update()
        {
            // Don't update editor visuals in play mode
            if (_isPlayMode) return;
            
            UpdateCameraFromState();
            UpdateHoveredTile();
            UpdatePreview();
        }
        
        private void OnMapChanged()
        {
            // Full rebuild only when map changes completely
            RebuildAll();
        }
        
        private void OnTilePlaced()
        {
            // Incremental update - only sync changed tiles
            SyncTiles();
            SyncPrefabs();
        }
        
        private void OnStateChanged()
        {
            if (controller?.State == null) return;
            
            // Don't respond to state changes in play mode
            if (_isPlayMode) return;
            
            // Check if grid visibility changed
            if (_lastShowGrid != controller.State.ShowGrid)
            {
                _lastShowGrid = controller.State.ShowGrid;
                UpdateGridVisibility();
            }
            
            // Check if collision visibility changed
            if (_lastShowCollisions != controller.State.ShowCollisions)
            {
                _lastShowCollisions = controller.State.ShowCollisions;
                UpdateCollisionVisibility();
            }
        }
        
        private void UpdateCameraFromState()
        {
            if (_cam == null || controller?.State == null) return;
            
            var state = controller.State;
            _cam.transform.position = new Vector3(
                state.CameraOffset.x,
                state.CameraOffset.y,
                -10f
            );
            _cam.orthographicSize = 5f / state.Zoom;
        }
        
        private void UpdateHoveredTile()
        {
            if (_cam == null || controller?.State == null) return;
            
            Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int tilePos = new Vector2Int(
                Mathf.FloorToInt(mouseWorld.x),
                Mathf.FloorToInt(mouseWorld.y)
            );
            
            controller.SetHoveredTile(tilePos);
        }
        
        private void CreatePreviewRenderer()
        {
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(transform);
            _previewRenderer = previewGo.AddComponent<SpriteRenderer>();
            _previewRenderer.sortingOrder = 1000;
            _previewRenderer.color = previewColor;
            previewGo.SetActive(false);
            
            var overlayGo = new GameObject("CollisionOverlay");
            overlayGo.transform.SetParent(previewGo.transform);
            overlayGo.transform.localPosition = Vector3.zero;
            _collisionOverlay = overlayGo.AddComponent<SpriteRenderer>();
            _collisionOverlay.sortingOrder = 1001;
            _collisionOverlay.color = collisionTint;
        }
        
        private void CreateBoundsRenderer()
        {
            var boundsGo = new GameObject("Bounds");
            boundsGo.transform.SetParent(transform);
            _boundsLine = boundsGo.AddComponent<LineRenderer>();
            _boundsLine.useWorldSpace = true;
            _boundsLine.loop = true;
            _boundsLine.positionCount = 4;
            _boundsLine.startWidth = boundsLineWidth;
            _boundsLine.endWidth = boundsLineWidth;
            _boundsLine.startColor = boundsColor;
            _boundsLine.endColor = boundsColor;
            _boundsLine.sortingOrder = 900;
            
            _boundsLine.material = new Material(Shader.Find("Sprites/Default"));
            _boundsLine.material.color = boundsColor;
            
            boundsGo.SetActive(false);
        }
        
        private void CreateGridParent()
        {
            var go = new GameObject("Grid");
            go.transform.SetParent(transform);
            _gridParent = go.transform;
        }
        
        private void UpdatePreview()
        {
            if (_previewRenderer == null || controller?.State == null) return;
            
            var state = controller.State;
            var hoveredPos = state.HoveredTile;
            
            // Check bounds
            bool inBounds = state.Map != null && 
                hoveredPos.x >= 0 && hoveredPos.x < state.Map.width &&
                hoveredPos.y >= 0 && hoveredPos.y < state.Map.height;
            
            if (state.ActiveTool == EditorTool.Eraser || !inBounds)
            {
                _previewRenderer.gameObject.SetActive(false);
                return;
            }
            
            Sprite previewSprite = null;
            bool hasCollision = false;
            
            if (state.ActiveTool == EditorTool.Brush)
            {
                if (!string.IsNullOrEmpty(state.SelectedTileId) && controller.Palette != null)
                {
                    var tileDef = controller.Palette.GetTile(state.SelectedTileId);
                    if (tileDef != null)
                    {
                        previewSprite = tileDef.sprite;
                        hasCollision = tileDef.hasCollision;
                    }
                }
            }
            else if (state.ActiveTool == EditorTool.Prefab)
            {
                if (!string.IsNullOrEmpty(state.SelectedPrefabId) && controller.Palette != null)
                {
                    var prefabDef = controller.Palette.GetPrefab(state.SelectedPrefabId);
                    previewSprite = prefabDef?.icon;
                }
            }
            else if (state.ActiveTool == EditorTool.Eyedropper)
            {
                // Show what's under cursor with highlight
                var layer = state.Map.GetLayer(state.ActiveLayer);
                var tile = layer?.GetTile(hoveredPos);
                
                if (tile != null && controller.Palette != null)
                {
                    var tileDef = controller.Palette.GetTile(tile.tileId);
                    previewSprite = tileDef?.sprite;
                }
                else
                {
                    // Check for prefab
                    var prefab = state.Map.GetPrefabAt(hoveredPos);
                    if (prefab != null && controller.Palette != null)
                    {
                        var prefabDef = controller.Palette.GetPrefab(prefab.prefabId);
                        previewSprite = prefabDef?.icon;
                    }
                }
                
                // Different color for eyedropper preview
                _previewRenderer.color = new Color(1, 1, 0, 0.5f);
            }
            
            if (previewSprite == null)
            {
                _previewRenderer.gameObject.SetActive(false);
                return;
            }
            
            _previewRenderer.gameObject.SetActive(true);
            _previewRenderer.sprite = previewSprite;
            _previewRenderer.transform.position = new Vector3(
                hoveredPos.x + 0.5f,
                hoveredPos.y + 0.5f,
                0
            );
            
            // Reset color if not eyedropper
            if (state.ActiveTool != EditorTool.Eyedropper)
            {
                _previewRenderer.color = previewColor;
            }
            
            _collisionOverlay.gameObject.SetActive(hasCollision && state.ShowCollisions);
            if (hasCollision)
            {
                _collisionOverlay.sprite = previewSprite;
            }
        }
        
        /// <summary>
        /// Full rebuild - only called when map changes completely
        /// </summary>
        public void RebuildAll()
        {
            ClearAllRenderers();
            _lastTileStates.Clear();
            _lastPrefabIds.Clear();
            
            UpdateBounds();
            UpdateGrid();
            
            if (controller?.State?.Map == null) return;
            
            _lastMap = controller.State.Map;
            _lastShowCollisions = controller.State.ShowCollisions;
            
            var map = controller.State.Map;
            var palette = controller.Palette;
            
            // Build all tiles
            foreach (var layer in map.layers)
            {
                if (!layer.isVisible) continue;
                
                foreach (var tile in layer.tiles)
                {
                    var tileDef = palette?.GetTile(tile.tileId);
                    if (tileDef?.sprite == null) continue;
                    
                    string key = GetTileKey(tile.x, tile.y, layer.sortingOrder);
                    CreateOrUpdateTileRenderer(key, tile, tileDef, layer.sortingOrder);
                    _lastTileStates[key] = GetTileState(tile);
                }
            }
            
            // Build all prefabs
            foreach (var prefab in map.prefabs)
            {
                var prefabDef = palette?.GetPrefab(prefab.prefabId);
                if (prefabDef?.icon == null) continue;
                
                CreateOrUpdatePrefabRenderer(prefab, prefabDef);
                _lastPrefabIds.Add(prefab.instanceId);
            }
        }
        
        /// <summary>
        /// Incremental sync - only updates changed tiles
        /// </summary>
        private void SyncTiles()
        {
            if (controller?.State?.Map == null) return;
            
            var map = controller.State.Map;
            var palette = controller.Palette;
            
            HashSet<string> currentKeys = new HashSet<string>();
            
            // Check each layer for changes
            foreach (var layer in map.layers)
            {
                if (!layer.isVisible) continue;
                
                foreach (var tile in layer.tiles)
                {
                    string key = GetTileKey(tile.x, tile.y, layer.sortingOrder);
                    currentKeys.Add(key);
                    
                    string newState = GetTileState(tile);
                    
                    // Check if tile changed or is new
                    if (!_lastTileStates.TryGetValue(key, out string oldState) || oldState != newState)
                    {
                        var tileDef = palette?.GetTile(tile.tileId);
                        if (tileDef?.sprite != null)
                        {
                            CreateOrUpdateTileRenderer(key, tile, tileDef, layer.sortingOrder);
                            _lastTileStates[key] = newState;
                        }
                    }
                }
            }
            
            // Remove tiles that no longer exist
            var keysToRemove = new List<string>();
            foreach (var key in _lastTileStates.Keys)
            {
                if (!currentKeys.Contains(key))
                {
                    keysToRemove.Add(key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                RemoveTileRenderer(key);
                _lastTileStates.Remove(key);
            }
        }
        
        /// <summary>
        /// Incremental sync for prefabs
        /// </summary>
        private void SyncPrefabs()
        {
            if (controller?.State?.Map == null) return;
            
            var map = controller.State.Map;
            var palette = controller.Palette;
            
            HashSet<string> currentIds = new HashSet<string>();
            
            foreach (var prefab in map.prefabs)
            {
                currentIds.Add(prefab.instanceId);
                
                // Create if new
                if (!_lastPrefabIds.Contains(prefab.instanceId))
                {
                    var prefabDef = palette?.GetPrefab(prefab.prefabId);
                    if (prefabDef?.icon != null)
                    {
                        CreateOrUpdatePrefabRenderer(prefab, prefabDef);
                    }
                }
            }
            
            // Remove prefabs that no longer exist
            var idsToRemove = new List<string>();
            foreach (var id in _lastPrefabIds)
            {
                if (!currentIds.Contains(id))
                {
                    idsToRemove.Add(id);
                }
            }
            
            foreach (var id in idsToRemove)
            {
                RemovePrefabRenderer(id);
            }
            
            _lastPrefabIds = currentIds;
        }
        
        private string GetTileKey(int x, int y, int sortOrder)
        {
            return $"{x}_{y}_{sortOrder}";
        }
        
        private string GetTileState(TileData tile)
        {
            return $"{tile.tileId}_{tile.hasCollision}";
        }
        
        private void CreateOrUpdateTileRenderer(string key, TileData tile, TileDefinition def, int sortOrder)
        {
            SpriteRenderer sr;
            GameObject go;
            
            if (_tileRenderers.TryGetValue(key, out sr) && sr != null)
            {
                // Update existing
                go = sr.gameObject;
            }
            else
            {
                // Create new from pool
                go = GetFromPool();
                go.name = $"Tile_{tile.x}_{tile.y}";
                sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                _tileRenderers[key] = sr;
            }
            
            go.transform.position = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0);
            sr.sprite = def.sprite;
            sr.sortingOrder = sortOrder;
            
            // Handle collision overlay
            UpdateTileCollisionOverlay(go, tile, def, sortOrder);
        }
        
        private void UpdateTileCollisionOverlay(GameObject tileGo, TileData tile, TileDefinition def, int sortOrder)
        {
            Transform overlayTransform = tileGo.transform.Find("Collision");
            
            // Don't show collision overlays in play mode
            bool shouldShowCollision = tile.hasCollision && controller.State.ShowCollisions && !_isPlayMode;
            
            if (shouldShowCollision)
            {
                GameObject overlayGo;
                SpriteRenderer overlaySr;
                
                if (overlayTransform != null)
                {
                    overlayGo = overlayTransform.gameObject;
                    overlaySr = overlayGo.GetComponent<SpriteRenderer>();
                }
                else
                {
                    overlayGo = new GameObject("Collision");
                    overlayGo.transform.SetParent(tileGo.transform);
                    overlayGo.transform.localPosition = Vector3.zero;
                    overlaySr = overlayGo.AddComponent<SpriteRenderer>();
                }
                
                overlaySr.sprite = def.sprite;
                overlaySr.color = collisionTint;
                overlaySr.sortingOrder = sortOrder + 100;
                overlayGo.SetActive(true);
            }
            else if (overlayTransform != null)
            {
                overlayTransform.gameObject.SetActive(false);
            }
        }
        
        private void RemoveTileRenderer(string key)
        {
            if (_tileRenderers.TryGetValue(key, out var sr) && sr != null)
            {
                ReturnToPool(sr.gameObject);
                _tileRenderers.Remove(key);
            }
        }
        
        private void CreateOrUpdatePrefabRenderer(PrefabData prefab, PrefabDefinition def)
        {
            SpriteRenderer sr;
            GameObject go;
            
            string key = prefab.instanceId;
            
            if (_prefabRenderers.TryGetValue(key, out sr) && sr != null)
            {
                go = sr.gameObject;
            }
            else
            {
                go = new GameObject($"Prefab_{prefab.instanceId}");
                go.transform.SetParent(tilesParent);
                sr = go.AddComponent<SpriteRenderer>();
                _prefabRenderers[key] = sr;
            }
            
            go.transform.position = new Vector3(prefab.x + 0.5f, prefab.y + 0.5f, 0);
            go.transform.rotation = Quaternion.Euler(0, 0, prefab.rotation);
            go.transform.localScale = prefab.scale;
            
            sr.sprite = def.icon;
            sr.sortingOrder = 500;
            sr.color = def.gizmoColor;
        }
        
        private void RemovePrefabRenderer(string instanceId)
        {
            if (_prefabRenderers.TryGetValue(instanceId, out var sr) && sr != null)
            {
                Destroy(sr.gameObject);
                _prefabRenderers.Remove(instanceId);
            }
        }
        
        private void ClearAllRenderers()
        {
            foreach (var sr in _tileRenderers.Values)
            {
                if (sr != null) ReturnToPool(sr.gameObject);
            }
            _tileRenderers.Clear();
            
            foreach (var sr in _prefabRenderers.Values)
            {
                if (sr != null) Destroy(sr.gameObject);
            }
            _prefabRenderers.Clear();
        }
        
        private void UpdateBounds()
        {
            var map = controller?.State?.Map;
            
            if (map == null || _boundsLine == null)
            {
                if (_boundsLine != null) _boundsLine.gameObject.SetActive(false);
                return;
            }
            
            float w = map.width;
            float h = map.height;
            
            _boundsLine.gameObject.SetActive(true);
            _boundsLine.SetPosition(0, new Vector3(0, 0, 0));
            _boundsLine.SetPosition(1, new Vector3(w, 0, 0));
            _boundsLine.SetPosition(2, new Vector3(w, h, 0));
            _boundsLine.SetPosition(3, new Vector3(0, h, 0));
        }
        
        private void UpdateGrid()
        {
            foreach (var line in _gridLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            _gridLines.Clear();
            
            var map = controller?.State?.Map;
            if (map == null) return;
            
            _lastMapWidth = map.width;
            _lastMapHeight = map.height;
            _lastShowGrid = controller.State.ShowGrid;
            
            if (!controller.State.ShowGrid) return;
            
            var material = new Material(Shader.Find("Sprites/Default"));
            material.color = gridColor;
            
            // Vertical lines
            for (int x = 0; x <= map.width; x++)
            {
                var line = CreateGridLine($"GridV_{x}", material);
                line.SetPosition(0, new Vector3(x, 0, 0));
                line.SetPosition(1, new Vector3(x, map.height, 0));
                _gridLines.Add(line);
            }
            
            // Horizontal lines
            for (int y = 0; y <= map.height; y++)
            {
                var line = CreateGridLine($"GridH_{y}", material);
                line.SetPosition(0, new Vector3(0, y, 0));
                line.SetPosition(1, new Vector3(map.width, y, 0));
                _gridLines.Add(line);
            }
        }
        
        private LineRenderer CreateGridLine(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_gridParent);
            
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = gridLineWidth;
            line.endWidth = gridLineWidth;
            line.material = material;
            line.startColor = gridColor;
            line.endColor = gridColor;
            line.sortingOrder = -5;
            
            return line;
        }
        
        private void UpdateGridVisibility()
        {
            if (controller?.State?.Map == null) return;
            
            if (controller.State.ShowGrid && _gridLines.Count == 0)
            {
                UpdateGrid();
            }
            else if (!controller.State.ShowGrid)
            {
                foreach (var line in _gridLines)
                {
                    if (line != null) line.gameObject.SetActive(false);
                }
            }
            else
            {
                foreach (var line in _gridLines)
                {
                    if (line != null) line.gameObject.SetActive(true);
                }
            }
        }
        
        private void UpdateCollisionVisibility()
        {
            // Don't show collision overlays in play mode
            if (_isPlayMode) return;
            
            // Update collision overlays for all tiles
            foreach (var kvp in _tileRenderers)
            {
                if (kvp.Value == null) continue;
                
                var collisionTransform = kvp.Value.transform.Find("Collision");
                if (collisionTransform != null)
                {
                    collisionTransform.gameObject.SetActive(controller.State.ShowCollisions);
                }
            }
        }
        
        /// <summary>
        /// Toggle Play Mode visuals - hides grid, bounds, collision overlays, and prefab gizmos
        /// </summary>
        public void SetPlayMode(bool isPlayMode)
        {
            _isPlayMode = isPlayMode;
            
            if (isPlayMode)
            {
                // Hide grid
                foreach (var line in _gridLines)
                {
                    if (line != null) line.gameObject.SetActive(false);
                }
                
                // Hide bounds
                if (_boundsLine != null)
                    _boundsLine.gameObject.SetActive(false);
                
                // Hide preview
                if (_previewRenderer != null)
                    _previewRenderer.gameObject.SetActive(false);
                
                // Hide collision overlays
                foreach (var kvp in _tileRenderers)
                {
                    if (kvp.Value == null) continue;
                    var collisionTransform = kvp.Value.transform.Find("Collision");
                    if (collisionTransform != null)
                        collisionTransform.gameObject.SetActive(false);
                }
                
                // Hide prefab gizmos (the editor preview sprites)
                foreach (var kvp in _prefabRenderers)
                {
                    if (kvp.Value != null)
                        kvp.Value.gameObject.SetActive(false);
                }
            }
            else
            {
                // Restore grid based on state
                if (controller?.State != null && controller.State.ShowGrid)
                {
                    foreach (var line in _gridLines)
                    {
                        if (line != null) line.gameObject.SetActive(true);
                    }
                }
                
                // Show bounds
                if (_boundsLine != null && controller?.State?.Map != null)
                    _boundsLine.gameObject.SetActive(true);
                
                // Restore collision overlays based on state
                if (controller?.State != null && controller.State.ShowCollisions)
                {
                    foreach (var kvp in _tileRenderers)
                    {
                        if (kvp.Value == null) continue;
                        var collisionTransform = kvp.Value.transform.Find("Collision");
                        if (collisionTransform != null)
                            collisionTransform.gameObject.SetActive(true);
                    }
                }
                
                // Show prefab gizmos
                foreach (var kvp in _prefabRenderers)
                {
                    if (kvp.Value != null)
                        kvp.Value.gameObject.SetActive(true);
                }
            }
        }
        
        private void OnDestroy()
        {
            ClearAllRenderers();
            
            // Clear pool
            while (_tilePool.Count > 0)
            {
                var go = _tilePool.Dequeue();
                if (go != null) Destroy(go);
            }
            
            foreach (var line in _gridLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            _gridLines.Clear();
        }
    }
}
