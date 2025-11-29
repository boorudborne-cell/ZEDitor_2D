using UnityEngine;

namespace MapEditor.Core
{
    /// <summary>
    /// Handles mouse input for drawing, panning, zooming
    /// </summary>
    public class EditorInputHandler : MonoBehaviour
    {
        [SerializeField] private MapEditorController controller;
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
        }
        
        private void Update()
        {
            if (controller?.State?.Map == null) return;
            
            HandleZoom();
            HandlePan();
            HandleDraw();
            HandleShortcuts();
        }
        
        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                controller.Zoom(scroll * zoomSpeed);
            }
        }
        
        private void HandlePan()
        {
            // Middle mouse or Space + drag
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
            // Skip if panning
            if (_isPanning) return;
            if (Input.GetKey(panKey)) return;
            
            // Get tile position under mouse
            Vector3 worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int tilePos = new Vector2Int(
                Mathf.FloorToInt(worldPos.x),
                Mathf.FloorToInt(worldPos.y)
            );
            
            // Left mouse button
            if (Input.GetMouseButtonDown(0))
            {
                _isDrawing = true;
                _lastTilePos = tilePos;
                controller.ApplyToolAt(tilePos);
            }
            else if (Input.GetMouseButton(0) && _isDrawing)
            {
                // Only apply if moved to new tile
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
            // Tool shortcuts
            if (Input.GetKeyDown(KeyCode.B))
                controller.SetTool(EditorTool.Brush);
            if (Input.GetKeyDown(KeyCode.E))
                controller.SetTool(EditorTool.Eraser);
            
            // Zoom shortcuts
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                controller.Zoom(1f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                controller.Zoom(-1f);
            
            // Save
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) 
                && Input.GetKeyDown(KeyCode.S))
                controller.SaveMap();
            
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
        }
    }
}
