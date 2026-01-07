using System;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing UnifiedSettingsWindow view implementation for ModelLibraryWindow.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Initializes settings state when navigating to the Settings view.
        /// </summary>
        public void InitializeSettingsState()
        {
            if (_settingsIdentityProvider == null)
            {
                _settingsIdentityProvider = new SimpleUserIdentityProvider();
            }

            // Load user settings
            _settingsUserName = _settingsIdentityProvider.GetUserName();
            _settingsUserRole = _settingsIdentityProvider.GetUserRole();

            // Load repository settings
            _settingsInstance = ModelLibrarySettings.GetOrCreate();
            _settingsRepositoryKind = _settingsInstance.repositoryKind;
            _settingsRepositoryRoot = _settingsInstance.repositoryRoot;
            _settingsLocalCacheRoot = _settingsInstance.localCacheRoot;

            _settingsHasUnsavedChanges = false;
        }

        /// <summary>
        /// Draws the Settings view.
        /// </summary>
        private void DrawSettingsView()
        {
            // Repaint during connection test to show animated loading indicator
            if (_settingsTestingConnection)
            {
                Repaint();
            }

            EditorGUILayout.Space(5);

            // Tab selection
            string[] tabLabels = { "User Settings", "Repository Settings" };
            int newTabIndex = GUILayout.Toolbar((int)_settingsSelectedTab, tabLabels);
            if (newTabIndex != (int)_settingsSelectedTab)
            {
                _settingsSelectedTab = (UnifiedSettingsWindow.SettingsTab)newTabIndex;
            }

            EditorGUILayout.Space(10);

            // Tab content
            switch (_settingsSelectedTab)
            {
                case UnifiedSettingsWindow.SettingsTab.User:
                    DrawSettingsUserTab();
                    break;
                case UnifiedSettingsWindow.SettingsTab.Repository:
                    DrawSettingsRepositoryTab();
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
                    InitializeSettingsState(); // Reload to discard changes
                }

                using (new EditorGUI.DisabledScope(!_settingsHasUnsavedChanges))
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
        private void DrawSettingsUserTab()
        {
            EditorGUILayout.LabelField("User Information", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            string newUserName = EditorGUILayout.TextField("User Name", _settingsUserName);
            if (newUserName != _settingsUserName)
            {
                _settingsUserName = newUserName;
                _settingsHasUnsavedChanges = true;
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("User Role", EditorStyles.boldLabel);
            string[] roleOptions = Enum.GetNames(typeof(UserRole));
            int currentRoleIndex = Array.IndexOf(roleOptions, _settingsUserRole.ToString());
            int selectedRoleIndex = EditorGUILayout.Popup("Role", currentRoleIndex, roleOptions);
            if (selectedRoleIndex >= 0 && selectedRoleIndex < roleOptions.Length)
            {
                UserRole newRole = (UserRole)Enum.Parse(typeof(UserRole), roleOptions[selectedRoleIndex]);
                if (newRole != _settingsUserRole)
                {
                    _settingsUserRole = newRole;
                    _settingsHasUnsavedChanges = true;
                }
            }

            EditorGUILayout.Space(5);
            string roleDescription = _settingsUserRole switch
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
        private void DrawSettingsRepositoryTab()
        {
            EditorGUILayout.LabelField("Repository Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Repository Kind
            ModelLibrarySettings.RepositoryKind newKind = (ModelLibrarySettings.RepositoryKind)EditorGUILayout.EnumPopup("Repository Type", _settingsRepositoryKind);
            if (newKind != _settingsRepositoryKind)
            {
                _settingsRepositoryKind = newKind;
                _settingsHasUnsavedChanges = true;
            }

            EditorGUILayout.Space(5);

            // Repository Root
            EditorGUILayout.LabelField("Repository Root", EditorStyles.boldLabel);
            string tooltip = _settingsRepositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                ? "Absolute path or UNC path to the repository (e.g., C:\\Models or \\\\server\\Models)"
                : "Base URL for the HTTP repository (e.g., https://api.example.com/models)";
            EditorGUILayout.HelpBox(tooltip, MessageType.None);

            string newRepoRoot = EditorGUILayout.TextField("Path/URL", _settingsRepositoryRoot);
            if (newRepoRoot != _settingsRepositoryRoot)
            {
                _settingsRepositoryRoot = newRepoRoot;
                _settingsHasUnsavedChanges = true;
                _settingsConnectionTestResult = null; // Clear test result when path changes
            }

            // Validation
            if (string.IsNullOrWhiteSpace(_settingsRepositoryRoot))
            {
                EditorGUILayout.HelpBox("Repository root is required.", MessageType.Warning);
            }
            else if (_settingsRepositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (!System.IO.Path.IsPathRooted(_settingsRepositoryRoot) && !_settingsRepositoryRoot.StartsWith("\\\\"))
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
                if (_settingsTestingConnection)
                {
                    // Animated loading indicator
                    float time = (float)EditorApplication.timeSinceStartup;
                    int frame = Mathf.FloorToInt(time * 2f) % 4;
                    string[] loadingFrames = { "◐", "◓", "◑", "◒" };
                    GUILayout.Label(loadingFrames[frame], GUILayout.Width(20));
                    GUILayout.Label("Testing connection...", EditorStyles.miniLabel);
                }
                else if (_settingsLastConnectionTest != DateTime.MinValue)
                {
                    // Show status with icon
                    string statusIcon = _settingsConnectionTestSuccess ? "✓" : "✗";
                    Color statusColor = _settingsConnectionTestSuccess ? Color.green : Color.red;
                    
                    GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                    statusStyle.normal.textColor = statusColor;
                    statusStyle.fontStyle = FontStyle.Bold;
                    
                    GUILayout.Label(statusIcon, statusStyle, GUILayout.Width(20));
                    
                    if (_settingsConnectionTestSuccess)
                    {
                        string statusText = $"Connected ({_settingsLastModelCount} models)";
                        if ((DateTime.Now - _settingsLastConnectionTest).TotalMinutes < 1)
                        {
                            statusText += " - Just now";
                        }
                        else
                        {
                            int minutesAgo = (int)(DateTime.Now - _settingsLastConnectionTest).TotalMinutes;
                            statusText += $" - {minutesAgo} min ago";
                        }
                        GUILayout.Label(statusText, statusStyle);
                    }
                    else
                    {
                        GUILayout.Label(_settingsConnectionTestResult ?? "Connection failed", statusStyle);
                    }
                }
                else
                {
                    GUILayout.Label("⚠", EditorStyles.miniLabel, GUILayout.Width(20));
                    GUILayout.Label("Not tested", EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                // Test button
                using (new EditorGUI.DisabledScope(_settingsTestingConnection || string.IsNullOrWhiteSpace(_settingsRepositoryRoot)))
                {
                    if (GUILayout.Button("Test Connection", GUILayout.Width(120), GUILayout.Height(25)))
                    {
                        _ = TestSettingsConnectionAsync();
                    }
                }
            }
            
            // Show detailed result if available
            if (!string.IsNullOrEmpty(_settingsConnectionTestResult) && !_settingsTestingConnection)
            {
                GUIStyle resultStyle = new GUIStyle(EditorStyles.helpBox);
                resultStyle.normal.textColor = _settingsConnectionTestSuccess ? new Color(0f, 0.6f, 0f) : new Color(0.8f, 0f, 0f);
                EditorGUILayout.LabelField(_settingsConnectionTestResult, resultStyle);
            }

            EditorGUILayout.Space(10);

            // Local Cache
            EditorGUILayout.LabelField("Local Cache", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Local cache directory for downloaded models. Relative to project root or absolute path.", MessageType.None);

            string newCacheRoot = EditorGUILayout.TextField("Cache Path", _settingsLocalCacheRoot);
            if (newCacheRoot != _settingsLocalCacheRoot)
            {
                _settingsLocalCacheRoot = newCacheRoot;
                _settingsHasUnsavedChanges = true;
            }
        }

        /// <summary>
        /// Tests the repository connection asynchronously.
        /// Attempts to load the model index to verify connectivity and accessibility.
        /// Updates connection status UI with results including model count and timestamp.
        /// </summary>
        private async Task TestSettingsConnectionAsync()
        {
            _settingsTestingConnection = true;
            _settingsConnectionTestResult = null;
            Repaint();

            try
            {
                // Create repository instance based on selected kind
                IModelRepository repo = _settingsRepositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new FileSystemRepository(_settingsRepositoryRoot)
                    : new HttpRepository(_settingsRepositoryRoot);

                // Try to load the index as a connection test
                // This verifies both connectivity and that the repository structure is correct
                ModelIndex index = await repo.LoadIndexAsync();
                
                if (index != null && index.entries != null)
                {
                    _settingsConnectionTestSuccess = true;
                    _settingsLastModelCount = index.entries.Count;
                    _settingsLastConnectionTest = DateTime.Now;
                    _settingsConnectionTestResult = $"Connected successfully! Found {index.entries.Count} model{(index.entries.Count == 1 ? "" : "s")} in the repository.";
                }
                else
                {
                    _settingsConnectionTestSuccess = false;
                    _settingsLastModelCount = 0;
                    _settingsLastConnectionTest = DateTime.Now;
                    _settingsConnectionTestResult = "Connection failed: No index found. Please verify the repository path/URL is correct.";
                }
            }
            catch (Exception ex)
            {
                // Handle connection errors with detailed feedback
                _settingsConnectionTestSuccess = false;
                _settingsLastModelCount = 0;
                _settingsLastConnectionTest = DateTime.Now;
                _settingsConnectionTestResult = $"Connection failed: {ex.Message}\n\nPlease check:\n• Repository path/URL is correct\n• Network connection is available\n• Repository server is accessible";
            }
            finally
            {
                _settingsTestingConnection = false;
                Repaint();
            }
        }

        /// <summary>
        /// Saves all settings to storage.
        /// Persists both user settings (name, role) and repository settings (kind, root, cache).
        /// Marks the settings asset as dirty and saves to disk.
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                // Save user settings
                _settingsIdentityProvider.SetUserName(_settingsUserName);
                _settingsIdentityProvider.SetUserRole(_settingsUserRole);

                // Save repository settings
                _settingsInstance.repositoryKind = _settingsRepositoryKind;
                _settingsInstance.repositoryRoot = _settingsRepositoryRoot;
                _settingsInstance.localCacheRoot = _settingsLocalCacheRoot;
                EditorUtility.SetDirty(_settingsInstance);
                AssetDatabase.SaveAssets();

                _settingsHasUnsavedChanges = false;

                EditorUtility.DisplayDialog("Settings Saved", "All settings have been saved successfully.", "OK");
                
                // Refresh this window to reflect saved state
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Save Error", $"Failed to save settings: {ex.Message}", "OK");
                UnityEngine.Debug.LogError($"[ModelLibraryWindow] Failed to save settings: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

