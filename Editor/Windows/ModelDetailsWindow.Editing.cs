using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Repository;
using ModelLibrary.Editor.Services;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing metadata editing functionality for ModelDetailsWindow.
    /// </summary>
    public partial class ModelDetailsWindow
    {
        private async Task SaveMetadataChangesAsync()
        {
            if (_meta == null || string.IsNullOrEmpty(_baselineMetaJson))
            {
                return;
            }

            _meta.tags ??= new Tags();

            if (_editingTags)
            {
                _meta.tags.values = new List<string>(_editableTags);
                _editingTags = false;
            }

            string trimmedDescription = string.IsNullOrEmpty(_editedDescription) ? string.Empty : _editedDescription.Trim();
            _meta.description = trimmedDescription;

            ModelMeta before = JsonUtil.FromJson<ModelMeta>(_baselineMetaJson);
            string summary = BuildMetadataChangeSummary(before, _meta);
            if (string.IsNullOrEmpty(summary))
            {
                EditorUtility.DisplayDialog("No Changes", "There are no metadata changes to save.", "OK");
                _editableTags = new List<string>(_meta.tags?.values ?? new List<string>());
                return;
            }

            try
            {
                _isSavingMetadata = true;
                string author = new SimpleUserIdentityProvider().GetUserName();
                await _service.PublishMetadataUpdateAsync(_meta, _version, summary, author);
                _version = _meta.version;
                await Load();
                EditorUtility.DisplayDialog("Metadata Updated", $"Published version {_version}.", "OK");
            }
            catch (Exception ex)
            {
                ErrorHandler.ShowErrorWithRetry("Save Failed", $"Failed to save metadata: {ex.Message}",
                    async () => await SaveMeta(), ex);
                await Load();
            }
            finally
            {
                _isSavingMetadata = false;
            }
        }

        private static string BuildMetadataChangeSummary(ModelMeta before, ModelMeta after)
        {
            if (after == null)
            {
                return null;
            }

            List<string> parts = new List<string>();
            string beforeDescription = before?.description ?? string.Empty;
            string afterDescription = after.description ?? string.Empty;
            if (!string.Equals(beforeDescription.Trim(), afterDescription.Trim(), StringComparison.Ordinal))
            {
                parts.Add("Description updated");
            }

            HashSet<string> beforeTags = new HashSet<string>(before?.tags?.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            HashSet<string> afterTags = new HashSet<string>(after.tags?.values ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            List<string> added = new List<string>();
            foreach (string tag in afterTags)
            {
                if (!beforeTags.Contains(tag))
                {
                    added.Add(tag);
                }
            }

            List<string> removed = new List<string>();
            foreach (string tag in beforeTags)
            {
                if (!afterTags.Contains(tag))
                {
                    removed.Add(tag);
                }
            }

            if (added.Count > 0 || removed.Count > 0)
            {
                string part = "Tags updated";
                if (added.Count > 0)
                {
                    part += $" (+{string.Join(", ", added)})";
                }
                if (removed.Count > 0)
                {
                    part += $" (-{string.Join(", ", removed)})";
                }
                parts.Add(part);
            }

            return parts.Count > 0 ? string.Join("; ", parts) : null;
        }

        private async Task SaveMeta()
        {
            try
            {
                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                IModelRepository repo = settings.repositoryKind == ModelLibrarySettings.RepositoryKind.FileSystem
                    ? new Repository.FileSystemRepository(settings.repositoryRoot)
                    : new Repository.HttpRepository(settings.repositoryRoot);
                await repo.SaveMetaAsync(_modelId, _version, _meta);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Save Failed", ex.Message, "OK");
            }
        }
    }
}


