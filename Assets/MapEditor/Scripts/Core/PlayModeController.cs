using System;
using System.Collections.Generic;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Core
{
    /// <summary>
    /// Manages Play Mode - spawns actual prefabs and collision tiles
    /// </summary>
    public class PlayModeController : MonoBehaviour
    {
        [SerializeField] private MapEditorController editorController;
        [SerializeField] private TileMapRenderer tileMapRenderer;
        [SerializeField] private TilePalette palette;
        [SerializeField] private Transform playModeParent;
        
        [Header("Player Settings")]
        [SerializeField] private GameObject defaultPlayerPrefab;
        [SerializeField] private Vector2 defaultSpawnPoint = new Vector2(1, 1);
        [SerializeField] private string playerPrefabTag = "Player";
        
        [Header("Camera Settings")]
        [SerializeField] private bool followPlayer = true;
        [SerializeField] private float cameraSmoothing = 5f;
        [SerializeField] private Vector2 cameraBoundsMargin = new Vector2(2, 2);
        
        [Header("Collision Settings")]
        [SerializeField] private PhysicsMaterial2D collisionMaterial;
        [SerializeField] private string collisionLayer = "Default";
        
        public bool IsPlaying { get; private set; }
        
        public event Action OnPlayModeStarted;
        public event Action OnPlayModeStopped;
        
        private List<GameObject> _spawnedPrefabs = new List<GameObject>();
        private List<GameObject> _spawnedColliders = new List<GameObject>();
        private GameObject _collisionParent;
        private GameObject _spawnedPlayer;
        private Camera _cam;
        private Vector3 _savedCameraPos;
        private float _savedCameraSize;
        
        // Editor state backup
        private Vector2 _savedCameraOffset;
        private float _savedZoom;
        
        private void Awake()
        {
            _cam = Camera.main;
            
            if (playModeParent == null)
            {
                var go = new GameObject("PlayModeObjects");
                go.transform.SetParent(transform);
                playModeParent = go.transform;
            }
            
            if (palette == null && editorController != null)
            {
                palette = editorController.Palette;
            }
            
            if (tileMapRenderer == null)
            {
                tileMapRenderer = FindObjectOfType<TileMapRenderer>();
            }
        }
        
        private void Update()
        {
            if (IsPlaying && followPlayer && _spawnedPlayer != null)
            {
                FollowPlayerCamera();
            }
        }
        
        /// <summary>
        /// Start Play Mode - spawns all prefabs and collision tiles
        /// </summary>
        public void StartPlayMode()
        {
            if (IsPlaying) return;
            if (editorController?.State?.Map == null)
            {
                Debug.LogWarning("[PlayMode] No map loaded");
                return;
            }
            
            IsPlaying = true;
            
            // Backup editor camera state
            SaveEditorState();
            
            // Create collision parent
            _collisionParent = new GameObject("Collisions");
            _collisionParent.transform.SetParent(playModeParent);
            
            // Spawn collision tiles
            SpawnCollisionTiles();
            
            // Spawn all prefabs from the map
            SpawnAllPrefabs();
            
            // Find or spawn player
            FindOrSpawnPlayer();
            
            // Setup camera for play mode
            SetupPlayModeCamera();
            
            // Hide editor visuals
            tileMapRenderer?.SetPlayMode(true);
            
            OnPlayModeStarted?.Invoke();
            
            Debug.Log($"[PlayMode] Started - {_spawnedPrefabs.Count} prefabs, {_spawnedColliders.Count} colliders");
        }
        
        /// <summary>
        /// Stop Play Mode - destroys spawned objects and restores editor state
        /// </summary>
        public void StopPlayMode()
        {
            if (!IsPlaying) return;
            
            IsPlaying = false;
            
            // Destroy all spawned prefabs and colliders
            DestroySpawnedObjects();
            
            // Show editor visuals
            tileMapRenderer?.SetPlayMode(false);
            
            // Restore editor camera state
            RestoreEditorState();
            
            OnPlayModeStopped?.Invoke();
            
            Debug.Log("[PlayMode] Stopped");
        }
        
        /// <summary>
        /// Toggle between Play and Edit modes
        /// </summary>
        public void TogglePlayMode()
        {
            if (IsPlaying)
                StopPlayMode();
            else
                StartPlayMode();
        }
        
        private void SaveEditorState()
        {
            if (editorController?.State != null)
            {
                _savedCameraOffset = editorController.State.CameraOffset;
                _savedZoom = editorController.State.Zoom;
            }
            
            if (_cam != null)
            {
                _savedCameraPos = _cam.transform.position;
                _savedCameraSize = _cam.orthographicSize;
            }
        }
        
        /// <summary>
        /// Spawns BoxCollider2D for all tiles with hasCollision = true
        /// </summary>
        private void SpawnCollisionTiles()
        {
            var map = editorController.State.Map;
            int layerMask = LayerMask.NameToLayer(collisionLayer);
            if (layerMask == -1) layerMask = 0;
            
            // Collect all collision tiles and merge adjacent ones
            var collisionTiles = new HashSet<Vector2Int>();
            
            foreach (var layer in map.layers)
            {
                foreach (var tile in layer.tiles)
                {
                    if (tile.hasCollision)
                    {
                        collisionTiles.Add(new Vector2Int(tile.x, tile.y));
                    }
                }
            }
            
            if (collisionTiles.Count == 0) return;
            
            // Option 1: Simple approach - one collider per tile
            // For better performance with many tiles, use CompositeCollider2D
            
            // Create a single GameObject with CompositeCollider2D for merged collisions
            var compositeGo = new GameObject("TileCollisions");
            compositeGo.transform.SetParent(_collisionParent.transform);
            compositeGo.layer = layerMask;
            
            var rb = compositeGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            
            var composite = compositeGo.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            
            if (collisionMaterial != null)
            {
                composite.sharedMaterial = collisionMaterial;
            }
            
            // Add individual BoxCollider2D for each tile (will be merged by CompositeCollider2D)
            foreach (var pos in collisionTiles)
            {
                var tileGo = new GameObject($"Col_{pos.x}_{pos.y}");
                tileGo.transform.SetParent(compositeGo.transform);
                tileGo.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);
                tileGo.layer = layerMask;
                
                var box = tileGo.AddComponent<BoxCollider2D>();
                box.size = Vector2.one;
                box.usedByComposite = true;
                
                _spawnedColliders.Add(tileGo);
            }
            
            _spawnedColliders.Add(compositeGo);
            
            Debug.Log($"[PlayMode] Created {collisionTiles.Count} collision tiles");
        }
        
        private void RestoreEditorState()
        {
            if (editorController?.State != null)
            {
                editorController.State.CameraOffset = _savedCameraOffset;
                editorController.State.Zoom = _savedZoom;
                editorController.State.NotifyChanged();
            }
        }
        
        private void SpawnAllPrefabs()
        {
            var map = editorController.State.Map;
            
            foreach (var prefabData in map.prefabs)
            {
                var prefabDef = palette?.GetPrefab(prefabData.prefabId);
                if (prefabDef?.prefab == null)
                {
                    Debug.LogWarning($"[PlayMode] Prefab not found: {prefabData.prefabId}");
                    continue;
                }
                
                Vector3 position = new Vector3(
                    prefabData.x + 0.5f,
                    prefabData.y + 0.5f,
                    0
                );
                
                Quaternion rotation = Quaternion.Euler(0, 0, prefabData.rotation);
                
                var instance = Instantiate(prefabDef.prefab, position, rotation, playModeParent);
                instance.transform.localScale = prefabData.scale;
                instance.name = $"[Play] {prefabDef.displayName}_{prefabData.instanceId}";
                
                // Store custom data if the prefab has a receiver component
                var dataReceiver = instance.GetComponent<IPrefabDataReceiver>();
                if (dataReceiver != null && !string.IsNullOrEmpty(prefabData.customData))
                {
                    dataReceiver.ReceiveData(prefabData.customData);
                }
                
                _spawnedPrefabs.Add(instance);
            }
        }
        
        private void FindOrSpawnPlayer()
        {
            // First, check if any spawned prefab is tagged as Player
            foreach (var prefab in _spawnedPrefabs)
            {
                if (prefab.CompareTag(playerPrefabTag))
                {
                    _spawnedPlayer = prefab;
                    Debug.Log($"[PlayMode] Found player prefab: {prefab.name}");
                    return;
                }
            }
            
            // Check for player spawn point prefab
            var map = editorController.State.Map;
            Vector2 spawnPos = defaultSpawnPoint;
            
            foreach (var prefabData in map.prefabs)
            {
                var prefabDef = palette?.GetPrefab(prefabData.prefabId);
                if (prefabDef != null && prefabDef.id.ToLower().Contains("spawn"))
                {
                    spawnPos = new Vector2(prefabData.x + 0.5f, prefabData.y + 0.5f);
                    break;
                }
            }
            
            // Spawn default player if provided
            if (defaultPlayerPrefab != null)
            {
                _spawnedPlayer = Instantiate(
                    defaultPlayerPrefab, 
                    new Vector3(spawnPos.x, spawnPos.y, 0), 
                    Quaternion.identity, 
                    playModeParent
                );
                _spawnedPlayer.name = "[Play] Player";
                _spawnedPrefabs.Add(_spawnedPlayer);
                Debug.Log($"[PlayMode] Spawned default player at {spawnPos}");
            }
        }
        
        private void SetupPlayModeCamera()
        {
            if (_cam == null || _spawnedPlayer == null) return;
            
            // Center camera on player
            var playerPos = _spawnedPlayer.transform.position;
            _cam.transform.position = new Vector3(playerPos.x, playerPos.y, -10f);
            
            // Set reasonable zoom
            _cam.orthographicSize = 5f;
        }
        
        private void FollowPlayerCamera()
        {
            if (_cam == null || _spawnedPlayer == null) return;
            
            var map = editorController?.State?.Map;
            if (map == null) return;
            
            Vector3 targetPos = _spawnedPlayer.transform.position;
            targetPos.z = -10f;
            
            // Clamp to map bounds
            float halfHeight = _cam.orthographicSize;
            float halfWidth = halfHeight * _cam.aspect;
            
            targetPos.x = Mathf.Clamp(
                targetPos.x, 
                halfWidth - cameraBoundsMargin.x, 
                map.width - halfWidth + cameraBoundsMargin.x
            );
            targetPos.y = Mathf.Clamp(
                targetPos.y, 
                halfHeight - cameraBoundsMargin.y, 
                map.height - halfHeight + cameraBoundsMargin.y
            );
            
            // Smooth follow
            _cam.transform.position = Vector3.Lerp(
                _cam.transform.position, 
                targetPos, 
                cameraSmoothing * Time.deltaTime
            );
        }
        
        private void DestroySpawnedObjects()
        {
            foreach (var obj in _spawnedPrefabs)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedPrefabs.Clear();
            
            foreach (var obj in _spawnedColliders)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedColliders.Clear();
            
            if (_collisionParent != null)
            {
                Destroy(_collisionParent);
                _collisionParent = null;
            }
            
            _spawnedPlayer = null;
        }
        
        /// <summary>
        /// Get all spawned prefab instances
        /// </summary>
        public IReadOnlyList<GameObject> GetSpawnedPrefabs() => _spawnedPrefabs;
        
        /// <summary>
        /// Get the spawned player object
        /// </summary>
        public GameObject GetPlayer() => _spawnedPlayer;
        
        /// <summary>
        /// Respawn player at spawn point
        /// </summary>
        public void RespawnPlayer()
        {
            if (!IsPlaying || _spawnedPlayer == null) return;
            
            var map = editorController.State.Map;
            Vector2 spawnPos = defaultSpawnPoint;
            
            // Find spawn point
            foreach (var prefabData in map.prefabs)
            {
                var prefabDef = palette?.GetPrefab(prefabData.prefabId);
                if (prefabDef != null && prefabDef.id.ToLower().Contains("spawn"))
                {
                    spawnPos = new Vector2(prefabData.x + 0.5f, prefabData.y + 0.5f);
                    break;
                }
            }
            
            _spawnedPlayer.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0);
            
            // Reset rigidbody velocity if present
            var rb = _spawnedPlayer.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
        
        private void OnDestroy()
        {
            if (IsPlaying)
            {
                DestroySpawnedObjects();
            }
        }
    }
    
    /// <summary>
    /// Interface for prefabs that can receive custom data from the map
    /// </summary>
    public interface IPrefabDataReceiver
    {
        void ReceiveData(string jsonData);
    }
}
