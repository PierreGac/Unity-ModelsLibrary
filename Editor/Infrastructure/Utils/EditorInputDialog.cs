using System;
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
        /// STABILITY (audit HIGH-16): The previous implementation used
        /// <c>window.ShowModal()</c> followed by <c>return window.Result</c>,
        /// but <c>EditorWindow.ShowModal()</c> is non-blocking in the Unity
        /// Editor — it returns immediately, so <c>Result</c> was always null
        /// (the user hadn't typed anything yet). This made the "Save Filter
        /// Preset" feature silently no-op.
        /// The new callback-based API (<see cref="Show(string, string, string, Action{string})"/>)
        /// is the correct pattern for Unity editor input dialogs.
        /// </summary>
        /// <param name="title">Title of the dialog.</param>
        /// <param name="message">Message/question to display.</param>
        /// <param name="defaultValue">Default value for the input field.</param>
        /// <returns>
        /// Always returns <c>null</c> (synchronous variant kept for backward
        /// compatibility). Use the callback-based overload
        /// <see cref="Show(string, string, string, Action{string})"/> instead.
        /// </returns>
        [Obsolete("Use the callback-based Show(title, message, defaultValue, onResult) instead. This synchronous overload always returns null because Unity's EditorWindow.ShowModal() is non-blocking.")]
        public static string Show(string title, string message, string defaultValue = "")
        {
            // Delegate to the callback-based API with a no-op callback.
            Show(title, message, defaultValue, null);
            return null;
        }

        /// <summary>
        /// Shows an input dialog and invokes the given callback with the
        /// user's input (or <c>null</c> if cancelled) when the dialog closes.
        /// This is the correct pattern for Unity editor input dialogs.
        /// </summary>
        /// <param name="title">Title of the dialog.</param>
        /// <param name="message">Message/question to display.</param>
        /// <param name="defaultValue">Default value for the input field.</param>
        /// <param name="onResult">Callback invoked with the user's input, or null if cancelled. May be null.</param>
        public static void Show(string title, string message, string defaultValue, Action<string> onResult)
        {
            InputDialogWindow window = ScriptableObject.CreateInstance<InputDialogWindow>();
            window.titleContent = new GUIContent(title);
            window.Initialize(message, defaultValue, onResult);
            window.ShowModal();
        }

        /// <summary>
        /// Simple modal window for text input.
        /// </summary>
        private class InputDialogWindow : EditorWindow
        {
            private string _message;
            private string _input;
            private Action<string> _onResult;
            private bool _initialized;
            private bool _closed;

            public void Initialize(string message, string defaultValue, Action<string> onResult)
            {
                _message = message;
                _input = defaultValue ?? string.Empty;
                _onResult = onResult;
                _initialized = true;
                _closed = false;
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

                // Enter = OK, Esc = Cancel.
                Event e = Event.current;
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        Confirm();
                        e.Use();
                        return;
                    }
                    if (e.keyCode == KeyCode.Escape)
                    {
                        Cancel();
                        e.Use();
                        return;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("OK", GUILayout.Width(80)))
                    {
                        Confirm();
                    }

                    if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                    {
                        Cancel();
                    }
                }

                // Focus the input field when window opens
                if (Event.current.type == EventType.Layout)
                {
                    EditorGUI.FocusTextInControl("InputField");
                }
            }

            private void Confirm()
            {
                if (_closed) return;
                _closed = true;
                _onResult?.Invoke(_input);
                Close();
            }

            private void Cancel()
            {
                if (_closed) return;
                _closed = true;
                _onResult?.Invoke(null);
                Close();
            }
        }
    }
}
