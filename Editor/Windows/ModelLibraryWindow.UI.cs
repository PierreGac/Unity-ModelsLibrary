using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    public partial class ModelLibraryWindow
    {
        private bool _isExiting = false;
        private const float __TOOLBAR_SEARCH_MIN_WIDTH = 200f;
        private const float __TOOLBAR_HELP_BUTTON_WIDTH = 24f;
        private const float __TOOLBAR_HISTORY_BUTTON_WIDTH = 20f;
        private const float __TOOLBAR_CLEAR_BUTTON_WIDTH = 60f;
        private const float __TOOLBAR_ACTIONS_BUTTON_WIDTH = 80f;
        private const float __TOOLBAR_UPDATE_BUTTON_WIDTH = 130f;
        private const float __TOOLBAR_UPDATE_BUTTON_HEIGHT = 20f;
        private const float __TOOLBAR_BULK_BUTTON_WIDTH = 70f;
        private const float __TOOLBAR_SETTINGS_BUTTON_WIDTH = 80f;
        private const float __TOOLBAR_HELP_ICON_WIDTH = 28f;
        private const float __TOOLBAR_HELP_TEXT_WIDTH = 50f;
        private const float __TOOLBAR_ROLE_BUTTON_WIDTH = 90f;
        private const float __TOOLBAR_SORT_LABEL_WIDTH = 35f;
        private const float __TOOLBAR_SORT_POPUP_WIDTH = 80f;
        private const float __TOOLBAR_VIEWMODE_WIDTH = 105f;

        private const float __TAG_FILTER_PRESET_WIDTH = 80f;
        private const float __TAG_FILTER_SAVE_WIDTH = 90f;
        private const float __TAG_FILTER_CLEAR_WIDTH = 90f;
        private const float __TAG_FILTER_SEARCH_LABEL_WIDTH = 80f;
        private const float __TAG_FILTER_SEARCH_CLEAR_WIDTH = 50f;
        private const float __TAG_FILTER_SCROLL_HEIGHT = 140f;

        private const float __LIST_BUTTON_STAR_WIDTH = 20f;
        private const float __LIST_BUTTON_DETAILS_WIDTH = 70f;
        private const float __LIST_BUTTON_DOWNLOAD_WIDTH = 90f;
        private const float __LIST_BUTTON_IMPORT_WIDTH = 80f;

        private const float __GRID_CARD_SPACING = UIConstants.SPACING_STANDARD;
        private const float __GRID_CARD_PADDING = UIConstants.PADDING_SMALL;
        private const float __GRID_CARD_EXTRA_HEIGHT = 80f;
        private const float __IMAGE_CARD_SPACING = UIConstants.SPACING_DEFAULT;

        private const float __LOADING_OVERLAY_ALPHA = 0.5f;
        private const float __LOADING_BOX_WIDTH = 300f;
        private const float __LOADING_BOX_HEIGHT = 100f;
        private const float __LOADING_TITLE_OFFSET_Y = 20f;
        private const float __LOADING_PROGRESS_OFFSET_Y = 50f;

        private const float __NOTIFICATION_HEIGHT = 30f;
        private const float __NOTIFICATION_CLOSE_WIDTH = 20f;
        private const float __NOTIFICATION_CLOSE_HEIGHT = 20f;
        private const float __NOTIFICATION_PADDING = 10f;
        private const float __NOTIFICATION_ALPHA = 0.9f;

        private const float __THUMBNAIL_PLACEHOLDER_BORDER_SIZE = 1f;
        private const float __THUMBNAIL_PLACEHOLDER_PADDING = 4f;

        private const float __THUMBNAIL_SLIDER_WIDTH = 100f;
        private const float __THUMBNAIL_SLIDER_HEIGHT = 18f;
        private const float __THUMBNAIL_SLIDER_MARGIN = 8f;
        private const float __THUMBNAIL_LABEL_WIDTH = 45f;
        private const float __THUMBNAIL_LABEL_OFFSET = 50f;

        private void OnGUI()
        {
            if(_isExiting)
            {
                return;
            }
            HandleKeyboardShortcuts();
            DrawNotification();

            // Draw loading overlay if refresh or loading is in progress (applies to all views)
            if (_refreshingManifest || _loadingIndex)
            {
                DrawLoadingOverlay();
                return; // Block all UI interaction during loading
            }

            // Draw Previous button if not on Browser view
            if (_currentView != ViewType.Browser)
            {
                DrawPreviousButton();
            }

            // Route to appropriate view based on current view type
            switch (_currentView)
            {
                case ViewType.Browser:
                    DrawBrowserView();
                    break;
                case ViewType.FirstRunWizard:
                    DrawFirstRunWizardView();
                    break;
                case ViewType.Submit:
                    DrawSubmitView();
                    break;
                case ViewType.ModelDetails:
                    DrawModelDetailsView();
                    break;
                case ViewType.Help:
                    DrawHelpView();
                    break;
                case ViewType.Shortcuts:
                    DrawShortcutsView();
                    break;
                case ViewType.Settings:
                    DrawSettingsView();
                    break;
                case ViewType.ErrorLog:
                    DrawErrorLogView();
                    break;
                case ViewType.PerformanceProfiler:
                    DrawPerformanceProfilerView();
                    break;
                case ViewType.Analytics:
                    DrawAnalyticsView();
                    break;
                case ViewType.BulkTag:
                    DrawBulkTagView();
                    break;
                case ViewType.BatchUpload:
                    DrawBatchUploadView();
                    break;
                case ViewType.VersionComparison:
                    DrawVersionComparisonView();
                    break;
                case ViewType.Preview3D:
                    DrawPreview3DView();
                    break;
                default:
                    DrawBrowserView();
                    break;
            }
        }

        /// <summary>
        /// Draws the main browser view with model listing, search, and filtering.
        /// </summary>
        private void DrawBrowserView()
        {
            if (!FirstRunWizard.IsConfigured())
            {
                DrawConfigurationRequired();
                return;
            }

            if (_service == null)
            {
                DrawServicesNotInitialized();
                return;
            }

            DrawToolbar();

            if (_indexCache == null)
            {
                ModelLibraryUIDrawer.DrawEmptyState("Loading model index...", "Please wait while we fetch the latest models from the repository.",
                    () =>
                    {
                        FullRefresh();
                    },
                    () => NavigateToView(ViewType.Submit));
                return;
            }

            ModelIndex index = _indexCache;
            DrawTagFilter(index);
            
            // Show authentication error if present
            if (!string.IsNullOrEmpty(_authenticationError))
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox(_authenticationError, MessageType.Error);
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Retry Connection", GUILayout.Height(30)))
                {
                    _authenticationError = null;
                    FullRefresh();
                }
                return;
            }
            
            if (index == null || index.entries == null)
            {
                ModelLibraryUIDrawer.DrawEmptyState("No models available",
                    "The repository appears to be empty.\n\nTo get started:\n• Submit your first model using 'Submit Model'\n• Check your repository configuration",
                    () =>
                    {
                        FullRefresh();
                    },
                    () => NavigateToView(ViewType.Submit));
                return;
            }

            IEnumerable<ModelIndex.Entry> query = index.entries;

            if (_filterMode == ModelLibraryUIDrawer.FilterMode.Favorites)
            {
                query = query.Where(entry => _favoritesManager.IsFavorite(entry.id));
            }
            else if (_filterMode == ModelLibraryUIDrawer.FilterMode.Recent)
            {
                query = query.Where(entry => _recentlyUsedManager.RecentlyUsed.Contains(entry.id));
            }

            string trimmedSearch = string.IsNullOrWhiteSpace(_search) ? null : _search.Trim();
            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                string lowerSearch = trimmedSearch.ToLowerInvariant();
                if (lowerSearch == "has:update" || lowerSearch == "has:updates")
                {
                    query = query.Where(entry => _modelUpdateStatus.TryGetValue(entry.id, out bool hasUpdate) && hasUpdate);
                }
                else if (lowerSearch == "has:notes" || lowerSearch == "has:note")
                {
                    query = query.Where(entry => HasNotes(entry.id, entry.latestVersion));
                }
                else
                {
                    query = query.Where(entry => ModelSearchUtils.EntryMatchesAdvancedSearch(entry, trimmedSearch));
                }
            }

            if (_selectedTags.Count > 0)
            {
                query = query.Where(EntryHasAllSelectedTags);
            }

            List<ModelIndex.Entry> filteredEntries = query.ToList();
            filteredEntries = ModelSortUtils.SortEntries(filteredEntries, _sortMode);
            UpdateKeyboardSelectionState(filteredEntries);

            _filterMode = ModelLibraryUIDrawer.DrawFilterModeTabs(_filterMode, _favoritesManager.Favorites.Count, _recentlyUsedManager.RecentlyUsed.Count, newMode =>
            {
                _filterMode = newMode;
                Repaint();
            });

            ModelLibraryUIDrawer.DrawFilterSummary(index.entries.Count, filteredEntries.Count, _search, _selectedTags, () =>
            {
                _search = string.Empty;
                _selectedTags.Clear();
                GUI.FocusControl(null);
            });

            // Show refresh and submit model buttons layout when showing all models
            if (_filterMode == ModelLibraryUIDrawer.FilterMode.All)
            {
                EditorGUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(_refreshingManifest || _loadingIndex))
                    {
                        string refreshLabel = _refreshingManifest ? "Refreshing..." : "Refresh";
                        if (GUILayout.Button(refreshLabel, GUILayout.Width(100), GUILayout.Height(30)))
                        {
                            FullRefresh();
                        }
                    }

                    SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                    if (identityProvider.GetUserRole() == UserRole.Artist)
                    {
                        if (GUILayout.Button("Submit Model", GUILayout.Width(120), GUILayout.Height(30)))
                        {
                            NavigateToView(ViewType.Submit);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.Space(5);
            }

            if (filteredEntries.Count == 0)
            {
                ModelLibraryUIDrawer.DrawEmptyState("No models match your filters",
                    $"Found {index.entries.Count} model(s) in repository, but none match your current search and filters.\n\nTry:\n• Clearing your search query\n• Removing some tag filters\n• Using different search terms",
                    () =>
                    {
                        FullRefresh();
                    },
                    () => NavigateToView(ViewType.Submit));
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_viewMode == ViewMode.Grid)
            {
                DrawGridView(filteredEntries);
            }
            else if (_viewMode == ViewMode.ImageOnly)
            {
                DrawImageOnlyView(filteredEntries);
            }
            else
            {
                DrawListView(filteredEntries);
            }
            EditorGUILayout.EndScrollView();

            // Draw thumbnail size slider in lower right corner (only for grid/image views)
            if (_viewMode == ViewMode.Grid || _viewMode == ViewMode.ImageOnly)
            {
                DrawThumbnailSizeSlider();
            }
        }








        /// <summary>
        /// Draws the BulkTag view. Shows bulk tag editor.
        /// </summary>
        private void DrawBulkTagView()
        {
            UIStyles.DrawPageHeader("Bulk Tag Editor", "Apply tag changes across selected models.");
            using (EditorGUILayout.VerticalScope cardScope = UIStyles.BeginCard())
            {
                EditorGUILayout.HelpBox("Bulk Tag view is being integrated. For now, please use the separate Bulk Tag window.", MessageType.Info);
                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
                if (GUILayout.Button("Open Bulk Tag Window", GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                {
                    // Bulk tag window requires service and entries - handled in Operations
                }
            }
        }



        public void CloseWindow()
        {
            _isExiting = true;
            Close();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.SetNextControlName("SearchField");
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true), GUILayout.MinWidth(__TOOLBAR_SEARCH_MIN_WIDTH));
                _search = newSearch;

                GUIContent searchHelpIcon = EditorGUIUtility.IconContent("_Help");
                if (searchHelpIcon == null || searchHelpIcon.image == null)
                {
                    searchHelpIcon = new GUIContent("?", "Search help and advanced operators");
                }
                else
                {
                    searchHelpIcon.tooltip = "Search help and advanced operators";
                }
                if (GUILayout.Button(searchHelpIcon, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_HELP_BUTTON_WIDTH)))
                {
                    ShowSearchHelpDialog();
                }

                bool hasHistory = _searchHistoryManager.History.Count > 0;
                GUIContent historyButtonContent = new GUIContent(hasHistory ? "▼" : "▼", hasHistory ? $"Search history available ({_searchHistoryManager.History.Count} items)" : "No search history");
                Color originalColor = GUI.color;
                if (hasHistory)
                {
                    GUI.color = UIConstants.COLOR_SEARCH_HISTORY_ACTIVE;
                }
                if (GUILayout.Button(historyButtonContent, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_HISTORY_BUTTON_WIDTH)))
                {
                    _searchHistoryManager.ShowSearchHistoryMenu(item =>
                    {
                        _search = item;
                        GUI.FocusControl(null);
                        Repaint();
                    }, () => { });
                }
                GUI.color = originalColor;

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    GUIContent clearSearchContent = new GUIContent("Clear", "Clear the current search text");
                    if (GUILayout.Button(clearSearchContent, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_CLEAR_BUTTON_WIDTH)))
                    {
                        _search = string.Empty;
                        GUI.FocusControl(null);
                    }
                }

                GUIContent actionsMenuContent = new GUIContent("Actions ▼", "Refresh repository data, check for updates, or submit new models");
                if (GUILayout.Button(actionsMenuContent, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_ACTIONS_BUTTON_WIDTH)))
                {
                    GenericMenu actionsMenu = new GenericMenu();
                    using (new EditorGUI.DisabledScope(_refreshingManifest || _loadingIndex))
                    {
                        string refreshLabel = _refreshingManifest ? "Refreshing..." : "Refresh";
                        actionsMenu.AddItem(new GUIContent(refreshLabel, "Reload the index and manifest cache"), false, () =>
                        {
                            FullRefresh();
                        });
                    }
                    actionsMenu.AddItem(new GUIContent("Check Updates", "Run an immediate update check for all models"), false, () => _ = CheckForUpdatesAsync());

                    SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                    if (identityProvider.GetUserRole() == UserRole.Artist)
                    {
                        actionsMenu.AddSeparator(string.Empty);
                        actionsMenu.AddItem(new GUIContent("Submit Model", "Open the submission form for new models or updates"), false, () => NavigateToView(ViewType.Submit));
                    }
                    actionsMenu.ShowAsContext();
                }

                if (_updateCount > 0)
                {
                    GUIStyle updateBadgeStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        normal = { textColor = Color.yellow },
                        fontStyle = FontStyle.Bold,
                        fontSize = 11
                    };

                    float pulseSpeed = 3f;
                    float pulseAlpha = 0.6f + (0.4f * Mathf.Sin((float)EditorApplication.timeSinceStartup * pulseSpeed));
                    Color savedColor = GUI.color;
                    GUI.color = new Color(1f, 0.8f, 0f, pulseAlpha);

                    string tooltipText = $"Updates available for {_updateCount} model{(_updateCount == 1 ? string.Empty : "s")}:\n";
                    tooltipText += "Click to filter and view models with updates.\n\n";
                    int shownCount = 0;
                    foreach (KeyValuePair<string, bool> kvp in _modelUpdateStatus)
                    {
                        if (kvp.Value && shownCount < 5)
                        {
                            ModelIndex.Entry entry = _indexCache?.entries?.FirstOrDefault(modelEntry => modelEntry.id == kvp.Key);
                            if (entry != null)
                            {
                                tooltipText += $"• {entry.name}\n";
                                shownCount++;
                            }
                        }
                    }
                    if (_updateCount > 5)
                    {
                        tooltipText += $"... and {_updateCount - 5} more";
                    }

                    GUIContent updateContent = new GUIContent($"🔄 Updates ({_updateCount})", tooltipText);
                    if (GUILayout.Button(updateContent, updateBadgeStyle, GUILayout.Width(__TOOLBAR_UPDATE_BUTTON_WIDTH), GUILayout.Height(__TOOLBAR_UPDATE_BUTTON_HEIGHT)))
                    {
                        _search = "has:update";
                        GUI.FocusControl(null);
                        Repaint();
                    }

                    GUI.color = savedColor;
                }

                GUIContent bulkMenuContent = new GUIContent("Bulk ▼", _bulkSelectionMode ? $"Selection mode active ({_selectedModels.Count} selected)" : "Enable selection mode and run batch import/update operations");
                Color originalBulkColor = GUI.color;
                if (_bulkSelectionMode)
                {
                    GUI.color = UIConstants.COLOR_BULK_SELECTION_ACTIVE;
                }
                if (GUILayout.Button(bulkMenuContent, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_BULK_BUTTON_WIDTH)))
                {
                    GenericMenu bulkMenu = new GenericMenu();
                    bulkMenu.AddItem(new GUIContent("Select Mode"), _bulkSelectionMode, () =>
                    {
                        _bulkSelectionMode = !_bulkSelectionMode;
                        if (!_bulkSelectionMode)
                        {
                            _selectedModels.Clear();
                        }
                        Repaint();
                    });
                    bulkMenu.AddSeparator(string.Empty);
                    using (new EditorGUI.DisabledScope(_selectedModels.Count == 0))
                    {
                        bulkMenu.AddItem(new GUIContent($"Import ({_selectedModels.Count})"), false, () => _ = BulkImportAsync());
                        bulkMenu.AddItem(new GUIContent($"Update ({_selectedModels.Count})"), false, () => _ = BulkUpdateAsync());
                        bulkMenu.AddSeparator("Tags/");
                        if (_selectedModels.Count == 0)
                        {
                            bulkMenu.AddDisabledItem(new GUIContent("Tags/Bulk Tag Editor"));
                        }
                        else
                        {
                            bulkMenu.AddItem(new GUIContent("Tags/Bulk Tag Editor..."), false, () => OpenBulkTagWindow());
                        }
                    }

                    // Add batch upload option for Artists
                    SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                    if (identityProvider.GetUserRole() == UserRole.Artist)
                    {
                        bulkMenu.AddSeparator(string.Empty);
                        bulkMenu.AddItem(new GUIContent("Batch Upload...", "Upload multiple models from a directory structure"), false, () =>
                        {
                            NavigateToView(ViewType.BatchUpload);
                            InitializeBatchUploadState();
                        });
                    }

                    bulkMenu.ShowAsContext();
                }
                GUI.color = originalBulkColor;
                SimpleUserIdentityProvider roleProvider = new SimpleUserIdentityProvider();
                UserRole currentRole = roleProvider.GetUserRole();

                GUIContent settingsMenuContent = new GUIContent("Settings ▼", "Open unified settings, error logs, or the configuration wizard");
                if (GUILayout.Button(settingsMenuContent, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_SETTINGS_BUTTON_WIDTH)))
                {
                    GenericMenu settingsMenu = new GenericMenu();
                    settingsMenu.AddItem(new GUIContent("Help / Documentation", "Open the Model Library help center"), false, () => NavigateToView(ViewType.Help));
                    settingsMenu.AddItem(new GUIContent("Keyboard Shortcuts", "View all keyboard shortcuts"), false, () => NavigateToView(ViewType.Shortcuts));
                    settingsMenu.AddSeparator(string.Empty);
                    settingsMenu.AddItem(new GUIContent("Settings", "Adjust user and repository preferences"), false, () => NavigateToView(ViewType.Settings));
                    settingsMenu.AddSeparator(string.Empty);
                    settingsMenu.AddItem(new GUIContent("Error Log Viewer", "Review recent errors and clear suppressions"), false, () => NavigateToView(ViewType.ErrorLog));
                    settingsMenu.AddItem(new GUIContent("Performance Profiler", "View async operation performance metrics"), false, () => NavigateToView(ViewType.PerformanceProfiler));

                    // Add analytics option for Admin and Artist roles
                    if (currentRole == UserRole.Admin || currentRole == UserRole.Artist)
                    {
                        settingsMenu.AddSeparator(string.Empty);
                        settingsMenu.AddItem(new GUIContent("Analytics", "View model usage statistics and reports"), false, () => NavigateToView(ViewType.Analytics));
                    }

                    settingsMenu.AddSeparator(string.Empty);
                    settingsMenu.AddItem(new GUIContent("Configuration Wizard", "Run the guided setup workflow"), false, () => NavigateToView(ViewType.FirstRunWizard));
                    settingsMenu.ShowAsContext();
                }

                GUIContent generalHelpContent = EditorGUIUtility.IconContent("_Help");
                if (generalHelpContent == null || generalHelpContent.image == null)
                {
                    generalHelpContent = new GUIContent("Help", "Open the Model Library help center");
                }
                else
                {
                    generalHelpContent.tooltip = "Open the Model Library help center";
                }
                float helpButtonWidth = generalHelpContent.image != null ? __TOOLBAR_HELP_ICON_WIDTH : __TOOLBAR_HELP_TEXT_WIDTH;
                if (GUILayout.Button(generalHelpContent, UIStyles.ToolbarButton, GUILayout.Width(helpButtonWidth)))
                {
                    NavigateToView(ViewType.Help);
                }

                GUILayout.FlexibleSpace();


                string roleLabel = currentRole.ToString();
                string roleTooltip = GetRoleTooltip(currentRole);

                GUIStyle roleStyle = new GUIStyle(EditorStyles.toolbarButton)
                {
                    normal = { textColor = GetRoleColor(currentRole) },
                    fontStyle = FontStyle.Bold
                };

                if (GUILayout.Button(new GUIContent($"👤 {roleLabel}", roleTooltip), roleStyle, GUILayout.Width(__TOOLBAR_ROLE_BUTTON_WIDTH)))
                {
                    NavigateToView(ViewType.Settings);
                }

                GUILayout.Label("Sort:", UIStyles.MutedLabel, GUILayout.Width(__TOOLBAR_SORT_LABEL_WIDTH));
                ModelSortMode newSortMode = (ModelSortMode)EditorGUILayout.EnumPopup(_sortMode, UIStyles.ToolbarPopup, GUILayout.Width(__TOOLBAR_SORT_POPUP_WIDTH));
                if (newSortMode != _sortMode)
                {
                    _sortMode = newSortMode;
                    EditorPrefs.SetInt(__SortModePrefKey, (int)_sortMode);
                    Repaint();
                }

                GUIContent listIcon = EditorGUIUtility.IconContent("UnityEditor.SceneHierarchyWindow");
                if (listIcon == null || listIcon.image == null)
                {
                    listIcon = new GUIContent("List", "List View (V)");
                }
                else
                {
                    listIcon.tooltip = "List View (V)";
                }

                GUIContent gridIcon = EditorGUIUtility.IconContent("Grid.Default");
                if (gridIcon == null || gridIcon.image == null)
                {
                    gridIcon = new GUIContent("Grid", "Grid View (V)");
                }
                else
                {
                    gridIcon.tooltip = "Grid View (V)";
                }

                GUIContent imageIcon = EditorGUIUtility.IconContent("d_ViewToolOrbit");
                if (imageIcon == null || imageIcon.image == null)
                {
                    imageIcon = new GUIContent("🖼️", "Image-Only View (V)");
                }
                else
                {
                    imageIcon.tooltip = "Image-Only View (V)";
                }

                GUIContent[] viewModeIcons = { listIcon, gridIcon, imageIcon };
                int newViewMode = GUILayout.Toolbar((int)_viewMode, viewModeIcons, UIStyles.ToolbarButton, GUILayout.Width(__TOOLBAR_VIEWMODE_WIDTH));
                if (newViewMode != (int)_viewMode)
                {
                    _viewMode = (ViewMode)newViewMode;
                    Repaint();
                }
            }
        }

        /// <summary>
        /// Draws a full-screen loading overlay that blocks all UI interaction during refresh operations.
        /// </summary>
        private void DrawLoadingOverlay()
        {
            Rect overlayRect = new Rect(0, 0, position.width, position.height);
            
            // Draw semi-transparent background
            Color originalColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, __LOADING_OVERLAY_ALPHA);
            GUI.DrawTexture(overlayRect, Texture2D.whiteTexture);
            GUI.color = originalColor;

            // Draw loading message box in center
            Rect boxRect = new Rect(
                (position.width - __LOADING_BOX_WIDTH) * 0.5f,
                (position.height - __LOADING_BOX_HEIGHT) * 0.5f,
                __LOADING_BOX_WIDTH,
                __LOADING_BOX_HEIGHT
            );

            GUI.Box(boxRect, string.Empty, UIStyles.CardBox);

            // Draw loading text
            GUIStyle labelStyle = new GUIStyle(UIStyles.SectionHeader)
            {
                alignment = TextAnchor.MiddleCenter
            };

            string loadingText = _refreshingManifest 
                ? "Refreshing model library..." 
                : "Loading model index...";

            Rect labelRect = new Rect(boxRect.x, boxRect.y + __LOADING_TITLE_OFFSET_Y, __LOADING_BOX_WIDTH, 30f);
            GUI.Label(labelRect, loadingText, labelStyle);

            // Draw progress indicator (animated dots)
            string progressDots = "";
            int dotCount = (int)(EditorApplication.timeSinceStartup * 2) % 4;
            for (int i = 0; i < dotCount; i++)
            {
                progressDots += ".";
            }

            Rect progressRect = new Rect(boxRect.x, boxRect.y + __LOADING_PROGRESS_OFFSET_Y, __LOADING_BOX_WIDTH, 20f);
            GUIStyle progressStyle = new GUIStyle(UIStyles.MutedLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(progressRect, progressDots, progressStyle);

            // Consume all events to block interaction
            if (Event.current.type == EventType.MouseDown || 
                Event.current.type == EventType.MouseUp ||
                Event.current.type == EventType.KeyDown ||
                Event.current.type == EventType.ScrollWheel)
            {
                Event.current.Use();
            }
        }

        private void DrawNotification()
        {
            if (string.IsNullOrEmpty(_notificationMessage))
            {
                return;
            }

            if ((DateTime.Now - _notificationTime) > NOTIFICATION_DURATION)
            {
                _notificationMessage = null;
                return;
            }

            Rect notificationRect = new Rect(0, 0, position.width, __NOTIFICATION_HEIGHT);
            GUI.Box(notificationRect, string.Empty, UIStyles.CardBox);

            Color originalColor = GUI.color;
            GUI.color = new Color(0.3f, 0.8f, 0.3f, __NOTIFICATION_ALPHA);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(__NOTIFICATION_PADDING);
                EditorGUILayout.LabelField(_notificationMessage, UIStyles.SectionHeader);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("×", GUILayout.Width(__NOTIFICATION_CLOSE_WIDTH), GUILayout.Height(__NOTIFICATION_CLOSE_HEIGHT)))
                {
                    _notificationMessage = null;
                }
                GUILayout.Space(__NOTIFICATION_PADDING);
            }

            GUI.color = originalColor;
        }

        /// <summary>
        /// Shows a notification message within the window for a short duration.
        /// </summary>
        private void ShowNotification(string title, string message)
        {
            _notificationMessage = string.Concat(title, ": ", message);
            _notificationTime = DateTime.Now;
            Repaint();
        }

        private void DrawTagFilter(ModelIndex index)
        {
            using (new EditorGUILayout.VerticalScope(UIStyles.CardBox))
            {
                _showTagFilter = EditorGUILayout.Foldout(_showTagFilter, "Filter by Tags", true);
                if (!_showTagFilter)
                {
                    return;
                }

                if (index == null)
                {
                    EditorGUILayout.LabelField("Loading tags...", UIStyles.MutedLabel);
                    return;
                }

                if (!ReferenceEquals(index, _tagSource))
                {
                    _tagCacheManager.UpdateTagCache(index);
                    _tagSource = index;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Presets ▼", UIStyles.ToolbarButton, GUILayout.Width(__TAG_FILTER_PRESET_WIDTH)))
                    {
                        _filterPresetManager.ShowFilterPresetsMenu((searchQuery, tags) =>
                        {
                            _search = searchQuery;
                            _selectedTags.Clear();
                            foreach (string tag in tags)
                            {
                                _selectedTags.Add(tag);
                            }
                            GUI.FocusControl(null);
                            Repaint();
                        }, () => _filterPresetManager.ShowManagePresetsDialog());
                    }

                    bool hasActiveFilters = !string.IsNullOrWhiteSpace(_search) || _selectedTags.Count > 0;
                    using (new EditorGUI.DisabledScope(!hasActiveFilters))
                    {
                        if (GUILayout.Button("Save Search", UIStyles.ToolbarButton, GUILayout.Width(__TAG_FILTER_SAVE_WIDTH)))
                        {
                            // Defer dialog to next frame to avoid breaking the layout stack (modal clears parent's GUI state)
                            string searchSnapshot = _search ?? string.Empty;
                            HashSet<string> tagsSnapshot = new HashSet<string>(_selectedTags);
                            EditorApplication.delayCall += () =>
                            {
                                _filterPresetManager.ShowSavePresetDialog(searchSnapshot, tagsSnapshot);
                            };
                        }
                    }

                    using (new EditorGUI.DisabledScope(_selectedTags.Count == 0))
                    {
                        if (GUILayout.Button("Clear Tags", UIStyles.ToolbarButton, GUILayout.Width(__TAG_FILTER_CLEAR_WIDTH)))
                        {
                            _selectedTags.Clear();
                            GUI.FocusControl(null);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                if (_tagCacheManager.SortedTags.Count == 0)
                {
                    EditorGUILayout.LabelField("No tags available.", UIStyles.MutedLabel);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search tags:", UIStyles.MutedLabel, GUILayout.Width(__TAG_FILTER_SEARCH_LABEL_WIDTH));
                    GUI.SetNextControlName("TagSearchField");
                    string newTagFilter = EditorGUILayout.TextField(_tagSearchFilter, EditorStyles.toolbarSearchField);
                    if (!string.Equals(newTagFilter, _tagSearchFilter, StringComparison.Ordinal))
                    {
                        _tagSearchFilter = newTagFilter;
                        Repaint();
                    }
                    if (GUILayout.Button("Clear", UIStyles.ToolbarButton, GUILayout.Width(__TAG_FILTER_SEARCH_CLEAR_WIDTH)))
                    {
                        _tagSearchFilter = string.Empty;
                        GUI.FocusControl(null);
                        Repaint();
                    }
                }

                List<string> filteredTags = new List<string>();
                if (string.IsNullOrWhiteSpace(_tagSearchFilter))
                {
                    filteredTags.AddRange(_tagCacheManager.SortedTags);
                }
                else
                {
                    string filterLower = _tagSearchFilter.Trim().ToLowerInvariant();
                    foreach (string tag in _tagCacheManager.SortedTags)
                    {
                        if (tag.ToLowerInvariant().Contains(filterLower))
                        {
                            filteredTags.Add(tag);
                        }
                    }
                }

                if (filteredTags.Count == 0)
                {
                    EditorGUILayout.LabelField($"No tags match \"{_tagSearchFilter}\".", UIStyles.MutedLabel);
                    return;
                }

                _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, GUILayout.MaxHeight(__TAG_FILTER_SCROLL_HEIGHT));
                for (int i = 0; i < filteredTags.Count; i++)
                {
                    string tag = filteredTags[i];
                    bool isSelected = _selectedTags.Contains(tag);
                    bool newSelected = EditorGUILayout.ToggleLeft($"{tag} ({_tagCacheManager.TagCounts[tag]})", isSelected);
                    if (newSelected != isSelected)
                    {
                        if (newSelected)
                        {
                            _selectedTags.Add(tag);
                        }
                        else
                        {
                            _selectedTags.Remove(tag);
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private bool EntryHasAllSelectedTags(ModelIndex.Entry entry)
        {
            if (entry?.tags == null || entry.tags.Count == 0)
            {
                return false;
            }

            foreach (string tag in _selectedTags)
            {
                bool match = entry.tags.Any(candidate => string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase));
                if (!match)
                {
                    return false;
                }
            }

            return true;
        }

        private void DrawListView(List<ModelIndex.Entry> entries)
        {
            float viewHeight = GetVisibleViewHeight();
            int firstVisibleIndex = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / __LIST_ESTIMATED_ITEM_HEIGHT));
            int visibleCount = Mathf.CeilToInt(viewHeight / __LIST_ESTIMATED_ITEM_HEIGHT) + __VIRTUALIZATION_BUFFER;
            int lastVisibleIndex = Mathf.Min(entries.Count - 1, firstVisibleIndex + visibleCount - 1);

            if (firstVisibleIndex > 0)
            {
                GUILayout.Space(firstVisibleIndex * __LIST_ESTIMATED_ITEM_HEIGHT);
            }

            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                DrawEntry(entries[i], i == _keyboardSelectionIndex);
            }

            int remainingItems = entries.Count - lastVisibleIndex - 1;
            if (remainingItems > 0)
            {
                GUILayout.Space(remainingItems * __LIST_ESTIMATED_ITEM_HEIGHT);
            }
        }

        private void DrawGridView(List<ModelIndex.Entry> entries)
        {
            float thumbnailSize = _thumbnailSize;
            float spacing = __GRID_CARD_SPACING;
            float cardPadding = __GRID_CARD_PADDING;
            float minCardWidth = thumbnailSize + (cardPadding * 2f);
            float minCardHeight = thumbnailSize + __GRID_CARD_EXTRA_HEIGHT;

            float availableWidth = EditorGUIUtility.currentViewWidth - 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (minCardWidth + spacing)));
            _lastGridColumns = columns;

            int totalRows = Mathf.CeilToInt((float)entries.Count / columns);
            float rowHeight = __GRID_ESTIMATED_ROW_HEIGHT;
            int firstVisibleRow = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / rowHeight));
            float viewHeight = GetVisibleViewHeight();
            int visibleRowCount = Mathf.CeilToInt(viewHeight / rowHeight) + __VIRTUALIZATION_BUFFER;
            int lastVisibleRow = Mathf.Min(totalRows - 1, firstVisibleRow + visibleRowCount - 1);

            if (firstVisibleRow > 0)
            {
                GUILayout.Space(firstVisibleRow * rowHeight);
            }

            for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = (row * columns) + col;
                        if (index >= entries.Count)
                        {
                            break;
                        }

                        ModelIndex.Entry entry = entries[index];
                        bool isHighlighted = index == _keyboardSelectionIndex;
                        DrawGridCard(entry, thumbnailSize, cardPadding, minCardHeight, isHighlighted);

                        // Add spacing between cards (except for the last card in row)
                        if (col < columns - 1)
                        {
                            GUILayout.Space(spacing);
                        }
                    }

                    // Add flexible space at the end to prevent the last card from stretching
                    GUILayout.FlexibleSpace();
                }
            }

            int remainingRows = totalRows - lastVisibleRow - 1;
            if (remainingRows > 0)
            {
                GUILayout.Space(remainingRows * rowHeight);
            }
        }

        private void DrawImageOnlyView(List<ModelIndex.Entry> entries)
        {
            float thumbnailSize = _thumbnailSize;
            float spacing = __IMAGE_CARD_SPACING;
            float minCardWidth = thumbnailSize + spacing;

            float availableWidth = EditorGUIUtility.currentViewWidth - 20f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / minCardWidth));
            _lastImageColumns = columns;
            int totalRows = Mathf.CeilToInt((float)entries.Count / columns);
            float rowHeight = __IMAGE_ESTIMATED_ROW_HEIGHT;
            int firstVisibleRow = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / rowHeight));
            float viewHeight = GetVisibleViewHeight();
            int visibleRowCount = Mathf.CeilToInt(viewHeight / rowHeight) + __VIRTUALIZATION_BUFFER;
            int lastVisibleRow = Mathf.Min(totalRows - 1, firstVisibleRow + visibleRowCount - 1);

            if (firstVisibleRow > 0)
            {
                GUILayout.Space(firstVisibleRow * rowHeight);
            }

            for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = (row * columns) + col;
                        if (index >= entries.Count)
                        {
                            break;
                        }

                        ModelIndex.Entry entry = entries[index];
                        bool isHighlighted = index == _keyboardSelectionIndex;
                        DrawImageOnlyCard(entry, thumbnailSize, isHighlighted);

                        // Add spacing between cards (except for the last card in row)
                        if (col < columns - 1)
                        {
                            GUILayout.Space(spacing);
                        }
                    }

                    // Add flexible space at the end to prevent the last card from stretching
                    GUILayout.FlexibleSpace();
                }
            }

            int remainingRows = totalRows - lastVisibleRow - 1;
            if (remainingRows > 0)
            {
                GUILayout.Space(remainingRows * rowHeight);
            }
        }

        private float GetVisibleViewHeight()
        {
            float height = position.height - 260f;
            if (height < 200f)
            {
                height = Mathf.Max(position.height * 0.7f, 200f);
            }
            return height;
        }

        private void DrawEntry(ModelIndex.Entry entry, bool highlight)
        {
            Color originalBackground = GUI.backgroundColor;
            if (highlight)
            {
                GUI.backgroundColor = UIConstants.COLOR_SELECTION_BACKGROUND;
            }

            using (new EditorGUILayout.VerticalScope(UIStyles.CardBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isFavorite = _favoritesManager.IsFavorite(entry.id);
                    string starText = isFavorite ? "★" : "☆";
                    Color originalColor = GUI.color;
                    if (isFavorite)
                    {
                        GUI.color = Color.yellow;
                    }
                    if (GUILayout.Button(starText, EditorStyles.label, GUILayout.Width(__LIST_BUTTON_STAR_WIDTH)))
                    {
                        _favoritesManager.ToggleFavorite(entry.id);
                        Repaint();
                    }
                    GUI.color = originalColor;

                    DrawNotificationBadges(entry);

                    GUILayout.Label(entry.name, UIStyles.SectionHeader);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"v{entry.latestVersion}", UIStyles.MutedLabel);

                    bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                    string localVersion = installed ? localMeta.version : null;
                    bool properlyInstalled = installed && !string.IsNullOrEmpty(localVersion) && localVersion != "(unknown)";
                    bool needsUpgrade = properlyInstalled && ModelVersionUtils.NeedsUpgrade(localVersion, entry.latestVersion);
                    bool isBusy = _importsInProgress.Contains(entry.id);

                    if (properlyInstalled)
                    {
                        string label = needsUpgrade ? $"Local v{localVersion} (update available)" : $"Local v{localVersion}";
                        GUILayout.Label(label, UIStyles.MutedLabel);
                    }
                    else if (installed && (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)"))
                    {
                        GUILayout.Label("Local (version unknown)", UIStyles.MutedLabel);
                    }

                    if (UIStyles.DrawSecondaryButton("Details", GUILayout.Width(__LIST_BUTTON_DETAILS_WIDTH)))
                    {
                        Dictionary<string, object> parameters = new Dictionary<string, object>
                        {
                            { "modelId", entry.id },
                            { "version", entry.latestVersion }
                        };
                        NavigateToView(ViewType.ModelDetails, parameters);
                    }

                    bool downloaded = IsDownloaded(entry.id, entry.latestVersion);
                    using (new EditorGUI.DisabledScope(downloaded || isBusy))
                    {
                        if (GUILayout.Button("Download", GUILayout.Width(__LIST_BUTTON_DOWNLOAD_WIDTH)))
                        {
                            _ = Download(entry.id, entry.latestVersion);
                        }
                    }

                    using (new EditorGUI.DisabledScope(isBusy || (properlyInstalled && !needsUpgrade)))
                    {
                        string actionLabel = needsUpgrade ? "Update" : "Import";
                        if (GUILayout.Button(actionLabel, GUILayout.Width(__LIST_BUTTON_IMPORT_WIDTH)))
                        {
                            string previousVersion = properlyInstalled ? localVersion : null;
                            _ = Import(entry.id, entry.latestVersion, needsUpgrade, previousVersion);
                        }
                    }
                }

                EditorGUILayout.LabelField(entry.description, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Tags:", string.Join(", ", entry.tags));
                EditorGUILayout.LabelField("Updated:", new DateTime(entry.updatedTimeTicks).ToString(CultureInfo.CurrentCulture));
                EditorGUILayout.LabelField("Release:", entry.releaseTimeTicks <= 0 ? string.Empty : new DateTime(entry.releaseTimeTicks).ToString(CultureInfo.CurrentCulture));

                string key = entry.id + "@" + entry.latestVersion;
                bool isLoadingMeta = _loadingMeta.Contains(key);
                if (!_metaCache.ContainsKey(key) && !isLoadingMeta)
                {
                    _ = LoadMetaAsync(entry.id, entry.latestVersion);
                }

                if (TryGetMetaFromCache(key, out ModelMeta meta))
                {
                    string thumbKey = key + "#thumb";
                    bool hasThumbnail = TryGetThumbnailFromCache(thumbKey, out Texture2D thumbnail);
                    bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
                    bool isLoadingMetaInner = _loadingMeta.Contains(key);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect thumbRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
                        if (hasThumbnail && thumbnail != null)
                        {
                            EditorGUI.DrawPreviewTexture(thumbRect, thumbnail, null, ScaleMode.ScaleAndCrop);
                            GUILayout.Space(6f);
                        }
                        else if (isLoadingThumbnail || isLoadingMetaInner)
                        {
                            EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f, 1f));
                            GUI.Label(thumbRect, "Loading...", EditorStyles.centeredGreyMiniLabel);
                            GUILayout.Space(6f);
                        }

                        using (new EditorGUILayout.VerticalScope())
                        {
                            if (meta.materials != null && meta.materials.Count > 0)
                            {
                                EditorGUILayout.LabelField("Materials:", string.Join(", ", meta.materials.ConvertAll(material => material.name)));
                            }
                            if (meta.textures != null && meta.textures.Count > 0)
                            {
                                EditorGUILayout.LabelField("Textures:", string.Join(", ", meta.textures.ConvertAll(texture => texture.name)));
                            }
                            if (meta.vertexCount > 0 || meta.triangleCount > 0)
                            {
                                string vertsLabel = meta.vertexCount.ToString("N0");
                                string trisLabel = meta.triangleCount > 0 ? meta.triangleCount.ToString("N0") : null;
                                string geometry = trisLabel != null ? $"{vertsLabel} verts / {trisLabel} tris" : $"{vertsLabel} verts";
                                EditorGUILayout.LabelField("Geometry:", geometry);
                            }
                        }
                    }
                }
            }

            GUI.backgroundColor = originalBackground;
        }

        private void DrawGridCard(ModelIndex.Entry entry, float thumbnailSize, float padding, float minHeight, bool highlight = false)
        {
            Color originalBackground = GUI.backgroundColor;
            if (highlight)
            {
                GUI.backgroundColor = UIConstants.COLOR_SELECTION_BACKGROUND;
            }

            using (new EditorGUILayout.VerticalScope(UIStyles.CardBox, GUILayout.Width(thumbnailSize + (padding * 2f)), GUILayout.MinHeight(minHeight), GUILayout.ExpandWidth(false)))
            {
                if (_bulkSelectionMode)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isSelected = _selectedModels.Contains(entry.id);
                        bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20f));
                        if (newSelected != isSelected)
                        {
                            if (newSelected)
                            {
                                _selectedModels.Add(entry.id);
                            }
                            else
                            {
                                _selectedModels.Remove(entry.id);
                            }
                        }
                        GUILayout.FlexibleSpace();
                    }
                }

                string key = entry.id + "@" + entry.latestVersion;
                if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
                {
                    _ = LoadMetaAsync(entry.id, entry.latestVersion);
                }

                string thumbKey = key + "#thumb";
                bool hasThumbnail = TryGetThumbnailFromCache(thumbKey, out Texture2D thumbnail);
                bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
                bool isLoadingMeta = _loadingMeta.Contains(key);

                // Use horizontal scope to center the thumbnail and ensure it fills available width
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    Rect thumbRect = GUILayoutUtility.GetRect(thumbnailSize, thumbnailSize, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize), GUILayout.ExpandWidth(false));
                    GUILayout.FlexibleSpace();

                    string tooltip = BuildModelTooltip(entry, key);

                    if (hasThumbnail && thumbnail != null)
                    {
                        // Draw thumbnail with proper scaling to fill the rect completely
                        // ScaleAndCrop ensures the image fills the entire button while maintaining aspect ratio
                        EditorGUI.DrawPreviewTexture(thumbRect, thumbnail, null, ScaleMode.ScaleAndCrop);

                        // Draw clickable button on top (transparent) with tooltip
                        GUIContent buttonContent = new GUIContent(string.Empty, tooltip);
                        if (GUI.Button(thumbRect, buttonContent, GUIStyle.none))
                        {
                            Dictionary<string, object> parameters = new Dictionary<string, object>
                            {
                                { "modelId", entry.id },
                                { "version", entry.latestVersion }
                            };
                            NavigateToView(ViewType.ModelDetails, parameters);
                        }
                    }
                    else
                    {
                        DrawThumbnailPlaceholder(thumbRect, entry.name, isLoadingThumbnail || isLoadingMeta, () =>
                        {
                            Dictionary<string, object> parameters = new Dictionary<string, object>
                            {
                                { "modelId", entry.id },
                                { "version", entry.latestVersion }
                            };
                            NavigateToView(ViewType.ModelDetails, parameters);
                        });

                        // Set tooltip for placeholder
                        if (thumbRect.Contains(Event.current.mousePosition))
                        {
                            GUI.tooltip = tooltip;
                        }
                    }

                    // Draw note badge overlaid on thumbnail in upper right corner
                    bool hasNotes = HasNotes(entry.id, entry.latestVersion);
                    if (hasNotes)
                    {
                        (bool hasNotesInfo, string notesTooltip) = GetNotesInfo(entry.id, entry.latestVersion);
                        DrawNoteBadgeOverlay(thumbRect, notesTooltip);
                    }
                }

                GUILayout.Space(UIConstants.SPACING_EXTRA_SMALL);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isFavorite = _favoritesManager.IsFavorite(entry.id);
                    string starText = isFavorite ? "★" : "☆";
                    Color originalColor = GUI.color;
                    if (isFavorite)
                    {
                        GUI.color = Color.yellow;
                    }
                    if (GUILayout.Button(starText, EditorStyles.miniLabel, GUILayout.Width(16f)))
                    {
                        _favoritesManager.ToggleFavorite(entry.id);
                        Repaint();
                    }
                    GUI.color = originalColor;

                    // Only draw update badge in horizontal layout (note badge is now overlaid on thumbnail)
                    bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatusBadge) && updateStatusBadge;
                    bool hasUpdateLocally = false;
                    bool installedForBadge = TryGetLocalInstall(entry, out ModelMeta localMetaForBadge);
                    if (installedForBadge && localMetaForBadge != null && !string.IsNullOrEmpty(localMetaForBadge.version) && localMetaForBadge.version != "(unknown)")
                    {
                        hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMetaForBadge.version, entry.latestVersion);
                    }
                    bool hasUpdateBadge = (hasUpdateFromCache || hasUpdateLocally) && !NotificationStateManager.IsUpdateRead(entry.id);
                    if (hasUpdateBadge)
                    {
                        GUIStyle updateStyle = new GUIStyle(GUI.skin.label);
                        updateStyle.normal.textColor = Color.yellow;
                        Color originalUpdateColor = GUI.color;
                        GUI.color = Color.yellow;
                        GUILayout.Label("🔄", updateStyle, GUILayout.Width(16), GUILayout.Height(16));
                        GUI.color = originalUpdateColor;
                    }

                    // Calculate available width for the label (accounting for star button and update badge)
                    float labelWidth = thumbnailSize + (padding * 2f) - 16f - (hasUpdateBadge ? 16f : 0f) - 8f; // Subtract space for buttons and margin

                    // Get truncated text with ellipsis if needed
                    string displayName = TruncateTextWithEllipsis(entry.name, EditorStyles.miniLabel, labelWidth);

                    // Use the label with fixed width
                    GUILayout.Label(displayName, EditorStyles.miniLabel, GUILayout.Width(labelWidth), GUILayout.ExpandWidth(false));
                }

                GUILayout.Label($"v{entry.latestVersion}", EditorStyles.centeredGreyMiniLabel);

                bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                string localVersion = installed ? localMeta.version : null;
                bool properlyInstalled = installed && !string.IsNullOrEmpty(localVersion) && localVersion != "(unknown)";
                bool hasUpdate = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;

                if (properlyInstalled)
                {
                    string statusText = hasUpdate ? "Update Available" : "Installed";
                    Color textColor = hasUpdate ? UIConstants.COLOR_STATUS_UPDATE : UIConstants.COLOR_STATUS_INSTALLED;
                    Color bgColor = hasUpdate ? UIConstants.COLOR_STATUS_UPDATE_BG : UIConstants.COLOR_STATUS_INSTALLED_BG;
                    UIStyles.DrawStatusBadge(statusText, textColor, bgColor);
                }
                else if (installed && (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)"))
                {
                    UIStyles.DrawStatusBadge("Installed", UIConstants.COLOR_STATUS_UNKNOWN, UIConstants.COLOR_STATUS_UNKNOWN_BG);
                }

                GUI.backgroundColor = originalBackground;
            }
        }

        private void DrawImageOnlyCard(ModelIndex.Entry entry, float thumbnailSize, bool highlight = false)
        {
            string key = entry.id + "@" + entry.latestVersion;
            if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
            {
                _ = LoadMetaAsync(entry.id, entry.latestVersion);
            }

            string thumbKey = key + "#thumb";
            bool hasThumbnail = TryGetThumbnailFromCache(thumbKey, out Texture2D thumbnail);
            bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
            bool isLoadingMeta = _loadingMeta.Contains(key);

            Rect thumbRect = GUILayoutUtility.GetRect(thumbnailSize, thumbnailSize, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize), GUILayout.ExpandWidth(false));

            if (highlight)
            {
                Rect outline = new Rect(thumbRect.x - 3f, thumbRect.y - 3f, thumbRect.width + 6f, thumbRect.height + 6f);
                EditorGUI.DrawRect(outline, UIConstants.COLOR_SELECTION_OUTLINE);
            }

            string tooltip = BuildModelTooltip(entry, key);

            if (hasThumbnail && thumbnail != null)
            {
                // Draw thumbnail with proper scaling to fill the rect completely
                // ScaleAndCrop ensures the image fills the entire button while maintaining aspect ratio
                EditorGUI.DrawPreviewTexture(thumbRect, thumbnail, null, ScaleMode.ScaleAndCrop);

                // Draw notification badges overlay on thumbnail
                bool hasNotes = HasNotes(entry.id, entry.latestVersion);
                bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;
                bool hasUpdateLocally = false;
                bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
                {
                    hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);
                }
                bool hasUpdate = (hasUpdateFromCache || hasUpdateLocally) && !NotificationStateManager.IsUpdateRead(entry.id);

                // Draw badges in top-right corner of thumbnail
                if (hasNotes || hasUpdate)
                {
                    Rect badgeRect = new Rect(thumbRect.xMax - 20f, thumbRect.y + 2f, 18f, 18f);
                    if (hasNotes)
                    {
                        GUI.Label(badgeRect, "📝", EditorStyles.boldLabel);
                        badgeRect.x -= 20f; // Offset for update badge if both present
                    }
                    if (hasUpdate)
                    {
                        Color originalColor = GUI.color;
                        GUI.color = Color.yellow;
                        GUI.Label(badgeRect, "🔄", EditorStyles.boldLabel);
                        GUI.color = originalColor;
                    }
                }

                // Draw clickable button on top (transparent) with tooltip
                GUIContent buttonContent = new GUIContent(string.Empty, tooltip);
                if (GUI.Button(thumbRect, buttonContent, GUIStyle.none))
                {
                    Dictionary<string, object> parameters = new Dictionary<string, object>
                    {
                        { "modelId", entry.id },
                        { "version", entry.latestVersion }
                    };
                    NavigateToView(ViewType.ModelDetails, parameters);
                }
            }
            else
            {
                DrawThumbnailPlaceholder(thumbRect, entry.name, isLoadingThumbnail || isLoadingMeta, () =>
                {
                    Dictionary<string, object> parameters = new Dictionary<string, object>
                    {
                        { "modelId", entry.id },
                        { "version", entry.latestVersion }
                    };
                    NavigateToView(ViewType.ModelDetails, parameters);
                });

                // Set tooltip for placeholder
                if (thumbRect.Contains(Event.current.mousePosition))
                {
                    GUI.tooltip = tooltip;
                }
            }
        }

        private string BuildModelTooltip(ModelIndex.Entry entry, string key)
        {
            System.Text.StringBuilder tooltip = new System.Text.StringBuilder();
            tooltip.AppendLine(entry.name);
            tooltip.AppendLine($"Version: {entry.latestVersion}");

            if (!string.IsNullOrEmpty(entry.description))
            {
                string desc = entry.description.Length > 100 ? string.Concat(entry.description.Substring(0, 97), "...") : entry.description;
                tooltip.AppendLine($"\n{desc}");
            }

            if (entry.tags != null && entry.tags.Count > 0)
            {
                tooltip.AppendLine($"\nTags: {string.Join(", ", entry.tags.Take(5))}");
                if (entry.tags.Count > 5)
                {
                    tooltip.Append($" (+{entry.tags.Count - 5} more)");
                }
            }

            bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;
            bool hasUpdateLocally = false;
            bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
            if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
            {
                hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);
            }
            bool hasUpdate = (hasUpdateFromCache || hasUpdateLocally) && !NotificationStateManager.IsUpdateRead(entry.id);
            bool hasNotes = HasNotes(entry.id, entry.latestVersion);
            bool isFavorite = _favoritesManager.IsFavorite(entry.id);
            bool isInstalled = TryGetLocalInstall(entry, out _);

            List<string> statusItems = new List<string>();
            if (hasUpdate)
            {
                statusItems.Add("Update available");
            }
            if (hasNotes)
            {
                statusItems.Add("Has notes");
            }
            if (isFavorite)
            {
                statusItems.Add("Favorite");
            }
            if (isInstalled)
            {
                statusItems.Add("Installed");
            }

            if (statusItems.Count > 0)
            {
                tooltip.AppendLine($"\nStatus: {string.Join(", ", statusItems)}");
            }

            tooltip.AppendLine("\nClick to view details");
            return tooltip.ToString();
        }

        /// <summary>
        /// Draws a note badge overlaid on the thumbnail in the upper right corner.
        /// </summary>
        /// <param name="thumbRect">The rectangle of the thumbnail.</param>
        /// <param name="tooltip">Tooltip text to display when hovering over the badge.</param>
        private void DrawNoteBadgeOverlay(Rect thumbRect, string tooltip)
        {
            const float badgeSize = 22f;
            const float offset = 3f;

            // Position badge in upper right corner
            Rect badgeRect = new Rect(
                thumbRect.x + thumbRect.width - badgeSize - offset,
                thumbRect.y + offset,
                badgeSize,
                badgeSize
            );

            // Draw note emoji/icon with light blue color (no background for transparency)
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = new Color(0.4f, 0.75f, 1f) } // Light blue
            };

            string tooltipText = !string.IsNullOrEmpty(tooltip) ? tooltip : "This model has feedback notes";
            GUIContent badgeContent = new GUIContent("💬", tooltipText);
            GUI.Label(badgeRect, badgeContent, badgeStyle);
        }

        private void DrawThumbnailPlaceholder(Rect rect, string modelName, bool isLoading, Action onClick)
        {
            EditorGUI.DrawRect(rect, UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_BG);

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, __THUMBNAIL_PLACEHOLDER_BORDER_SIZE), UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_BORDER);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - __THUMBNAIL_PLACEHOLDER_BORDER_SIZE, rect.width, __THUMBNAIL_PLACEHOLDER_BORDER_SIZE), UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_BORDER);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, __THUMBNAIL_PLACEHOLDER_BORDER_SIZE, rect.height), UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_BORDER);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - __THUMBNAIL_PLACEHOLDER_BORDER_SIZE, rect.y, __THUMBNAIL_PLACEHOLDER_BORDER_SIZE, rect.height), UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_BORDER);

            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset((int)__THUMBNAIL_PLACEHOLDER_PADDING, (int)__THUMBNAIL_PLACEHOLDER_PADDING, (int)__THUMBNAIL_PLACEHOLDER_PADDING, (int)__THUMBNAIL_PLACEHOLDER_PADDING)
            };

            if (isLoading)
            {
                float time = (float)EditorApplication.timeSinceStartup;
                int frame = Mathf.FloorToInt(time * 2f) % 4;
                string[] loadingFrames = { "◐", "◓", "◑", "◒" };
                string loadingText = string.Concat(loadingFrames[frame], " Loading...");

                centeredStyle.normal.textColor = UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_TEXT_LOADING;
                GUI.Label(rect, loadingText, centeredStyle);
            }
            else
            {
                centeredStyle.normal.textColor = UIConstants.COLOR_THUMBNAIL_PLACEHOLDER_TEXT;
                string displayText = string.IsNullOrEmpty(modelName) ? "No Preview" : modelName;
                if (displayText.Length > 20)
                {
                    displayText = string.Concat(displayText.Substring(0, 17), "...");
                }
                GUI.Label(rect, displayText, centeredStyle);
            }

            if (GUI.Button(rect, string.Empty, GUIStyle.none))
            {
                onClick?.Invoke();
            }
        }

        private void DrawNotificationBadges(ModelIndex.Entry entry)
        {
            (bool hasNotes, string notesTooltip) = GetNotesInfo(entry.id, entry.latestVersion);

            bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;

            bool hasUpdateLocally = false;
            bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
            if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
            {
                hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);
            }

            bool hasUpdate = (hasUpdateFromCache || hasUpdateLocally) && !NotificationStateManager.IsUpdateRead(entry.id);

            ModelLibraryUIDrawer.DrawNotificationBadges(hasNotes, hasUpdate, notesTooltip);
        }

        private void DrawCompactNotificationBadges(ModelIndex.Entry entry)
        {
            bool hasNotes = HasNotes(entry.id, entry.latestVersion);
            bool hasUpdateFromCache = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;

            bool hasUpdateLocally = false;
            bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
            if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
            {
                hasUpdateLocally = ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);
            }

            bool hasUpdate = (hasUpdateFromCache || hasUpdateLocally) && !NotificationStateManager.IsUpdateRead(entry.id);

            ModelLibraryUIDrawer.DrawCompactNotificationBadges(hasNotes, hasUpdate);
        }

        private void HandleKeyboardShortcuts()
        {
            Event currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            bool ctrlOrCmd = currentEvent.control || currentEvent.command;
            bool shift = currentEvent.shift;
            bool textActive = IsTextInputActive();
            string focusedControl = GUI.GetNameOfFocusedControl();

            if ((currentEvent.keyCode == KeyCode.Comma || currentEvent.keyCode == KeyCode.Semicolon) && ctrlOrCmd)
            {
                NavigateToView(ViewType.Settings);
                currentEvent.Use();
                return;
            }

            if (ctrlOrCmd && shift && currentEvent.keyCode == KeyCode.L)
            {
                NavigateToView(ViewType.Shortcuts);
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.F5)
            {
                if (_service != null && !_loadingIndex)
                {
                    _indexCache = null;
                    _ = LoadIndexAsync();
                    currentEvent.Use();
                }
                return;
            }

            if (ctrlOrCmd && currentEvent.keyCode == KeyCode.F)
            {
                GUI.FocusControl("SearchField");
                currentEvent.Use();
                return;
            }

            if ((currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter) && string.Equals(focusedControl, "SearchField", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(_search))
            {
                _searchHistoryManager.AddToSearchHistory(_search);
                currentEvent.Use();
                return;
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                if (string.Equals(focusedControl, "SearchField", StringComparison.Ordinal) || !string.IsNullOrEmpty(_search))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                    currentEvent.Use();
                }
                return;
            }

            if (currentEvent.keyCode == KeyCode.V && !ctrlOrCmd && !shift && !currentEvent.alt)
            {
                if (string.IsNullOrEmpty(focusedControl) || focusedControl == "SearchField" || focusedControl == "TagSearchField")
                {
                    _viewMode = _viewMode switch
                    {
                        ViewMode.List => ViewMode.Grid,
                        ViewMode.Grid => ViewMode.ImageOnly,
                        _ => ViewMode.List
                    };
                    Repaint();
                    currentEvent.Use();
                }
                return;
            }

            if (ctrlOrCmd && currentEvent.keyCode == KeyCode.B)
            {
                _bulkSelectionMode = !_bulkSelectionMode;
                if (!_bulkSelectionMode)
                {
                    _selectedModels.Clear();
                }
                Repaint();
                currentEvent.Use();
                return;
            }

            if (ctrlOrCmd && shift && currentEvent.keyCode == KeyCode.T)
            {
                if (_selectedModels.Count > 0)
                {
                    OpenBulkTagWindow();
                }
                else if (TryGetKeyboardSelectedEntry(out ModelIndex.Entry highlightedEntry))
                {
                    LaunchBulkTagEditor(new List<ModelIndex.Entry> { highlightedEntry });
                }
                else
                {
                    ShowNotification("Bulk Tags", "Select at least one model to open the bulk tag editor.");
                    Repaint();
                }
                currentEvent.Use();
                return;
            }

            if (ctrlOrCmd && currentEvent.keyCode == KeyCode.I)
            {
                if (TryGetKeyboardSelectedEntry(out ModelIndex.Entry importEntry))
                {
                    bool installed = TryGetLocalInstall(importEntry, out ModelMeta localMeta);
                    string previousVersion = installed ? localMeta?.version : null;
                    bool needsUpgrade = installed && !string.IsNullOrEmpty(previousVersion) && ModelVersionUtils.NeedsUpgrade(previousVersion, importEntry.latestVersion);
                    _ = Import(importEntry.id, importEntry.latestVersion, needsUpgrade, previousVersion);
                    Repaint();
                }
                else
                {
                    ShowNotification("Import", "Highlight a model to import.");
                }
                currentEvent.Use();
                return;
            }

            if (ctrlOrCmd && currentEvent.keyCode == KeyCode.U)
            {
                if (TryGetKeyboardSelectedEntry(out ModelIndex.Entry updateEntry))
                {
                    bool canUpdate = _modelUpdateStatus.TryGetValue(updateEntry.id, out bool hasUpdate) && hasUpdate;
                    if (canUpdate && TryGetLocalInstall(updateEntry, out ModelMeta localMeta))
                    {
                        string previousVersion = localMeta?.version;
                        _ = Import(updateEntry.id, updateEntry.latestVersion, true, previousVersion);
                        Repaint();
                    }
                    else
                    {
                        ShowNotification("Update", "Highlighted model is already up to date.");
                    }
                }
                else
                {
                    ShowNotification("Update", "Highlight a model to update.");
                }
                currentEvent.Use();
                return;
            }

            if (!textActive)
            {
                int navigationColumns = _viewMode switch
                {
                    ViewMode.Grid => Mathf.Max(1, _lastGridColumns),
                    ViewMode.ImageOnly => Mathf.Max(1, _lastImageColumns),
                    _ => 1
                };

                if (currentEvent.keyCode == KeyCode.DownArrow)
                {
                    int delta = _viewMode == ViewMode.List ? 1 : navigationColumns;
                    AdjustKeyboardSelection(delta);
                    currentEvent.Use();
                    return;
                }

                if (currentEvent.keyCode == KeyCode.UpArrow)
                {
                    int delta = _viewMode == ViewMode.List ? -1 : -navigationColumns;
                    AdjustKeyboardSelection(delta);
                    currentEvent.Use();
                    return;
                }

                if (currentEvent.keyCode == KeyCode.RightArrow)
                {
                    AdjustKeyboardSelection(1);
                    currentEvent.Use();
                    return;
                }

                if (currentEvent.keyCode == KeyCode.LeftArrow)
                {
                    AdjustKeyboardSelection(-1);
                    currentEvent.Use();
                    return;
                }

                if (currentEvent.keyCode == KeyCode.Space)
                {
                    if (TryGetKeyboardSelectedEntry(out ModelIndex.Entry toggleEntry))
                    {
                        ToggleSelection(toggleEntry);
                    }
                    currentEvent.Use();
                    return;
                }

                if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
                {
                    if (TryGetKeyboardSelectedEntry(out ModelIndex.Entry openEntry))
                    {
                        Dictionary<string, object> parameters = new Dictionary<string, object>
                        {
                            { "modelId", openEntry.id },
                            { "version", openEntry.latestVersion }
                        };
                        NavigateToView(ViewType.ModelDetails, parameters);
                        currentEvent.Use();
                        return;
                    }
                }
            }
        }

        private void ShowSearchHelpDialog()
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "helpSection", ModelLibraryHelpWindow.HelpSection.Searching }
            };
            NavigateToView(ViewType.Help, parameters);
        }

        private void UpdateKeyboardSelectionState(List<ModelIndex.Entry> entries)
        {
            _currentEntries.Clear();
            _currentEntries.AddRange(entries);

            if (_currentEntries.Count == 0)
            {
                _keyboardSelectionIndex = -1;
                return;
            }

            if (_keyboardSelectionIndex < 0 || _keyboardSelectionIndex >= _currentEntries.Count)
            {
                _keyboardSelectionIndex = 0;
            }
        }

        private bool TryGetKeyboardSelectedEntry(out ModelIndex.Entry entry)
        {
            if (_keyboardSelectionIndex >= 0 && _keyboardSelectionIndex < _currentEntries.Count)
            {
                entry = _currentEntries[_keyboardSelectionIndex];
                return true;
            }

            entry = null;
            return false;
        }

        private void AdjustKeyboardSelection(int delta)
        {
            if (_currentEntries.Count == 0)
            {
                _keyboardSelectionIndex = -1;
                return;
            }

            int newIndex = Mathf.Clamp(_keyboardSelectionIndex + delta, 0, _currentEntries.Count - 1);
            if (newIndex != _keyboardSelectionIndex)
            {
                _keyboardSelectionIndex = newIndex;
                Repaint();
            }
        }

        private bool IsTextInputActive()
        {
            if (EditorGUIUtility.editingTextField || (Event.current.type == EventType.KeyDown && Event.current.character != 0))
            {
                return true;
            }

            string focusedControl = GUI.GetNameOfFocusedControl();
            return string.Equals(focusedControl, "SearchField", StringComparison.Ordinal) ||
                   string.Equals(focusedControl, "TagSearchField", StringComparison.Ordinal);
        }

        private void ToggleSelection(ModelIndex.Entry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (_bulkSelectionMode)
            {
                if (_selectedModels.Contains(entry.id))
                {
                    _selectedModels.Remove(entry.id);
                }
                else
                {
                    _selectedModels.Add(entry.id);
                }
            }
            else
            {
                _favoritesManager.ToggleFavorite(entry.id);
            }

            Repaint();
        }

        /// <summary>
        /// Truncates text to fit within the specified width, adding ellipsis if needed.
        /// </summary>
        /// <param name="text">The text to truncate.</param>
        /// <param name="style">The GUIStyle to use for measuring text width.</param>
        /// <param name="maxWidth">The maximum width the text should fit within.</param>
        /// <returns>The truncated text with ellipsis if needed, or the original text if it fits.</returns>
        private static string TruncateTextWithEllipsis(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Check if text fits without truncation
            Vector2 textSize = style.CalcSize(new GUIContent(text));
            if (textSize.x <= maxWidth)
            {
                return text;
            }

            // Calculate ellipsis width
            Vector2 ellipsisSize = style.CalcSize(new GUIContent("..."));
            float availableWidth = maxWidth - ellipsisSize.x;

            // Binary search for the maximum length that fits
            int minLength = 0;
            int maxLength = text.Length;
            string bestFit = "...";

            while (minLength <= maxLength)
            {
                int midLength = (minLength + maxLength) / 2;
                string testText = text[..midLength];
                Vector2 testSize = style.CalcSize(new GUIContent(testText));

                if (testSize.x <= availableWidth)
                {
                    bestFit = testText + "...";
                    minLength = midLength + 1;
                }
                else
                {
                    maxLength = midLength - 1;
                }
            }

            return bestFit;
        }

        /// <summary>
        /// Draws a thumbnail size slider in the lower right corner of the window.
        /// Similar to Unity's Project view slider for controlling icon sizes.
        /// </summary>
        private void DrawThumbnailSizeSlider()
        {
            // Get window position and size
            Rect windowRect = position;
            float sliderX = windowRect.width - __THUMBNAIL_SLIDER_WIDTH - __THUMBNAIL_SLIDER_MARGIN;
            float sliderY = windowRect.height - __THUMBNAIL_SLIDER_HEIGHT - __THUMBNAIL_SLIDER_MARGIN;

            // Create slider rect in lower right corner
            Rect sliderRect = new Rect(sliderX, sliderY, __THUMBNAIL_SLIDER_WIDTH, __THUMBNAIL_SLIDER_HEIGHT);

            // Draw slider
            float newSize = GUI.HorizontalSlider(sliderRect, _thumbnailSize, __MIN_THUMBNAIL_SIZE, __MAX_THUMBNAIL_SIZE);
            if (Mathf.Abs(newSize - _thumbnailSize) > 0.1f)
            {
                _thumbnailSize = newSize;
                EditorPrefs.SetFloat(__ThumbnailSizePrefKey, _thumbnailSize);
                Repaint();
            }

            // Draw size label next to slider (optional, for user feedback)
            Rect labelRect = new Rect(sliderX - __THUMBNAIL_LABEL_OFFSET, sliderY, __THUMBNAIL_LABEL_WIDTH, __THUMBNAIL_SLIDER_HEIGHT);
            GUIStyle labelStyle = new GUIStyle(UIStyles.MutedLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = EditorStyles.miniLabel.normal.textColor }
            };
            GUI.Label(labelRect, $"{(int)_thumbnailSize}px", labelStyle);
        }
    }
}

