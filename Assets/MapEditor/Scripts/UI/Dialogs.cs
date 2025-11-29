using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MapEditor.UI
{
    /// <summary>
    /// Dialog for creating a new map
    /// </summary>
    public class NewMapDialog : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField widthInput;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private Button createButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text errorText;
        
        [Header("Default Values")]
        [SerializeField] private int defaultWidth = 50;
        [SerializeField] private int defaultHeight = 30;
        [SerializeField] private int maxWidth = 2000;
        [SerializeField] private int maxHeight = 2000;
        
        private Action<string, int, int> _onConfirm;
        
        private void Awake()
        {
            createButton?.onClick.AddListener(OnCreateClicked);
            cancelButton?.onClick.AddListener(Hide);
            
            // Add validation listeners
            widthInput?.onEndEdit.AddListener(_ => ValidateInputs());
            heightInput?.onEndEdit.AddListener(_ => ValidateInputs());
            
            Hide();
        }
        
        public void Show(Action<string, int, int> onConfirm)
        {
            _onConfirm = onConfirm;
            
            // Set default values
            nameInput.text = "NewMap";
            widthInput.text = defaultWidth.ToString();
            heightInput.text = defaultHeight.ToString();
            
            if (errorText != null)
                errorText.text = "";
            
            dialogPanel?.SetActive(true);
            
            // Focus name input
            nameInput?.Select();
        }
        
        public void Hide()
        {
            dialogPanel?.SetActive(false);
            _onConfirm = null;
        }
        
        private void OnCreateClicked()
        {
            if (!ValidateInputs())
                return;
            
            string name = nameInput.text.Trim();
            int width = int.Parse(widthInput.text);
            int height = int.Parse(heightInput.text);
            
            _onConfirm?.Invoke(name, width, height);
            Hide();
        }
        
        private bool ValidateInputs()
        {
            // Validate name
            if (string.IsNullOrWhiteSpace(nameInput.text))
            {
                ShowError("Map name cannot be empty");
                return false;
            }
            
            // Validate width
            if (!int.TryParse(widthInput.text, out int width) || width <= 0)
            {
                ShowError("Width must be a positive number");
                return false;
            }
            
            if (width > maxWidth)
            {
                ShowError($"Width cannot exceed {maxWidth}");
                return false;
            }
            
            // Validate height
            if (!int.TryParse(heightInput.text, out int height) || height <= 0)
            {
                ShowError("Height must be a positive number");
                return false;
            }
            
            if (height > maxHeight)
            {
                ShowError($"Height cannot exceed {maxHeight}");
                return false;
            }
            
            ClearError();
            return true;
        }
        
        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.color = Color.red;
            }
        }
        
        private void ClearError()
        {
            if (errorText != null)
                errorText.text = "";
        }
    }
    
    /// <summary>
    /// Dialog for saving and loading map files
    /// </summary>
    public class FileDialog : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_InputField fileNameInput;
        [SerializeField] private Transform fileListContainer;
        [SerializeField] private GameObject fileItemPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private TMP_Text errorText;
        
        private Action<string> _onConfirm;
        private bool _isSaveMode;
        private string _selectedFile;
        
        private void Awake()
        {
            confirmButton?.onClick.AddListener(OnConfirmClicked);
            cancelButton?.onClick.AddListener(Hide);
            deleteButton?.onClick.AddListener(OnDeleteClicked);
            
            fileNameInput?.onValueChanged.AddListener(_ => ClearError());
            
            Hide();
        }
        
        public void ShowSave(string defaultName, Action<string> onConfirm)
        {
            _isSaveMode = true;
            _onConfirm = onConfirm;
            
            if (titleText != null)
                titleText.text = "Save Map";
            
            if (confirmButton != null)
                confirmButton.GetComponentInChildren<TMP_Text>().text = "Save";
            
            fileNameInput.text = defaultName;
            fileNameInput.interactable = true;
            
            if (deleteButton != null)
                deleteButton.gameObject.SetActive(false);
            
            ClearError();
            dialogPanel?.SetActive(true);
            fileNameInput?.Select();
        }
        
        public void ShowLoad(string[] availableFiles, Action<string> onConfirm)
        {
            _isSaveMode = false;
            _onConfirm = onConfirm;
            _selectedFile = null;
            
            if (titleText != null)
                titleText.text = "Load Map";
            
            if (confirmButton != null)
                confirmButton.GetComponentInChildren<TMP_Text>().text = "Load";
            
            fileNameInput.text = "";
            fileNameInput.interactable = false;
            
            if (deleteButton != null)
                deleteButton.gameObject.SetActive(true);
            
            PopulateFileList(availableFiles);
            ClearError();
            dialogPanel?.SetActive(true);
        }
        
        public void Hide()
        {
            dialogPanel?.SetActive(false);
            _onConfirm = null;
            ClearFileList();
        }
        
        private void PopulateFileList(string[] files)
        {
            ClearFileList();
            
            if (fileItemPrefab == null || fileListContainer == null)
                return;
            
            foreach (var file in files)
            {
                var item = Instantiate(fileItemPrefab, fileListContainer);
                
                var text = item.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = file;
                
                var button = item.GetComponent<Button>();
                if (button != null)
                {
                    string fileName = file; // Capture for closure
                    button.onClick.AddListener(() => OnFileSelected(fileName, item));
                }
            }
        }
        
        private void ClearFileList()
        {
            if (fileListContainer == null)
                return;
            
            foreach (Transform child in fileListContainer)
            {
                Destroy(child.gameObject);
            }
        }
        
        private void OnFileSelected(string fileName, GameObject item)
        {
            _selectedFile = fileName;
            fileNameInput.text = fileName;
            
            // Highlight selected item
            foreach (Transform child in fileListContainer)
            {
                var image = child.GetComponent<Image>();
                if (image != null)
                {
                    image.color = child.gameObject == item ? 
                        new Color(0.3f, 0.6f, 1f, 0.3f) : Color.clear;
                }
            }
        }
        
        private void OnConfirmClicked()
        {
            string fileName = fileNameInput.text.Trim();
            
            if (string.IsNullOrWhiteSpace(fileName))
            {
                ShowError(_isSaveMode ? "Enter a file name" : "Select a file");
                return;
            }
            
            _onConfirm?.Invoke(fileName);
            Hide();
        }
        
        private void OnDeleteClicked()
        {
            if (string.IsNullOrWhiteSpace(_selectedFile))
            {
                ShowError("Select a file to delete");
                return;
            }
            
            // TODO: Add confirmation dialog
            // For now, just report that deletion would happen
            Debug.Log($"[FileDialog] Would delete: {_selectedFile}");
        }
        
        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.color = Color.red;
            }
        }
        
        private void ClearError()
        {
            if (errorText != null)
                errorText.text = "";
        }
    }
}
