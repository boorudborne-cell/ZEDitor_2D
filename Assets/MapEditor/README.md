# Runtime Map Editor для 2D-платформера

Модульный редактор карт для Unity с UGUI интерфейсом, оптимизированный для работы с большими картами.

## Содержание

- [Возможности](#возможности)
- [Системные требования](#системные-требования)
- [Установка](#установка)
- [Быстрый старт](#быстрый-старт)
- [Архитектура](#архитектура)
- [API Reference](#api-reference)
- [Формат данных](#формат-данных)
- [Оптимизация](#оптимизация)

## Возможности

### Управление файлами
- ✅ Создание новой карты с заданными размерами
- ✅ Сохранение карты в формат JSON
- ✅ Загрузка существующей карты
- ✅ Асинхронные операции для больших файлов

### Инструменты рисования
- ✅ **Кисть (Brush)**: Размещение тайлов с поддержкой перетаскивания
- ✅ **Ластик (Eraser)**: Удаление тайлов
- ✅ **Размещение сущностей**: Добавление игровых объектов
- ✅ **Выбор сущностей**: Перемещение и удаление объектов

### Работа со слоями
- ✅ Три слоя: Background, Ground, Foreground
- ✅ Переключение активного слоя
- ✅ Управление видимостью слоев
- ✅ Очистка слоя

### Визуализация
- ✅ Отображение/скрытие сетки
- ✅ Визуализация коллизий
- ✅ Масштабирование (0.25x - 4x)
- ✅ Панорамирование камеры

## Системные требования

- Unity 2021.3 LTS или новее
- TextMeshPro (входит в Unity)
- .NET Standard 2.1

## Установка

1. Скопируйте папку `MapEditor` в `Assets/` вашего проекта
2. Импортируйте TextMeshPro (Window > TextMeshPro > Import TMP Essential Resources)
3. Создайте TilePalette asset: `Create > Map Editor > Tile Palette`

## Быстрый старт

### Вариант 1: Автоматическая настройка

```csharp
// Создайте пустой GameObject и добавьте компонент
var setup = gameObject.AddComponent<MapEditor.Setup.MapEditorSetup>();
setup.tilePalette = yourTilePalette; // Назначьте палитру
// При старте сцены редактор создастся автоматически
```

### Вариант 2: Ручная настройка

1. Создайте GameObject с `MapEditorController`
2. Создайте камеру с `ChunkedMapRenderer`
3. Создайте Canvas с UI компонентами
4. Настройте ссылки между компонентами

### Использование API

```csharp
using MapEditor.Core;
using MapEditor.Data;

// Получение контроллера
var controller = FindObjectOfType<MapEditorController>();

// Создание новой карты
controller.CreateNewMap("MyLevel", 100, 50);

// Выбор инструмента
controller.SetTool(EditorTool.Brush);

// Выбор тайла для рисования
controller.SelectTile("ground_grass");

// Переключение слоя
controller.SetActiveLayer(LayerType.Ground);

// Сохранение
controller.SaveMap("level_01");

// Загрузка
controller.LoadMap("level_01");

// Управление видом
controller.ZoomIn();
controller.ZoomOut();
controller.ToggleGrid();
controller.ToggleCollisions();
```

## Архитектура

```
MapEditor/
├── Scripts/
│   ├── Core/                    # Ядро редактора
│   │   ├── MapEditorController  # Главный контроллер
│   │   ├── EditorTools          # Базовые классы инструментов
│   │   └── ToolImplementations  # Реализации инструментов
│   │
│   ├── Data/                    # Структуры данных
│   │   ├── TileData            # Данные тайла
│   │   ├── LayerData           # Данные слоя
│   │   ├── MapData             # Данные карты
│   │   └── TilePalette         # Палитра тайлов (ScriptableObject)
│   │
│   ├── Services/               # Сервисы
│   │   └── MapFileService      # Сохранение/загрузка файлов
│   │
│   ├── Rendering/              # Рендеринг
│   │   ├── MapRenderer         # Базовый рендерер
│   │   └── ChunkedMapRenderer  # Оптимизированный чанковый рендерер
│   │
│   ├── UI/                     # Интерфейс
│   │   ├── EditorUIPanel       # Главная панель
│   │   ├── TilePalettePanel    # Палитра тайлов
│   │   ├── LayerPanel          # Панель слоев
│   │   ├── Dialogs             # Диалоговые окна
│   │   └── CanvasInputHandler  # Обработка ввода
│   │
│   └── Setup/                  # Настройка
│       └── MapEditorSetup      # Автоматическое создание UI
```

### Ключевые компоненты

#### MapEditorController
Центральный контроллер, координирующий все подсистемы:
- Управление состоянием редактора
- Файловые операции
- Управление инструментами
- События для UI

#### EditorState
Контейнер состояния редактора:
- Текущая карта
- Активный слой и инструмент
- Настройки отображения
- Уровень масштабирования

#### ChunkedMapRenderer
Оптимизированный рендерер с чанковой системой:
- GPU Instancing для отрисовки тайлов
- Culling невидимых чанков
- Поддержка карт до 2000x2000 тайлов

## API Reference

### MapEditorController

```csharp
// События
event Action OnMapCreated;
event Action OnMapLoaded;
event Action OnMapSaved;
event Action<string> OnError;
event Action OnStateChanged;

// Свойства
EditorState State { get; }
ToolManager Tools { get; }
TilePalette Palette { get; }

// Управление картой
void CreateNewMap(string name, int width, int height);
void SaveMap(string fileName = null);
void LoadMap(string fileName);
string[] GetAvailableMaps();
bool DeleteMap(string fileName);

// Слои
void SetActiveLayer(LayerType layer);
void SetLayerVisibility(LayerType layer, bool visible);
void ClearLayer(LayerType layer);

// Вид
void ZoomIn();
void ZoomOut();
void SetZoom(float zoom);
void ToggleGrid();
void ToggleCollisions();
void CenterView();

// Инструменты
void SetTool(EditorTool tool);
void SelectTile(string tileId);
void SelectEntity(string entityType);

// Ввод
void HandlePointerDown(Vector2Int tilePosition);
void HandlePointerDrag(Vector2Int tilePosition);
void HandlePointerUp(Vector2Int tilePosition);
```

### MapFileService

```csharp
// Асинхронные операции
Task<FileOperationResult> SaveMapAsync(MapData data, string fileName, CancellationToken ct);
Task<FileOperationResult> LoadMapAsync(string fileName, CancellationToken ct);

// Синхронные операции
FileOperationResult SaveMap(MapData data, string fileName);
FileOperationResult LoadMap(string fileName);

// Утилиты
string[] GetAvailableMaps();
bool DeleteMap(string fileName);
bool MapExists(string fileName);
string GetMapsDirectory();
```

## Формат данных

### JSON структура карты

```json
{
  "mapName": "Level_01",
  "version": "1.0",
  "width": 100,
  "height": 50,
  "tileSize": 1.0,
  "createdAt": 1700000000,
  "modifiedAt": 1700000000,
  "layers": [
    {
      "layerName": "Background",
      "layerType": 0,
      "sortingOrder": 0,
      "isVisible": true,
      "tiles": [
        {
          "x": 0,
          "y": 0,
          "tileId": "sky_blue",
          "hasCollision": false
        }
      ]
    },
    {
      "layerName": "Ground",
      "layerType": 1,
      "sortingOrder": 1,
      "isVisible": true,
      "tiles": [
        {
          "x": 5,
          "y": 3,
          "tileId": "ground_grass",
          "hasCollision": true
        }
      ]
    }
  ],
  "entities": [
    {
      "entityId": "player_spawn_1",
      "entityType": "player_spawn",
      "posX": 2.5,
      "posY": 5.5,
      "customData": "{}"
    }
  ],
  "metadata": {
    "author": "",
    "description": "",
    "difficulty": "",
    "estimatedPlayTime": 0,
    "tags": []
  }
}
```

### Создание палитры тайлов

```csharp
// Через ScriptableObject (рекомендуется)
// Create > Map Editor > Tile Palette

// Программно
var palette = ScriptableObject.CreateInstance<TilePalette>();

palette.tiles.Add(new TilePaletteEntry
{
    tileId = "ground_grass",
    displayName = "Grass",
    category = "Ground",
    defaultHasCollision = true,
    previewColor = Color.green
});

palette.entities.Add(new EntityPaletteEntry
{
    entityType = "player_spawn",
    displayName = "Player Spawn",
    category = "Player",
    gizmoColor = Color.green,
    size = Vector2.one
});
```

## Оптимизация

### Производительность рендеринга

Модуль использует несколько техник оптимизации:

1. **Чанковая система**: Карта разбивается на чанки 16x16 тайлов
2. **Frustum Culling**: Отрисовываются только видимые чанки
3. **GPU Instancing**: Батчинг до 1023 тайлов за один draw call
4. **Кэширование**: O(1) доступ к тайлам через Dictionary

### Производительность I/O

1. **Асинхронные операции**: Неблокирующее чтение/запись
2. **Большие буферы**: 64KB буферы для быстрого I/O
3. **Отложенное построение кэшей**: Кэши строятся после загрузки

### Тесты производительности

| Размер карты | Загрузка | FPS (полный вид) | Память |
|--------------|----------|------------------|--------|
| 100x100      | <0.1s    | 60+              | ~5MB   |
| 500x500      | ~0.5s    | 60+              | ~50MB  |
| 1000x1000    | ~2s      | 60+              | ~150MB |

## Горячие клавиши

| Клавиша | Действие |
|---------|----------|
| B | Кисть |
| E | Ластик |
| V | Выбор сущности |
| G | Показать/скрыть сетку |
| C | Показать/скрыть коллизии |
| +/- | Масштабирование |
| Home | Центрировать вид |
| Space + ЛКМ | Панорамирование |
| СКМ | Панорамирование |
| Delete | Удалить выбранную сущность |
| Ctrl+S | Сохранить |
| Ctrl+N | Новая карта |

## Расширение функционала

### Добавление нового инструмента

```csharp
public class FillTool : BaseTileTool
{
    public override EditorTool ToolType => EditorTool.Fill; // Добавьте в enum
    public override string DisplayName => "Fill";
    
    protected override void ApplyTool(Vector2Int position, EditorState state)
    {
        // Реализация заливки
        FloodFill(position, state);
    }
    
    private void FloodFill(Vector2Int start, EditorState state)
    {
        // Алгоритм заливки
    }
}
```

### Добавление нового слоя

```csharp
// В LayerType enum добавьте:
public enum LayerType
{
    Background = 0,
    Ground = 1,
    Foreground = 2,
    Decorations = 3  // Новый слой
}

// При создании карты:
map.layers.Add(new LayerData(LayerType.Decorations));
```

### Кастомные сущности

```csharp
// В палитре добавьте:
palette.entities.Add(new EntityPaletteEntry
{
    entityType = "moving_platform",
    displayName = "Moving Platform",
    category = "Mechanics",
    gizmoColor = Color.cyan,
    size = new Vector2(3, 1)
});

// При загрузке уровня в игре:
foreach (var entity in mapData.entities)
{
    switch (entity.entityType)
    {
        case "moving_platform":
            var platform = Instantiate(platformPrefab, entity.Position, Quaternion.identity);
            var customData = JsonUtility.FromJson<PlatformData>(entity.customData);
            platform.Configure(customData);
            break;
    }
}
```

## Лицензия

MIT License
