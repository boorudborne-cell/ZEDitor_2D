# Runtime Map Editor — UI Toolkit Version

## Быстрая установка

### 1. Импорт

1. Скопируйте папку `MapEditor` в `Assets/`
2. Дождитесь компиляции

### 2. Создание TilePalette

1. **ПКМ в Project → Create → Map Editor → Tile Palette**
2. Назовите `MainPalette`
3. Добавьте тайлы:
   - Нажмите `+` в секции Tiles
   - Заполните: `id`, `displayName`, `category`, `sprite`, `hasCollision`

**Пример тайлов:**
```
id: grass          | displayName: Grass    | category: Ground | hasCollision: ✓
id: dirt           | displayName: Dirt     | category: Ground | hasCollision: ✓
id: stone          | displayName: Stone    | category: Ground | hasCollision: ✓
id: spike          | displayName: Spike    | category: Hazard | hasCollision: ✓
id: flower         | displayName: Flower   | category: Deco   | hasCollision: ✗
```

### 3. Настройка сцены

Создайте иерархию объектов:

```
[MapEditor]
├── Controller        → MapEditorController
├── Renderer          → TileMapRenderer
├── InputHandler      → EditorInputHandler
├── UI                → UIDocument + MapEditorUIController
└── Main Camera       → Camera (Orthographic)
```

#### 3.1 Controller (MapEditorController)

1. Создайте пустой объект `Controller`
2. Добавьте компонент `MapEditorController`
3. Назначьте `Palette` → ваш `MainPalette`

#### 3.2 Renderer (TileMapRenderer)

1. Создайте пустой объект `Renderer`
2. Добавьте компонент `TileMapRenderer`
3. Назначьте `Controller` → объект Controller

#### 3.3 InputHandler (EditorInputHandler)

1. Создайте пустой объект `InputHandler`
2. Добавьте компонент `EditorInputHandler`
3. Назначьте `Controller` → объект Controller

#### 3.4 UI (UIDocument)

1. Создайте пустой объект `UI`
2. Добавьте компонент `UIDocument`
3. Назначьте:
   - **Panel Settings** — создайте новый: `Create → UI Toolkit → Panel Settings Asset`
   - **Source Asset** → `MapEditor/UI/MapEditorUI.uxml`
4. Добавьте компонент `MapEditorUIController`
5. Назначьте:
   - **UI Document** → сам UIDocument
   - **Controller** → объект Controller

#### 3.5 Camera

1. Выберите Main Camera
2. Настройки:
   - **Projection**: Orthographic
   - **Size**: 5
   - **Background**: тёмно-серый (#1a1a1a)

### 4. Panel Settings

Создайте Panel Settings Asset:

1. **Create → UI Toolkit → Panel Settings Asset**
2. Назовите `EditorPanelSettings`
3. Настройки:
   - **Scale Mode**: Scale With Screen Size
   - **Reference Resolution**: 1920 x 1080
   - **Match**: 0.5

---

## Использование

### Запуск

1. Play
2. Нажмите **New** в тулбаре
3. Введите имя, размеры карты
4. **Create**

### Рисование

1. Выберите тайл в правой панели (кликните на спрайт)
2. Кликайте или перетаскивайте по канвасу
3. Под курсором отображается превью тайла

### Инструменты

| Кнопка | Клавиша | Действие |
|--------|---------|----------|
| Brush | B | Ставить тайлы |
| Eraser | E | Удалять тайлы |
| Entity | — | Ставить сущности |

### Навигация

| Действие | Управление |
|----------|------------|
| Масштаб | Колесо мыши / +/- |
| Панорама | СКМ или Space + ЛКМ |
| Центрировать | Home |

### Слои

- **Background** — задний фон (sortOrder: 0)
- **Ground** — основной слой (sortOrder: 1)
- **Foreground** — передний план (sortOrder: 2)

### Сохранение

- **Save** — сохраняет в `Application.persistentDataPath/Maps/`
- **Load** — открывает список сохранённых карт
- **Ctrl+S** — быстрое сохранение

---

## Структура проекта

```
MapEditor/
├── Scripts/
│   ├── Core/
│   │   ├── EditorState.cs         — состояние редактора
│   │   ├── MapEditorController.cs — главный контроллер
│   │   ├── TileMapRenderer.cs     — рендеринг спрайтов + превью
│   │   └── EditorInputHandler.cs  — обработка ввода
│   ├── Data/
│   │   ├── MapData.cs             — структуры данных карты
│   │   └── TilePalette.cs         — ScriptableObject палитры
│   ├── Services/
│   │   └── MapFileService.cs      — сохранение/загрузка JSON
│   └── UI/
│       └── MapEditorUIController.cs — UI Toolkit контроллер
├── UI/
│   ├── MapEditorUI.uxml           — разметка интерфейса
│   └── MapEditorStyles.uss        — стили
└── MapEditor.asmdef
```

---

## Формат карты (JSON)

```json
{
  "mapName": "Level1",
  "width": 50,
  "height": 30,
  "layers": [
    {
      "name": "Ground",
      "layerType": 1,
      "sortingOrder": 1,
      "isVisible": true,
      "tiles": [
        { "x": 5, "y": 3, "tileId": "grass", "hasCollision": true }
      ]
    }
  ],
  "entities": [
    { "entityId": "spawn_123", "entityType": "player_spawn", "x": 2, "y": 5 }
  ]
}
```

---

## Интеграция с игрой

```csharp
using MapEditor.Data;
using MapEditor.Services;

public class LevelLoader : MonoBehaviour
{
    public TilePalette palette;
    
    void LoadLevel(string name)
    {
        var service = new MapFileService();
        var map = service.Load(name);
        
        foreach (var layer in map.layers)
        {
            foreach (var tile in layer.tiles)
            {
                var def = palette.GetTile(tile.tileId);
                if (def?.sprite == null) continue;
                
                var go = new GameObject(tile.tileId);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = def.sprite;
                sr.sortingOrder = layer.sortingOrder;
                
                // Позиция на Vector2Int!
                go.transform.position = new Vector3(
                    tile.x + 0.5f, 
                    tile.y + 0.5f, 
                    0
                );
                
                if (tile.hasCollision)
                {
                    go.AddComponent<BoxCollider2D>();
                }
            }
        }
    }
}
```

---

## Особенности

- **Превью под курсором** — полупрозрачный спрайт показывает, где будет поставлен тайл
- **Vector2Int координаты** — все тайлы на целочисленных позициях
- **UI Toolkit** — современный, масштабируемый интерфейс
- **Спрайты** — тайлы отображаются как реальные спрайты, не цветные квадраты
