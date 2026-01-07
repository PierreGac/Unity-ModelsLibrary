using System;
using System.Collections.Generic;
using System.IO;
using ModelLibrary.Editor.Identity;
using ModelLibrary.Editor.Settings;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing FirstRunWizard view implementation for ModelLibraryWindow.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        private const int __WIZARD_TOTAL_STEPS = 4;
        private static readonly string[] __WizardStepTitles =
        {
            "Welcome",
            "Your Identity",
            "Repository Setup",
            "Summary"
        };

        /// <summary>
        /// Initializes wizard state when navigating to the wizard view.
        /// </summary>
        public void InitializeWizardState()
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
            _wizardUserName = identityProvider.GetUserName();
            _wizardUserRole = identityProvider.GetUserRole();
            _wizardRepoRoot = settings.repositoryRoot;
            _wizardRepoKind = settings.repositoryKind;
            _wizardStep = FirstRunWizard.WizardStep.Welcome;
            _wizardRepoTested = false;
            _wizardRepoTestMessage = null;
            _wizardRepoValidationMessage = null;
            _wizardOpenHelpAfterFinish = true;
        }

        /// <summary>
        /// Draws the FirstRunWizard view.
        /// </summary>
        private void DrawFirstRunWizardView()
        {
            GUILayout.Space(8f);
            DrawWizardStepHeader();
            GUILayout.Space(8f);

            switch (_wizardStep)
            {
                case FirstRunWizard.WizardStep.Welcome:
                    DrawWizardWelcomeStep();
                    break;
                case FirstRunWizard.WizardStep.Identity:
                    DrawWizardIdentityStep();
                    break;
                case FirstRunWizard.WizardStep.Repository:
                    DrawWizardRepositoryStep();
                    break;
                case FirstRunWizard.WizardStep.Summary:
                    DrawWizardSummaryStep();
                    break;
            }

            GUILayout.FlexibleSpace();
            DrawWizardNavigationBar();
        }

        private void DrawWizardStepHeader()
        {
            int stepIndex = (int)_wizardStep;
            float progress = (stepIndex + 1f) / __WIZARD_TOTAL_STEPS;

            EditorGUILayout.LabelField($"Step {stepIndex + 1} of {__WIZARD_TOTAL_STEPS}: {__WizardStepTitles[stepIndex]}", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, progress, $"{Mathf.RoundToInt(progress * 100f)}% complete");
        }

        private void DrawWizardWelcomeStep()
        {
            EditorGUILayout.HelpBox("Welcome! Let's configure the essentials so you can start browsing and importing models.", MessageType.Info);
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("This quick setup will:", EditorStyles.boldLabel);
            DrawWizardBullet("Capture who you are so notes and submissions display your name.");
            DrawWizardBullet("Point the browser at your shared repository or HTTP endpoint.");
            DrawWizardBullet("Offer a short tour of key features after configuration.");

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Help Center", GUILayout.Width(150f), GUILayout.Height(24f)))
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "helpSection", ModelLibraryHelpWindow.HelpSection.Overview }
                };
                NavigateToView(ViewType.Help, parameters);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWizardIdentityStep()
        {
            EditorGUILayout.LabelField("About You", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("We use this information to attribute notes, submissions, and review feedback.", MessageType.Info);
            GUILayout.Space(4f);

            _wizardUserName = EditorGUILayout.TextField("User Name", _wizardUserName);
            if (string.IsNullOrWhiteSpace(_wizardUserName))
            {
                EditorGUILayout.HelpBox("User name is required.", MessageType.Warning);
            }

            UserRole newRole = (UserRole)EditorGUILayout.EnumPopup("Role", _wizardUserRole);
            if (newRole != _wizardUserRole)
            {
                _wizardUserRole = newRole;
            }

            GUILayout.Space(4f);
            string roleDescription = GetWizardRoleDescription(_wizardUserRole);
            EditorGUILayout.HelpBox(roleDescription, MessageType.None);
        }

        private void DrawWizardRepositoryStep()
        {
            EditorGUILayout.LabelField("Repository Connection", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose the storage type and location for shared models.", MessageType.Info);
            GUILayout.Space(4f);

            ModelLibrarySettings.RepositoryKind newKind = (ModelLibrarySettings.RepositoryKind)EditorGUILayout.EnumPopup("Repository Kind", _wizardRepoKind);
            if (newKind != _wizardRepoKind)
            {
                _wizardRepoKind = newKind;
                _wizardRepoTested = false;
                _wizardRepoTestMessage = null;
                _wizardRepoTestMessageType = MessageType.None;
            }

            EditorGUILayout.BeginHorizontal();
            _wizardRepoRoot = EditorGUILayout.TextField("Repository Root", _wizardRepoRoot);
            if (_wizardRepoKind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (GUILayout.Button("Browse...", GUILayout.Width(90f)))
                {
                    string startPath = string.IsNullOrEmpty(_wizardRepoRoot) ? Application.dataPath : _wizardRepoRoot;
                    string selectedPath = EditorUtility.OpenFolderPanel("Select Repository Root", startPath, string.Empty);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _wizardRepoRoot = selectedPath;
                        _wizardRepoTested = false;
                        _wizardRepoTestMessage = null;
                        _wizardRepoTestMessageType = MessageType.None;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            bool repoValid = ValidateWizardRepository(out _wizardRepoValidationMessage, out _wizardRepoValidationType);
            if (!string.IsNullOrEmpty(_wizardRepoValidationMessage))
            {
                EditorGUILayout.HelpBox(_wizardRepoValidationMessage, _wizardRepoValidationType);
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Test Connection", GUILayout.Width(140f), GUILayout.Height(24f)))
            {
                RunWizardRepositoryTest();
            }
            EditorGUILayout.EndHorizontal();

            if (_wizardRepoTested && !string.IsNullOrEmpty(_wizardRepoTestMessage))
            {
                EditorGUILayout.HelpBox(_wizardRepoTestMessage, _wizardRepoTestMessageType);
            }

            GUILayout.Space(6f);
            EditorGUILayout.HelpBox("Examples:\n• File System: C\\\\Models or \\\\server\\share\\ModelLibrary\n• HTTP: https://models.example.com/api", MessageType.None);
        }

        private void DrawWizardSummaryStep()
        {
            EditorGUILayout.LabelField("Review & Finish", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("If everything looks correct, choose Finish to apply your configuration.", MessageType.Info);
            GUILayout.Space(4f);

            EditorGUILayout.LabelField("User", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Name: {_wizardUserName}");
            EditorGUILayout.LabelField($"Role: {_wizardUserRole}");

            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Repository", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Type: {_wizardRepoKind}");
            EditorGUILayout.LabelField($"Location: {_wizardRepoRoot}");

            GUILayout.Space(8f);
            _wizardOpenHelpAfterFinish = EditorGUILayout.ToggleLeft("Open a quick tour after finishing", _wizardOpenHelpAfterFinish);
        }

        private void DrawWizardNavigationBar()
        {
            bool canGoBack = _wizardStep != FirstRunWizard.WizardStep.Welcome;
            bool isLastStep = _wizardStep == FirstRunWizard.WizardStep.Summary;
            bool currentStepValid = IsWizardCurrentStepValid();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (canGoBack)
                {
                    if (GUILayout.Button("Back", GUILayout.Width(90f), GUILayout.Height(26f)))
                    {
                        int previous = Mathf.Max(0, (int)_wizardStep - 1);
                        _wizardStep = (FirstRunWizard.WizardStep)previous;
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
                            int next = Mathf.Min((int)_wizardStep + 1, __WIZARD_TOTAL_STEPS - 1);
                            _wizardStep = (FirstRunWizard.WizardStep)next;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Finish", GUILayout.Width(110f), GUILayout.Height(26f)))
                        {
                            SaveWizardConfiguration();
                            if (_wizardOpenHelpAfterFinish)
                            {
                                Dictionary<string, object> parameters = new Dictionary<string, object>
                                {
                                    { "helpSection", ModelLibraryHelpWindow.HelpSection.Overview }
                                };
                                NavigateToView(ViewType.Help, parameters);
                            }
                            else
                            {
                                NavigateToView(ViewType.Browser);
                            }
                        }
                    }
                }
            }
        }

        private bool IsWizardCurrentStepValid()
        {
            switch (_wizardStep)
            {
                case FirstRunWizard.WizardStep.Welcome:
                    return true;
                case FirstRunWizard.WizardStep.Identity:
                    return !string.IsNullOrWhiteSpace(_wizardUserName);
                case FirstRunWizard.WizardStep.Repository:
                    return ValidateWizardRepository(out _, out _);
                case FirstRunWizard.WizardStep.Summary:
                    return !string.IsNullOrWhiteSpace(_wizardUserName) && ValidateWizardRepository(out _, out _);
                default:
                    return false;
            }
        }

        private bool ValidateWizardRepository(out string message, out MessageType type)
        {
            message = string.Empty;
            type = MessageType.None;

            if (string.IsNullOrWhiteSpace(_wizardRepoRoot))
            {
                message = "Repository root is required.";
                type = MessageType.Error;
                return false;
            }

            if (_wizardRepoKind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (!Path.IsPathRooted(_wizardRepoRoot) && !_wizardRepoRoot.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    message = "File system paths should be absolute (e.g., C\\\\Models or \\\\server\\share).";
                    type = MessageType.Error;
                    return false;
                }

                if (!Directory.Exists(_wizardRepoRoot))
                {
                    message = "Directory not found. You can still proceed, but double-check the network share.";
                    type = MessageType.Warning;
                    return true;
                }
            }
            else
            {
                if (!Uri.TryCreate(_wizardRepoRoot, UriKind.Absolute, out Uri uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    message = "Please provide a valid HTTP or HTTPS URL.";
                    type = MessageType.Error;
                    return false;
                }
            }

            return true;
        }

        private void RunWizardRepositoryTest()
        {
            _wizardRepoTested = true;

            if (!ValidateWizardRepository(out string validationMessage, out MessageType validationType))
            {
                _wizardRepoTestMessage = validationMessage;
                _wizardRepoTestMessageType = validationType;
                return;
            }

            if (_wizardRepoKind == ModelLibrarySettings.RepositoryKind.FileSystem)
            {
                if (Directory.Exists(_wizardRepoRoot))
                {
                    _wizardRepoTestMessage = "Directory located successfully.";
                    _wizardRepoTestMessageType = MessageType.Info;
                }
                else
                {
                    _wizardRepoTestMessage = "Directory not found. Ensure the path exists and you have access.";
                    _wizardRepoTestMessageType = MessageType.Warning;
                }
            }
            else
            {
                _wizardRepoTestMessage = "URL format looks valid. Remember to verify credentials and server availability.";
                _wizardRepoTestMessageType = MessageType.Info;
            }
        }

        private string GetWizardRoleDescription(UserRole role)
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

        private void DrawWizardBullet(string text) => EditorGUILayout.LabelField("• " + text, EditorStyles.wordWrappedLabel);

        private void SaveWizardConfiguration()
        {
            try
            {
                // Validate before saving
                if (string.IsNullOrWhiteSpace(_wizardUserName) || _wizardUserName == "anonymous")
                {
                    EditorUtility.DisplayDialog("Invalid Configuration",
                        "User name cannot be empty or 'anonymous'. Please enter a valid name.",
                        "OK");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_wizardRepoRoot))
                {
                    EditorUtility.DisplayDialog("Invalid Configuration",
                        "Repository root cannot be empty. Please specify a repository location.",
                        "OK");
                    return;
                }

                // Save user identity
                SimpleUserIdentityProvider identityProvider = new SimpleUserIdentityProvider();
                identityProvider.SetUserName(_wizardUserName);
                identityProvider.SetUserRole(_wizardUserRole);

                // Save repository settings
                ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
                if (settings == null)
                {
                    EditorUtility.DisplayDialog("Save Failed",
                        "Could not load ModelLibrarySettings. Please try again.",
                        "OK");
                    return;
                }

                settings.repositoryKind = _wizardRepoKind;
                settings.repositoryRoot = _wizardRepoRoot;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                // Verify configuration was saved
                if (!FirstRunWizard.IsConfigured())
                {
                    EditorUtility.DisplayDialog("Save Warning",
                        "Configuration was saved but verification failed. The wizard will remain open.\n\n" +
                        "Please check your settings and try again.",
                        "OK");
                    return;
                }

                Debug.Log($"[ModelLibraryWindow] Configuration saved successfully: User='{_wizardUserName}', Repo='{_wizardRepoRoot}'");
                ReinitializeAfterConfiguration();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Save Configuration Failed",
                    $"Failed to save configuration: {ex.Message}",
                    ErrorHandler.CategorizeException(ex), ex);
                
                EditorUtility.DisplayDialog("Save Failed",
                    $"Failed to save configuration: {ex.Message}\n\nPlease try again.",
                    "OK");
            }
        }
    }
}

