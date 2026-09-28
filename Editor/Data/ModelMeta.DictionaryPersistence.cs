using System;
using System.Collections.Generic;

namespace ModelLibrary.Data
{
    /// <summary>
    /// One stored pair from <see cref="ModelMeta.extra"/>.
    /// </summary>
    [Serializable]
    public class ModelMetaExtraEntry
    {
        /// <summary>Dictionary key.</summary>
        public string key;

        /// <summary>Dictionary value.</summary>
        public string value;
    }

    /// <summary>
    /// One stored pair from <see cref="ModelMeta.modelImporters"/>.
    /// </summary>
    [Serializable]
    public class ModelImporterDictionaryEntry
    {
        /// <summary>Payload-relative path.</summary>
        public string key;

        /// <summary>Importer settings captured for that path.</summary>
        public ModelImporterSettings value;
    }

    public partial class ModelMeta
    {
        /// <summary>
        /// Serializable form of <see cref="extra"/>. JsonUtility cannot store dictionaries.
        /// </summary>
        public List<ModelMetaExtraEntry> extraEntries = new List<ModelMetaExtraEntry>();

        /// <summary>
        /// Serializable form of <see cref="modelImporters"/>. JsonUtility cannot store dictionaries.
        /// </summary>
        public List<ModelImporterDictionaryEntry> modelImporterEntries = new List<ModelImporterDictionaryEntry>();

        private Dictionary<string, string> _extra;
        private Dictionary<string, ModelImporterSettings> _modelImporters;
        private bool _extraMaterialized;
        private bool _modelImportersMaterialized;

        /// <summary>
        /// Extra key-value pairs for future extensibility.
        /// Examples: "polyCount": "1500", "textureSize": "1024x1024".
        /// </summary>
        public Dictionary<string, string> extra
        {
            get
            {
                if (!_extraMaterialized)
                {
                    _extra = ReadStringEntries(extraEntries);
                    _extraMaterialized = true;
                }

                return _extra;
            }
            set
            {
                _extra = value ?? new Dictionary<string, string>();
                _extraMaterialized = true;
                extraEntries = WriteStringEntries(_extra);
            }
        }

        /// <summary>
        /// Per-FBX/OBJ importer settings captured at submit time, keyed by payload-relative path.
        /// </summary>
        public Dictionary<string, ModelImporterSettings> modelImporters
        {
            get
            {
                if (!_modelImportersMaterialized)
                {
                    _modelImporters = ReadImporterEntries(modelImporterEntries);
                    _modelImportersMaterialized = true;
                }

                return _modelImporters;
            }
            set
            {
                _modelImporters = value ?? new Dictionary<string, ModelImporterSettings>();
                _modelImportersMaterialized = true;
                modelImporterEntries = WriteImporterEntries(_modelImporters);
            }
        }

        /// <summary>
        /// Copies materialized dictionaries into the lists written by JsonUtility.
        /// </summary>
        internal void WriteSerializedEntries()
        {
            if (_extraMaterialized)
            {
                extraEntries = WriteStringEntries(_extra);
            }

            if (_modelImportersMaterialized)
            {
                modelImporterEntries = WriteImporterEntries(_modelImporters);
            }
        }

        /// <summary>
        /// Rebuilds the dictionaries from the lists JsonUtility just loaded.
        /// </summary>
        internal void ReadSerializedEntries()
        {
            if (extraEntries == null)
            {
                extraEntries = new List<ModelMetaExtraEntry>();
            }

            if (modelImporterEntries == null)
            {
                modelImporterEntries = new List<ModelImporterDictionaryEntry>();
            }

            _extra = ReadStringEntries(extraEntries);
            _extraMaterialized = true;
            _modelImporters = ReadImporterEntries(modelImporterEntries);
            _modelImportersMaterialized = true;
        }

        private static Dictionary<string, string> ReadStringEntries(List<ModelMetaExtraEntry> entries)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            if (entries == null)
            {
                return dictionary;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModelMetaExtraEntry entry = entries[i];
                if (entry == null || entry.key == null)
                {
                    continue;
                }

                dictionary[entry.key] = entry.value ?? string.Empty;
            }

            return dictionary;
        }

        private static List<ModelMetaExtraEntry> WriteStringEntries(Dictionary<string, string> source)
        {
            List<ModelMetaExtraEntry> entries = new List<ModelMetaExtraEntry>();
            if (source == null)
            {
                return entries;
            }

            List<string> keys = CopySortedKeys(source);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                ModelMetaExtraEntry entry = new ModelMetaExtraEntry();
                entry.key = key;
                entry.value = source[key] ?? string.Empty;
                entries.Add(entry);
            }

            return entries;
        }

        private static Dictionary<string, ModelImporterSettings> ReadImporterEntries(List<ModelImporterDictionaryEntry> entries)
        {
            Dictionary<string, ModelImporterSettings> dictionary = new Dictionary<string, ModelImporterSettings>();
            if (entries == null)
            {
                return dictionary;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModelImporterDictionaryEntry entry = entries[i];
                if (entry == null || entry.key == null)
                {
                    continue;
                }

                dictionary[entry.key] = entry.value;
            }

            return dictionary;
        }

        private static List<ModelImporterDictionaryEntry> WriteImporterEntries(Dictionary<string, ModelImporterSettings> source)
        {
            List<ModelImporterDictionaryEntry> entries = new List<ModelImporterDictionaryEntry>();
            if (source == null)
            {
                return entries;
            }

            List<string> keys = CopySortedKeys(source);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                ModelImporterDictionaryEntry entry = new ModelImporterDictionaryEntry();
                entry.key = key;
                entry.value = source[key];
                entries.Add(entry);
            }

            return entries;
        }

        private static List<string> CopySortedKeys<TValue>(Dictionary<string, TValue> source)
        {
            List<string> keys = new List<string>();
            foreach (string key in source.Keys)
            {
                keys.Add(key);
            }

            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }
}
