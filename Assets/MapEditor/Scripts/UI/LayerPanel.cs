using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MapEditor.Core;
using MapEditor.Data;

namespace MapEditor.UI
{
    /// <summary>
    /// UI panel for managing layers
    /// </summary>
    public class LayerPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapEditorController editorController;
        
        [Header("Layer Items")]
        [SerializeField] private LayerItem backgroundLayerItem;
        [SerializeField] private LayerItem groundLayerItem;
        [SerializeField] private LayerItem foregroundLayerItem;
        
        [Header("Actions")]
        [SerializeField] private Button clearLayerButton;
        
        [Header("Colors")]
        [SerializeField] private Color activeLayerColor = new Color(0.3f, 0.6f, 1f, 0.5f);
        [SerializeField] private Color inactiveLayerColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        
        private void Awake()
        {
            SetupLayerItems();
            clearLayerButton?.onClick.AddListener(OnClearLayerClicked);
        }
        
        private void OnEnable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged += UpdateUI;
                editorController.OnMapLoaded += UpdateUI;
                editorController.OnMapCreated += UpdateUI;
            }
        }
        
        private void OnDisable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged -= UpdateUI;
                editorController.OnMapLoaded -= UpdateUI;
                editorController.OnMapCreated -= UpdateUI;
            }
        }
        
        private void Start()
        {
            UpdateUI();
        }
        
        private void SetupLayerItems()
        {
            backgroundLayerItem?.Setup(LayerType.Background, "Background", 
                () => OnLayerSelected(LayerType.Background),
                (visible) => OnLayerVisibilityChanged(LayerType.Background, visible));
            
            groundLayerItem?.Setup(LayerType.Ground, "Ground",
                () => OnLayerSelected(LayerType.Ground),
                (visible) => OnLayerVisibilityChanged(LayerType.Ground, visible));
            
            foregroundLayerItem?.Setup(LayerType.Foreground, "Foreground",
                () => OnLayerSelected(LayerType.Foreground),
                (visible) => OnLayerVisibilityChanged(LayerType.Foreground, visible));
        }
        
        private void OnLayerSelected(LayerType layer)
        {
            editorController?.SetActiveLayer(layer);
        }
        
        private void OnLayerVisibilityChanged(LayerType layer, bool visible)
        {
            editorController?.SetLayerVisibility(layer, visible);
        }
        
        private void OnClearLayerClicked()
        {
            if (editorController?.State == null)
                return;
            
            editorController.ClearLayer(editorController.State.ActiveLayer);
        }
        
        private void UpdateUI()
        {
            if (editorController?.State == null)
                return;
            
            var state = editorController.State;
            var map = state.CurrentMap;
            
            // Update layer items
            UpdateLayerItem(backgroundLayerItem, LayerType.Background, state, map);
            UpdateLayerItem(groundLayerItem, LayerType.Ground, state, map);
            UpdateLayerItem(foregroundLayerItem, LayerType.Foreground, state, map);
        }
        
        private void UpdateLayerItem(LayerItem item, LayerType layerType, EditorState state, MapData map)
        {
            if (item == null)
                return;
            
            bool isActive = state.ActiveLayer == layerType;
            item.SetActive(isActive, isActive ? activeLayerColor : inactiveLayerColor);
            
            if (map != null)
            {
                var layer = map.GetLayer(layerType);
                if (layer != null)
                {
                    item.SetVisible(layer.isVisible);
                    item.SetTileCount(layer.tiles.Count);
                }
            }
        }
    }
    
    /// <summary>
    /// Individual layer item in the layer panel
    /// </summary>
    public class LayerItem : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Button selectButton;
        [SerializeField] private Toggle visibilityToggle;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text countLabel;
        [SerializeField] private Image backgroundImage;
        
        public LayerType LayerType { get; private set; }
        
        private System.Action _onSelect;
        private System.Action<bool> _onVisibilityChanged;
        
        public void Setup(LayerType type, string displayName, System.Action onSelect, System.Action<bool> onVisibilityChanged)
        {
            LayerType = type;
            _onSelect = onSelect;
            _onVisibilityChanged = onVisibilityChanged;
            
            if (nameLabel != null)
                nameLabel.text = displayName;
            
            if (selectButton != null)
                selectButton.onClick.AddListener(() => _onSelect?.Invoke());
            
            if (visibilityToggle != null)
                visibilityToggle.onValueChanged.AddListener(OnVisibilityToggle);
        }
        
        private void OnVisibilityToggle(bool visible)
        {
            _onVisibilityChanged?.Invoke(visible);
        }
        
        public void SetActive(bool isActive, Color color)
        {
            if (backgroundImage != null)
                backgroundImage.color = color;
        }
        
        public void SetVisible(bool visible)
        {
            if (visibilityToggle != null)
                visibilityToggle.SetIsOnWithoutNotify(visible);
        }
        
        public void SetTileCount(int count)
        {
            if (countLabel != null)
                countLabel.text = count.ToString();
        }
    }
}
