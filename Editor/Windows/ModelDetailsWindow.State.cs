using System;
using System.Collections.Generic;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing all field declarations for ModelDetailsWindow.
    /// </summary>
    public partial class ModelDetailsWindow
    {
        // Model Identification
        /// <summary>The unique identifier of the model being displayed.</summary>
        private string _modelId;
        /// <summary>The version of the model being displayed.</summary>
        private string _version;

        // Services
        /// <summary>Service instance for repository operations.</summary>
        private ModelLibraryService _service;

        // Metadata
        /// <summary>The loaded model metadata.</summary>
        private ModelMeta _meta;
        /// <summary>Original metadata JSON for change detection when saving.</summary>
        private string _baselineMetaJson;
        /// <summary>Edited description text (may differ from _meta.description).</summary>
        private string _editedDescription;

        // UI State
        /// <summary>Scroll position for the main content area.</summary>
        private Vector2 _scroll;
        /// <summary>Scroll position for the model structure section.</summary>
        private Vector2 _structureScroll;
        /// <summary>Whether the structure section is expanded.</summary>
        private bool _showStructure = true;
        /// <summary>Whether the notes section is expanded.</summary>
        private bool _showNotes = true;
        /// <summary>Whether the changelog section is expanded.</summary>
        private bool _showChangelog = true;

        // Editing State
        /// <summary>Whether tag editing mode is active.</summary>
        private bool _editingTags = false;
        /// <summary>Flag indicating if metadata is currently being saved.</summary>
        private bool _isSavingMetadata = false;
        /// <summary>Editable list of tags (used during tag editing).</summary>
        private List<string> _editableTags = new();
        /// <summary>Text field for adding new tags.</summary>
        private string _newTag = string.Empty;

        // Notes
        /// <summary>Text field for the new note message.</summary>
        private string _newNoteMessage = string.Empty;
        /// <summary>Selected tag for the new note.</summary>
        private string _newNoteTag = "remarks";
        /// <summary>Available note tags for categorization.</summary>
        private readonly string[] _noteTags = { "bugfix", "improvements", "remarks", "question", "praise" };

        // Version deletion state
        private readonly List<string> _availableVersions = new List<string>();
        private bool _loadingVersions;
        private bool _hasOlderVersions;
        private bool _isLatestVersion;
        private bool _deletingVersion;
        private bool _deletingModel;

        // Installation status
        private bool _isInstalled;
        private string _installedVersion;
        private bool _hasUpdate;
        private bool _checkingInstallStatus;
    }
}

