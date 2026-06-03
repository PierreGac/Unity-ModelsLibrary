using System;
using System.Collections.Generic;

namespace ModelLibrary.Data
{
    /// <summary>
    /// Result of scanning the repository and optionally rebuilding models_index.json.
    /// </summary>
    [Serializable]
    public class ModelIndexRebuildReport
    {
        /// <summary>Whether the operation completed without fatal errors.</summary>
        public bool success;

        /// <summary>True when only a preview scan was performed (no index written).</summary>
        public bool isPreview;

        /// <summary>Number of top-level model folders that contained at least one valid model.json.</summary>
        public int discoveredModelFolderCount;

        /// <summary>Number of entries in the rebuilt or preview index.</summary>
        public int indexEntryCount;

        /// <summary>Number of entries in models_index.json before rebuild (0 if missing).</summary>
        public int previousIndexEntryCount;

        /// <summary>Folder names skipped (no valid model.json in any version subfolder).</summary>
        public List<string> skippedFolders = new List<string>();

        /// <summary>Non-fatal warnings (e.g. identity id vs folder name mismatch).</summary>
        public List<string> warnings = new List<string>();

        /// <summary>Fatal or per-folder errors (e.g. failed to load model.json).</summary>
        public List<string> errors = new List<string>();

        /// <summary>Path to the backup file if one was created.</summary>
        public string backupPath;
    }
}
