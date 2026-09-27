using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ModelLibrary.Data;
using ModelLibrary.Editor;
using ModelLibrary.Editor.Services;
using NUnit.Framework;
using UnityEditor;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Verifies that project import copies allowlisted model files and rejects disallowed payload files.
    /// </summary>
    public class ModelProjectImporterAllowlistTests
    {
        private const string INSTALL_RELATIVE_PATH = "Assets/__ModelLibraryImportAllowlistFixture";
        private const string PRIMARY_MODEL_FILE_NAME = "Hero.obj";
        private const string DLL_FILE_NAME = "evil.dll";
        private const string CS_FILE_NAME = "evil.cs";
        private const string ASMDEF_FILE_NAME = "evil.asmdef";
        private const string NESTED_DLL_FILE_NAME = "nested.dll";
        private const string MANIFEST_FILE_NAME = ".modelLibrary.meta.json";
        private const string PAYLOAD_DIRECTORY_NAME = "payload";
        private const string DEPENDENCY_DIRECTORY_NAME = "deps";
        private const string OBJ_BODY = "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n";
        private const string DISALLOWED_BODY = "disallowed";

        private string _cacheRoot;

        /// <summary>
        /// Imports an OBJ plus disallowed payload and dependency files, then removes the fixture.
        /// </summary>
        [Test]
        public async Task ImportFromCacheAsync_RejectsDisallowedFilesAndImportsPrimaryModel()
        {
            _cacheRoot = Path.Combine(Path.GetTempPath(), "ModelLibraryImportAllowlist_" + Guid.NewGuid().ToString("N"));
            string payloadRoot = Path.Combine(_cacheRoot, PAYLOAD_DIRECTORY_NAME);
            string dependencyRoot = Path.Combine(payloadRoot, DEPENDENCY_DIRECTORY_NAME);
            Directory.CreateDirectory(dependencyRoot);

            File.WriteAllText(Path.Combine(payloadRoot, PRIMARY_MODEL_FILE_NAME), OBJ_BODY);
            File.WriteAllText(Path.Combine(payloadRoot, DLL_FILE_NAME), DISALLOWED_BODY);
            File.WriteAllText(Path.Combine(payloadRoot, CS_FILE_NAME), DISALLOWED_BODY);
            File.WriteAllText(Path.Combine(payloadRoot, ASMDEF_FILE_NAME), DISALLOWED_BODY);
            File.WriteAllText(Path.Combine(dependencyRoot, NESTED_DLL_FILE_NAME), DISALLOWED_BODY);

            ModelMeta meta = new ModelMeta
            {
                identity = new ModelIdentity
                {
                    id = "allowlist-fixture",
                    name = "AllowlistFixture"
                },
                version = "1.0.0",
                payloadRelativePaths = new List<string>
                {
                    PAYLOAD_DIRECTORY_NAME + "/" + PRIMARY_MODEL_FILE_NAME,
                    PAYLOAD_DIRECTORY_NAME + "/" + DLL_FILE_NAME,
                    PAYLOAD_DIRECTORY_NAME + "/" + CS_FILE_NAME,
                    PAYLOAD_DIRECTORY_NAME + "/" + ASMDEF_FILE_NAME,
                    PAYLOAD_DIRECTORY_NAME + "/" + DEPENDENCY_DIRECTORY_NAME + "/" + NESTED_DLL_FILE_NAME
                }
            };

            try
            {
                string installPath = await ModelProjectImporter.ImportFromCacheAsync(
                    _cacheRoot,
                    meta,
                    cleanDestination: true,
                    overrideInstallPath: INSTALL_RELATIVE_PATH,
                    isUpdate: true);

                string destination = Path.GetFullPath(installPath);
                Assert.IsTrue(File.Exists(Path.Combine(destination, PRIMARY_MODEL_FILE_NAME)), "Allowlisted primary model should be imported.");
                Assert.IsFalse(File.Exists(Path.Combine(destination, DLL_FILE_NAME)), "DLL payload must not reach Assets.");
                Assert.IsFalse(File.Exists(Path.Combine(destination, CS_FILE_NAME)), "C# payload must not reach Assets.");
                Assert.IsFalse(File.Exists(Path.Combine(destination, ASMDEF_FILE_NAME)), "Asmdef payload must not reach Assets.");
                Assert.IsFalse(File.Exists(Path.Combine(destination, NESTED_DLL_FILE_NAME)), "Nested DLL dependency must not reach Assets.");

                string[] importedFiles = Directory.GetFiles(destination, "*", SearchOption.AllDirectories);
                for (int i = 0; i < importedFiles.Length; i++)
                {
                    string importedFile = importedFiles[i];
                    string extension = Path.GetExtension(importedFile);
                    string fileName = Path.GetFileName(importedFile);
                    bool isManifest = string.Equals(fileName, MANIFEST_FILE_NAME, StringComparison.OrdinalIgnoreCase);
                    bool isMeta = string.Equals(extension, FileExtensions.META, StringComparison.OrdinalIgnoreCase);
                    bool isAcceptable = FileExtensions.IsAcceptablePayloadExtension(extension);
                    Assert.IsTrue(isManifest || isMeta || isAcceptable, "Imported file is outside the payload allowlist: " + fileName);
                }
            }
            finally
            {
                DeleteFixture();
            }
        }

        private void DeleteFixture()
        {
            string destination = Path.GetFullPath(INSTALL_RELATIVE_PATH);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            string destinationMeta = destination + FileExtensions.META;
            if (File.Exists(destinationMeta))
            {
                File.Delete(destinationMeta);
            }

            if (!string.IsNullOrEmpty(_cacheRoot) && Directory.Exists(_cacheRoot))
            {
                Directory.Delete(_cacheRoot, true);
            }

            AssetDatabase.Refresh();
        }
    }
}
