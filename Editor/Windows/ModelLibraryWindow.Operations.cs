using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    public partial class ModelLibraryWindow
    {
        private async Task Download(string id, string version)
        {
            try
            {
                ModelDownloader downloader = new ModelDownloader(_service);
                // UX (HIGH-08): Use DisplayCancelableProgressBar so the user can cancel.
                if (EditorUtility.DisplayCancelableProgressBar("Downloading Model", "Connecting to repository...", ProgressBarConstants.INITIAL))
                {
                    return;
                }

                (string root, ModelMeta meta) = await downloader.DownloadAsync(id, version);

                if (EditorUtility.DisplayCancelableProgressBar("Downloading Model", "Download complete", ProgressBarConstants.COMPLETE))
                {
                    return;
                }
                await Task.Delay(DelayConstants.UI_UPDATE_DELAY_MS);

                ShowNotification("Downloaded", $"Cached at: {root}");
                Debug.Log($"Model cached at: {root}");
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowErrorWithRetry("Download Failed",
                    "The model could not be downloaded from the repository.",
                    () => _ = Download(id, version), ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private async Task Import(string id, string version, bool isUpgrade = false, string previousVersion = null)
        {
            if (_importsInProgress.Contains(id))
            {
                return;
            }

            _importsInProgress.Add(id);
            _importCancellation[id] = false;
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            _importCancellationTokens[id] = cancellationTokenSource;
            string progressTitle = isUpgrade ? "Updating Model" : "Importing Model";

            try
            {
                titleContent.text = $"Model Library - {progressTitle}...";

                ModelDownloader downloader = new ModelDownloader(_service);
                EditorUtility.DisplayProgressBar(progressTitle, "Downloading from repository...", ProgressBarConstants.INITIAL);

                if (_importCancellation.TryGetValue(id, out bool cancelled) && cancelled)
                {
                    cancellationTokenSource.Cancel();
                    return;
                }

                (string root, ModelMeta meta) = await downloader.DownloadAsync(id, version);

                if (_importCancellation.TryGetValue(id, out cancelled) && cancelled)
                {
                    cancellationTokenSource.Cancel();
                    return;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Preparing import...", ProgressBarConstants.PREPARING);

                ModelMeta localInstallMeta = null;
                if (_localInstallCache.TryGetValue(id, out ModelMeta cachedLocalMeta) && cachedLocalMeta != null)
                {
                    localInstallMeta = cachedLocalMeta;
                }
                else if (_manifestCache.TryGetValue(id, out ModelMeta cachedManifestMeta) && cachedManifestMeta != null)
                {
                    localInstallMeta = cachedManifestMeta;
                }

                // New version meta.installPath is the source of truth; do not prefer local path on update.
                string defaultInstallPath = _installPathHelper.DetermineInstallPath(meta, localInstallMeta, preferredInstallPath: null);
                string modelName = meta.identity?.name ?? "Model";
                bool allowExistingModelContent = isUpgrade || localInstallMeta != null;
                InstallPathValidator.ValidationResult storedPathValidation = InstallPathValidator.Validate(
                    defaultInstallPath,
                    modelName,
                    allowExistingModelContent,
                    InstallPathValidator.InstallPathValidationMode.Import);
                string resolvedStoredInstallPath = !string.IsNullOrWhiteSpace(storedPathValidation.SuggestedInstallPath)
                    ? storedPathValidation.SuggestedInstallPath
                    : defaultInstallPath;
                string chosenInstallPath = resolvedStoredInstallPath;

                if (!storedPathValidation.IsValid)
                {
                    chosenInstallPath = _installPathHelper.ResolveInstallPathForImport(
                        meta,
                        allowExistingModelContent,
                        localInstallMeta,
                        preferredInstallPath: null);
                    if (string.IsNullOrEmpty(chosenInstallPath))
                    {
                        _importCancellation[id] = true;
                        cancellationTokenSource.Cancel();
                        titleContent.text = "Model Library";
                        return;
                    }
                }
                else if (isUpgrade || localInstallMeta != null)
                {
                    chosenInstallPath = resolvedStoredInstallPath;
                }
                else
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Import Model",
                        $"Select an install location for '{meta.identity.name}'.\nStored path: {resolvedStoredInstallPath}",
                        "Use Stored Path",
                        "Choose Folder...",
                        "Cancel");

                    if (choice == 2)
                    {
                        _importCancellation[id] = true;
                        cancellationTokenSource.Cancel();
                        titleContent.text = "Model Library";
                        return;
                    }

                    if (_importCancellation.TryGetValue(id, out cancelled) && cancelled)
                    {
                        cancellationTokenSource.Cancel();
                        return;
                    }

                    if (choice == 1)
                    {
                        string custom = _installPathHelper.PromptForInstallPathWithValidation(
                            resolvedStoredInstallPath,
                            modelName,
                            allowExistingModelContent);
                        if (string.IsNullOrEmpty(custom))
                        {
                            _importCancellation[id] = true;
                            cancellationTokenSource.Cancel();
                            titleContent.text = "Model Library";
                            return;
                        }
                        chosenInstallPath = custom;
                    }
                }

                if (allowExistingModelContent && !_installPathHelper.ConfirmModelUpdateOverwrite(modelName, chosenInstallPath))
                {
                    _importCancellation[id] = true;
                    cancellationTokenSource.Cancel();
                    titleContent.text = "Model Library";
                    return;
                }

                if (_importCancellation.TryGetValue(id, out cancelled) && cancelled)
                {
                    cancellationTokenSource.Cancel();
                    return;
                }

                string oldInstallPath = localInstallMeta != null ? localInstallMeta.installPath : null;
                bool installPathChanged = InstalledModelAssetCleaner.HasInstallPathChanged(oldInstallPath, chosenInstallPath);
                bool cleanDestination = !installPathChanged;

                EditorUtility.DisplayProgressBar(progressTitle, "Copying files to Assets folder...", ProgressBarConstants.COPYING);
                await ModelProjectImporter.ImportFromCacheAsync(root, meta, cleanDestination, chosenInstallPath, isUpgrade, cancellationTokenSource.Token);

                if (installPathChanged && localInstallMeta != null)
                {
                    InstalledModelAssetCleaner.DeleteAssetsFromManifest(localInstallMeta);
                }

                if (_importCancellation.TryGetValue(id, out cancelled) && cancelled)
                {
                    cancellationTokenSource.Cancel();
                    return;
                }

                EditorUtility.DisplayProgressBar(progressTitle, "Finalizing import...", ProgressBarConstants.FINALIZING);
                await Task.Delay(DelayConstants.UI_UPDATE_DELAY_MS);

                InvalidateLocalInstallCache(id);
                
                // Read the manifest file we just created and add it to the cache
                // This ensures the cache is immediately updated with the actual file content
                // Use new naming convention (.modelLibrary.meta.json) with fallback for old files
                string manifestPath = Path.Combine(chosenInstallPath, ".modelLibrary.meta.json");
                if (!File.Exists(manifestPath))
                {
                    // Fallback for old files created before the naming change
                    manifestPath = Path.Combine(chosenInstallPath, "modelLibrary.meta.json");
                }
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(manifestPath);
                        ModelMeta manifestMeta = JsonUtil.FromJson<ModelMeta>(json);
                        if (manifestMeta != null && manifestMeta.identity != null && manifestMeta.identity.id == id)
                        {
                            // Use the manifest file's meta, which has the correct local path info
                            _localInstallCache[id] = manifestMeta;
                            _manifestCache[id] = manifestMeta;
                        }
                        else
                        {
                            // Fallback to the meta we have
                            _localInstallCache[id] = meta;
                            _manifestCache[id] = meta;
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("Read Manifest Failed", 
                            $"Failed to read manifest file {manifestPath}: {ex.Message}. Using provided meta.", 
                            ErrorHandler.CategorizeException(ex), ex, $"ManifestPath: {manifestPath}, ModelId: {id}");
                        // Fallback to the meta we have
                        _localInstallCache[id] = meta;
                        _manifestCache[id] = meta;
                    }
                }
                else
                {
                    // Manifest file doesn't exist yet, use the meta we have
                    _localInstallCache[id] = meta;
                    _manifestCache[id] = meta;
                }
                
                _negativeCache.Remove(id);
                
                // Ensure manifest cache is marked as initialized so TryGetLocalInstall can use it
                if (!_manifestCacheInitialized)
                {
                    _manifestCacheInitialized = true;
                }
                
                // Force a repaint to update the UI immediately with the new cache state
                // This ensures the browser view shows the model as installed right away
                Repaint();
                
                // Trigger a background refresh of the manifest cache to ensure it's up to date
                // This will pick up any other manifest files and ensure consistency
                // Use delayCall to avoid clearing the cache we just added
                EditorApplication.delayCall += () => _ = RefreshManifestCacheAsync();

                AddToImportHistory(new ImportHistoryEntry
                {
                    modelId = id,
                    version = version,
                    installPath = chosenInstallPath,
                    importedAssets = meta.assetGuids?.ToList() ?? new List<string>(),
                    timestamp = DateTime.Now
                });

                _recentlyUsedManager.AddToRecentlyUsed(id);

                // STABILITY (MED-20): Null-safe access to meta.identity.name.
                // Older manifests or malformed metadata may have identity == null.
                string modelDisplayName = meta.identity?.name ?? "Unknown";

                AnalyticsService.RecordEvent(isUpgrade ? "update" : "import", id, version, modelDisplayName);

                string message = isUpgrade && !string.IsNullOrEmpty(previousVersion)
                    ? $"Updated '{modelDisplayName}' from v{previousVersion} to v{meta.version} at {chosenInstallPath}."
                    : $"Imported '{modelDisplayName}' v{meta.version} to {chosenInstallPath}.";
                ShowNotification(isUpgrade ? "Update Complete" : "Import Complete", message);
                Debug.Log($"{(isUpgrade ? "Update" : "Import")} Complete: {message}");
                Repaint();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException || (_importCancellation.TryGetValue(id, out bool cancelled) && cancelled))
                {
                    ShowNotification("Import Cancelled", $"The import of '{id}' was cancelled.");
                    Debug.Log($"Import cancelled: {id}");
                }
                else
                {
                    string operation = isUpgrade ? "Update" : "Import";
                    string operationLower = operation.ToLowerInvariant();
                    ErrorHandler.ShowErrorWithRetry($"{operation} Failed",
                        $"The model could not be {operationLower}ed into your project.",
                        () => _ = Import(id, version, isUpgrade, previousVersion), ex);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                titleContent.text = "Model Library";
                _importsInProgress.Remove(id);
                _importCancellation.Remove(id);
                if (_importCancellationTokens.TryGetValue(id, out CancellationTokenSource cts))
                {
                    cts.Dispose();
                    _importCancellationTokens.Remove(id);
                }
            }
        }

        public void CancelImport(string modelId)
        {
            if (_importsInProgress.Contains(modelId))
            {
                _importCancellation[modelId] = true;
                if (_importCancellationTokens.TryGetValue(modelId, out CancellationTokenSource cts))
                {
                    cts.Cancel();
                }
            }
        }

        public void UndoLastImport()
        {
            if (_importHistory.Count == 0)
            {
                ShowNotification("No Undo Available", "No recent imports to undo.");
                return;
            }

            ImportHistoryEntry lastImport = _importHistory[0];

            if (!EditorUtility.DisplayDialog("Undo Last Import",
                string.Concat("Are you sure you want to undo the import of '", lastImport.modelId, "' v", lastImport.version, "?\n\nThis will remove the imported assets from:\n", lastImport.installPath),
                "Yes, Undo", "Cancel"))
            {
                return;
            }

            try
            {
                int removedCount = 0;
                foreach (string guid in lastImport.importedAssets)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.DeleteAsset(assetPath))
                    {
                        removedCount++;
                    }
                }

                // Use new naming convention (.modelLibrary.meta.json) with fallback for old files
                string manifestPath = Path.Combine(lastImport.installPath, ".modelLibrary.meta.json");
                if (!File.Exists(manifestPath))
                {
                    // Fallback for old files created before the naming change
                    manifestPath = Path.Combine(lastImport.installPath, "modelLibrary.meta.json");
                }
                if (File.Exists(manifestPath))
                {
                    AssetDatabase.DeleteAsset(manifestPath);
                }

                InvalidateLocalInstallCache(lastImport.modelId);
                _localInstallCache.Remove(lastImport.modelId);
                _manifestCache.Remove(lastImport.modelId);

                _importHistory.RemoveAt(0);
                SaveImportHistory();

                AssetDatabase.Refresh();

                ShowNotification("Import Undone", $"Removed {removedCount} assets from {lastImport.installPath}");
                Debug.Log($"Undo import: Removed {removedCount} assets");
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowError("Undo Failed",
                    "The previous import could not be undone. Some files may have already been removed or modified.", ex);
            }
        }

        private async Task BulkImportAsync()
        {
            if (_selectedModels.Count == 0)
            {
                return;
            }

            List<string> modelIds = _selectedModels.ToList();
            int total = modelIds.Count;
            int current = 0;
            int successCount = 0;
            int failCount = 0;

            foreach (string modelId in modelIds)
            {
                current++;
                try
                {
                    ModelIndex.Entry entry = _indexCache?.entries?.FirstOrDefault(e => e.id == modelId);
                    if (entry == null)
                    {
                        failCount++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Bulk Import", $"Importing {entry.name} ({current}/{total})...", (float)current / total);

                    bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                    bool needsUpgrade = installed && !string.IsNullOrEmpty(localMeta.version) && ModelVersionUtils.NeedsUpgrade(localMeta.version, entry.latestVersion);

                    await Import(entry.id, entry.latestVersion, needsUpgrade, installed ? localMeta.version : null);
                    successCount++;
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Bulk Import Failed", 
                        $"Failed to import model {modelId}: {ex.Message}", 
                        ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}, Progress: {current}/{total}");
                    failCount++;
                }
            }

            EditorUtility.ClearProgressBar();

            string message = $"Bulk Import Complete! Successful: {successCount}, Failed: {failCount}";
            ShowNotification("Bulk Import Results", message);
            Debug.Log($"Bulk Import: {message}");

            _selectedModels.Clear();
            _bulkSelectionMode = false;
            Repaint();
        }

        private async Task BulkUpdateAsync()
        {
            if (_selectedModels.Count == 0)
            {
                return;
            }

            List<string> modelsWithUpdates = new List<string>();
            foreach (string modelId in _selectedModels)
            {
                if (_modelUpdateStatus.TryGetValue(modelId, out bool hasUpdate) && hasUpdate)
                {
                    modelsWithUpdates.Add(modelId);
                }
            }

            if (modelsWithUpdates.Count == 0)
            {
                ShowNotification("No Updates", "None of the selected models have available updates.");
                return;
            }

            int total = modelsWithUpdates.Count;
            int current = 0;
            int successCount = 0;
            int failCount = 0;

            foreach (string modelId in modelsWithUpdates)
            {
                current++;
                try
                {
                    ModelIndex.Entry entry = _indexCache?.entries?.FirstOrDefault(e => e.id == modelId);
                    if (entry == null)
                    {
                        failCount++;
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Bulk Update", $"Updating {entry.name} ({current}/{total})...", (float)current / total);

                    bool installed = TryGetLocalInstall(entry, out ModelMeta localMeta);
                    string previousVersion = installed ? localMeta.version : null;

                    await Import(entry.id, entry.latestVersion, true, previousVersion);
                    successCount++;
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Bulk Update Failed", 
                        $"Failed to update model {modelId}: {ex.Message}", 
                        ErrorHandler.CategorizeException(ex), ex, $"ModelId: {modelId}, Progress: {current}/{total}");
                    failCount++;
                }
            }

            EditorUtility.ClearProgressBar();

            string message = $"Bulk Update Complete! Successful: {successCount}, Failed: {failCount}";
            ShowNotification("Bulk Update Results", message);
            Debug.Log($"Bulk Update: {message}");

            _selectedModels.Clear();
            _bulkSelectionMode = false;
            Repaint();
        }

        private void OpenBulkTagWindow()
        {
            if (_service == null || _indexCache?.entries == null || _selectedModels.Count == 0)
            {
                return;
            }

            List<ModelIndex.Entry> selectedEntries = _indexCache.entries
                .Where(entry => _selectedModels.Contains(entry.id))
                .ToList();

            LaunchBulkTagEditor(selectedEntries);
        }

        private void LaunchBulkTagEditor(List<ModelIndex.Entry> entries)
        {
            if (_service == null || entries == null || entries.Count == 0)
            {
                return;
            }

            ModelBulkTagWindow.Open(_service, entries, () =>
            {
                // STABILITY (audit CRIT-11): Previously this was
                //   EditorApplication.delayCall += async () => { ... };
                // which created an `async void` lambda (because delayCall is
                // Action). Any exception thrown by LoadIndexAsync would be
                // unhandled and become UnobservedTaskException. We now wrap
                // the async work in a proper try/catch via RefreshAfterBulkTagAsync.
                EditorApplication.delayCall += () =>
                {
                    _ = RefreshAfterBulkTagAsync();
                };
            });
        }

        /// <summary>
        /// Refreshes the index cache and tag cache after a bulk-tag operation.
        /// STABILITY (audit CRIT-11): Wrapped in try/catch so exceptions
        /// are logged instead of becoming UnobservedTaskException.
        /// </summary>
        private async Task RefreshAfterBulkTagAsync()
        {
            try
            {
                _indexCache = null;
                await LoadIndexAsync();
                _tagCacheManager.UpdateTagCache(_indexCache);
                Repaint();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Refresh After Bulk Tag",
                    $"Failed to refresh after bulk tag operation: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex);
            }
        }

        private void AddToImportHistory(ImportHistoryEntry entry)
        {
            _importHistory.Insert(0, entry);
            if (_importHistory.Count > __MAX_IMPORT_HISTORY)
            {
                _importHistory.RemoveAt(_importHistory.Count - 1);
            }
            SaveImportHistory();
        }

        private void SaveImportHistory()
        {
            try
            {
                string json = JsonUtility.ToJson(new ImportHistoryWrapper { entries = _importHistory });
                EditorPrefs.SetString(__IMPORT_HISTORY_PREF_KEY, json);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Save Import History Failed", 
                    $"Failed to save import history: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex);
            }
        }

        private void LoadImportHistory()
        {
            try
            {
                string json = EditorPrefs.GetString(__IMPORT_HISTORY_PREF_KEY, string.Empty);
                if (!string.IsNullOrEmpty(json))
                {
                    ImportHistoryWrapper wrapper = JsonUtility.FromJson<ImportHistoryWrapper>(json);
                    if (wrapper?.entries != null)
                    {
                        _importHistory.Clear();
                        _importHistory.AddRange(wrapper.entries);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Load Import History Failed", 
                    $"Failed to load import history: {ex.Message}", 
                    ErrorHandler.CategorizeException(ex), ex);
            }
        }
    }
}

