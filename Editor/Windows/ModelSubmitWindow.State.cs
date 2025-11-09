using System;
using System.Collections.Generic;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Services;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing all field declarations, enums, and constants for ModelSubmitWindow.
    /// </summary>
    public partial class ModelSubmitWindow
    {
        /// <summary>
        /// Submission mode indicating whether this is a new model or an update to an existing one.
        /// </summary>
        private enum SubmitMode
        {
            /// <summary>Creating a new model entry in the repository.</summary>
            New,
            /// <summary>Submitting a new version of an existing model.</summary>
            Update
        }

        /// <summary>
        /// Tab selection for the submission form.
        /// </summary>
        private enum FormTab
        {
            /// <summary>Basic information tab (name, version, description, tags).</summary>
            BasicInfo,
            /// <summary>Assets tab (install path, relative path).</summary>
            Assets,
            /// <summary>Images tab (preview images).</summary>
            Images,
            /// <summary>Advanced tab (changelog).</summary>
            Advanced
        }

        // UI Constants
        /// <summary>Width for small buttons (e.g., remove buttons).</summary>
        private const int __BUTTON_WIDTH_SMALL = 24;
        /// <summary>Width for medium-sized buttons.</summary>
        private const int __BUTTON_WIDTH_MEDIUM = 50;
        /// <summary>Minimum height for the description text area.</summary>
        private const int __TEXT_AREA_HEIGHT_DESCRIPTION = 60;
        /// <summary>Minimum height for the changelog text area.</summary>
        private const int __TEXT_AREA_HEIGHT_CHANGELOG = 40;
        /// <summary>Control name for the changelog text area to maintain focus.</summary>
        private const string __CHANGELOG_CONTROL_NAME = "ModelSubmitWindow_Changelog";
        /// <summary>EditorPrefs key for draft data.</summary>
        private const string __DRAFT_PREF_KEY = "ModelLibrary.SubmitDraft";
        /// <summary>Duration to show notification (3 seconds).</summary>
        private static readonly TimeSpan _notificationDuration = TimeSpan.FromSeconds(3);

        // File Size Constants
        /// <summary>Maximum allowed image file size (50MB).</summary>
        private const long __MAX_IMAGE_FILE_SIZE_BYTES = 50 * 1024 * 1024;
        /// <summary>Number of bytes per kilobyte for file size calculations.</summary>
        private const long __BYTES_PER_KILOBYTE = 1024;

        // Submission State
        /// <summary>Current submission mode (New or Update).</summary>
        private SubmitMode _mode = SubmitMode.New;
        /// <summary>Currently selected form tab.</summary>
        private FormTab _selectedTab = FormTab.BasicInfo;
        /// <summary>Service instance for repository operations.</summary>
        private ModelLibraryService _service;
        /// <summary>List of existing models loaded from the index (for update mode).</summary>
        private readonly List<ModelIndex.Entry> _existingModels = new();
        /// <summary>Index of the selected model in update mode.</summary>
        private int _selectedModelIndex;
        /// <summary>Flag indicating if the index is currently being loaded.</summary>
        private bool _isLoadingIndex;
        /// <summary>Flag indicating if base metadata is being loaded for the selected model.</summary>
        private bool _loadingBaseMeta;
        /// <summary>Flag indicating if a submission is currently in progress.</summary>
        private bool _isSubmitting;
        /// <summary>Flag indicating if submission should be cancelled.</summary>
        private bool _cancelSubmission = false;
        /// <summary>Changelog summary for the submission (required for updates).</summary>
        private string _changeSummary = "Initial submission";
        /// <summary>Cached metadata of the latest version of the selected model (update mode).</summary>
        private ModelMeta _latestSelectedMeta;

        // Form Fields
        /// <summary>Model name (required).</summary>
        private string _name = "New Model";
        /// <summary>Model version in SemVer format (required).</summary>
        private string _version = "1.0.0";
        /// <summary>Model description (optional).</summary>
        private string _description = string.Empty;
        /// <summary>Absolute install path in the Unity project (e.g., "Assets/Models/ModelName").</summary>
        private string _installPath;
        /// <summary>Relative path from Assets folder (e.g., "Models/ModelName").</summary>
        private string _relativePath;
        /// <summary>List of absolute paths to preview images.</summary>
        private List<string> _imageAbsPaths = new();
        /// <summary>List of tags for categorizing the model.</summary>
        private List<string> _tags = new();
        /// <summary>Text field for adding new tags.</summary>
        private string _newTag = string.Empty;
        /// <summary>Scroll position for the images list.</summary>
        private Vector2 _scrollPosition = Vector2.zero;
        /// <summary>Whether the advanced tag options section is expanded.</summary>
        private bool _showAdvancedTagOptions = false;
        /// <summary>Whether the advanced path options section is expanded.</summary>
        private bool _showAdvancedPathOptions = false;
        /// <summary>User identity provider for getting author information.</summary>
        private readonly IUserIdentityProvider _idProvider = new SimpleUserIdentityProvider();
        /// <summary>Notification message to display temporarily.</summary>
        private string _notificationMessage = null;
        /// <summary>Timestamp when notification was shown.</summary>
        private DateTime _notificationTime = DateTime.MinValue;
    }
}


