# Versioned Deserialization System

This system provides robust JSON deserialization that can handle schema changes gracefully, ensuring backward compatibility when the `ModelMeta` data structure evolves.

## Problem Solved

Unity's `JsonUtility` is very strict about field matching. If you add, remove, or rename fields in `ModelMeta`, older JSON files will fail to deserialize completely. This system solves that by:

1. **Schema Versioning**: Each `ModelMeta` includes a `schemaVersion` field
2. **Automatic Migration**: Older data is automatically migrated to the current schema
3. **Fallback Deserialization**: If migration fails, basic fields are extracted manually
4. **Graceful Degradation**: The system never completely fails - it always returns a valid object

## Usage

### Basic Usage (Recommended)

```csharp
// Load ModelMeta with automatic migration
string json = File.ReadAllText("model.json");
ModelMeta modelMeta = JsonUtil.FromJsonModelMeta(json);

// This will:
// 1. Try standard deserialization first
// 2. If that fails, attempt migration
// 3. If migration fails, use fallback extraction
// 4. Always return a valid ModelMeta object (never null)
```

### Advanced Usage

```csharp
// For other types with migration support
MyCustomData data = JsonUtil.FromJsonWithMigration<MyCustomData>(json);

// Disable migration if you want to handle errors manually
ModelMeta modelMeta = JsonUtil.FromJsonWithMigration<ModelMeta>(json, migrate: false);
```

### Standard Usage (No Migration)

```csharp
// Use standard Unity JsonUtility (for new data or when you're sure about compatibility)
ModelMeta modelMeta = JsonUtil.FromJson<ModelMeta>(json);
```

## How It Works

### 1. Schema Versioning

Each `ModelMeta` now includes a `schemaVersion` field:

```csharp
public class ModelMeta
{
    public int schemaVersion = 1;  // Current schema version
    // ... other fields
}
```

### 2. Migration System

The `ModelMetaMigration` class handles upgrading data between schema versions:

```csharp
// Migration from version 0 to 1
private static bool MigrateFrom0To1(Data.ModelMeta modelMeta)
{
    // Initialize null collections
    if (modelMeta.payloadRelativePaths == null)
        modelMeta.payloadRelativePaths = new List<string>();
    // ... initialize other collections
    return true;
}
```

### 3. Fallback Deserialization

If both standard deserialization and migration fail, the system uses regex-based extraction to pull out basic fields:

```csharp
// Extract basic string fields even if full deserialization fails
if (TryExtractStringValue(json, "version", out string version))
{
    modelMeta.version = version;
}
```

## Adding New Migrations

When you make breaking changes to `ModelMeta`, follow these steps:

### 1. Increment Schema Version

```csharp
// In ModelMeta.cs
public int schemaVersion = 2;  // Increment this

// In ModelMetaMigration.cs
public const int CURRENT_SCHEMA_VERSION = 2;  // Update this
```

### 2. Add Migration Method

```csharp
// In ModelMetaMigration.cs
private static bool MigrateFrom1To2(Data.ModelMeta modelMeta)
{
    try
    {
        // Handle the changes from version 1 to 2
        // For example, if you renamed a field:
        // if (modelMeta.oldFieldName != null)
        // {
        //     modelMeta.newFieldName = modelMeta.oldFieldName;
        //     modelMeta.oldFieldName = null;
        // }
        
        return true;
    }
    catch (Exception ex)
    {
        Debug.LogError($"Migration from 1 to 2 failed: {ex.Message}");
        return false;
    }
}
```

### 3. Register Migration

```csharp
// In ApplyMigration method
case 1: return MigrateFrom1To2(modelMeta);
```

## Common Migration Patterns

### Adding New Fields

```csharp
// New fields with default values don't need migration
public string newField = "defaultValue";
```

### Renaming Fields

```csharp
// In migration method
if (modelMeta.oldFieldName != null)
{
    modelMeta.newFieldName = modelMeta.oldFieldName;
    modelMeta.oldFieldName = null;
}
```

### Changing Field Types

```csharp
// In migration method
if (modelMeta.oldStringField != null)
{
    if (int.TryParse(modelMeta.oldStringField, out int newIntValue))
    {
        modelMeta.newIntField = newIntValue;
    }
    modelMeta.oldStringField = null;
}
```

### Removing Fields

```csharp
// In migration method
// Just set to null or remove - no special handling needed
modelMeta.removedField = null;
```

## Testing

The system includes comprehensive error handling and logging. Check the Unity Console for:

- **Warnings**: When fallback deserialization is used
- **Errors**: When migration fails completely
- **Info**: When successful migration occurs

## Performance

- **Standard deserialization**: Fastest (Unity JsonUtility)
- **Migration**: Slightly slower (one-time cost)
- **Fallback**: Slowest (regex parsing, only used as last resort)

The system tries the fastest method first and only falls back to slower methods when necessary.

## Best Practices

1. **Always use `FromJsonModelMeta()`** for loading ModelMeta data
2. **Test migrations thoroughly** before deploying schema changes
3. **Keep migrations simple** - complex transformations should be avoided
4. **Document breaking changes** in your changelog
5. **Consider backward compatibility** when designing new fields

## Example: Complete Migration Scenario

Let's say you want to rename `description` to `summary` and add a new `category` field:

### 1. Update ModelMeta

```csharp
public class ModelMeta
{
    public int schemaVersion = 2;  // Increment
    public string summary;         // Renamed from description
    public string category;        // New field
    // ... other fields
}
```

### 2. Add Migration

```csharp
private static bool MigrateFrom1To2(Data.ModelMeta modelMeta)
{
    try
    {
        // Migrate renamed field
        if (!string.IsNullOrEmpty(modelMeta.description))
        {
            modelMeta.summary = modelMeta.description;
            modelMeta.description = null;
        }
        
        // Initialize new field
        if (string.IsNullOrEmpty(modelMeta.category))
        {
            modelMeta.category = "Uncategorized";
        }
        
        return true;
    }
    catch (Exception ex)
    {
        Debug.LogError($"Migration from 1 to 2 failed: {ex.Message}");
        return false;
    }
}
```

### 3. Update Current Version

```csharp
public const int CURRENT_SCHEMA_VERSION = 2;
```

Now all existing data will automatically migrate when loaded, and new data will use the new schema!
