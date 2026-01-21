using System;
using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Custom error dialog window with prominent retry button and "don't show again" option.
    /// </summary>
    public class ErrorDialogWindow : EditorWindow
    {
        private const string __SuppressionKeysPrefKey = "ModelLibrary.ErrorSuppressed.Keys";
        private const float __WINDOW_MIN_WIDTH = 500f;
        private const float __WINDOW_MIN_HEIGHT = 300f;
        private const float __WINDOW_MAX_WIDTH = 600f;
        private const float __WINDOW_MAX_HEIGHT = 500f;
        private const float __MESSAGE_SCROLL_HEIGHT = 150f;
        private const float __STACK_TRACE_HEIGHT = 100f;
        private const float __BUTTON_WIDTH = 120f;
        private const float __RETRY_BUTTON_PADDING_HORIZONTAL = 20f;
        private const float __RETRY_BUTTON_PADDING_VERTICAL = 8f;

        private string _title;
        private string _message;
        private ErrorHandler.ErrorCategory _category;
        private Exception _exception;
        private Action _retryAction;
        private bool _dontShowAgain = false;
        private Vector2 _scrollPosition = Vector2.zero;
        private bool _showDetails = false;
        private static bool _isShowing = false;

        /// <summary>
        /// Shows an error dialog with retry option and "don't show again" checkbox.
        /// </summary>
        public static void Show(string title, string message, ErrorHandler.ErrorCategory category, 
            Exception exception, Action retryAction)
        {
            // Check if this error is suppressed
            string suppressionKey = GetSuppressionKey(title, message, category);
            if (EditorPrefs.GetBool(suppressionKey, false))
            {
                // Error is suppressed, just log it and return
                Debug.LogWarning($"[ModelLibrary] Suppressed error: {title}: {message}");
                ErrorLogger.LogError(title, message, category, exception);
                return;
            }

            // Don't show multiple dialogs at once
            if (_isShowing)
            {
                ErrorLogger.LogError(title, message, category, exception);
                return;
            }

            _isShowing = true;

            ErrorDialogWindow window = CreateInstance<ErrorDialogWindow>();
            window._title = title;
            window._message = message;
            window._category = category;
            window._exception = exception;
            window._retryAction = retryAction;
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(__WINDOW_MIN_WIDTH, __WINDOW_MIN_HEIGHT);
            window.maxSize = new Vector2(__WINDOW_MAX_WIDTH, __WINDOW_MAX_HEIGHT);
            window.ShowUtility(); // Show as utility window (modal-like behavior)
            window.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Title
            EditorGUILayout.LabelField(_title, UIStyles.TitleLabel);
            
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Message (scrollable)
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(__MESSAGE_SCROLL_HEIGHT));
            EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Category badge
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                Color categoryColor = GetCategoryColor(_category);
                Color originalColor = GUI.color;
                GUI.color = categoryColor;
                GUILayout.Label($"[{_category}]", EditorStyles.miniLabel);
                GUI.color = originalColor;
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Details section (collapsible)
            if (_exception != null)
            {
                _showDetails = EditorGUILayout.Foldout(_showDetails, "Technical Details", true);
                if (_showDetails)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"Exception Type: {_exception.GetType().FullName}", EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrEmpty(_exception.Message))
                    {
                        EditorGUILayout.LabelField($"Message: {_exception.Message}", EditorStyles.wordWrappedMiniLabel);
                    }
                    if (!string.IsNullOrEmpty(_exception.StackTrace))
                    {
                        EditorGUILayout.Space(UIConstants.SPACING_SMALL);
                        EditorGUILayout.LabelField("Stack Trace:", UIStyles.MutedLabel);
                        EditorGUILayout.TextArea(_exception.StackTrace, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(__STACK_TRACE_HEIGHT));
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(UIConstants.SPACING_SMALL);

            // Don't show again checkbox
            _dontShowAgain = EditorGUILayout.ToggleLeft("Don't show this error again", _dontShowAgain);

            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

            // Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Retry button (prominent, using primary style)
                if (UIStyles.DrawPrimaryButton("Retry", GUILayout.Width(__BUTTON_WIDTH), GUILayout.Height(UIConstants.BUTTON_HEIGHT_EXTRA_LARGE)))
                {
                    HandleSuppression();
                    if (_retryAction != null)
                    {
                        try
                        {
                            _retryAction();
                        }
                        catch (Exception retryEx)
                        {
                            ErrorHandler.ShowErrorWithRetry(_title, $"Retry failed: {retryEx.Message}", 
                                null, retryEx);
                        }
                    }
                    Close();
                }

                GUILayout.Space(UIConstants.SPACING_DEFAULT);

                // OK button (using secondary style)
                if (UIStyles.DrawSecondaryButton("OK", GUILayout.Width(__BUTTON_WIDTH), GUILayout.Height(UIConstants.BUTTON_HEIGHT_EXTRA_LARGE)))
                {
                    HandleSuppression();
                    Close();
                }
            }

            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
        }

        private void OnDestroy() => _isShowing = false;

        private void HandleSuppression()
        {
            if (_dontShowAgain)
            {
                string suppressionKey = GetSuppressionKey(_title, _message, _category);
                EditorPrefs.SetBool(suppressionKey, true);
                RegisterSuppressionKey(suppressionKey);
                Debug.Log($"[ModelLibrary] Error suppressed: {_title}");
            }
        }

        private static string GetSuppressionKey(string title, string message, ErrorHandler.ErrorCategory category)
        {
            // Create a unique key based on title, first 50 chars of message, and category
            string messageHash = message.Length > 50 ? message.Substring(0, 50) : message;
            return $"ModelLibrary.ErrorSuppressed.{category}.{title}.{messageHash.GetHashCode()}";
        }

        private static void RegisterSuppressionKey(string key)
        {
            List<string> keys = GetRegisteredSuppressionKeys();
            if (!keys.Contains(key))
            {
                keys.Add(key);
                string serialized = string.Join("|", keys);
                EditorPrefs.SetString(__SuppressionKeysPrefKey, serialized);
            }
        }

        private static List<string> GetRegisteredSuppressionKeys()
        {
            string serialized = EditorPrefs.GetString(__SuppressionKeysPrefKey, string.Empty);
            List<string> keys = new List<string>();
            if (!string.IsNullOrEmpty(serialized))
            {
                string[] parts = serialized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (!string.IsNullOrEmpty(part) && !keys.Contains(part))
                    {
                        keys.Add(part);
                    }
                }
            }
            return keys;
        }

        private Color GetCategoryColor(ErrorHandler.ErrorCategory category)
        {
            return category switch
            {
                ErrorHandler.ErrorCategory.Connection => new Color(1f, 0.6f, 0f), // Orange
                ErrorHandler.ErrorCategory.FileSystem => new Color(1f, 0.3f, 0.3f), // Red
                ErrorHandler.ErrorCategory.Validation => new Color(1f, 0.8f, 0f), // Yellow
                ErrorHandler.ErrorCategory.Permission => new Color(1f, 0.5f, 0.5f), // Light red
                ErrorHandler.ErrorCategory.Configuration => new Color(0.5f, 0.5f, 1f), // Light blue
                _ => Color.gray
            };
        }

        /// <summary>
        /// Clears all suppressed error preferences.
        /// </summary>
        public static void ClearSuppressions()
        {
            List<string> keys = GetRegisteredSuppressionKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                EditorPrefs.DeleteKey(key);
            }
            EditorPrefs.DeleteKey(__SuppressionKeysPrefKey);
        }
    }
}

