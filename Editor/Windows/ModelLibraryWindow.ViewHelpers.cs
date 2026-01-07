using System;
using System.Reflection;
using UnityEditor;

namespace ModelLibrary.Editor.Windows
{
    /// <summary>
    /// Partial class containing helper methods for view management and reflection operations.
    /// Provides utilities for initializing and rendering EditorWindow instances as views.
    /// </summary>
    public partial class ModelLibraryWindow
    {
        /// <summary>
        /// Binding flags for accessing private members via reflection.
        /// </summary>
        private const BindingFlags PRIVATE_INSTANCE_FLAGS = BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Calls a private method on an object instance using reflection.
        /// </summary>
        /// <param name="instance">The object instance to call the method on.</param>
        /// <param name="methodName">The name of the method to call.</param>
        /// <param name="parameters">Optional parameters to pass to the method.</param>
        /// <returns>True if the method was found and called successfully, false otherwise.</returns>
        private bool InvokePrivateMethod(object instance, string methodName, object[] parameters = null)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                MethodInfo method = instance.GetType().GetMethod(methodName, PRIVATE_INSTANCE_FLAGS);
                if (method != null)
                {
                    method.Invoke(instance, parameters);
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ModelLibraryWindow] Failed to invoke method '{methodName}' on {instance.GetType().Name}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Sets a private field value on an object instance using reflection.
        /// </summary>
        /// <param name="instance">The object instance to set the field on.</param>
        /// <param name="fieldName">The name of the field to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if the field was found and set successfully, false otherwise.</returns>
        private bool SetPrivateField(object instance, string fieldName, object value)
        {
            if (instance == null)
            {
                return false;
            }

            try
            {
                FieldInfo field = instance.GetType().GetField(fieldName, PRIVATE_INSTANCE_FLAGS);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ModelLibraryWindow] Failed to set field '{fieldName}' on {instance.GetType().Name}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Gets a private field value from an object instance using reflection.
        /// </summary>
        /// <typeparam name="T">The expected type of the field value.</typeparam>
        /// <param name="instance">The object instance to get the field from.</param>
        /// <param name="fieldName">The name of the field to get.</param>
        /// <param name="defaultValue">Default value to return if field is not found or cannot be cast.</param>
        /// <returns>The field value, or defaultValue if not found or cannot be cast.</returns>
        private T GetPrivateField<T>(object instance, string fieldName, T defaultValue = default(T))
        {
            if (instance == null)
            {
                return defaultValue;
            }

            try
            {
                FieldInfo field = instance.GetType().GetField(fieldName, PRIVATE_INSTANCE_FLAGS);
                if (field != null)
                {
                    object value = field.GetValue(instance);
                    if (value is T)
                    {
                        return (T)value;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ModelLibraryWindow] Failed to get field '{fieldName}' from {instance.GetType().Name}: {ex.Message}");
            }

            return defaultValue;
        }

        /// <summary>
        /// Initializes an EditorWindow instance by calling its OnEnable method.
        /// </summary>
        /// <typeparam name="T">The type of EditorWindow to initialize.</typeparam>
        /// <param name="instance">The EditorWindow instance to initialize.</param>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        private bool InitializeEditorWindowInstance<T>(T instance) where T : EditorWindow
        {
            if (instance == null)
            {
                return false;
            }

            return InvokePrivateMethod(instance, "OnEnable");
        }

        /// <summary>
        /// Renders an EditorWindow instance by calling its OnGUI method.
        /// </summary>
        /// <typeparam name="T">The type of EditorWindow to render.</typeparam>
        /// <param name="instance">The EditorWindow instance to render.</param>
        /// <returns>True if rendering was successful, false otherwise.</returns>
        private bool RenderEditorWindowInstance<T>(T instance) where T : EditorWindow
        {
            if (instance == null)
            {
                return false;
            }

            return InvokePrivateMethod(instance, "OnGUI");
        }

        /// <summary>
        /// Cleans up an EditorWindow instance by calling its OnDisable method.
        /// </summary>
        /// <typeparam name="T">The type of EditorWindow to clean up.</typeparam>
        /// <param name="instance">The EditorWindow instance to clean up.</param>
        /// <returns>True if cleanup was successful, false otherwise.</returns>
        private bool CleanupEditorWindowInstance<T>(T instance) where T : EditorWindow
        {
            if (instance == null)
            {
                return false;
            }

            return InvokePrivateMethod(instance, "OnDisable");
        }

        /// <summary>
        /// Draws a generic view that uses a hidden EditorWindow instance for rendering.
        /// Handles initialization, instance management, and rendering.
        /// OnEnable is only called once when the instance is first created.
        /// </summary>
        /// <typeparam name="T">The type of EditorWindow to use for rendering.</typeparam>
        /// <param name="instance">Reference to the EditorWindow instance (will be created if null).</param>
        /// <param name="initializeAction">Optional action to perform during initialization (e.g., setting fields). Called before OnEnable.</param>
        /// <returns>True if the view was rendered successfully, false otherwise.</returns>
        private bool DrawEditorWindowView<T>(ref T instance, Action<T> initializeAction = null) where T : EditorWindow
        {
            // Create and initialize instance if needed
            bool isNewInstance = false;
            if (instance == null)
            {
                instance = CreateInstance<T>();
                if (instance == null)
                {
                    UnityEngine.Debug.LogError($"[ModelLibraryWindow] Failed to create instance of {typeof(T).Name}");
                    return false;
                }
                isNewInstance = true;
            }

            // Perform custom initialization if provided (only on new instances or when explicitly needed)
            if (isNewInstance && initializeAction != null)
            {
                try
                {
                    initializeAction(instance);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ModelLibraryWindow] Error during view initialization for {typeof(T).Name}: {ex.Message}");
                    return false;
                }
            }

            // Initialize the instance (call OnEnable) only for new instances
            // OnEnable should not be called every frame, only when the instance is first created
            if (isNewInstance)
            {
                if (!InitializeEditorWindowInstance(instance))
                {
                    UnityEngine.Debug.LogWarning($"[ModelLibraryWindow] OnEnable not found or failed for {typeof(T).Name}");
                }
            }

            // Render the instance (call OnGUI) - this is called every frame
            return RenderEditorWindowInstance(instance);
        }
    }
}

