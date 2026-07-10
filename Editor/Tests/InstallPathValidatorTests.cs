using System.IO;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for install path validation during model submission.
    /// </summary>
    public class InstallPathValidatorTests
    {
        private const string MODEL_NAME = "My_Model";

        [Test]
        public void Validate_AcceptsNewDedicatedModelFolder()
        {
            InstallPathValidator.ValidationResult result =
                InstallPathValidator.Validate("Assets/MyFolder/My_Model", MODEL_NAME, false);

            Assert.IsTrue(result.IsValid, string.Join(", ", result.Errors));
            Assert.AreEqual("Assets/MyFolder/My_Model", result.SuggestedInstallPath);
        }

        [Test]
        public void Validate_RejectsContainerWithNestedModels()
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
                    InstallPathValidator.Validate("Assets/My Path", MODEL_NAME, false);

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
        public void Validate_RejectsFolderThatAlreadyContainsModelFiles()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstallPathValidator_" + System.Guid.NewGuid().ToString("N"));
            string modelPath = Path.Combine(tempRoot, "Assets", "My Path");
            Directory.CreateDirectory(modelPath);
            File.WriteAllText(Path.Combine(modelPath, "Existing.fbx"), "dummy");

            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                InstallPathValidator.ValidationResult result =
                    InstallPathValidator.Validate("Assets/My Path", MODEL_NAME, false);

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
        public void Validate_AllowsExistingModelContentForUpdatePath()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "InstallPathValidator_" + System.Guid.NewGuid().ToString("N"));
            string modelPath = Path.Combine(tempRoot, "Assets", "Models", "My_Model");
            Directory.CreateDirectory(modelPath);
            File.WriteAllText(Path.Combine(modelPath, "My_Model.fbx"), "dummy");

            try
            {
                Directory.SetCurrentDirectory(tempRoot);

                InstallPathValidator.ValidationResult result =
                    InstallPathValidator.Validate("Assets/Models/My_Model", MODEL_NAME, true);

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
    }
}
