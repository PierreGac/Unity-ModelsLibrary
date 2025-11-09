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
        private void OnGUI()
        {
            HandleKeyboardShortcuts();
            DrawNotification();

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
                        _indexCache = null;
                        _ = LoadIndexAsync();
                        _ = RefreshManifestCacheAsync();
                    },
                    () => ModelSubmitWindow.Open());
                return;
            }

            ModelIndex index = _indexCache;
            DrawTagFilter(index);
            if (index == null || index.entries == null)
            {
                ModelLibraryUIDrawer.DrawEmptyState("No models available",
                    "The repository appears to be empty.\n\nTo get started:\n• Submit your first model using 'Submit Model'\n• Check your repository configuration",
                    () =>
                    {
                        _indexCache = null;
                        _ = LoadIndexAsync();
                        _ = RefreshManifestCacheAsync();
                    },
                    () => ModelSubmitWindow.Open());
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
                            _indexCache = null;
                            _ = LoadIndexAsync();
                            _ = RefreshManifestCacheAsync();
                        }
                    }

                    SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                    if (identityProvider.GetUserRole() == UserRole.Artist)
                    {
                        if (GUILayout.Button("Submit Model", GUILayout.Width(120), GUILayout.Height(30)))
                        {
                            ModelSubmitWindow.Open();
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
                        _indexCache = null;
                        _ = LoadIndexAsync();
                        _ = RefreshManifestCacheAsync();
                    },
                    () => ModelSubmitWindow.Open());
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
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.SetNextControlName("SearchField");
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true), GUILayout.MinWidth(200));
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
                if (GUILayout.Button(searchHelpIcon, EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    ShowSearchHelpDialog();
                }

                bool hasHistory = _searchHistoryManager.History.Count > 0;
                GUIContent historyButtonContent = new GUIContent(hasHistory ? "▼" : "▼", hasHistory ? $"Search history available ({_searchHistoryManager.History.Count} items)" : "No search history");
                Color originalColor = GUI.color;
                if (hasHistory)
                {
                    GUI.color = new Color(0.7f, 0.9f, 1f);
                }
                if (GUILayout.Button(historyButtonContent, EditorStyles.toolbarButton, GUILayout.Width(20)))
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
                    if (GUILayout.Button(clearSearchContent, EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        _search = string.Empty;
                        GUI.FocusControl(null);
                    }
                }

                GUIContent actionsMenuContent = new GUIContent("Actions ▼", "Refresh repository data, check for updates, or submit new models");
                if (GUILayout.Button(actionsMenuContent, EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GenericMenu actionsMenu = new GenericMenu();
                    using (new EditorGUI.DisabledScope(_refreshingManifest || _loadingIndex))
                    {
                        string refreshLabel = _refreshingManifest ? "Refreshing..." : "Refresh";
                        actionsMenu.AddItem(new GUIContent(refreshLabel, "Reload the index and manifest cache"), false, () =>
                        {
                            _indexCache = null;
                            _ = LoadIndexAsync();
                            _ = RefreshManifestCacheAsync();
                        });
                    }
                    actionsMenu.AddItem(new GUIContent("Check Updates", "Run an immediate update check for all models"), false, () => _ = CheckForUpdatesAsync());

                    SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                    if (identityProvider.GetUserRole() == UserRole.Artist)
                    {
                        actionsMenu.AddSeparator(string.Empty);
                        actionsMenu.AddItem(new GUIContent("Submit Model", "Open the submission form for new models or updates"), false, () => ModelSubmitWindow.Open());
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
                    if (GUILayout.Button(updateContent, updateBadgeStyle, GUILayout.Width(130), GUILayout.Height(20)))
                    {
                        _search = "has:update";
                        GUI.FocusControl(null);
                        Repaint();
                    }

                    GUI.color = savedColor;
                }

                GUIContent bulkMenuContent = new GUIContent("Bulk ▼", "Enable selection mode and run batch import/update operations");
                if (GUILayout.Button(bulkMenuContent, EditorStyles.toolbarButton, GUILayout.Width(70)))
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
                        bulkMenu.AddItem(new GUIContent("Batch Upload...", "Upload multiple models from a directory structure"), false, () => BatchUploadWindow.Open());
                    }

                    bulkMenu.ShowAsContext();
                }
                SimpleUserIdentityProvider roleProvider = new SimpleUserIdentityProvider();
                UserRole currentRole = roleProvider.GetUserRole();

                GUIContent settingsMenuContent = new GUIContent("Settings ▼", "Open unified settings, error logs, or the configuration wizard");
                if (GUILayout.Button(settingsMenuContent, EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GenericMenu settingsMenu = new GenericMenu();
                    settingsMenu.AddItem(new GUIContent("Help / Documentation", "Open the Model Library help center"), false, () => ModelLibraryHelpWindow.Open());
                    settingsMenu.AddItem(new GUIContent("Keyboard Shortcuts", "View all keyboard shortcuts"), false, () => ModelLibraryShortcutsWindow.Open());
                    settingsMenu.AddSeparator(string.Empty);
                    settingsMenu.AddItem(new GUIContent("Settings", "Adjust user and repository preferences"), false, () => UnifiedSettingsWindow.Open());
                    settingsMenu.AddSeparator(string.Empty);
                    settingsMenu.AddItem(new GUIContent("Error Log Viewer", "Review recent errors and clear suppressions"), false, () => ErrorLogViewerWindow.Open());
                    settingsMenu.AddItem(new GUIContent("Performance Profiler", "View async operation performance metrics"), false, () => PerformanceProfilerWindow.Open());

                    // Add analytics option for Admin and Artist roles
                    if (currentRole == UserRole.Admin || currentRole == UserRole.Artist)
                    {
                        settingsMenu.AddSeparator(string.Empty);
                        settingsMenu.AddItem(new GUIContent("Analytics", "View model usage statistics and reports"), false, () => AnalyticsWindow.Open());
                    }

                    settingsMenu.AddSeparator(string.Empty);
                    settingsMenu.AddItem(new GUIContent("Configuration Wizard", "Run the guided setup workflow"), false, () => FirstRunWizard.MaybeShow());
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
                float helpButtonWidth = generalHelpContent.image != null ? 28f : 50f;
                if (GUILayout.Button(generalHelpContent, EditorStyles.toolbarButton, GUILayout.Width(helpButtonWidth)))
                {
                    ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Overview);
                }

                GUILayout.FlexibleSpace();


                string roleLabel = currentRole.ToString();
                string roleTooltip = GetRoleTooltip(currentRole);

                GUIStyle roleStyle = new GUIStyle(EditorStyles.toolbarButton)
                {
                    normal = { textColor = GetRoleColor(currentRole) },
                    fontStyle = FontStyle.Bold
                };

                if (GUILayout.Button(new GUIContent($"👤 {roleLabel}", roleTooltip), roleStyle, GUILayout.Width(90)))
                {
                    UnifiedSettingsWindow.Open();
                }

                GUILayout.Label("Sort:", EditorStyles.miniLabel, GUILayout.Width(35));
                ModelSortMode newSortMode = (ModelSortMode)EditorGUILayout.EnumPopup(_sortMode, EditorStyles.toolbarPopup, GUILayout.Width(80));
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
                int newViewMode = GUILayout.Toolbar((int)_viewMode, viewModeIcons, EditorStyles.toolbarButton, GUILayout.Width(105));
                if (newViewMode != (int)_viewMode)
                {
                    _viewMode = (ViewMode)newViewMode;
                    Repaint();
                }
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

            Rect notificationRect = new Rect(0, 0, position.width, 30f);
            GUI.Box(notificationRect, string.Empty, EditorStyles.helpBox);

            Color originalColor = GUI.color;
            GUI.color = new Color(0.3f, 0.8f, 0.3f, 0.9f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10f);
                EditorGUILayout.LabelField(_notificationMessage, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("×", GUILayout.Width(20f), GUILayout.Height(20f)))
                {
                    _notificationMessage = null;
                }
                GUILayout.Space(10f);
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
            using (new EditorGUILayout.VerticalScope("box"))
            {
                _showTagFilter = EditorGUILayout.Foldout(_showTagFilter, "Filter by Tags", true);
                if (!_showTagFilter)
                {
                    return;
                }

                if (index == null)
                {
                    EditorGUILayout.LabelField("Loading tags...", EditorStyles.miniLabel);
                    return;
                }

                if (!ReferenceEquals(index, _tagSource))
                {
                    _tagCacheManager.UpdateTagCache(index);
                    _tagSource = index;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Presets ▼", EditorStyles.toolbarButton, GUILayout.Width(80f)))
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
                        if (GUILayout.Button("Save Search", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                        {
                            _filterPresetManager.ShowSavePresetDialog(_search, _selectedTags);
                        }
                    }

                    using (new EditorGUI.DisabledScope(_selectedTags.Count == 0))
                    {
                        if (GUILayout.Button("Clear Tags", GUILayout.Width(90f)))
                        {
                            _selectedTags.Clear();
                            GUI.FocusControl(null);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }

                if (_tagCacheManager.SortedTags.Count == 0)
                {
                    EditorGUILayout.LabelField("No tags available.", EditorStyles.miniLabel);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Search tags:", GUILayout.Width(80f));
                    GUI.SetNextControlName("TagSearchField");
                    string newTagFilter = EditorGUILayout.TextField(_tagSearchFilter, EditorStyles.toolbarSearchField);
                    if (!string.Equals(newTagFilter, _tagSearchFilter, StringComparison.Ordinal))
                    {
                        _tagSearchFilter = newTagFilter;
                        Repaint();
                    }
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50f)))
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
                    EditorGUILayout.LabelField($"No tags match \"{_tagSearchFilter}\".", EditorStyles.miniLabel);
                    return;
                }

                _tagScroll = EditorGUILayout.BeginScrollView(_tagScroll, GUILayout.MaxHeight(140f));
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
            const float thumbnailSize = 128f;
            const float spacing = 8f;
            const float cardPadding = 4f;
            const float minCardWidth = thumbnailSize + (cardPadding * 2f);
            const float minCardHeight = thumbnailSize + 80f;

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
                EditorGUILayout.BeginHorizontal();
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
                }
                EditorGUILayout.EndHorizontal();
            }

            int remainingRows = totalRows - lastVisibleRow - 1;
            if (remainingRows > 0)
            {
                GUILayout.Space(remainingRows * rowHeight);
            }
        }

        private void DrawImageOnlyView(List<ModelIndex.Entry> entries)
        {
            const float thumbnailSize = 200f;
            const float spacing = 10f;
            const float minCardWidth = thumbnailSize + spacing;

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
                EditorGUILayout.BeginHorizontal();
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
                }
                EditorGUILayout.EndHorizontal();
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
                GUI.backgroundColor = new Color(0.2f, 0.35f, 0.55f, 0.35f);
            }

            using (new EditorGUILayout.VerticalScope("box"))
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
                    if (GUILayout.Button(starText, EditorStyles.label, GUILayout.Width(20f)))
                    {
                        _favoritesManager.ToggleFavorite(entry.id);
                        Repaint();
                    }
                    GUI.color = originalColor;

                    DrawNotificationBadges(entry);

                    GUILayout.Label(entry.name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"v{entry.latestVersion}");

                    bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                    string localVersion = installed ? localMeta.version : null;
                    bool properlyInstalled = installed && !string.IsNullOrEmpty(localVersion) && localVersion != "(unknown)";
                    bool needsUpgrade = properlyInstalled && ModelVersionUtils.NeedsUpgrade(localVersion, entry.latestVersion);
                    bool isBusy = _importsInProgress.Contains(entry.id);

                    if (properlyInstalled)
                    {
                        string label = needsUpgrade ? $"Local v{localVersion} (update available)" : $"Local v{localVersion}";
                        GUILayout.Label(label, EditorStyles.miniLabel);
                    }
                    else if (installed && (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)"))
                    {
                        GUILayout.Label("Local (version unknown)", EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button("Details", GUILayout.Width(70f)))
                    {
                        ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                    }

                    bool downloaded = IsDownloaded(entry.id, entry.latestVersion);
                    using (new EditorGUI.DisabledScope(downloaded || isBusy))
                    {
                        if (GUILayout.Button("Download", GUILayout.Width(90f)))
                        {
                            _ = Download(entry.id, entry.latestVersion);
                        }
                    }

                    using (new EditorGUI.DisabledScope(isBusy || (properlyInstalled && !needsUpgrade)))
                    {
                        string actionLabel = needsUpgrade ? "Update" : "Import";
                        if (GUILayout.Button(actionLabel, GUILayout.Width(80f)))
                        {
                            string previousVersion = properlyInstalled ? localVersion : null;
                            _ = Import(entry.id, entry.latestVersion, needsUpgrade, previousVersion);
                        }
                    }
                }

                EditorGUILayout.LabelField(entry.description);
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
                    Texture2D thumbnail;
                    bool hasThumbnail = TryGetThumbnailFromCache(thumbKey, out thumbnail);
                    bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
                    bool isLoadingMetaInner = _loadingMeta.Contains(key);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect thumbRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
                        if (hasThumbnail && thumbnail != null)
                        {
                            EditorGUI.DrawPreviewTexture(thumbRect, thumbnail, null, ScaleMode.ScaleToFit);
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
                GUI.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.35f);
            }

            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width(thumbnailSize + (padding * 2f)), GUILayout.MinHeight(minHeight)))
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
                Texture2D thumbnail;
                bool hasThumbnail = TryGetThumbnailFromCache(thumbKey, out thumbnail);
                bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
                bool isLoadingMeta = _loadingMeta.Contains(key);

                Rect thumbRect = GUILayoutUtility.GetRect(thumbnailSize, thumbnailSize, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));
                string tooltip = BuildModelTooltip(entry, key);

                if (hasThumbnail && thumbnail != null)
                {
                    GUIContent thumbContent = new GUIContent(thumbnail, tooltip);
                    if (GUI.Button(thumbRect, thumbContent, GUIStyle.none))
                    {
                        ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                    }
                }
                else
                {
                    DrawThumbnailPlaceholder(thumbRect, entry.name, isLoadingThumbnail || isLoadingMeta, () =>
                    {
                        ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                    });

                    if (thumbRect.Contains(Event.current.mousePosition))
                    {
                        GUI.tooltip = tooltip;
                    }
                }

                GUILayout.Space(2f);

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

                    DrawCompactNotificationBadges(entry);

                    string displayName = entry.name;
                    if (displayName.Length > 15)
                    {
                        displayName = string.Concat(displayName.Substring(0, 12), "...");
                    }
                    GUILayout.Label(displayName, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                }

                GUILayout.Label($"v{entry.latestVersion}", EditorStyles.centeredGreyMiniLabel);

                bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                string localVersion = installed ? localMeta.version : null;
                bool properlyInstalled = installed && !string.IsNullOrEmpty(localVersion) && localVersion != "(unknown)";
                bool hasUpdate = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;

                if (properlyInstalled)
                {
                    // Draw colored "Installed" label with box background
                    GUIStyle installedLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(6, 6, 2, 2),
                        normal = { textColor = new Color(0.1f, 0.7f, 0.2f) }, // Green text
                        fontStyle = FontStyle.Bold
                    };
                    
                    Color originalBgColor = GUI.backgroundColor;
                    // Green background with transparency for better visibility
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f, 0.3f);
                    
                    string statusText = hasUpdate ? "Update Available" : "Installed";
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(statusText, installedLabelStyle);
                        GUILayout.FlexibleSpace();
                    }
                    
                    GUI.backgroundColor = originalBgColor;
                }
                else if (installed && (string.IsNullOrEmpty(localVersion) || localVersion == "(unknown)"))
                {
                    // Draw colored label for installed but unknown version
                    GUIStyle installedLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        padding = new RectOffset(6, 6, 2, 2),
                        normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } // Grey text
                    };
                    
                    Color originalBgColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 0.2f);
                    
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Installed", installedLabelStyle);
                        GUILayout.FlexibleSpace();
                    }
                    
                    GUI.backgroundColor = originalBgColor;
                }
            }

            GUI.backgroundColor = originalBackground;
        }

        private void DrawImageOnlyCard(ModelIndex.Entry entry, float thumbnailSize, bool highlight = false)
        {
            string key = entry.id + "@" + entry.latestVersion;
            if (!_metaCache.ContainsKey(key) && !_loadingMeta.Contains(key))
            {
                _ = LoadMetaAsync(entry.id, entry.latestVersion);
            }

            string thumbKey = key + "#thumb";
            Texture2D thumbnail;
            bool hasThumbnail = TryGetThumbnailFromCache(thumbKey, out thumbnail);
            bool isLoadingThumbnail = _loadingThumbnails.Contains(thumbKey);
            bool isLoadingMeta = _loadingMeta.Contains(key);

            Rect thumbRect = GUILayoutUtility.GetRect(thumbnailSize, thumbnailSize, GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize));

            if (highlight)
            {
                Rect outline = new Rect(thumbRect.x - 3f, thumbRect.y - 3f, thumbRect.width + 6f, thumbRect.height + 6f);
                EditorGUI.DrawRect(outline, new Color(0.25f, 0.45f, 0.7f, 0.3f));
            }

            string tooltip = BuildModelTooltip(entry, key);

            if (hasThumbnail && thumbnail != null)
            {
                GUIContent thumbContent = new GUIContent(thumbnail, tooltip);
                if (GUI.Button(thumbRect, thumbContent, GUIStyle.none))
                {
                    ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                }
            }
            else
            {
                DrawThumbnailPlaceholder(thumbRect, entry.name, isLoadingThumbnail || isLoadingMeta, () =>
                {
                    ModelDetailsWindow.Open(entry.id, entry.latestVersion);
                });

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

            bool hasUpdate = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;
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

        private void DrawThumbnailPlaceholder(Rect rect, string modelName, bool isLoading, Action onClick)
        {
            Color backgroundColor = new Color(0.25f, 0.25f, 0.3f, 1f);
            EditorGUI.DrawRect(rect, backgroundColor);

            Color borderColor = new Color(0.4f, 0.4f, 0.45f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1f, rect.width, 1f), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), borderColor);
            EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1f, rect.y, 1f, rect.height), borderColor);

            GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(4, 4, 4, 4)
            };

            if (isLoading)
            {
                float time = (float)EditorApplication.timeSinceStartup;
                int frame = Mathf.FloorToInt(time * 2f) % 4;
                string[] loadingFrames = { "◐", "◓", "◑", "◒" };
                string loadingText = string.Concat(loadingFrames[frame], " Loading...");

                centeredStyle.normal.textColor = new Color(0.7f, 0.7f, 0.8f, 1f);
                GUI.Label(rect, loadingText, centeredStyle);
            }
            else
            {
                centeredStyle.normal.textColor = new Color(0.5f, 0.5f, 0.6f, 1f);
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

            bool hasUpdate = hasUpdateFromCache || hasUpdateLocally;

            ModelLibraryUIDrawer.DrawNotificationBadges(hasNotes, hasUpdate, notesTooltip);
        }

        private void DrawCompactNotificationBadges(ModelIndex.Entry entry)
        {
            bool hasNotes = HasNotes(entry.id, entry.latestVersion);
            bool hasUpdate = _modelUpdateStatus.TryGetValue(entry.id, out bool updateStatus) && updateStatus;

            bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
            if (installed && localMeta != null && !string.IsNullOrEmpty(localMeta.version) && localMeta.version != "(unknown)")
            {
                hasUpdate = hasUpdate || ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);
            }

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
                UnifiedSettingsWindow.Open();
                currentEvent.Use();
                return;
            }

            if (ctrlOrCmd && shift && currentEvent.keyCode == KeyCode.L)
            {
                ModelLibraryShortcutsWindow.Open();
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
                        ModelDetailsWindow.Open(openEntry.id, openEntry.latestVersion);
                        currentEvent.Use();
                        return;
                    }
                }
            }
        }

        private void ShowSearchHelpDialog() => ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Searching);

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
    }
}

