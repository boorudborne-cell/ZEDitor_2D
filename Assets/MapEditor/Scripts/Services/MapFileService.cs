using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using MapEditor.Data;

namespace MapEditor.Services
{
    public class MapFileService
    {
        private readonly string _basePath;
        
        public MapFileService(string basePath = null)
        {
            _basePath = basePath ?? Path.Combine(Application.persistentDataPath, "Maps");
            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);
        }
        
        public void Save(MapData map, string fileName)
        {
            var path = GetPath(fileName);
            var json = JsonUtility.ToJson(map, true);
            File.WriteAllText(path, json);
            Debug.Log($"[MapFileService] Saved: {path}");
        }
        
        public MapData Load(string fileName)
        {
            var path = GetPath(fileName);
            if (!File.Exists(path)) return null;
            
            var json = File.ReadAllText(path);
            var map = JsonUtility.FromJson<MapData>(json);
            map?.BuildCaches();
            return map;
        }
        
        public async Task SaveAsync(MapData map, string fileName)
        {
            var path = GetPath(fileName);
            var json = JsonUtility.ToJson(map, true);
            await File.WriteAllTextAsync(path, json);
        }
        
        public async Task<MapData> LoadAsync(string fileName)
        {
            var path = GetPath(fileName);
            if (!File.Exists(path)) return null;
            
            var json = await File.ReadAllTextAsync(path);
            var map = JsonUtility.FromJson<MapData>(json);
            map?.BuildCaches();
            return map;
        }
        
        public string[] GetMapList()
        {
            var files = Directory.GetFiles(_basePath, "*.json");
            for (int i = 0; i < files.Length; i++)
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            return files;
        }
        
        public bool Delete(string fileName)
        {
            var path = GetPath(fileName);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        
        public string GetMapsFolder() => _basePath;
        
        private string GetPath(string fileName)
        {
            if (!fileName.EndsWith(".json")) fileName += ".json";
            return Path.Combine(_basePath, fileName);
        }
    }
}
