using System;
using System.Collections.Generic;

namespace ModelLibrary.Data
{
    /// <summary>
    /// One stored model-id to version-list pair from <see cref="ModelIndex.versions"/>.
    /// </summary>
    [Serializable]
    public class ModelVersionListEntry
    {
        /// <summary>Model id.</summary>
        public string key;

        /// <summary>Known versions for that model.</summary>
        public List<string> versions = new List<string>();
    }

    public partial class ModelIndex
    {
        /// <summary>
        /// Serializable form of <see cref="versions"/>. JsonUtility cannot store dictionaries.
        /// </summary>
        public List<ModelVersionListEntry> versionEntries = new List<ModelVersionListEntry>();

        private Dictionary<string, List<string>> _versions;
        private bool _versionsMaterialized;

        /// <summary>
        /// Optional map of model ID to known version list.
        /// This map may be empty when only the latest version is tracked.
        /// </summary>
        public Dictionary<string, List<string>> versions
        {
            get
            {
                if (!_versionsMaterialized)
                {
                    _versions = ReadVersionEntries(versionEntries);
                    _versionsMaterialized = true;
                }

                return _versions;
            }
            set
            {
                _versions = value ?? new Dictionary<string, List<string>>();
                _versionsMaterialized = true;
                versionEntries = WriteVersionEntries(_versions);
            }
        }

        /// <summary>
        /// Copies the version map into the list written by JsonUtility.
        /// </summary>
        internal void WriteSerializedEntries()
        {
            if (_versionsMaterialized)
            {
                versionEntries = WriteVersionEntries(_versions);
            }
        }

        /// <summary>
        /// Rebuilds the version map from the list JsonUtility just loaded.
        /// </summary>
        internal void ReadSerializedEntries()
        {
            if (versionEntries == null)
            {
                versionEntries = new List<ModelVersionListEntry>();
            }

            _versions = ReadVersionEntries(versionEntries);
            _versionsMaterialized = true;
        }

        private static Dictionary<string, List<string>> ReadVersionEntries(List<ModelVersionListEntry> entries)
        {
            Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
            if (entries == null)
            {
                return dictionary;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModelVersionListEntry entry = entries[i];
                if (entry == null || entry.key == null)
                {
                    continue;
                }

                List<string> versions = new List<string>();
                if (entry.versions != null)
                {
                    for (int versionIndex = 0; versionIndex < entry.versions.Count; versionIndex++)
                    {
                        versions.Add(entry.versions[versionIndex]);
                    }
                }

                dictionary[entry.key] = versions;
            }

            return dictionary;
        }

        private static List<ModelVersionListEntry> WriteVersionEntries(Dictionary<string, List<string>> source)
        {
            List<ModelVersionListEntry> entries = new List<ModelVersionListEntry>();
            if (source == null)
            {
                return entries;
            }

            List<string> keys = new List<string>();
            foreach (string key in source.Keys)
            {
                keys.Add(key);
            }

            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                ModelVersionListEntry entry = new ModelVersionListEntry();
                entry.key = key;
                entry.versions = new List<string>();
                List<string> versions = source[key];
                if (versions != null)
                {
                    for (int versionIndex = 0; versionIndex < versions.Count; versionIndex++)
                    {
                        entry.versions.Add(versions[versionIndex]);
                    }
                }

                entries.Add(entry);
            }

            return entries;
        }
    }
}
