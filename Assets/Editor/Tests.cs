using NUnit.Framework;
using UnityEngine;
using MapEditor.Core;
using MapEditor.Data;
using System.Collections.Generic;

namespace MapEditor.Tests
{
    public class SafeMapEditorTests
    {
        // --- ТЕСТЫ ЧИСТЫХ ДАННЫХ (Эти пройдут всегда, так как не зависят от Unity Scene) ---

        [Test]
        public void T01_MapData_Create_GeneratesCorrectStructure()
        {
            // Тестируем статический метод создания карты
            var map = MapData.Create("TestMap", 50, 50);

            Assert.IsNotNull(map);
            Assert.AreEqual("TestMap", map.mapName);
            Assert.AreEqual(50, map.width);
            Assert.AreEqual(50, map.height);
            // Проверяем, что создались 3 стандартных слоя
            Assert.AreEqual(3, map.layers.Count);
        }

        [Test]
        public void T02_LayerData_LayerType_CastCorrectly()
        {
            // Проверяем приведение int к enum
            var layer = new LayerData { layerType = 1 }; // 1 = Ground
            Assert.AreEqual(LayerType.Ground, layer.Type);
        }

        [Test]
        public void T03_TileData_PositionProperty_Works()
        {
            // Проверяем геттер/сеттер Position
            var tile = new TileData { x = 10, y = 20 };

            Assert.AreEqual(new Vector2Int(10, 20), tile.Position);

            tile.Position = new Vector2Int(5, 5);
            Assert.AreEqual(5, tile.x);
            Assert.AreEqual(5, tile.y);
        }

        [Test]
        public void T04_LayerData_ManualCache_Operations()
        {
            // Тестируем логику словаря внутри LayerData напрямую
            var layer = new LayerData();
            var tile = new TileData { x = 1, y = 1, tileId = "grass" };

            // Добавляем тайл вручную
            layer.tiles.Add(tile);

            // Строим кэш
            layer.BuildCache();

            // Проверяем получение
            var retrieved = layer.GetTile(new Vector2Int(1, 1));
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("grass", retrieved.tileId);
        }

        [Test]
        public void T05_EditorState_Zoom_ClampsValues()
        {
            // Тестируем логику EditorState (она не зависит от MonoBehaviour)
            var state = new EditorState();

            // Пытаемся установить Zoom меньше минимума (0.25f)
            state.SetZoom(0.1f);
            Assert.AreEqual(0.25f, state.Zoom);

            // Пытаемся установить Zoom больше максимума (4f)
            state.SetZoom(10f);
            Assert.AreEqual(4f, state.Zoom);
        }

        // --- ТЕСТЫ КОНТРОЛЛЕРА (Минималистичные, без Palette) ---

        private GameObject _hostParams;
        private MapEditorController _controller;

        [SetUp]
        public void Setup()
        {
            _hostParams = new GameObject("TestHost");
            _controller = _hostParams.AddComponent<MapEditorController>();
            // Awake вызовется автоматически при AddComponent
        }

        [TearDown]
        public void Teardown()
        {
            if (_hostParams != null)
                Object.DestroyImmediate(_hostParams);
        }

        [Test]
        public void T06_MapData_Factory_CreatesValidMap()
        {
            // Logic: Контроллер внутри CreateMap вызывает MapData.Create.
            // Мы тестируем именно этот метод, так как он не зависит от MonoBehaviour.
            var map = MapData.Create("SafeMap", 100, 100);

            Assert.IsNotNull(map);
            Assert.AreEqual("SafeMap", map.mapName);
            Assert.AreEqual(100, map.width);
            Assert.AreEqual(100, map.height);
            // Проверяем, что создались слои Background, Ground, Foreground
            Assert.IsTrue(map.layers.Exists(l => l.name == "Ground"));
        }

        [Test]
        public void T07_EditorState_ToolSelection_Works()
        {
            // Logic: Контроллер просто меняет переменную в State.
            // Тестируем сам класс State.
            var state = new EditorState();

            // По умолчанию Brush
            Assert.AreEqual(EditorTool.Brush, state.ActiveTool);

            // Меняем на Eraser
            state.ActiveTool = EditorTool.Eraser;
            Assert.AreEqual(EditorTool.Eraser, state.ActiveTool);

            // Меняем на Prefab
            state.ActiveTool = EditorTool.Prefab;
            Assert.AreEqual(EditorTool.Prefab, state.ActiveTool);
        }

        [Test]
        public void T08_EditorState_LayerSwitching_Works()
        {
            // Logic: Аналогично инструментам, тестируем хранилище состояния.
            var state = new EditorState();

            // По умолчанию Ground
            Assert.AreEqual(LayerType.Ground, state.ActiveLayer);

            // Меняем на Foreground
            state.ActiveLayer = LayerType.Foreground;
            Assert.AreEqual(LayerType.Foreground, state.ActiveLayer);
        }

        [Test]
        public void T09_Controller_UndoRedo_EmptyInitially()
        {
            // Сразу после создания стеки должны быть пусты
            Assert.IsFalse(_controller.CanUndo);
            Assert.IsFalse(_controller.CanRedo);
        }

        [Test]
        public void T10_LayerData_RemoveTile_ReturnsCorrectBool()
        {
            // Logic: Метод EraseTile в контроллере вызывает layer.RemoveTile().
            // Тестируем, что этот метод реально удаляет данные.
            var layer = new LayerData();
            var pos = new Vector2Int(5, 5);

            // 1. Добавляем тайл
            layer.SetTile(new TileData { x = 5, y = 5, tileId = "stone" });
            Assert.IsNotNull(layer.GetTile(pos), "Setup failed: Tile not added");

            // 2. Удаляем существующий тайл
            bool removed = layer.RemoveTile(pos);

            // Проверки
            Assert.IsTrue(removed, "RemoveTile должен вернуть true, если тайл был удален");
            Assert.IsNull(layer.GetTile(pos), "Тайл должен исчезнуть из данных");
            Assert.AreEqual(0, layer.tiles.Count, "Список тайлов должен стать пустым");

            // 3. Пытаемся удалить несуществующий тайл
            bool removedAgain = layer.RemoveTile(pos);
            Assert.IsFalse(removedAgain, "RemoveTile должен вернуть false, если удалять нечего");
        }
    }
}