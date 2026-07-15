using System.IO;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for install path validation during model submission and import.
    /// </summary>
    public class InstallPathValidatorTests
    {
        private const string MODEL_NAME = "My_Model";

        [Test]
        public void Validate_Submission_AcceptsNewDedicatedModelFolder()
        {
            InstallPathValidator.ValidationResult result =
                InstallPathValidator.Validate(
                    "Assets/MyFolder/My_Model",
                    MODEL_NAME,
                    false,
                    InstallPathValidator.InstallPathValidationMode.Submission);

            Assert.IsTrue(result.IsValid, string.Join(", ", result.Errors));
            Assert.AreEqual("Assets/MyFolder/My_Model", result.SuggestedInstallPath);
        }

        [Test]
        public void Validate_Submission_AcceptsNonExistentPathEndingWithModelName()
        {
            InstallPathValidator.ValidationResult result =
                InstallPathValidator.Validate(
                    "Assets/Models/BrandNewModel",
                    "BrandNewModel",
                    false,
                    InstallPathValidator.InstallPathValidationMode.Submission);

            Assert.IsTrue(result.IsValid, string.Join(", ", result.Errors));
        }

        [Test]
        public void Validate_Submission_IgnoresExistingModelFilesOnDisk()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstallPathValidator_" + System.Guid.NewGuid().ToString("N"));
            string modelPath = Path.Combine(tempRoot, "Assets", "Models", "My_Model");
            Directory.CreateDirectory(modelPath);
            File.WriteAllText(Path.Combine(modelPath, "Existing.fbx"), "dummy");

            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                InstallPathValidator.ValidationResult result =
                    InstallPathValidator.Validate(
                        "Assets/Models/My_Model",
                        MODEL_NAME,
                        false,
                        InstallPathValidator.InstallPathValidationMode.Submission);

                Assert.IsTrue(result.IsValid, string.Join(", ", result.Errors));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void Validate_Import_RejectsContainerWithNestedModels()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstallPathValidator_" + System.Guid.NewGuid().ToString("N"));
            string containerPath = Path.Combine(tempRoot, "Assets", "My Path");
            string nestedModelPath = Path.Combine(containerPath, "ExistingModel");
            Directory.CreateDirectory(nestedModelPath);
            File.WriteAllText(Path.Combine(nestedModelPath, "ExistingModel.fbx"), "dummy");

            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                InstallPathValidator.ValidationResult result =
                    InstallPathValidator.Validate(
                        "Assets/My Path",
                        MODEL_NAME,
                        false,
                        InstallPathValidator.InstallPathValidationMode.Import);

                Assert.IsFalse(result.IsValid);
                Assert.IsTrue(result.Errors.Exists(error => error.Contains("nested models")));
                Assert.AreEqual("Assets/My Path/My_Model", result.SuggestedInstallPath);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void Validate_Import_RejectsFolderThatAlreadyContainsModelFiles()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstallPathValidator_" + System.Guid.NewGuid().ToString("N"));
            string modelPath = Path.Combine(tempRoot, "Assets", "My Path");
            Directory.CreateDirectory(modelPath);
            File.WriteAllText(Path.Combine(modelPath, "Existing.fbx"), "dummy");

            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                InstallPathValidator.ValidationResult result =
                    InstallPathValidator.Validate(
                        "Assets/My Path",
                        MODEL_NAME,
                        false,
                        InstallPathValidator.InstallPathValidationMode.Import);

                Assert.IsFalse(result.IsValid);
                Assert.IsTrue(result.Errors.Exists(error => error.Contains("already contains model files")));
                Assert.AreEqual("Assets/My Path/My_Model", result.SuggestedInstallPath);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void Validate_Import_AllowsExistingModelContentForUpdatePath()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstallPathValidator_" + System.Guid.NewGuid().ToString("N"));
            string modelPath = Path.Combine(tempRoot, "Assets", "Models", "My_Model");
            Directory.CreateDirectory(modelPath);
            File.WriteAllText(Path.Combine(modelPath, "My_Model.fbx"), "dummy");

            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                InstallPathValidator.ValidationResult result =
                    InstallPathValidator.Validate(
                        "Assets/Models/My_Model",
                        MODEL_NAME,
                        true,
                        InstallPathValidator.InstallPathValidationMode.Import);

                Assert.IsTrue(result.IsValid, string.Join(", ", result.Errors));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void BuildSuggestedInstallPath_AppendsModelNameToParent()
        {
            string suggested = InstallPathValidator.BuildSuggestedInstallPath("Assets/My Path", "My Model");

            Assert.AreEqual("Assets/My Path/My Model", suggested);
        }

        [Test]
        public void BuildSuggestedInstallPath_ReplacesStaleDefaultModelFolder()
        {
            string suggested = InstallPathValidator.BuildSuggestedInstallPath("Assets/New Model", "Foo");

            Assert.AreEqual("Assets/Foo", suggested);
        }

        [Test]
        public void BuildSuggestedInstallPath_ReplacesDefaultInstallPathWhenModelRenamed()
        {
            string suggested = InstallPathValidator.BuildSuggestedInstallPath("Assets/Models/New Model", "Foo");

            Assert.AreEqual("Assets/Models/Foo", suggested);
        }

        [Test]
        public void Validate_Submission_SuggestsCorrectPathWhenDefaultFolderNameIsStale()
        {
            InstallPathValidator.ValidationResult result =
                InstallPathValidator.Validate(
                    "Assets/New Model",
                    "Foo",
                    false,
                    InstallPathValidator.InstallPathValidationMode.Submission);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Assets/Foo", result.SuggestedInstallPath);
        }

        [Test]
        public void Validate_Submission_AcceptsInstallPathWithTrailingSlash()
        {
            InstallPathValidator.ValidationResult result =
                InstallPathValidator.Validate(
                    "Assets/Models/fghfghfgh/",
                    "fghfghfgh",
                    false,
                    InstallPathValidator.InstallPathValidationMode.Submission);

            Assert.IsTrue(result.IsValid, string.Join(", ", result.Errors));
            Assert.AreEqual("Assets/Models/fghfghfgh", result.SuggestedInstallPath);
        }

        [Test]
        public void BuildSuggestedInstallPath_StripsTrailingSlash()
        {
            string suggested = InstallPathValidator.BuildSuggestedInstallPath("Assets/Models/fghfghfgh/", "fghfghfgh");

            Assert.AreEqual("Assets/Models/fghfghfgh", suggested);
        }
    }
}
