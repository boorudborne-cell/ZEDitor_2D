using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using MapEditor.Data;

namespace MapEditor.Core
{
    /// <summary>
    /// Handles mouse input for drawing, panning, zooming and keyboard shortcuts
    /// </summary>
    public class EditorInputHandler : MonoBehaviour
    {
        [SerializeField] private MapEditorController controller;
        [SerializeField] private PlayModeController playModeController;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private KeyCode panKey = KeyCode.Space;
        [SerializeField] private float panSpeed = 0.01f;
        [SerializeField] private float zoomSpeed = 0.5f;
        
        private Camera _cam;
        private bool _isDrawing;
        private bool _isPanning;
        private Vector3 _lastMousePos;
        private Vector2Int _lastTilePos;
        
        private void Awake()
        {
            _cam = Camera.main;
            
            if (uiDocument == null)
                uiDocument = FindObjectOfType<UIDocument>();
        }
        
        /// <summary>
        /// Check if mouse is over UI elements (blocks map interaction)
        /// </summary>
        private bool IsPointerOverUI()
        {
            // Check Unity's legacy UI (EventSystem)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;
            
            // Check UI Toolkit
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                var root = uiDocument.rootVisualElement;
                var panel = root.panel;
                
                if (panel != null)
                {
                    // Convert screen position to panel position
                    Vector2 mousePos = Input.mousePosition;
                    mousePos.y = Screen.height - mousePos.y; // Flip Y for UI Toolkit
                    
                    // Pick element at position
                    var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, 
                        new Vector2(Input.mousePosition.x, Input.mousePosition.y)));
                    
