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
            window.minSize = new Vector2(500, 300);
            window.maxSize = new Vector2(600, 500);
            window.ShowUtility(); // Show as utility window (modal-like behavior)
            window.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 16;
            titleStyle.wordWrap = true;
            EditorGUILayout.LabelField(_title, titleStyle);
            
            EditorGUILayout.Space(10);

            // Message (scrollable)
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
            GUIStyle messageStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            messageStyle.fontSize = 12;
            EditorGUILayout.LabelField(_message, messageStyle);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

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

            EditorGUILayout.Space(10);

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
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("Stack Trace:", EditorStyles.miniLabel);
                        EditorGUILayout.TextArea(_exception.StackTrace, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(100));
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            // Don't show again checkbox
            _dontShowAgain = EditorGUILayout.ToggleLeft("Don't show this error again", _dontShowAgain);

            EditorGUILayout.Space(10);

            // Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                // Retry button (prominent)
                GUIStyle retryStyle = new GUIStyle(GUI.skin.button);
                retryStyle.fontSize = 13;
                retryStyle.fontStyle = FontStyle.Bold;
                retryStyle.padding = new RectOffset(20, 20, 8, 8);
                
                Color originalBgColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.6f, 1f); // Blue
                Color originalTextColor = GUI.color;
                GUI.color = Color.white;

                if (GUILayout.Button("Retry", retryStyle, GUILayout.Width(120), GUILayout.Height(35)))
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

                GUI.backgroundColor = originalBgColor;
                GUI.color = originalTextColor;

                GUILayout.Space(10);

                // OK button
                if (GUILayout.Button("OK", GUILayout.Width(120), GUILayout.Height(35)))
                {
                    HandleSuppression();
                    Close();
                }
            }

            EditorGUILayout.Space(5);
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

