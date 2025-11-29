using UnityEngine;
using UnityEngine.EventSystems;
using MapEditor.Core;

namespace MapEditor.UI
{
    /// <summary>
    /// Handles mouse/touch input on the map canvas
    /// Converts screen coordinates to tile positions and dispatches to tools
    /// </summary>
    public class CanvasInputHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("References")]
        [SerializeField] private MapEditorController editorController;
        [SerializeField] private Camera editorCamera;
        [SerializeField] private EditorUIPanel uiPanel;
        
        [Header("Pan Settings")]
        [SerializeField] private bool enablePan = true;
        [SerializeField] private float panSpeed = 1f;
        [SerializeField] private KeyCode panModifierKey = KeyCode.Space;
        
        [Header("Zoom Settings")]
        [SerializeField] private bool enableScrollZoom = true;
        [SerializeField] private float scrollZoomSpeed = 0.1f;
        
        private bool _isDrawing;
        private bool _isPanning;
        private Vector2 _lastMousePosition;
        private Vector2Int _lastTilePosition;
        
        private void Update()
        {
            HandleKeyboardInput();
            HandleScrollZoom();
            HandleCursorPosition();
        }
        
        private void HandleKeyboardInput()
        {
            if (editorController?.State == null)
                return;
            
            // Tool shortcuts
            if (Input.GetKeyDown(KeyCode.B))
                editorController.SetTool(EditorTool.Brush);
            else if (Input.GetKeyDown(KeyCode.E))
                editorController.SetTool(EditorTool.Eraser);
            else if (Input.GetKeyDown(KeyCode.V))
                editorController.SetTool(EditorTool.EntitySelect);
            
            // View shortcuts
            if (Input.GetKeyDown(KeyCode.G))
                editorController.ToggleGrid();
            if (Input.GetKeyDown(KeyCode.C))
                editorController.ToggleCollisions();
            
            // Zoom shortcuts
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                editorController.ZoomIn();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                editorController.ZoomOut();
            
            // Save/Load shortcuts
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.S))
                    editorController.SaveMap();
                if (Input.GetKeyDown(KeyCode.N))
                    editorController.CreateNewMap("NewMap");
            }
            
            // Delete selected entity
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                var selectTool = editorController.Tools?.GetTool<EntitySelectTool>();
                selectTool?.DeleteSelected(editorController.State);
            }
            
            // Center view
            if (Input.GetKeyDown(KeyCode.Home))
                editorController.CenterView();
        }
        
        private void HandleScrollZoom()
        {
            if (!enableScrollZoom || editorController == null)
                return;
            
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newZoom = editorController.State.Zoom + scroll * scrollZoomSpeed;
                editorController.SetZoom(newZoom);
            }
        }
        
        private void HandleCursorPosition()
        {
            if (editorController?.State?.CurrentMap == null || editorCamera == null)
                return;
            
            Vector2Int tilePos = ScreenToTile(Input.mousePosition);
            
            if (tilePos != _lastTilePosition)
            {
                _lastTilePosition = tilePos;
                uiPanel?.UpdateCursorPosition(tilePos);
            }
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (editorController?.State?.CurrentMap == null)
                return;
            
            // Check for pan modifier
            if (enablePan && Input.GetKey(panModifierKey))
            {
                _isPanning = true;
                _lastMousePosition = eventData.position;
                return;
            }
            
            // Middle mouse button for panning
            if (eventData.button == PointerEventData.InputButton.Middle)
            {
                _isPanning = true;
                _lastMousePosition = eventData.position;
                return;
            }
            
            // Left click for drawing
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isDrawing = true;
                Vector2Int tilePos = ScreenToTile(eventData.position);
                editorController.HandlePointerDown(tilePos);
            }
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            if (editorController?.State == null)
                return;
            
            // Handle panning
            if (_isPanning)
            {
                Vector2 delta = eventData.position - _lastMousePosition;
                _lastMousePosition = eventData.position;
                
                // Convert screen delta to world delta
                float orthoSize = editorCamera.orthographicSize;
                float screenHeight = Screen.height;
                float worldDelta = (delta.y / screenHeight) * orthoSize * 2f * panSpeed;
                float worldDeltaX = (delta.x / screenHeight) * orthoSize * 2f * panSpeed;
                
                Vector2 currentPos = editorController.State.CameraPosition;
                editorController.SetCameraPosition(new Vector2(
                    currentPos.x - worldDeltaX,
                    currentPos.y - worldDelta
                ));
                return;
            }
            
            // Handle drawing
            if (_isDrawing)
            {
                Vector2Int tilePos = ScreenToTile(eventData.position);
                editorController.HandlePointerDrag(tilePos);
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isPanning)
            {
                _isPanning = false;
                return;
            }
            
            if (_isDrawing)
            {
                _isDrawing = false;
                Vector2Int tilePos = ScreenToTile(eventData.position);
                editorController?.HandlePointerUp(tilePos);
            }
        }
        
        private Vector2Int ScreenToTile(Vector2 screenPosition)
        {
            if (editorCamera == null || editorController?.State?.CurrentMap == null)
                return Vector2Int.zero;
            
            Vector3 worldPos = editorCamera.ScreenToWorldPoint(screenPosition);
            float tileSize = editorController.State.CurrentMap.tileSize;
            
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / tileSize),
                Mathf.FloorToInt(worldPos.y / tileSize)
            );
        }
    }
}
