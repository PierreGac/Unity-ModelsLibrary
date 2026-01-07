# Model Library for Unity

A comprehensive Unity Editor tool that enables teams to version, browse, submit, and import 3D models (FBX, OBJ, materials, textures) from a shared repository. Features include intelligent caching, automatic update detection, role-based permissions, and seamless integration with Unity's asset pipeline.

![Unity Version](https://img.shields.io/badge/Unity-6000.3.2f1-blue)
![.NET Standard](https://img.shields.io/badge/.NET-Standard%202.1-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

## 🎯 Key Features

- **📦 Browser Window** - Search, filter, and browse models with grid/list views
- **✅ Installation Status** - Visual badges showing which models are installed
- **🔄 Automatic Updates** - Detects and manages model updates with version comparison
- **📤 One-Click Import** - Import models directly into your project with preserved GUIDs
- **📝 Model Submission** - Submit new models or update existing ones with smart name prefilling
- **🏷️ Tag System** - Organize models with tags and filter by them
- **⭐ Favorites & Recent** - Quick access to frequently used models
- **📊 Analytics** - Track model usage and statistics (Admin/Artist roles)
- **👥 Role-Based Access** - Developer, Artist, and Admin roles with appropriate permissions
- **💬 Notes System** - Leave feedback and notes on models
- **📋 Changelog Tracking** - Version history with detailed changelog entries
- **🖼️ Preview Images** - High-resolution preview thumbnails
- **🔍 Smart Search** - Search by name, tags, or description
- **📦 Bulk Operations** - Batch import, update, and tag management
- **⚙️ Hidden Metadata** - Manifest files automatically hidden from Project window
- **🔄 Auto-Refresh** - Browser automatically refreshes after model submission

## 📋 Prerequisites

Before installing Model Library, ensure you meet the following requirements:

- **Unity Version**: Unity 6.0.2 or later (tested with Unity 6000.2.6f2)
- **.NET Compatibility**: .NET Standard 2.1
  - ⚠️ **Note**: This package has been developed and tested with .NET Standard 2.1 only
  - .NET Framework compatibility has **not been tested** and is not guaranteed
- **Platform**: Windows, macOS, or Linux (Unity Editor)
- **Editor Only**: This is an Editor-only package with no runtime dependencies

### System Requirements

- Unity Editor (not required in builds)
- Sufficient disk space for model cache (configurable in settings)
- Network access (if using HTTP repository backend)

## 📦 Installation

### Option 1: Unity Package Manager (Git URL) - Recommended

This is the recommended installation method for easy updates and version management.

1. Open your Unity project
2. Open Unity Package Manager: `Window > Package Manager`
3. Click the `+` button in the top-left corner
4. Select `Add package from git URL...`
5. Enter the following URL:

   ```
   https://github.com/PierreGac/Unity-ModelsLibrary.git?path=Assets/ModelLibrary
   ```

6. Click `Add`
7. Unity will download and import the package automatically

**Note**: For a specific version or branch, append `#version` or `#branch` to the URL:

- Version: `https://github.com/PierreGac/Unity-ModelsLibrary.git?path=Assets/ModelLibrary#v1.0.0`
- Branch: `https://github.com/PierreGac/Unity-ModelsLibrary.git?path=Assets/ModelLibrary#main`

### Option 2: Manual Installation

If you prefer to install manually or need to modify the source code:

1. Download or clone the repository:

   ```bash
   git clone https://github.com/PierreGac/Unity-ModelsLibrary.git
   ```

2. Copy the `Assets/ModelLibrary` folder into your Unity project's `Assets` folder
3. Unity will automatically detect and import the package
4. Wait for Unity to finish importing all assets

### Option 3: Unity Package Manager (Local Package)

For development or testing:

1. Clone the repository to a local directory
2. In Unity Package Manager, click `+` → `Add package from disk...`
3. Navigate to the cloned repository and select `Assets/ModelLibrary/package.json`
4. Click `Open`

### Post-Installation

After installation:

1. Open Unity Editor
2. Navigate to `Tools > Model Library > Browser`
3. The first-run wizard will guide you through initial configuration
4. Set up your repository location and user identity

### Updating

**If installed via Git URL:**

- Unity Package Manager will show available updates
- Click `Update` in the Package Manager window

**If installed manually:**

- Pull the latest changes from the repository
- Unity will automatically detect and reimport changed files

### Troubleshooting

**Package not appearing in Package Manager:**

- Ensure you're using Unity 6.0.2 or later
- Check that the Git URL is correct
- Verify you have internet connectivity

**Import errors:**

- Close and reopen Unity Editor
- Delete `Library` folder and let Unity regenerate it
- Check Unity Console for specific error messages

**First-run wizard not appearing:**

- Manually open: `Tools > Model Library > Settings > Configuration Wizard`

## 🚀 Quick Start

### 1. Open the Browser Window

- Unity menu: `Tools > Model Library > Browser`
- On first run, the setup wizard guides you through configuration

### 2. Configure Repository

The first-run wizard helps you set up:

- **Repository Type**:
  - **File System**: A shared folder accessible to your team
  - **HTTP**: A web endpoint implementing the repository interface. This approach needs testing. The development have been done only with Windows Explorer.
- **Repository Root**: Path or base URL to your model repository
- **User Identity**: Your name and role (Developer, Artist, or Admin)

### 3. Browse and Import Models

- Use the search bar to find models by name or tags
- Click on a model card to view details
- Use the "Import to Project" button to add models to your Unity project
- Installed models show a green "Installed" badge

### 4. Submit Models

- Unity menu: `Tools > Model Library > Submit Model`
- Or right-click on assets in Project window: `Submit Model`
- Select your model assets (FBX, OBJ, materials, textures)
- Fill in metadata (name, version, description, tags)
- Submit as a new model or update an existing one

## 📸 Visual Tour

### Browser Window

The main browser window provides a comprehensive view of all available models with search, filtering, and quick access to common operations.

![Model Library Browser](Editor/Documentation~/browser.jpg)

**Key Features Visible:**

- **Search Bar**: Filter models by name or tags
- **View Modes**: Toggle between Grid and List views
- **Filter Tabs**: All, Favorites, and Recent models
- **Installed Badge**: Green "Installed" label on models already in your project
- **Update Indicators**: Visual cues for models with available updates
- **Action Buttons**: Refresh, Submit Model, Bulk Operations
- **User Role Display**: Current user role (Developer/Artist/Admin)
- **Sort Options**: Sort by name, date, or version

### Model Details Window

View comprehensive information about a specific model, including metadata, version history, changelog, and notes.

![Model Details Window](Editor/Documentation~/existing_model_details.jpg)

**Features:**

- **Model Information**: Name, version, author, description
- **Installation Status**: Shows if model is installed and current version
- **Update Detection**: "Update Available" button when newer version exists
- **Model Structure**: View all files included in the model
- **Changelog**: Version history with dates and authors
- **Notes System**: Add and view feedback notes
- **Metadata Editing**: Edit description and tags (requires Artist/Admin role)
- **Version Management**: Delete old versions (requires Artist/Admin role)
- **Import/Update**: One-click import or update to latest version
- **3D Preview**: Launch interactive 3D preview window

### Submit Model Window

Submit new models or update existing ones with a comprehensive form and validation.

![Submit Model Window](Editor/Documentation~/submit_01.jpg)

**Features:**

- **Mode Selection**: New Model or Update Existing
- **Smart Name Prefilling**: Automatically extracts name from:
  1. FBX/OBJ file name (e.g., "MyModel.fbx" → "MyModel")
  2. Folder name (if no FBX/OBJ selected)
  3. Existing model manifest (if asset belongs to installed model)
- **Tabbed Interface**: Basic Info, Assets, Images, Advanced
- **Version Validation**: Semantic versioning (SemVer) format validation
- **Tag Management**: Add and remove tags
- **Image Upload**: Drag-and-drop preview images
- **Draft Saving**: Save work in progress and resume later
- **Inline Validation**: Real-time feedback on form fields
- **Changelog Entry**: Required for updates with change summary

## 🎨 Detailed Features

### Installation Status Detection

- **Visual Indicators**: Green "Installed" badge on model cards
- **Version Tracking**: Shows installed version vs. latest available
- **Update Detection**: "Update Available" badge when newer version exists
- **Automatic Detection**: Scans project for manifest files to detect installations
- **Backward Compatibility**: Supports both old and new manifest file naming conventions

### Smart Name Prefilling

When submitting a model, the system intelligently prefills the model name:

1. **Priority 1**: Extract from FBX/OBJ file name
   - `MyModel.fbx` → `MyModel`
   - `Character_01.obj` → `Character_01`

2. **Priority 2**: Extract from folder name
   - If no FBX/OBJ is directly selected, uses parent folder name

3. **Priority 3**: Extract from existing model manifest
   - If selected assets belong to an already-installed model, uses that model's name

### Hidden Metadata Files

- Manifest files (`.modelLibrary.meta.json`) are automatically hidden from Unity's Project window
- Uses dot-prefix naming convention (`.modelLibrary.meta.json`) to leverage Unity's native hiding
- Backward compatible with old naming (`modelLibrary.meta.json`)
- Files remain accessible programmatically but don't clutter the Project view

### Automatic Refresh

- Browser window automatically refreshes after model submission
- No need to manually refresh or reopen the window
- Index and manifest cache are updated automatically
- All open browser windows are synchronized

### Role-Based Permissions

**Developer Role:**

- Browse and import models
- View model details
- Leave notes and feedback
- Cannot submit or delete models

**Artist Role:**

- All Developer permissions
- Submit new models
- Update existing models
- Edit metadata (description, tags)
- Delete model versions
- Access analytics

**Admin Role:**

- All Artist permissions
- Full system access
- Advanced analytics
- System management

### Notes System

- Add notes to models with tags (remarks, bug, feature request, etc.)
- Notes are shared across all versions of a model
- View note history in the Model Details window
- Notes include author, timestamp, and tag

### Changelog Tracking

- Automatic changelog entry creation on updates
- Required change summary for version updates
- View full version history in Model Details window
- Changelog entries include version, date, author, and summary

### Bulk Operations

- **Bulk Import**: Import multiple models at once
- **Bulk Update**: Update multiple installed models
- **Bulk Tagging**: Add or remove tags from multiple models
- **Batch Upload**: Upload multiple models from a directory

### Search and Filtering

- **Text Search**: Search by model name, description, or tags
- **Tag Filtering**: Filter by specific tags using the tag foldout
- **View Modes**: Grid view (with thumbnails) or List view (compact)
- **Sort Options**: Sort by name, date, or version
- **Favorites**: Mark models as favorites for quick access
- **Recent**: View recently used models

### Update Detection

- Automatic background checking for model updates
- Visual indicators for models with available updates
- Version comparison using Semantic Versioning (SemVer)
- One-click update to latest version
- Preserves local customizations during updates

### Preview System

- High-resolution preview thumbnails in grid view
- 3D interactive preview window
- Automatic thumbnail generation
- Support for multiple preview images per model

## 🔧 Configuration

### Repository Settings

Configure your model repository in `Tools > Model Library > Settings`:

- **Repository Type**: File System or HTTP
- **Repository Root**: Path or URL to repository
- **Cache Location**: Local cache directory for downloaded models

### User Settings

Set your identity and preferences:

- **User Name**: Your display name for submissions and notes
- **User Role**: Developer, Artist, or Admin
- **Preferences**: Various UI and behavior preferences

### First-Run Wizard

On first launch, a guided wizard helps you:

1. Set your user name
2. Choose repository type
3. Configure repository location
4. Set initial preferences

## 📋 Workflows

### Browsing and Importing

1. Open the Browser window (`Tools > Model Library > Browser`)
2. Use search or filters to find models
3. Click on a model card to view details
4. Click "Import to Project" to add to your project
5. Model is copied to `Assets/Models/[ModelName]/` with preserved GUIDs

### Updating Models

1. Models with updates show an "Update Available" badge
2. Open Model Details window for the model
3. Click "Update" button (replaces "Import" when update available)
4. System updates to latest version while preserving local files

### Submitting New Models

1. Select model assets in Project window (FBX, OBJ, materials, textures)
2. Right-click → `Submit Model` or use menu `Tools > Model Library > Submit Model`
3. Model name is automatically prefilled (if possible)
4. Fill in version, description, and tags
5. Add preview images (optional)
6. Click "Submit"
7. Browser window automatically refreshes to show new model

### Updating Existing Models

1. Open Submit window and select "Update Existing" mode
2. Choose the model to update from dropdown
3. Version is auto-suggested (patch bump from latest)
4. Enter change summary (required)
5. Modify description, tags, or assets as needed
6. Click "Submit"
7. New version is created with updated metadata

### Managing Metadata

1. Open Model Details window for any model
2. Click "Edit" next to Description or Tags
3. Make changes (requires Artist/Admin role)
4. Click "Save Metadata Changes"
5. Changes create a new metadata version

### Adding Notes

1. Open Model Details window
2. Scroll to "Notes" section
3. Enter note text
4. Select note tag (remarks, bug, feature request, etc.)
5. Click "Submit Note"
6. Note is saved and visible to all users

## 🏗️ Architecture

### Repository Layout

```text
<repository-root>/
  models_index.json              # Master index of all models
  <modelId>/
    <version>/
      model.json                 # Version metadata
      payload/                    # Model files (FBX, materials, textures)
        <files and dependencies>
      images/                     # Preview images and screenshots
        <preview images>
```

### Local Cache

- Downloads are cached under a configurable library path
- Cache location set in Settings
- Import copies from cache into `Assets/` folder
- GUIDs are preserved for reliable update detection

### Metadata Structure

**Index (`models_index.json`):**

- List of all models with latest version
- Includes: id, name, latest version, description, tags, timestamps

**Model Metadata (`model.json`):**

- Identity (id, name)
- Version (SemVer format)
- Description and tags
- Author and timestamps
- Asset GUIDs (for update detection)
- Payload paths
- Changelog entries
- Notes
- Preview image references

### Manifest Files

Each imported model creates a manifest file in the installation directory:

- **File Name**: `.modelLibrary.meta.json` (hidden from Project window)
- **Location**: `Assets/Models/[ModelName]/.modelLibrary.meta.json`
- **Purpose**: Tracks installed model ID and version
- **Backward Compatible**: Also supports old naming `modelLibrary.meta.json`

## 🛠️ Development

### Project Structure

```text
Assets/ModelLibrary/
  Editor/
    Windows/                      # Editor windows
      ModelLibraryWindow.cs      # Main browser window
      ModelDetailsWindow.cs      # Model details and import
      ModelSubmitWindow.cs       # Model submission form
      ModelVersionComparisonWindow.cs  # Version comparison
      ModelBulkTagWindow.cs      # Bulk tag editor
      BatchUploadWindow.cs       # Batch upload interface
      UserSettingsWindow.cs      # User preferences
      UnifiedSettingsWindow.cs   # Repository settings
      AnalyticsWindow.cs         # Usage analytics
      ModelPreview3DWindow.cs   # 3D preview
      ...
    Infrastructure/
      Repository/                # Storage backends
        IModelRepository.cs      # Repository interface
        FileSystemRepository.cs # File system implementation
        HttpRepository.cs       # HTTP implementation
      Services/                  # Business logic
        ModelLibraryService.cs   # Main service facade
        ModelIndexService.cs     # Index management
        ModelMetadataService.cs  # Metadata operations
        ModelPreviewService.cs  # Preview generation
        ModelScanService.cs     # Project scanning
        ModelDeployer.cs        # Model deployment
        ModelProjectImporter.cs # Project import
        ...
      Utils/                     # Utilities
        JsonUtil.cs              # JSON serialization
        SemVer.cs                # Version parsing
        PathUtils.cs             # Path utilities
        AssetVisibilityUtility.cs # File hiding
        ...
    ScriptableSettings/          # Settings
      ModelLibrarySettings.cs    # Repository settings
      SimpleUserIdentityProvider.cs # User identity
    Tests/                       # Unit tests
      ...
  Data/                          # Data models
    ModelMeta.cs                 # Model metadata
    ModelIndex.cs                # Index structure
    ModelNote.cs                   # Notes
    ModelIdentity.cs             # Model identity
    Tags.cs                      # Tag system
  Documentation~/                # Documentation assets
    browser.jpg                  # Browser screenshot
    existing_model_details.jpg   # Details window screenshot
    submit_01.jpg                # Submit window screenshot
```

### Key Components

**ModelLibraryService**: Main facade service that coordinates all operations

- Index management
- Metadata operations
- Preview generation
- Project scanning
- Update detection

**ModelIndexService**: Handles index operations

- Loading and caching index
- Refreshing index
- Version queries

**ModelMetadataService**: Manages model metadata

- Loading metadata
- Publishing updates
- Version file cloning

**ModelScanService**: Scans Unity project for installed models

- Manifest file discovery
- Installation status detection
- Version matching

### Building and Running

- No build step required
- Open Unity project and use menu commands
- Editor-only code (no runtime components)
- Compatible with Unity 2022+ and Unity 6

### Testing

Comprehensive unit test suite covering:

- Asset visibility and hiding
- Manifest file discovery
- Model name prefilling
- Installation detection
- Version management
- Bulk operations
- Settings and permissions
- Refresh operations

Run tests via Unity Test Runner: `Window > General > Test Runner`

## 🔌 Extensibility

### Repository Interface

Implement `IModelRepository` to add custom storage backends:

```csharp
public interface IModelRepository
{
    Task<ModelIndex> GetIndexAsync();
    Task<ModelMeta> GetMetaAsync(string modelId, string version);
    Task SaveIndexAsync(ModelIndex index);
    // ... more methods
}
```

### Metadata Extension

The `model.json` schema includes flexible fields:

- `extra`: Dictionary for custom metadata
- `dependencies`: Dependency references
- Extend without breaking schema changes

### Custom Services

Services are designed for dependency injection:

- Implement service interfaces
- Replace default implementations
- Extend functionality without modifying core

## 📚 Additional Windows

### Version Comparison Window

Compare two versions of a model side-by-side:

- View differences in metadata
- Compare asset lists
- Review changelog entries
- Access via Model Details window

### Bulk Tag Editor

Manage tags for multiple models:

- Select multiple models
- Add or remove tags in bulk
- Generate changelog entries for tag changes

### Analytics Window

View model usage statistics (Artist/Admin only):

- Import history
- Most used models
- User activity
- Version distribution

### 3D Preview Window

Interactive 3D preview of models:

- Rotate and zoom
- Material preview
- Lighting controls
- Export preview images

### Performance Profiler

Monitor async operation performance:

- Operation timing
- Cache hit rates
- Performance metrics

### Error Log Viewer

Review and manage errors:

- Recent error log
- Error suppression
- Debug information

## 🤝 Contributing

Contributions are welcome! Suggested areas:

- Enhanced preview generation
- Additional bulk operations
- CI/CD integration helpers?
- Performance optimizations, especially for the server interactions
- UI/UX improvements
- Documentation improvements

### Development Guidelines

- No `var` declarations (explicit types required)
- Use `for` loops instead of `foreach` for arrays/lists
- No magic numbers (use named constants)
- Comprehensive XML documentation
- .NET Standard 2.1 compatibility

Please open an issue to discuss significant changes before submitting a PR.

## 📜 Version History

### Version 1.0.0 - Initial Release

The first stable release of Model Library for Unity, featuring a comprehensive set of tools for managing 3D models in team environments.

#### 🚀 Core Features

##### Browser & Navigation

- Full-featured browser window with grid and list views
- Advanced search by name, tags, or description
- Tag-based filtering with multi-select support
- Favorites and recently used models tracking
- Sort by name, date, or version
- Visual installation status badges
- Update available indicators

##### Model Management

- One-click import with preserved Unity GUIDs
- Automatic update detection and version comparison
- Smart installation status detection (supports old and new manifest formats)
- Version comparison window for side-by-side analysis
- Model details window with comprehensive metadata view
- Automatic window refresh after model submission

##### Model Submission

- Submit new models or update existing ones
- Smart name prefilling from FBX/OBJ files, folder names, or existing models
- Comprehensive validation (paths, versions, changelog)
- Drag-and-drop preview image upload
- Draft saving and loading
- Changelog entry management
- Multi-tab interface (Basic Info, Assets, Images, Advanced)

##### Version Control

- Semantic versioning (SemVer) support
- Automatic version suggestion for updates
- Changelog tracking with author and timestamp
- Version history viewing
- Version deletion (with safeguards)

##### Metadata Management

- Rich metadata editing (description, tags)
- Tag system with case-insensitive matching
- Notes system with categorized feedback (remarks, bug, feature request)
- Model structure visualization
- Asset dependency tracking

##### User & Permissions

- Role-based access control (Developer, Artist, Admin)
- User identity management
- Permission-based UI elements
- Analytics access control

##### Repository Support

- File System repository backend (fully tested)
- HTTP repository backend (interface ready, needs testing)
- Configurable repository locations
- Local caching for offline access

##### Advanced Features

- Bulk operations (import, update, tagging)
- Batch upload from directory
- Analytics and usage statistics
- 3D interactive preview window
- Performance profiler
- Error log viewer
- Hidden metadata files (`.modelLibrary.meta.json`)

#### ✨ User Experience

- First-run configuration wizard
- Comprehensive help system
- Keyboard shortcuts support
- Progress indicators for long operations
- Real-time validation feedback
- Context menu integration (right-click to submit)
- Automatic refresh after operations
- Window auto-close after successful import

#### 🏗️ Architecture

- Clean separation of concerns (Repository, Services, UI layers)
- Dependency injection ready
- Extensible repository interface
- Comprehensive error handling
- Backward compatibility with old manifest files
- Migration system for metadata schema changes

#### 🧪 Testing

- Comprehensive unit test suite
- Coverage for critical workflows
- Performance testing infrastructure
- Backward compatibility tests

#### 📚 Documentation

- Comprehensive README with screenshots
- Architecture documentation
- Code documentation (XML comments)
- Development guidelines (AGENTS.md)
- Changelog tracking

#### 🔧 Technical Details

- .NET Standard 2.1 compatible
- Unity 6.0.2+ required
- Editor-only (no runtime dependencies)
- Async/await throughout for non-blocking operations
- File system enumeration for hidden files
- GUID preservation for reliable update detection

#### 🐛 Bug Fixes & Improvements

- Fixed Unity freeze issues with async operations
- Improved manifest file discovery (old and new formats)
- Enhanced installation status detection
- Fixed path validation and sanitization
- Improved error handling and user feedback
- Performance optimizations
- Code quality improvements (explicit typing, no magic numbers)

#### 📝 Known Limitations

- HTTP repository backend needs additional testing
- Performance optimization ongoing for very large datasets (1000+ models)
- Bulk operations may need additional validation in production environments

---

## 📝 License

MIT License - see LICENSE file for details

## 🙏 Acknowledgements

- Unity and the Editor ecosystem
- Contributors and teams using this workflow
- Open source community

## 📖 Additional Resources

- **Help Window**: `Tools > Model Library > Settings > Help / Documentation`
- **Keyboard Shortcuts**: `Tools > Model Library > Settings > Keyboard Shortcuts`
- **Architecture Documentation**: See `architecture.md` for detailed design

## 📋 Todo / Known Issues

This section tracks known issues and planned improvements based on user feedback:

### Network & Authentication Issues

- **Silent failure on network authentication**: When working locally (without VPN), the refresh operation doesn't retrieve any models if the user is not logged into the server (Windows credentials), but doesn't provide any error information. The browser simply shows "0 models found" without indicating the authentication problem.

### Cache & File System Issues

- **Access denied on cached JSON files**: After discarding models from SmartGit (or other version control tools) and retrying an update on the same version, an "access denied" error occurs on JSON files in the cache. The workaround is to manually delete the model version folder in the cache.

- **Cache cleanup after version deletion**: When deleting a version and restoring the previous one, the freshly deleted version remains displayed in the UI. Clicking on it opens a window that stays empty with "loading meta" message. Even after manually deleting the folder in the cache, the issue persists.

- **3D Preview causing import errors**: If a 3D Preview is launched for an asset before importing it, an "access denied" error occurs during the import process. The workaround is to manually delete the cache folder for that model version.

- **3D Preview textures disappear on focus loss**: When the Unity Editor loses focus (e.g., switching to another application) and then regains focus, textures in the 3D Preview window disappear. The textures are reloaded from asset paths, but the preview display does not update to show them. This appears to be related to Unity's asset reference system when the editor loses focus. Workaround: Close and reopen the 3D Preview window to restore textures.

### Import & GUID Management

- **Frequent GUID conflicts**: GUID conflicts occur frequently during imports. The only solution to avoid losing references is to use the "Keep" button, which can be tedious.

- **Incomplete import cancellation**: The "Cancel Import" button doesn't seem to cancel all operations completely, leaving some partial state or files behind.

### Validation & Integrity

- **Empty model submission**: It's possible to submit "empty" models (models without valid assets). Asset integrity verification should be added to prevent this.

### User Interface & Clarity

- **Asset path display**: The asset path should be displayed in the preview window to help users understand where assets are located.

- **Absolute vs relative path confusion**: The distinction between absolute and relative paths is not clear to users. Better documentation or UI clarification is needed to explain when each path type is used and why.

### Model Submission Issues

- **Paths not always saved on submission**: When submitting a model, the relative and absolute paths (Assets > Relative/Absolute Path fields) are not always written/saved correctly. They sometimes remain at their default values instead of being updated with the user's input.

---

**Note**: This tool is Editor-only and does not include any runtime components. All functionality is available only within the Unity Editor.
