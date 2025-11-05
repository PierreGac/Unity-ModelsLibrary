using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for showing input dialogs in the Unity Editor.
    /// Provides simple text input functionality for user prompts.
    /// </summary>
    public static class EditorInputDialog
    {
        /// <summary>
        /// Shows a simple input dialog and returns the user's input.
        /// </summary>
        /// <param name="title">Title of the dialog.</param>
        /// <param name="message">Message/question to display.</param>
        /// <param name="defaultValue">Default value for the input field.</param>
        /// <returns>The user's input string, or null if cancelled.</returns>
        public static string Show(string title, string message, string defaultValue = "")
        {
            // Unity doesn't have a built-in input dialog, so we use a simple approach
            // For a more sophisticated solution, we could create a custom EditorWindow
            // For now, we'll use a workaround with EditorUtility.DisplayDialog and a simple input
            
            // Note: Unity's EditorUtility.DisplayDialog doesn't support text input
            // We'll create a simple modal window approach
            InputDialogWindow window = EditorWindow.GetWindow<InputDialogWindow>(true, title);
            window.Initialize(message, defaultValue);
            window.ShowModal();
            
            return window.Result;
        }

        /// <summary>
        /// Simple modal window for text input.
        /// </summary>
        private class InputDialogWindow : EditorWindow
        {
            private string _message;
            private string _input;
            private string _result;
            private bool _initialized;
            
            public string Result => _result;
            
            public void Initialize(string message, string defaultValue)
            {
                _message = message;
                _input = defaultValue;
                _initialized = true;
            }
            
            private void OnGUI()
            {
                if (!_initialized)
                {
                    return;
                }
                
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(5);
                
                GUI.SetNextControlName("InputField");
                _input = EditorGUILayout.TextField(_input);
                
                EditorGUILayout.Space(10);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("OK", GUILayout.Width(80)))
                    {
                        _result = _input;
                        Close();
                    }
                    
                    if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                    {
                        _result = null;
                        Close();
                    }
                }
                
                // Focus the input field when window opens
                if (Event.current.type == EventType.Layout)
                {
                    EditorGUI.FocusTextInControl("InputField");
                }
            }
        }
    }
}

