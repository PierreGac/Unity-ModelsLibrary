using System;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// Information about a model's local install versus the latest repository version.
    /// </summary>
    public class ModelUpdateInfo
    {
        /// <summary>Catalog model identifier.</summary>
        public string modelId { get; set; }

        /// <summary>Display name of the model.</summary>
        public string modelName { get; set; }

        /// <summary>Version currently installed in the project, if any.</summary>
        public string localVersion { get; set; }

        /// <summary>Latest version available in the repository.</summary>
        public string remoteVersion { get; set; }

        /// <summary>True when the repository version is newer than the local install.</summary>
        public bool hasUpdate { get; set; }

        /// <summary>UTC time of the last update check for this model.</summary>
        public DateTime lastChecked { get; set; }

        /// <summary>Optional description of the available update.</summary>
        public string updateDescription { get; set; }
    }
}
