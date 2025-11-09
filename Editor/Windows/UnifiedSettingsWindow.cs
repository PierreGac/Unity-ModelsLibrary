using System;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Unified settings window combining User Settings and Repository Settings.
    /// Provides a single interface to configure all Model Library settings.
    /// </summary>
    public class UnifiedSettingsWindow : EditorWindow
    {
        /// <summary>
        /// Tab selection for the settings window.
        /// </summary>
        private enum SettingsTab
        {
            /// <summary>User settings tab (name, role).</summary>
            User,
            /// <summary>Repository settings tab (repository type, URL/path, cache).</summary>
            Repository
        }

        // User Settings
        /// <summary>User identity provider for reading/writing user settings.</summary>
        private readonly SimpleUserIdentityProvider _id = new();
        /// <summary>Current user name value from the form.</summary>
        private string _userName;
        /// <summary>Current user role value from the form.</summary>
        private UserRole _userRole;

        // Repository Settings
        /// <summary>Current repository settings instance.</summary>
        private ModelLibrarySettings _settings;
        /// <summary>Repository kind (FileSystem or Http).</summary>
        private ModelLibrarySettings.RepositoryKind _repositoryKind;
        /// <summary>Repository root path or URL.</summary>
        private string _repositoryRoot;
        /// <summary>Local cache root path.</summary>
        private string _localCacheRoot;
        /// <summary>Whether repository connection test is in progress.</summary>
        private bool _testingConnection = false;
        /// <summary>Last connection test result message.</summary>
        private string _connectionTestResult = null;
        /// <summary>Whether the last connection test was successful.</summary>
        private bool _connectionTestSuccess = false;
        /// <summary>Timestamp of last connection test.</summary>
        private DateTime _lastConnectionTest = DateTime.MinValue;
        /// <summary>Number of models found in last successful connection test.</summary>
        private int _lastModelCount = 0;

        // UI State
        /// <summary>Currently selected settings tab.</summary>
        private SettingsTab _selectedTab = SettingsTab.User;
        /// <summary>Whether changes have been made that need saving.</summary>
        private bool _hasUnsavedChanges = false;

        /// <summary>
        /// Opens the unified settings window.
        /// </summary>
        public static void Open()
        {
            UnifiedSettingsWindow w = GetWindow<UnifiedSettingsWindow>("Model Library Settings");
            w.minSize = new Vector2(500, 400);
            w.Show();
        }

        private void OnEnable() => LoadSettings();

        /// <summary>
        /// Loads current settings from storage.
        /// </summary>
        private void LoadSettings()
        {
            // Load user settings
            _userName = _id.GetUserName();
            _userRole = _id.GetUserRole();

            // Load repository settings
            _settings = ModelLibrarySettings.GetOrCreate();
            _repositoryKind = _settings.repositoryKind;
            _repositoryRoot = _settings.repositoryRoot;
            _localCacheRoot = _settings.localCacheRoot;

            _hasUnsavedChanges = false;
        }

        private void OnGUI()
        {
            // Repaint during connection test to show animated loading indicator
            if (_testingConnection)
            {
                Repaint();
            }

            EditorGUILayout.Space(5);

            // Tab selection
            string[] tabLabels = { "User Settings", "Repository Settings" };
            int newTabIndex = GUILayout.Toolbar((int)_selectedTab, tabLabels);
            if (newTabIndex != (int)_selectedTab)
            {
                _selectedTab = (SettingsTab)newTabIndex;
            }

            EditorGUILayout.Space(10);

            // Tab content
            switch (_selectedTab)
            {
                case SettingsTab.User:
                    DrawUserSettingsTab();
                    break;
                case SettingsTab.Repository:
                    DrawRepositorySettingsTab();
                    break;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Save/Cancel buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(25)))
                {
                    LoadSettings(); // Reload to discard changes
                    if (!_hasUnsavedChanges)
                    {
                        Close();
                    }
                }

                using (new EditorGUI.DisabledScope(!_hasUnsavedChanges))
                {
                    if (GUILayout.Button("Save", GUILayout.Width(100), GUILayout.Height(25)))
                    {
                        SaveSettings();
                    }
                }
            }
        }

        /// <summary>
        /// Draws the User Settings tab content.
        /// </summary>
        private void DrawUserSettingsTab()
        {
            EditorGUILayout.LabelField("User Information", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            string newUserName = EditorGUILayout.TextField("User Name", _userName);
            if (newUserName != _userName)
            {
                _userName = newUserName;
                _hasUnsavedChanges = true;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("User Role", EditorStyles.boldLabel);
            string[] roleOptions = Enum.GetNames(typeof(UserRole));
            int currentRoleIndex = Array.IndexOf(roleOptions, _userRole.ToString());
            int selectedRoleIndex = EditorGUILayout.Popup("Role", currentRoleIndex, roleOptions);
            if (selectedRoleIndex >= 0 && selectedRoleIndex < roleOptions.Length)
            {
                UserRole newRole = (UserRole)Enum.Parse(typeof(UserRole), roleOptions[selectedRoleIndex]);
                if (newRole != _userRole)
                {
                    _userRole = newRole;
                    _hasUnsavedChanges = true;
                }
            }

            EditorGUILayout.Space(5);
            string roleDescription = _userRole switch
            {
                UserRole.Artist => "Artist role: Can submit models, manage versions, and browse the library.",
                UserRole.Admin => "Admin role: Full access including analytics, version deletion, and system management.",
                _ => "Developer role: Can browse, import models, and leave feedback notes."
            };
            EditorGUILayout.HelpBox(roleDescription, MessageType.Info);
        }

        /// <summary>
        /// Draws the Repository Settings tab content.
        /// </summary>
        private void DrawRepositorySettingsTab()
        {
            EditorGUILayout.LabelField("Repository Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Repository Kind
            ModelLibrarySettings.RepositoryKind newKind = (ModelLibrarySettings.RepositoryKind)EditorGUILayout.EnumPopup("Repository Type", _repositoryKind);
            if (newKind != _repositoryKind)
            {
                _repositoryKind = newKind;
                _hasUnsavedChanges = true;
            }

            EditorGUILayout.Space(5);

            // Repository Root
            EditorGUILayout.LabelField("Repository Root", EditorStyles.boldLabel);
            string tooltip = _repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? "Absolute path or UNC path to the repository (e.g., C:\\Models or \\\\server\\Models)"
                : "Base URL for the HTTP repository (e.g., https://api.example.com/models)";
            EditorGUILayout.HelpBox(tooltip, MessageType.None);

            string newRepoRoot = EditorGUILayout.TextField("Path/URL", _repositoryRoot);
            if (newRepoRoot != _repositoryRoot)
            {
                _repositoryRoot = newRepoRoot;
                _hasUnsavedChanges = true;
                _connectionTestResult = null; // Clear test result when path changes
            }

            // Validation
            if (string.IsNullOrWhiteSpace(_repositoryRoot))
            {
                EditorGUILayout.HelpBox("Repository root is required.", MessageType.Warning);
            }
            else if (_repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (!System.IO.Path.IsPathRooted(_repositoryRoot) && !_repositoryRoot.StartsWith("\\\\"))
                {
                    EditorGUILayout.HelpBox("File system path should be absolute (e.g., C:\\Models or \\\\server\\share)", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            // Connection Status and Test
            EditorGUILayout.LabelField("Connection Status", EditorStyles.boldLabel);
            
            // Status indicator
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                // Status icon/indicator
                if (_testingConnection)
                {
                    // Animated loading indicator
                    float time = (float)EditorApplication.timeSinceStartup;
                    int frame = Mathf.FloorToInt(time * 2f) % 4;
                    string[] loadingFrames = { "◐", "◓", "◑", "◒" };
                    GUILayout.Label(loadingFrames[frame], GUILayout.Width(20));
                    GUILayout.Label("Testing connection...", EditorStyles.miniLabel);
                }
                else if (_lastConnectionTest != DateTime.MinValue)
                {
                    // Show status with icon
                    string statusIcon = _connectionTestSuccess ? "✓" : "✗";
                    Color statusColor = _connectionTestSuccess ? Color.green : Color.red;
                    
                    GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                    statusStyle.normal.textColor = statusColor;
                    statusStyle.fontStyle = FontStyle.Bold;
                    
                    GUILayout.Label(statusIcon, statusStyle, GUILayout.Width(20));
                    
                    if (_connectionTestSuccess)
                    {
                        string statusText = $"Connected ({_lastModelCount} models)";
                        if ((DateTime.Now - _lastConnectionTest).TotalMinutes < 1)
                        {
                            statusText += " - Just now";
                        }
                        else
                        {
                            int minutesAgo = (int)(DateTime.Now - _lastConnectionTest).TotalMinutes;
                            statusText += $" - {minutesAgo} min ago";
                        }
                        GUILayout.Label(statusText, statusStyle);
                    }
                    else
                    {
                        GUILayout.Label(_connectionTestResult ?? "Connection failed", statusStyle);
                    }
                }
                else
                {
                    GUILayout.Label("⚠", EditorStyles.miniLabel, GUILayout.Width(20));
                    GUILayout.Label("Not tested", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                // Test button
                using (new EditorGUI.DisabledScope(_testingConnection || string.IsNullOrWhiteSpace(_repositoryRoot)))
                {
                    if (GUILayout.Button("Test Connection", GUILayout.Width(120), GUILayout.Height(25)))
                    {
                        _ = TestConnectionAsync();
                    }
                }
            }
            
            // Show detailed result if available
            if (!string.IsNullOrEmpty(_connectionTestResult) && !_testingConnection)
            {
                GUIStyle resultStyle = new GUIStyle(EditorStyles.helpBox);
                resultStyle.normal.textColor = _connectionTestSuccess ? new Color(0f, 0.6f, 0f) : new Color(0.8f, 0f, 0f);
                EditorGUILayout.LabelField(_connectionTestResult, resultStyle);
            }

            EditorGUILayout.Space(10);

            // Local Cache
            EditorGUILayout.LabelField("Local Cache", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Local cache directory for downloaded models. Relative to project root or absolute path.", MessageType.None);

            string newCacheRoot = EditorGUILayout.TextField("Cache Path", _localCacheRoot);
            if (newCacheRoot != _localCacheRoot)
            {
                _localCacheRoot = newCacheRoot;
                _hasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// Tests the repository connection asynchronously.
        /// </summary>
        private async System.Threading.Tasks.Task TestConnectionAsync()
        {
            _testingConnection = true;
            _connectionTestResult = null;
            Repaint();

            try
            {
                IModelRepository repo = _repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new Repository.FileSystemRepository(_repositoryRoot)
                    : new Repository.HttpRepository(_repositoryRoot);

                // Try to load the index as a connection test
                ModelIndex index = await repo.LoadIndexAsync();
                
                if (index != null && index.entries != null)
                {
                    _connectionTestSuccess = true;
                    _lastModelCount = index.entries.Count;
                    _lastConnectionTest = DateTime.Now;
                    _connectionTestResult = $"Connected successfully! Found {index.entries.Count} model{(index.entries.Count == 1 ? "" : "s")} in the repository.";
                }
                else
                {
                    _connectionTestSuccess = false;
                    _lastModelCount = 0;
                    _lastConnectionTest = DateTime.Now;
                    _connectionTestResult = "Connection failed: No index found. Please verify the repository path/URL is correct.";
                }
            }
            catch (Exception ex)
            {
                _connectionTestSuccess = false;
                _lastModelCount = 0;
                _lastConnectionTest = DateTime.Now;
                _connectionTestResult = $"Connection failed: {ex.Message}\n\nPlease check:\n• Repository path/URL is correct\n• Network connection is available\n• Repository server is accessible";
            }
            finally
            {
                _testingConnection = false;
                Repaint();
            }
        }

        /// <summary>
        /// Saves all settings to storage.
        /// </summary>
        private void SaveSettings()
        {
            // Save user settings
            _id.SetUserName(_userName);
            _id.SetUserRole(_userRole);

            // Save repository settings
            _settings.repositoryKind = _repositoryKind;
            _settings.repositoryRoot = _repositoryRoot;
            _settings.localCacheRoot = _localCacheRoot;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();

            _hasUnsavedChanges = false;

            EditorUtility.DisplayDialog("Settings Saved", "All settings have been saved successfully.", "OK");
            
            // Refresh any open Model Library windows
            ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
            foreach (ModelLibraryWindow window in windows)
            {
                window.Repaint();
            }
        }
    }
}

