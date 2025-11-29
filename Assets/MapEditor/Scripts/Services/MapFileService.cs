using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Services
{
    /// <summary>
    /// Result of a file operation
    /// </summary>
    public class FileOperationResult
    {
        public bool Success { get; private set; }
        public string ErrorMessage { get; private set; }
        public MapData Data { get; private set; }
        public float LoadTime { get; private set; }
        
        public static FileOperationResult Successful(MapData data, float loadTime = 0)
        {
            return new FileOperationResult 
            { 
                Success = true, 
                Data = data,
                LoadTime = loadTime
            };
        }
        
        public static FileOperationResult Failed(string error)
        {
            return new FileOperationResult 
            { 
                Success = false, 
                ErrorMessage = error 
            };
        }
    }
    
    /// <summary>
    /// Service for saving and loading map files
    /// Thread-safe async operations with proper error handling
    /// </summary>
    public class MapFileService
    {
        private const string FILE_EXTENSION = ".json";
        private const int BUFFER_SIZE = 65536; // 64KB buffer for large files
        
        private readonly string _basePath;
        
        public MapFileService(string basePath = null)
        {
            _basePath = basePath ?? Path.Combine(Application.persistentDataPath, "Maps");
            EnsureDirectoryExists(_basePath);
        }
        
        /// <summary>
        /// Saves map data to JSON file asynchronously
        /// </summary>
        public async Task<FileOperationResult> SaveMapAsync(MapData mapData, string fileName, 
            CancellationToken cancellationToken = default)
        {
            if (mapData == null)
                return FileOperationResult.Failed("Map data is null");
            
            if (string.IsNullOrWhiteSpace(fileName))
                return FileOperationResult.Failed("File name is empty");
            
            try
            {
                // Ensure extension
                if (!fileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    fileName += FILE_EXTENSION;
                
                string filePath = Path.Combine(_basePath, fileName);
                
                // Update modification timestamp
                mapData.MarkModified();
                
                // Serialize on main thread (Unity objects)
                string json = JsonUtility.ToJson(mapData, true);
                
                // Write async
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, 
                    FileShare.None, BUFFER_SIZE, FileOptions.Asynchronous))
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                }
                
                Debug.Log($"[MapFileService] Saved map to: {filePath} ({bytes.Length} bytes)");
                return FileOperationResult.Successful(mapData);
            }
            catch (OperationCanceledException)
            {
                return FileOperationResult.Failed("Save operation was cancelled");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[MapFileService] IO error saving map: {ex.Message}");
                return FileOperationResult.Failed($"Failed to save file: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapFileService] Error saving map: {ex}");
                return FileOperationResult.Failed($"Unexpected error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads map data from JSON file asynchronously
        /// Optimized for large files (1000x1000 tiles in under 3 seconds)
        /// </summary>
        public async Task<FileOperationResult> LoadMapAsync(string fileName, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return FileOperationResult.Failed("File name is empty");
            
            var startTime = Time.realtimeSinceStartup;
            
            try
            {
                // Ensure extension
                if (!fileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    fileName += FILE_EXTENSION;
                
                string filePath = Path.Combine(_basePath, fileName);
                
                if (!File.Exists(filePath))
                    return FileOperationResult.Failed($"File not found: {fileName}");
                
                // Read async with large buffer
                string json;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, 
                    FileShare.Read, BUFFER_SIZE, FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, BUFFER_SIZE))
                {
                    json = await reader.ReadToEndAsync();
                }
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // Deserialize (must be on main thread for Unity)
                MapData mapData = JsonUtility.FromJson<MapData>(json);
                
                if (mapData == null)
                    return FileOperationResult.Failed("Failed to parse map data");
                
                // Build caches for fast runtime access
                mapData.BuildAllCaches();
                
                float loadTime = Time.realtimeSinceStartup - startTime;
                Debug.Log($"[MapFileService] Loaded map: {fileName} in {loadTime:F3}s " +
                    $"({mapData.GetTotalTileCount()} tiles)");
                
                return FileOperationResult.Successful(mapData, loadTime);
            }
            catch (OperationCanceledException)
            {
                return FileOperationResult.Failed("Load operation was cancelled");
            }
            catch (IOException ex)
            {
                Debug.LogError($"[MapFileService] IO error loading map: {ex.Message}");
                return FileOperationResult.Failed($"Failed to read file: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapFileService] Error loading map: {ex}");
                return FileOperationResult.Failed($"Unexpected error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Synchronous save for editor use (blocking)
        /// </summary>
        public FileOperationResult SaveMap(MapData mapData, string fileName)
        {
            if (mapData == null)
                return FileOperationResult.Failed("Map data is null");
            
            if (string.IsNullOrWhiteSpace(fileName))
                return FileOperationResult.Failed("File name is empty");
            
            try
            {
                if (!fileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    fileName += FILE_EXTENSION;
                
                string filePath = Path.Combine(_basePath, fileName);
                
                mapData.MarkModified();
                string json = JsonUtility.ToJson(mapData, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                
                return FileOperationResult.Successful(mapData);
            }
            catch (Exception ex)
            {
                return FileOperationResult.Failed(ex.Message);
            }
        }
        
        /// <summary>
        /// Synchronous load for editor use (blocking)
        /// </summary>
        public FileOperationResult LoadMap(string fileName)
        {
            var startTime = Time.realtimeSinceStartup;
            
            if (string.IsNullOrWhiteSpace(fileName))
                return FileOperationResult.Failed("File name is empty");
            
            try
            {
                if (!fileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    fileName += FILE_EXTENSION;
                
                string filePath = Path.Combine(_basePath, fileName);
                
                if (!File.Exists(filePath))
                    return FileOperationResult.Failed($"File not found: {fileName}");
                
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                MapData mapData = JsonUtility.FromJson<MapData>(json);
                
                if (mapData == null)
                    return FileOperationResult.Failed("Failed to parse map data");
                
                mapData.BuildAllCaches();
                
                float loadTime = Time.realtimeSinceStartup - startTime;
                return FileOperationResult.Successful(mapData, loadTime);
            }
            catch (Exception ex)
            {
                return FileOperationResult.Failed(ex.Message);
            }
        }
        
        /// <summary>
        /// Gets list of available map files
        /// </summary>
        public string[] GetAvailableMaps()
        {
            try
            {
                var files = Directory.GetFiles(_basePath, "*" + FILE_EXTENSION);
                for (int i = 0; i < files.Length; i++)
                {
                    files[i] = Path.GetFileNameWithoutExtension(files[i]);
                }
                return files;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapFileService] Error listing maps: {ex.Message}");
                return Array.Empty<string>();
            }
        }
        
        /// <summary>
        /// Deletes a map file
        /// </summary>
        public bool DeleteMap(string fileName)
        {
            try
            {
                if (!fileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    fileName += FILE_EXTENSION;
                
                string filePath = Path.Combine(_basePath, fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapFileService] Error deleting map: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a map file exists
        /// </summary>
        public bool MapExists(string fileName)
        {
            if (!fileName.EndsWith(FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                fileName += FILE_EXTENSION;
            
            return File.Exists(Path.Combine(_basePath, fileName));
        }
        
        /// <summary>
        /// Gets the full path to the maps directory
        /// </summary>
        public string GetMapsDirectory() => _basePath;
        
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
