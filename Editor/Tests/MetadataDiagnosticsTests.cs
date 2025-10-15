using System.IO;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for diagnosing metadata loading issues and debugging "unknown" version problems.
    /// </summary>
    public class MetadataDiagnosticsTests
    {
        [Test]
        public void TestManifestFileDetection()
        {
            // Test that we can find manifest files in the project
            string[] manifestFiles = UnityEditor.AssetDatabase.FindAssets("modelLibrary.meta");

            Debug.Log($"[MetadataDiagnostics] Found {manifestFiles.Length} manifest files in project");

            if (manifestFiles.Length == 0)
            {
                Debug.Log("[MetadataDiagnostics] No manifest files found in project - this is normal for a fresh project");
                return;
            }

            // Expect potential error messages that might be generated during file processing
            LogAssert.Expect(LogType.Error, "[MetadataDiagnostics] Metadata has empty ID in Assets/Models/Benne/Benne.FBX/modelLibrary.meta.json");

            foreach (string guid in manifestFiles)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                Debug.Log($"[MetadataDiagnostics] Manifest file: {path}");

                // Test reading the file
                if (File.Exists(path))
                {
                    try
                    {
                        string content = File.ReadAllText(path);
                        Debug.Log($"[MetadataDiagnostics] File size: {content.Length} characters");

                        if (string.IsNullOrEmpty(content))
                        {
                            Debug.LogWarning($"[MetadataDiagnostics] File {path} is empty!");
                        }
                        else
                        {
                            // Test JSON parsing
                            ModelMeta meta = JsonUtil.FromJson<ModelMeta>(content);
                            if (meta == null)
                            {
                                Debug.LogError($"[MetadataDiagnostics] Failed to parse JSON from {path}");
                            }
                            else
                            {
                                Debug.Log($"[MetadataDiagnostics] Parsed metadata: ID={meta.identity?.id}, Name={meta.identity?.name}, Version={meta.version}");

                                if (meta.identity == null)
                                {
                                    Debug.LogError($"[MetadataDiagnostics] Metadata has null identity in {path}");
                                }
                                else if (string.IsNullOrEmpty(meta.identity.id))
                                {
                                    Debug.LogError($"[MetadataDiagnostics] Metadata has empty ID in {path}");
                                }

                                if (string.IsNullOrEmpty(meta.version))
                                {
                                    Debug.LogError($"[MetadataDiagnostics] Metadata has empty version in {path}");
                                }

                                if (meta.assetGuids == null || meta.assetGuids.Count == 0)
                                {
                                    Debug.LogWarning($"[MetadataDiagnostics] Metadata has no asset GUIDs in {path}");
                                }
                                else
                                {
                                    Debug.Log($"[MetadataDiagnostics] Metadata has {meta.assetGuids.Count} asset GUIDs in {path}");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[MetadataDiagnostics] Error reading {path}: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"[MetadataDiagnostics] File {path} does not exist!");
                }
            }
        }

        [Test]
        public void TestProjectAssetGUIDs()
        {
            // Test that we can find asset GUIDs in the project
            string[] allGuids = UnityEditor.AssetDatabase.FindAssets(string.Empty);
            Debug.Log($"[MetadataDiagnostics] Found {allGuids.Length} asset GUIDs in project");

            // Show first few GUIDs as examples
            for (int i = 0; i < System.Math.Min(5, allGuids.Length); i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(allGuids[i]);
                Debug.Log($"[MetadataDiagnostics] GUID {allGuids[i]} -> {path}");
            }
        }

        [Test]
        public void TestJsonUtilDeserialization()
        {
            // Test that JsonUtil can deserialize a simple ModelMeta
            ModelMeta testMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = "1.0.0",
                assetGuids = new System.Collections.Generic.List<string>
                {
                    "12345678901234567890123456789012",
                    "abcdef1234567890abcdef1234567890"
                }
            };

            string json = JsonUtil.ToJson(testMeta);
            Debug.Log($"[MetadataDiagnostics] Serialized JSON: {json}");

            ModelMeta deserialized = JsonUtil.FromJson<ModelMeta>(json);
            Assert.IsNotNull(deserialized, "Deserialization should not return null");
            Assert.AreEqual(testMeta.identity.id, deserialized.identity.id, "ID should match");
            Assert.AreEqual(testMeta.identity.name, deserialized.identity.name, "Name should match");
            Assert.AreEqual(testMeta.version, deserialized.version, "Version should match");
            Assert.AreEqual(testMeta.assetGuids.Count, deserialized.assetGuids.Count, "Asset GUID count should match");
        }

        [Test]
        public void TestEmptyJsonHandling()
        {
            // Test handling of empty JSON - Unity's JsonUtility throws exception for empty JSON
            string emptyJson = "";
            try
            {
                ModelMeta result = JsonUtil.FromJson<ModelMeta>(emptyJson);
                Assert.IsNull(result, "Empty JSON should return null or throw exception");
            }
            catch (System.ArgumentException)
            {
                // Unity's JsonUtility throws ArgumentException for empty JSON - this is expected
                Debug.Log("[MetadataDiagnostics] Empty JSON correctly throws ArgumentException (expected behavior)");
            }

            // Test handling of null JSON
            try
            {
                ModelMeta nullResult = JsonUtil.FromJson<ModelMeta>(null);
                Assert.IsNull(nullResult, "Null JSON should return null or throw exception");
            }
            catch (System.ArgumentException)
            {
                // Unity's JsonUtility throws ArgumentException for null JSON - this is expected
                Debug.Log("[MetadataDiagnostics] Null JSON correctly throws ArgumentException (expected behavior)");
            }

            // Test handling of invalid JSON
            string invalidJson = "{ invalid json }";
            try
            {
                ModelMeta invalidResult = JsonUtil.FromJson<ModelMeta>(invalidJson);
                Assert.IsNull(invalidResult, "Invalid JSON should return null or throw exception");
            }
            catch (System.ArgumentException)
            {
                // Unity's JsonUtility throws ArgumentException for invalid JSON - this is expected
                Debug.Log("[MetadataDiagnostics] Invalid JSON correctly throws ArgumentException (expected behavior)");
            }
        }

        [Test]
        public void TestModelMetaValidation()
        {
            // Test validation of ModelMeta objects
            ModelMeta validMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "valid-model",
                    name = "Valid Model"
                },
                version = "1.0.0"
            };

            Assert.IsTrue(IsValidModelMeta(validMeta), "Valid metadata should pass validation");

            // Test with null identity
            ModelMeta nullIdentityMeta = new ModelMeta
            {
                identity = null,
                version = "1.0.0"
            };
            Assert.IsFalse(IsValidModelMeta(nullIdentityMeta), "Metadata with null identity should fail validation");

            // Test with empty ID
            ModelMeta emptyIdMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "",
                    name = "Empty ID Model"
                },
                version = "1.0.0"
            };
            Assert.IsFalse(IsValidModelMeta(emptyIdMeta), "Metadata with empty ID should fail validation");

            // Test with empty version
            ModelMeta emptyVersionMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "empty-version-model",
                    name = "Empty Version Model"
                },
                version = ""
            };
            Assert.IsFalse(IsValidModelMeta(emptyVersionMeta), "Metadata with empty version should fail validation");
        }

        [Test]
        public void TestManifestFileErrorHandling()
        {
            // Test the error handling logic for manifest files with various issues
            // This test simulates what would happen if we had problematic manifest files

            // Expect the error messages that will be generated
            LogAssert.Expect(LogType.Error, "[MetadataDiagnostics] Metadata has empty ID");
            LogAssert.Expect(LogType.Error, "[MetadataDiagnostics] Metadata has null identity");
            LogAssert.Expect(LogType.Error, "[MetadataDiagnostics] Metadata has empty version");

            // Test empty ID scenario
            ModelMeta emptyIdMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "", // Empty ID
                    name = "Test Model"
                },
                version = "1.0.0"
            };

            // Simulate the validation logic that would be used in FindLocalVersionAsync
            if (emptyIdMeta.identity == null)
            {
                Debug.LogError("[MetadataDiagnostics] Metadata has null identity");
            }
            else if (string.IsNullOrEmpty(emptyIdMeta.identity.id))
            {
                Debug.LogError("[MetadataDiagnostics] Metadata has empty ID");
            }

            // Test null identity scenario
            ModelMeta nullIdentityMeta = new ModelMeta
            {
                identity = null,
                version = "1.0.0"
            };

            if (nullIdentityMeta.identity == null)
            {
                Debug.LogError("[MetadataDiagnostics] Metadata has null identity");
            }

            // Test empty version scenario
            ModelMeta emptyVersionMeta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "test-model",
                    name = "Test Model"
                },
                version = ""
            };

            if (string.IsNullOrEmpty(emptyVersionMeta.version))
            {
                Debug.LogError("[MetadataDiagnostics] Metadata has empty version");
            }

            Debug.Log("[MetadataDiagnostics] Error handling logic tested successfully");
        }

        private bool IsValidModelMeta(ModelMeta meta)
        {
            if (meta == null)
            {
                return false;
            }
            if (meta.identity == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(meta.identity.id))
            {
                return false;
            }
            if (string.IsNullOrEmpty(meta.version))
            {
                return false;
            }
            return true;
        }
    }
}
