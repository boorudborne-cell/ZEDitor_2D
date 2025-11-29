using System.Collections.Generic;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Core
{
    /// <summary>
    /// Renders tiles as sprites with preview ghost under cursor
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
        [SerializeField] private Color boundsColor = new Color(1, 1, 1, 0.5f);
        [SerializeField] private Color boundsOutsideColor = new Color(0.1f, 0.1f, 0.1f, 0.3f);
        [SerializeField] private float boundsLineWidth = 0.05f;
        
        private Dictionary<string, SpriteRenderer> _renderers = new Dictionary<string, SpriteRenderer>();
        private SpriteRenderer _previewRenderer;
        private SpriteRenderer _collisionOverlay;
        private LineRenderer _boundsLine;
        private SpriteRenderer _outsideOverlay;
        private Camera _cam;
        
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
        }
        
        private void OnEnable()
        {
            if (controller == null) return;
            
            controller.OnMapChanged += RebuildAll;
            controller.OnTilePlaced += RebuildAll;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            
            controller.OnMapChanged -= RebuildAll;
            controller.OnTilePlaced -= RebuildAll;
        }
        
        private void Update()
        {
            UpdateCameraFromState();
            UpdateHoveredTile();
            UpdatePreview(); // Update preview every frame
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
            
            // Collision overlay for preview
            var overlayGo = new GameObject("CollisionOverlay");
            overlayGo.transform.SetParent(previewGo.transform);
            overlayGo.transform.localPosition = Vector3.zero;
            _collisionOverlay = overlayGo.AddComponent<SpriteRenderer>();
            _collisionOverlay.sortingOrder = 1001;
            _collisionOverlay.color = collisionTint;
        }
        
        private void CreateBoundsRenderer()
        {
            // Line renderer for bounds rectangle
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
            _boundsLine.sortingOrder = -10;
            
            // Create simple material
            _boundsLine.material = new Material(Shader.Find("Sprites/Default"));
            _boundsLine.material.color = boundsColor;
            
            // Outside area overlay (dark tint)
            var outsideGo = new GameObject("OutsideArea");
            outsideGo.transform.SetParent(transform);
            _outsideOverlay = outsideGo.AddComponent<SpriteRenderer>();
            _outsideOverlay.sortingOrder = -100;
            _outsideOverlay.color = boundsOutsideColor;
            
            // Create a simple white square sprite for the outside overlay
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _outsideOverlay.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            
            boundsGo.SetActive(false);
            outsideGo.SetActive(false);
        }
        
        private void UpdatePreview()
        {
            if (_previewRenderer == null || controller?.State == null) return;
            
            var state = controller.State;
            var hoveredPos = state.HoveredTile;
            
            // Hide preview if wrong tool
            if (state.ActiveTool == EditorTool.Eraser)
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
            else if (state.ActiveTool == EditorTool.Entity)
            {
                if (!string.IsNullOrEmpty(state.SelectedEntityId) && controller.Palette != null)
                {
                    var entityDef = controller.Palette.GetEntity(state.SelectedEntityId);
                    previewSprite = entityDef?.icon;
                }
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
            
            // Show collision overlay
            _collisionOverlay.gameObject.SetActive(hasCollision && state.ShowCollisions);
            if (hasCollision)
            {
                _collisionOverlay.sprite = previewSprite;
            }
        }
        
        public void RebuildAll()
        {
            ClearRenderers();
            UpdateBounds();
            
            if (controller?.State?.Map == null) return;
            
            var map = controller.State.Map;
            var palette = controller.Palette;
            
            foreach (var layer in map.layers)
            {
                if (!layer.isVisible) continue;
                
                foreach (var tile in layer.tiles)
                {
                    var tileDef = palette?.GetTile(tile.tileId);
                    if (tileDef?.sprite == null) continue;
                    
                    CreateTileRenderer(tile, tileDef, layer.sortingOrder);
                }
            }
            
            // Render entities
            foreach (var entity in map.entities)
            {
                var entityDef = palette?.GetEntity(entity.entityType);
                if (entityDef?.icon == null) continue;
                
                CreateEntityRenderer(entity, entityDef);
            }
        }
        
        private void CreateTileRenderer(TileData tile, TileDefinition def, int sortOrder)
        {
            var go = tilePrefab != null ? 
                Instantiate(tilePrefab, tilesParent) : 
                new GameObject($"Tile_{tile.x}_{tile.y}");
            
            go.transform.SetParent(tilesParent);
            go.transform.position = new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0);
            
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            
            sr.sprite = def.sprite;
            sr.sortingOrder = sortOrder;
            
            string key = $"{tile.x}_{tile.y}_{sortOrder}";
            _renderers[key] = sr;
            
            // Add collision indicator
            if (tile.hasCollision && controller.State.ShowCollisions)
            {
                var overlay = new GameObject("Collision");
                overlay.transform.SetParent(go.transform);
                overlay.transform.localPosition = Vector3.zero;
                var overlaySr = overlay.AddComponent<SpriteRenderer>();
                overlaySr.sprite = def.sprite;
                overlaySr.color = collisionTint;
                overlaySr.sortingOrder = sortOrder + 100;
            }
        }
        
        private void CreateEntityRenderer(EntityData entity, EntityDefinition def)
        {
            var go = new GameObject($"Entity_{entity.entityId}");
            go.transform.SetParent(tilesParent);
            go.transform.position = new Vector3(entity.x + 0.5f, entity.y + 0.5f, 0);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = def.icon;
            sr.sortingOrder = 500;
            sr.color = def.gizmoColor;
            
            _renderers[$"entity_{entity.entityId}"] = sr;
        }
        
        private void ClearRenderers()
        {
            foreach (var r in _renderers.Values)
            {
                if (r != null) Destroy(r.gameObject);
            }
            _renderers.Clear();
        }
        
        private void UpdateBounds()
        {
            var map = controller?.State?.Map;
            
            if (map == null || _boundsLine == null)
            {
                if (_boundsLine != null) _boundsLine.gameObject.SetActive(false);
                if (_outsideOverlay != null) _outsideOverlay.gameObject.SetActive(false);
                return;
            }
            
            float w = map.width;
            float h = map.height;
            
            // Update bounds rectangle
            _boundsLine.gameObject.SetActive(true);
            _boundsLine.SetPosition(0, new Vector3(0, 0, 0));
            _boundsLine.SetPosition(1, new Vector3(w, 0, 0));
            _boundsLine.SetPosition(2, new Vector3(w, h, 0));
            _boundsLine.SetPosition(3, new Vector3(0, h, 0));
            
            // Update outside overlay - make it very large and position behind bounds
            // This creates a "window" effect where inside is clear
            _outsideOverlay.gameObject.SetActive(false); // Disable for now, just show bounds line
        }
        
        private void OnDestroy()
        {
            ClearRenderers();
        }
    }
}
