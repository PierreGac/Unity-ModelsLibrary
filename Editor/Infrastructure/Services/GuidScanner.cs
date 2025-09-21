#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelLibrary.Data;
using UnityEditor;

namespace ModelLibrary.Editor.Services
{
    /// <summary>
    /// GUID-based project model detection.
    /// </summary>
    public static class GuidScanner
    {
        public static async Task<Dictionary<string, string>> FindInstalledModelVersionsAsync(IEnumerable<ModelIndex.Entry> entries, System.Func<string, Task<ModelMeta>> metaLoader)
        {
            string[] all = AssetDatabase.FindAssets("");
            HashSet<string> set = new HashSet<string>(all);
            Dictionary<string, string> map = new Dictionary<string, string>(); // modelId → localVersion

            foreach (ModelIndex.Entry e in entries)
            {
                ModelMeta meta = await metaLoader(e.id);
                if (meta == null)
                {
                    continue;
                }

                if (meta.assetGuids.Any(g => set.Contains(g)))
                {
                    // At least some assets present. We mark as present; version heuristic minimal.
                    map[e.id] = e.latestVersion;
                }
            }
            return map;
        }
    }
}
#endif


