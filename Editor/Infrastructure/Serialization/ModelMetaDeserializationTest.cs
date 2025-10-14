using UnityEngine;
using ModelLibrary.Data;
using ModelLibrary.Editor.Utils;

namespace ModelLibrary.Editor.Serialization
{
    /// <summary>
    /// Test class demonstrating the versioned deserialization system.
    /// This shows how the system handles different scenarios gracefully.
    /// </summary>
    public static class ModelMetaDeserializationTest
    {
        /// <summary>
        /// Test the versioned deserialization with various JSON scenarios.
        /// Call this from a Unity Editor script to see the system in action.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void RunTests()
        {
            Debug.Log("=== ModelMeta Versioned Deserialization Tests ===");

            // Test 1: Valid current schema JSON
            TestValidCurrentSchema();

            // Test 2: Missing schema version (version 0)
            TestMissingSchemaVersion();

            // Test 3: Corrupted JSON
            TestCorruptedJson();

            // Test 4: Empty JSON
            TestEmptyJson();

            // Test 5: Null JSON
            TestNullJson();

            Debug.Log("=== All Tests Completed ===");
        }

        private static void TestValidCurrentSchema()
        {
            Debug.Log("Test 1: Valid current schema JSON");

            ModelMeta original = CreateTestModelMeta();
            string json = JsonUtil.ToJson(original);

            ModelMeta deserialized = JsonUtil.FromJsonModelMeta(json);

            if (deserialized != null && deserialized.identity.name == original.identity.name)
            {
                Debug.Log("✓ Valid current schema test passed");
            }
            else
            {
                Debug.LogError("✗ Valid current schema test failed");
            }
        }

        private static void TestMissingSchemaVersion()
        {
            Debug.Log("Test 2: Missing schema version (version 0)");

            // Create JSON without schemaVersion field (simulating old data)
            string oldJson = @"{
                ""identity"": {
                    ""id"": ""test-id-123"",
                    ""name"": ""Test Model""
                },
                ""version"": ""1.0.0"",
                ""description"": ""A test model"",
                ""author"": ""Test Author"",
                ""createdTimeTicks"": 1234567890
            }";

            ModelMeta deserialized = JsonUtil.FromJsonModelMeta(oldJson);

            if (deserialized != null && deserialized.identity.name == "Test Model")
            {
                Debug.Log("✓ Missing schema version test passed - migration worked");
            }
            else
            {
                Debug.LogError("✗ Missing schema version test failed");
            }
        }

        private static void TestCorruptedJson()
        {
            Debug.Log("Test 3: Corrupted JSON");

            string corruptedJson = @"{
                ""identity"": {
                    ""id"": ""test-id-123"",
                    ""name"": ""Test Model""
                },
                ""version"": ""1.0.0"",
                ""description"": ""A test model"",
                ""author"": ""Test Author"",
                ""createdTimeTicks"": 1234567890,
                ""invalidField"": ""this should be ignored"",
                ""missingClosingBrace"": ""oops
            }";

            ModelMeta deserialized = JsonUtil.FromJsonModelMeta(corruptedJson);

            if (deserialized != null)
            {
                Debug.Log("✓ Corrupted JSON test passed - fallback deserialization worked");
            }
            else
            {
                Debug.LogError("✗ Corrupted JSON test failed");
            }
        }

        private static void TestEmptyJson()
        {
            Debug.Log("Test 4: Empty JSON");

            ModelMeta deserialized = JsonUtil.FromJsonModelMeta("");

            if (deserialized != null)
            {
                Debug.Log("✓ Empty JSON test passed - returned default object");
            }
            else
            {
                Debug.LogError("✗ Empty JSON test failed");
            }
        }

        private static void TestNullJson()
        {
            Debug.Log("Test 5: Null JSON");

            ModelMeta deserialized = JsonUtil.FromJsonModelMeta(null);

            if (deserialized != null)
            {
                Debug.Log("✓ Null JSON test passed - returned default object");
            }
            else
            {
                Debug.LogError("✗ Null JSON test failed");
            }
        }

        private static ModelMeta CreateTestModelMeta()
        {
            return new ModelMeta
            {
                schemaVersion = 1,
                identity = new ModelIdentity
                {
                    id = "test-id-123",
                    name = "Test Model"
                },
                version = "1.0.0",
                description = "A test model for demonstration",
                author = "Test Author",
                createdTimeTicks = System.DateTime.Now.Ticks,
                payloadRelativePaths = new System.Collections.Generic.List<string> { "model.fbx", "texture.png" },
                materials = new System.Collections.Generic.List<AssetRef>(),
                textures = new System.Collections.Generic.List<AssetRef>(),
                assetGuids = new System.Collections.Generic.List<string>(),
                imageRelativePaths = new System.Collections.Generic.List<string>(),
                projectTags = new System.Collections.Generic.List<string>(),
                notes = new System.Collections.Generic.List<ModelNote>(),
                dependencies = new System.Collections.Generic.List<string>(),
                dependenciesDetailed = new System.Collections.Generic.List<DependencyRef>(),
                extra = new System.Collections.Generic.Dictionary<string, string>(),
                modelImporters = new System.Collections.Generic.Dictionary<string, ModelImporterSettings>(),
                changelog = new System.Collections.Generic.List<ModelChangelogEntry>(),
                tags = new Tags(),
                vertexCount = 1000,
                triangleCount = 2000
            };
        }
    }
}
