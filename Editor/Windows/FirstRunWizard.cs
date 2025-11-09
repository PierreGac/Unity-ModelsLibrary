using System;
using System.IO;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// First-run configuration wizard for the Model Library system.
    /// Provides a guided, multi-step setup experience covering user identity and repository configuration.
    /// </summary>
    public class FirstRunWizard : EditorWindow
    {
        private enum WizardStep
        {
            Welcome = 0,
            Identity = 1,
            Repository = 2,
            Summary = 3
        }

        private const int __TOTAL_STEPS = 4;
        private static readonly string[] __StepTitles =
        {
            "Welcome",
            "Your Identity",
            "Repository Setup",
            "Summary"
        };

        /// <summary>User name entered in the form.</summary>
        private string _userName;
        /// <summary>User role entered in the form.</summary>
        private UserRole _userRole;
        /// <summary>Repository root path or URL entered in the form.</summary>
        private string _repoRoot;
        /// <summary>Selected repository type (FileSystem or HTTP).</summary>
        private ModelLibrarySettings.RepositoryKind _kind;
        /// <summary>Current wizard step.</summary>
        private WizardStep _currentStep = WizardStep.Welcome;

        /// <summary>Repository validation feedback message.</summary>
        private string _repositoryValidationMessage;
        /// <summary>Repository validation message type.</summary>
        private MessageType _repositoryValidationType = MessageType.Info;

        /// <summary>Flag indicating if the repository check/test has been performed.</summary>
        private bool _repositoryTested;
        /// <summary>Latest repository test result message.</summary>
        private string _repositoryTestMessage;
        /// <summary>Latest repository test result type.</summary>
        private MessageType _repositoryTestMessageType = MessageType.None;

        /// <summary>Whether to open the help window after finishing the wizard.</summary>
        private bool _openHelpAfterFinish = true;

        /// <summary>
        /// Checks if the Model Library has been properly configured.
        /// Verifies that both user name and repository root are set and not default values.
        /// </summary>
        /// <returns>True if configuration is complete, false if the wizard should be shown.</returns>
        public static bool IsConfigured()
        {
            try
            {
                if (!EditorPrefs.HasKey("ModelLibrary.UserName"))
                {
                    return false;
                }

                SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                string userName = identityProvider.GetUserName();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return false;
                }

                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                if (string.IsNullOrWhiteSpace(settings.repositoryRoot))
                {
                    return false;
                }

                if (settings.repositoryRoot == "\\\\SERVER\\ModelLibrary")
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FirstRunWizard.IsConfigured() failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shows the first-run wizard if configuration is not complete.
        /// Called automatically when the Model Library browser opens and configuration is missing.
        /// </summary>
        public static void MaybeShow()
        {
            if (!IsConfigured())
            {
                FirstRunWizard window = GetWindow<FirstRunWizard>(true, "Model Library Setup", true);
                window.minSize = new Vector2(480f, 360f);
                window.maxSize = new Vector2(640f, 520f);
                window.Init();
                window.ShowUtility();
            }
        }

        private void Init()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            _userName = identityProvider.GetUserName();
            _userRole = identityProvider.GetUserRole();
            _repoRoot = settings.repositoryRoot;
            _kind = settings.repositoryKind;
            _currentStep = WizardStep.Welcome;
            _repositoryTested = false;
            _repositoryTestMessage = null;
            _repositoryValidationMessage = null;
        }

        private void SaveConfiguration()
        {
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            identityProvider.SetUserName(_userName);
            identityProvider.SetUserRole(_userRole);

            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            settings.repositoryKind = _kind;
            settings.repositoryRoot = _repoRoot;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            NotifyConfigurationChanged();
        }

        private void NotifyConfigurationChanged()
        {
            ModelLibraryWindow[] windows = Resources.FindObjectsOfTypeAll<ModelLibraryWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                ModelLibraryWindow window = windows[i];
                if (window != null)
                {
                    window.ReinitializeAfterConfiguration();
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(8f);
            DrawStepHeader();
            GUILayout.Space(8f);

            switch (_currentStep)
            {
                case WizardStep.Welcome:
                    DrawWelcomeStep();
                    break;
                case WizardStep.Identity:
                    DrawIdentityStep();
                    break;
                case WizardStep.Repository:
                    DrawRepositoryStep();
                    break;
                case WizardStep.Summary:
                    DrawSummaryStep();
                    break;
            }

            GUILayout.FlexibleSpace();
            DrawNavigationBar();
        }

        private void DrawStepHeader()
        {
            int stepIndex = (int)_currentStep;
            float progress = (stepIndex + 1f) / __TOTAL_STEPS;

            EditorGUILayout.LabelField($"Step {stepIndex + 1} of {__TOTAL_STEPS}: {__StepTitles[stepIndex]}", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, progress, $"{Mathf.RoundToInt(progress * 100f)}% complete");
        }

        private void DrawWelcomeStep()
        {
            EditorGUILayout.HelpBox("Welcome! Let's configure the essentials so you can start browsing and importing models.", MessageType.Info);
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("This quick setup will:", EditorStyles.boldLabel);
            DrawBullet("Capture who you are so notes and submissions display your name.");
            DrawBullet("Point the browser at your shared repository or HTTP endpoint.");
            DrawBullet("Offer a short tour of key features after configuration.");

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Help Center", GUILayout.Width(150f), GUILayout.Height(24f)))
            {
                ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Overview);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawIdentityStep()
        {
            EditorGUILayout.LabelField("About You", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("We use this information to attribute notes, submissions, and review feedback.", MessageType.Info);
            GUILayout.Space(4f);

            _userName = EditorGUILayout.TextField("User Name", _userName);
            if (string.IsNullOrWhiteSpace(_userName))
            {
                EditorGUILayout.HelpBox("User name is required.", MessageType.Warning);
            }

            UserRole newRole = (UserRole)EditorGUILayout.EnumPopup("Role", _userRole);
            if (newRole != _userRole)
            {
                _userRole = newRole;
            }

            GUILayout.Space(4f);
            string roleDescription = GetRoleDescription(_userRole);
            EditorGUILayout.HelpBox(roleDescription, MessageType.None);
        }

        private void DrawRepositoryStep()
        {
            EditorGUILayout.LabelField("Repository Connection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose the storage type and location for shared models.", MessageType.Info);
            GUILayout.Space(4f);

            ModelLibrarySettings.RepositoryKind newKind = (ModelLibrarySettings.RepositoryKind)EditorGUILayout.EnumPopup("Repository Kind", _kind);
            if (newKind != _kind)
            {
                _kind = newKind;
                _repositoryTested = false;
                _repositoryTestMessage = null;
                _repositoryTestMessageType = MessageType.None;
            }

            EditorGUILayout.BeginHorizontal();
            _repoRoot = EditorGUILayout.TextField("Repository Root", _repoRoot);
            if (_kind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (GUILayout.Button("Browse...", GUILayout.Width(90f)))
                {
                    string startPath = string.IsNullOrEmpty(_repoRoot) ? Application.dataPath : _repoRoot;
                    string selectedPath = EditorUtility.OpenFolderPanel("Select Repository Root", startPath, string.Empty);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _repoRoot = selectedPath;
                        _repositoryTested = false;
                        _repositoryTestMessage = null;
                        _repositoryTestMessageType = MessageType.None;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            bool repoValid = ValidateRepository(out _repositoryValidationMessage, out _repositoryValidationType);
            if (!string.IsNullOrEmpty(_repositoryValidationMessage))
            {
                EditorGUILayout.HelpBox(_repositoryValidationMessage, _repositoryValidationType);
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Test Connection", GUILayout.Width(140f), GUILayout.Height(24f)))
            {
                RunRepositoryTest();
            }
            EditorGUILayout.EndHorizontal();

            if (_repositoryTested && !string.IsNullOrEmpty(_repositoryTestMessage))
            {
                EditorGUILayout.HelpBox(_repositoryTestMessage, _repositoryTestMessageType);
            }

            GUILayout.Space(6f);
            EditorGUILayout.HelpBox("Examples:\n• File System: C\\\\Models or \\\\server\\share\\ModelLibrary\n• HTTP: https://models.example.com/api", MessageType.None);
        }

        private void DrawSummaryStep()
        {
            EditorGUILayout.LabelField("Review & Finish", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("If everything looks correct, choose Finish to apply your configuration.", MessageType.Info);
            GUILayout.Space(4f);

            EditorGUILayout.LabelField("User", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Name: {_userName}");
            EditorGUILayout.LabelField($"Role: {_userRole}");

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Repository", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {_kind}");
            EditorGUILayout.LabelField($"Location: {_repoRoot}");

            GUILayout.Space(8f);
            _openHelpAfterFinish = EditorGUILayout.ToggleLeft("Open a quick tour after finishing", _openHelpAfterFinish);
        }

        private void DrawNavigationBar()
        {
            bool canGoBack = _currentStep != WizardStep.Welcome;
            bool isLastStep = _currentStep == WizardStep.Summary;
            bool currentStepValid = IsCurrentStepValid();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (canGoBack)
                {
                    if (GUILayout.Button("Back", GUILayout.Width(90f), GUILayout.Height(26f)))
                    {
                        int previous = Mathf.Max(0, (int)_currentStep - 1);
                        _currentStep = (WizardStep)previous;
                    }
                }
                else
                {
                    GUILayout.Space(94f);
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!currentStepValid))
                {
                    if (!isLastStep)
                    {
                        if (GUILayout.Button("Next", GUILayout.Width(110f), GUILayout.Height(26f)))
                        {
                            int next = Mathf.Min((int)_currentStep + 1, __TOTAL_STEPS - 1);
                            _currentStep = (WizardStep)next;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Finish", GUILayout.Width(110f), GUILayout.Height(26f)))
                        {
                            SaveConfiguration();
                            if (_openHelpAfterFinish)
                            {
                                ModelLibraryHelpWindow.OpenToSection(ModelLibraryHelpWindow.HelpSection.Overview);
                            }
                            Close();
                        }
                    }
                }
            }
        }

        private bool IsCurrentStepValid()
        {
            switch (_currentStep)
            {
                case WizardStep.Welcome:
                    return true;
                case WizardStep.Identity:
                    return !string.IsNullOrWhiteSpace(_userName);
                case WizardStep.Repository:
                    return ValidateRepository(out _, out _);
                case WizardStep.Summary:
                    return !string.IsNullOrWhiteSpace(_userName) && ValidateRepository(out _, out _);
                default:
                    return false;
            }
        }

        private bool ValidateRepository(out string message, out MessageType type)
        {
            message = string.Empty;
            type = MessageType.None;

            if (string.IsNullOrWhiteSpace(_repoRoot))
            {
                message = "Repository root is required.";
                type = MessageType.Error;
                return false;
            }

            if (_kind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (!Path.IsPathRooted(_repoRoot) && !_repoRoot.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    message = "File system paths should be absolute (e.g., C\\\\Models or \\\\server\\share).";
                    type = MessageType.Error;
                    return false;
                }

                if (!Directory.Exists(_repoRoot))
                {
                    message = "Directory not found. You can still proceed, but double-check the network share.";
                    type = MessageType.Warning;
                    return true;
                }
            }
            else
            {
                if (!Uri.TryCreate(_repoRoot, UriKind.Absolute, out Uri uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    message = "Please provide a valid HTTP or HTTPS URL.";
                    type = MessageType.Error;
                    return false;
                }
            }

            return true;
        }

        private void RunRepositoryTest()
        {
            _repositoryTested = true;

            if (!ValidateRepository(out string validationMessage, out MessageType validationType))
            {
                _repositoryTestMessage = validationMessage;
                _repositoryTestMessageType = validationType;
                return;
            }

            if (_kind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (Directory.Exists(_repoRoot))
                {
                    _repositoryTestMessage = "Directory located successfully.";
                    _repositoryTestMessageType = MessageType.Info;
                }
                else
                {
                    _repositoryTestMessage = "Directory not found. Ensure the path exists and you have access.";
                    _repositoryTestMessageType = MessageType.Warning;
                }
            }
            else
            {
                _repositoryTestMessage = "URL format looks valid. Remember to verify credentials and server availability.";
                _repositoryTestMessageType = MessageType.Info;
            }
        }

        private string GetRoleDescription(UserRole role)
        {
            switch (role)
            {
                case UserRole.Artist:
                    return "Artist: submit new models, manage versions, and collaborate on content reviews.";
                case UserRole.Admin:
                    return "Admin: full access, including deletion, analytics, and system configuration.";
                default:
                    return "Developer: import models, leave notes, and monitor updates.";
            }
        }

        private void DrawBullet(string text) => EditorGUILayout.LabelField("• " + text, EditorStyles.wordWrappedLabel);
    }
}


