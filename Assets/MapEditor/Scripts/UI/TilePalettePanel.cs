using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MapEditor.Core;
using MapEditor.Data;

namespace MapEditor.UI
{
    /// <summary>
    /// UI panel displaying available tiles for selection
    /// </summary>
    public class TilePalettePanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapEditorController editorController;
        
        [Header("UI Components")]
        [SerializeField] private Transform tileContainer;
        [SerializeField] private Transform entityContainer;
        [SerializeField] private TMP_Dropdown categoryDropdown;
        [SerializeField] private Toggle tilesTab;
        [SerializeField] private Toggle entitiesTab;
        
        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private GameObject entityPrefab;
        
        [Header("Settings")]
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color normalColor = Color.white;
        
        private readonly List<TileButton> _tileButtons = new List<TileButton>();
        private readonly List<EntityButton> _entityButtons = new List<EntityButton>();
        private string _selectedCategory = "All";
        private bool _showingTiles = true;
        
        private void Awake()
        {
            SetupTabs();
            SetupCategoryDropdown();
        }
        
        private void OnEnable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged += UpdateSelection;
            }
        }
        
        private void OnDisable()
        {
            if (editorController != null)
            {
                editorController.OnStateChanged -= UpdateSelection;
            }
        }
        
        private void Start()
        {
            PopulatePalette();
        }
        
        private void SetupTabs()
        {
            if (tilesTab != null)
            {
                tilesTab.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) ShowTiles();
                });
            }
            
            if (entitiesTab != null)
            {
                entitiesTab.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) ShowEntities();
                });
            }
        }
        
        private void SetupCategoryDropdown()
        {
            if (categoryDropdown == null || editorController?.Palette == null)
                return;
            
            categoryDropdown.ClearOptions();
            
            var options = new List<string> { "All" };
            
            if (_showingTiles)
            {
                options.AddRange(editorController.Palette.tileCategories);
            }
            else
            {
                options.AddRange(editorController.Palette.entityCategories);
            }
            
            categoryDropdown.AddOptions(options);
            categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
        }
        
        private void OnCategoryChanged(int index)
        {
            _selectedCategory = categoryDropdown.options[index].text;
            FilterPalette();
        }
        
        private void ShowTiles()
        {
            _showingTiles = true;
            
            if (tileContainer != null)
                tileContainer.gameObject.SetActive(true);
            
            if (entityContainer != null)
                entityContainer.gameObject.SetActive(false);
            
            SetupCategoryDropdown();
            FilterPalette();
        }
        
        private void ShowEntities()
        {
            _showingTiles = false;
            
            if (tileContainer != null)
                tileContainer.gameObject.SetActive(false);
            
            if (entityContainer != null)
                entityContainer.gameObject.SetActive(true);
            
            SetupCategoryDropdown();
            FilterPalette();
        }
        
        private void PopulatePalette()
        {
            if (editorController?.Palette == null)
                return;
            
            PopulateTiles();
            PopulateEntities();
            
            ShowTiles();
        }
        
        private void PopulateTiles()
        {
            var palette = editorController.Palette;
            
            // Clear existing
            foreach (var btn in _tileButtons)
            {
                if (btn.gameObject != null)
                    Destroy(btn.gameObject);
            }
            _tileButtons.Clear();
            
            if (tilePrefab == null || tileContainer == null)
                return;
            
            foreach (var tile in palette.tiles)
            {
                var go = Instantiate(tilePrefab, tileContainer);
                var tileBtn = go.GetComponent<TileButton>();
                
                if (tileBtn == null)
                    tileBtn = go.AddComponent<TileButton>();
                
                tileBtn.Setup(tile, () => OnTileSelected(tile.tileId));
                _tileButtons.Add(tileBtn);
            }
        }
        
        private void PopulateEntities()
        {
            var palette = editorController.Palette;
            
            // Clear existing
            foreach (var btn in _entityButtons)
            {
                if (btn.gameObject != null)
                    Destroy(btn.gameObject);
            }
            _entityButtons.Clear();
            
            if (entityPrefab == null || entityContainer == null)
                return;
            
            foreach (var entity in palette.entities)
            {
                var go = Instantiate(entityPrefab, entityContainer);
                var entityBtn = go.GetComponent<EntityButton>();
                
                if (entityBtn == null)
                    entityBtn = go.AddComponent<EntityButton>();
                
                entityBtn.Setup(entity, () => OnEntitySelected(entity.entityType));
                _entityButtons.Add(entityBtn);
            }
        }
        
        private void FilterPalette()
        {
            if (_showingTiles)
            {
                foreach (var btn in _tileButtons)
                {
                    bool show = _selectedCategory == "All" || btn.Category == _selectedCategory;
                    btn.gameObject.SetActive(show);
                }
            }
            else
            {
                foreach (var btn in _entityButtons)
                {
                    bool show = _selectedCategory == "All" || btn.Category == _selectedCategory;
                    btn.gameObject.SetActive(show);
                }
            }
        }
        
        private void OnTileSelected(string tileId)
        {
            editorController?.SelectTile(tileId);
            UpdateSelection();
        }
        
        private void OnEntitySelected(string entityType)
        {
            editorController?.SelectEntity(entityType);
            UpdateSelection();
        }
        
        private void UpdateSelection()
        {
            if (editorController?.State == null)
                return;
            
            var state = editorController.State;
            
            // Update tile selection visual
            foreach (var btn in _tileButtons)
            {
                btn.SetSelected(btn.TileId == state.SelectedTileId);
            }
            
            // Update entity selection visual
            foreach (var btn in _entityButtons)
            {
                btn.SetSelected(btn.EntityType == state.SelectedEntityType);
            }
        }
    }
    
    /// <summary>
    /// Button representing a tile in the palette
    /// </summary>
    public class TileButton : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image tilePreviewImage;
        [SerializeField] private TMP_Text nameLabel;
        
        public string TileId { get; private set; }
        public string Category { get; private set; }
        
        private Button _button;
        private Color _normalColor = Color.white;
        private Color _selectedColor = new Color(0.3f, 0.6f, 1f);
        
        public void Setup(TilePaletteEntry entry, System.Action onClick)
        {
            TileId = entry.tileId;
            Category = entry.category;
            
            _button = GetComponent<Button>();
            if (_button == null)
                _button = gameObject.AddComponent<Button>();
            
            _button.onClick.AddListener(() => onClick?.Invoke());
            
            // Setup visuals
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            
            if (tilePreviewImage != null)
            {
                if (entry.sprite != null)
                {
                    tilePreviewImage.sprite = entry.sprite;
                    tilePreviewImage.color = Color.white;
                }
                else
                {
                    tilePreviewImage.sprite = null;
                    tilePreviewImage.color = entry.previewColor;
                }
            }
            
            if (nameLabel != null)
            {
                nameLabel.text = entry.displayName;
            }
            
            // Tooltip
            gameObject.name = entry.displayName;
        }
        
        public void SetSelected(bool selected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? _selectedColor : _normalColor;
            }
        }
    }
    
    /// <summary>
    /// Button representing an entity in the palette
    /// </summary>
    public class EntityButton : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        
        public string EntityType { get; private set; }
        public string Category { get; private set; }
        
        private Button _button;
        private Color _normalColor = Color.white;
        private Color _selectedColor = new Color(0.3f, 0.6f, 1f);
        
        public void Setup(EntityPaletteEntry entry, System.Action onClick)
        {
            EntityType = entry.entityType;
            Category = entry.category;
            
            _button = GetComponent<Button>();
            if (_button == null)
                _button = gameObject.AddComponent<Button>();
            
            _button.onClick.AddListener(() => onClick?.Invoke());
            
            // Setup visuals
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            
            if (iconImage != null)
            {
                if (entry.icon != null)
                {
                    iconImage.sprite = entry.icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = entry.gizmoColor;
                }
            }
            
            if (nameLabel != null)
            {
                nameLabel.text = entry.displayName;
            }
            
            gameObject.name = entry.displayName;
        }
        
        public void SetSelected(bool selected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = selected ? _selectedColor : _normalColor;
            }
        }
    }
}