                    // If picked element exists and is not the canvas-area (which has picking-mode: ignore)
                    if (picked != null)
                    {
                        // Check if we hit an interactive element (not canvas-area or root)
                        var current = picked;
                        while (current != null)
                        {
                            if (current.name == "canvas-area")
                                return false; // Canvas area - allow interaction
                            
                            // Check if element blocks picking
                            if (current.pickingMode == PickingMode.Position)
                            {
                                // It's a UI element that should block input
                                if (current.name != "root" && current.parent != null)
                                    return true;
                            }
                            
                            current = current.parent;
                        }
                    }
                }
            }
            
            return false;
        }
        
        private void Update()
        {
            // Always handle play mode toggle
            HandlePlayModeShortcut();
            
            // Block editor input in play mode
            if (controller?.State?.IsPlayMode == true) return;
            if (controller?.State?.Map == null) return;
            
            HandleZoom();
            HandlePan();
            HandleDraw();
            HandleShortcuts();
        }
        
        private void HandlePlayModeShortcut()
        {
            // F5 to toggle play mode
            if (Input.GetKeyDown(KeyCode.F5))
            {
                playModeController?.TogglePlayMode();
            }
            
            // Escape to stop play mode
            if (Input.GetKeyDown(KeyCode.Escape) && controller?.State?.IsPlayMode == true)
            {
                playModeController?.StopPlayMode();
            }
        }
        
        private void HandleZoom()
        {
            // Allow zoom even over UI for convenience, but can be restricted
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUI())
            {
                controller.Zoom(scroll * zoomSpeed);
            }
        }
        
        private void HandlePan()
        {
            bool panButton = Input.GetMouseButton(2) || (Input.GetKey(panKey) && Input.GetMouseButton(0));
            
            if (panButton)
            {
                if (!_isPanning)
                {
                    _isPanning = true;
                    _lastMousePos = Input.mousePosition;
                }
                else
                {
                    Vector3 delta = Input.mousePosition - _lastMousePos;
                    float scale = _cam.orthographicSize * panSpeed;
                    controller.Pan(new Vector2(-delta.x * scale, -delta.y * scale));
                    _lastMousePos = Input.mousePosition;
                }
            }
            else
            {
                _isPanning = false;
            }
        }
        
        private void HandleDraw()
        {
            if (_isPanning) return;
            if (Input.GetKey(panKey)) return;
            if (IsPointerOverUI()) 
            {
                _isDrawing = false;
                return;
            }
            
            Vector3 worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int tilePos = new Vector2Int(
                Mathf.FloorToInt(worldPos.x),
                Mathf.FloorToInt(worldPos.y)
            );
            
            if (Input.GetMouseButtonDown(0))
            {
                _isDrawing = true;
                _lastTilePos = tilePos;
                controller.ApplyToolAt(tilePos);
            }
            else if (Input.GetMouseButton(0) && _isDrawing)
            {
                if (tilePos != _lastTilePos)
                {
                    _lastTilePos = tilePos;
                    controller.ApplyToolAt(tilePos);
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDrawing = false;
            }
        }
        
        private void HandleShortcuts()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            
            // Undo: Ctrl+Z
            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (shift)
                    controller.Redo(); // Ctrl+Shift+Z = Redo
                else
                    controller.Undo();
                return;
            }
            
            // Redo: Ctrl+Y
            if (ctrl && Input.GetKeyDown(KeyCode.Y))
            {
                controller.Redo();
                return;
            }
            
            // Save: Ctrl+S
            if (ctrl && Input.GetKeyDown(KeyCode.S))
            {
                controller.SaveMap();
                return;
            }
            
            // New: Ctrl+N (handled in UI for confirmation dialog)
            // Load: Ctrl+O (handled in UI for confirmation dialog)
            
            // Tool shortcuts
            if (Input.GetKeyDown(KeyCode.B))
                controller.SetTool(EditorTool.Brush);
            if (Input.GetKeyDown(KeyCode.E))
                controller.SetTool(EditorTool.Eraser);
            if (Input.GetKeyDown(KeyCode.P) && !ctrl)
                controller.SetTool(EditorTool.Prefab);
            if (Input.GetKeyDown(KeyCode.I))
                controller.SetTool(EditorTool.Eyedropper);
            
            // Layer shortcuts: 1, 2, 3
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                controller.SetLayer(LayerType.Background);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                controller.SetLayer(LayerType.Ground);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                controller.SetLayer(LayerType.Foreground);
            
            // Toggle shortcuts
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (controller.State != null)
                {
                    controller.State.ShowCollisions = !controller.State.ShowCollisions;
                    controller.State.NotifyChanged();
                }
            }
            
            if (Input.GetKeyDown(KeyCode.G))
            {
                if (controller.State != null)
                {
                    controller.State.ShowGrid = !controller.State.ShowGrid;
                    controller.State.NotifyChanged();
                }
            }
            
            // Zoom shortcuts
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                controller.Zoom(1f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                controller.Zoom(-1f);
            
            // Reset zoom: 0
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
                controller.State?.SetZoom(1f);
            
            // Home to center
            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (controller.State.Map != null)
                {
                    controller.State.CameraOffset = new Vector2(
                        controller.State.Map.width / 2f,
                        controller.State.Map.height / 2f
                    );
                    controller.State.NotifyChanged();
                }
            }
            
            // F to fit map in view
            if (Input.GetKeyDown(KeyCode.F))
            {
                FitMapInView();
            }
        }
        
        private void FitMapInView()
        {
            if (controller?.State?.Map == null || _cam == null) return;
            
            var map = controller.State.Map;
            
            // Center camera
            controller.State.CameraOffset = new Vector2(map.width / 2f, map.height / 2f);
            
            // Calculate zoom to fit
            float screenAspect = (float)Screen.width / Screen.height;
            float mapAspect = (float)map.width / map.height;
            
            float targetSize;
            if (mapAspect > screenAspect)
            {
                // Map is wider than screen
                targetSize = map.width / (2f * screenAspect);
            }
            else
            {
                // Map is taller than screen
                targetSize = map.height / 2f;
            }
            
            // Add some padding
            targetSize *= 1.1f;
            
            // Calculate zoom from orthographic size
            float baseSize = 5f;
            float zoom = baseSize / targetSize;
            
            controller.State.SetZoom(zoom);
        }
    }
}
