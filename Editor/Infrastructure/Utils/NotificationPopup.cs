using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Meta Quest-style notification popup system for displaying notifications in the Unity Editor.
    /// Provides a modern, animated notification system that appears at the top of the screen.
    /// </summary>
    public class NotificationPopup : EditorWindow
    {
        /// <summary>Queue of pending notifications to display.</summary>
        private static readonly Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
        /// <summary>Currently displayed notification, if any.</summary>
        private static NotificationData _currentNotification;
        /// <summary>Animation progress for the current notification (0-1).</summary>
        private static float _animationProgress = 0f;
        /// <summary>Whether the notification is currently animating in.</summary>
        private static bool _isAnimatingIn = false;
        /// <summary>Whether the notification is currently animating out.</summary>
        private static bool _isAnimatingOut = false;
        /// <summary>Time when the current notification started displaying.</summary>
        private static DateTime _notificationStartTime;
        /// <summary>Duration to display the notification before auto-dismissing (in seconds).</summary>
        private const float __NotificationDuration = 4f;
        /// <summary>Animation duration for fade in/out (in seconds).</summary>
        private const float __AnimationDuration = 0.3f;
        /// <summary>Singleton window instance.</summary>
        private static NotificationPopup _instance;

        /// <summary>
        /// Data structure for a notification.
        /// </summary>
        [Serializable]
        public class NotificationData
        {
            public string title;
            public string message;
            public NotificationType type;
            public Action onClickAction;
            public string actionButtonText;
        }

        /// <summary>
        /// Types of notifications that can be displayed.
        /// </summary>
        public enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error
        }

        /// <summary>
        /// Shows a notification popup in the Unity Editor.
        /// </summary>
        /// <param name="title">Title of the notification.</param>
        /// <param name="message">Message body of the notification.</param>
        /// <param name="type">Type of notification (affects color and icon).</param>
        /// <param name="onClickAction">Optional action to execute when notification is clicked.</param>
        /// <param name="actionButtonText">Optional text for an action button.</param>
        public static void Show(string title, string message, NotificationType type = NotificationType.Info, 
            Action onClickAction = null, string actionButtonText = null)
        {
            NotificationData notification = new NotificationData
            {
                title = title,
                message = message,
                type = type,
                onClickAction = onClickAction,
                actionButtonText = actionButtonText
            };

            _notificationQueue.Enqueue(notification);

            if (_instance == null)
            {
                _instance = GetWindow<NotificationPopup>(true);
                _instance.ShowPopup();
            }

            _instance.Repaint();
            ProcessNextNotification();
        }

        /// <summary>
        /// Unity lifecycle method called when the window is enabled.
        /// </summary>
        private void OnEnable()
        {
            _instance = this;
            // Configure window to be a small popup
            minSize = new Vector2(400f, 100f);
            maxSize = new Vector2(400f, 400f);
            // Start processing notifications
            EditorApplication.update += UpdateNotifications;
        }

        /// <summary>
        /// Unity lifecycle method called when the window is disabled.
        /// </summary>
        private void OnDisable()
        {
            EditorApplication.update -= UpdateNotifications;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Updates notification animations and timing.
        /// </summary>
        private static void UpdateNotifications()
        {
            if (_currentNotification == null && _notificationQueue.Count > 0)
            {
                ProcessNextNotification();
            }

            if (_currentNotification != null)
            {
                float elapsed = (float)(DateTime.Now - _notificationStartTime).TotalSeconds;
                
                if (!_isAnimatingIn && !_isAnimatingOut && elapsed >= __NotificationDuration)
                {
                    DismissNotification();
                }
                else if (_isAnimatingIn || _isAnimatingOut)
                {
                    _animationProgress += Time.deltaTime / __AnimationDuration;
                    _animationProgress = Mathf.Clamp01(_animationProgress);
                    
                    if (_animationProgress >= 1f)
                    {
                        _isAnimatingIn = false;
                        if (_isAnimatingOut)
                        {
                            _currentNotification = null;
                            _isAnimatingOut = false;
                            ProcessNextNotification();
                        }
                    }
                }

                if (_instance != null)
                {
                    _instance.Repaint();
                }
            }
        }

        /// <summary>
        /// Processes the next notification in the queue.
        /// </summary>
        private static void ProcessNextNotification()
        {
            if (_currentNotification != null || _notificationQueue.Count == 0)
            {
                return;
            }

            _currentNotification = _notificationQueue.Dequeue();
            _notificationStartTime = DateTime.Now;
            _isAnimatingIn = true;
            _animationProgress = 0f;
        }

        /// <summary>
        /// Dismisses the current notification.
        /// </summary>
        private static void DismissNotification()
        {
            if (_currentNotification == null)
            {
                return;
            }

            _isAnimatingOut = true;
            _animationProgress = 0f;
        }

        /// <summary>
        /// Unity GUI method called to draw the window.
        /// </summary>
        private void OnGUI()
        {
            if (_currentNotification == null)
            {
                // Draw empty content while waiting
                EditorGUILayout.LabelField("Waiting for notifications...");
                return;
            }

            // Calculate position and size
            float width = 400f;
            float height = CalculateNotificationHeight(_currentNotification);
            
            // Update window size
            Vector2 size = new Vector2(width, height);
            if (position.size != size)
            {
                position = new Rect(position.x, position.y, size.x, size.y);
            }

            // Position window at top-right of main Unity window
            // Get main editor window position
            Rect mainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            float x = mainWindowRect.xMax - width - 20f;
            float y = mainWindowRect.yMin + 20f;
            
            // Update position if window is not already positioned
            if (Mathf.Abs(position.x - x) > 1f || Mathf.Abs(position.y - y) > 1f)
            {
                position = new Rect(x, y, width, height);
            }

            // Apply animation offset
            float offset = 0f;
            if (_isAnimatingIn)
            {
                offset = (1f - _animationProgress) * height;
            }
            else if (_isAnimatingOut)
            {
                offset = _animationProgress * height;
            }

            Rect notificationRect = new Rect(0, -offset, width, height);
            float alpha = _isAnimatingOut ? (1f - _animationProgress) : (_isAnimatingIn ? _animationProgress : 1f);

            // Draw notification background
            Color backgroundColor = GetNotificationColor(_currentNotification.type);
            backgroundColor.a = alpha;
            Color originalColor = GUI.color;
            GUI.color = backgroundColor;
            GUI.Box(notificationRect, GUIContent.none);
            GUI.color = originalColor;

            // Draw notification content
            Rect contentRect = new Rect(notificationRect.x + 10, notificationRect.y + 10, notificationRect.width - 20, notificationRect.height - 20);
            using (new GUI.GroupScope(contentRect))
            {
                Color textColor = Color.white;
                textColor.a = alpha;
                GUI.color = textColor;

                // Title
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = textColor }
                };
                GUI.Label(new Rect(0, 0, contentRect.width, 20), _currentNotification.title, titleStyle);

                // Message
                GUIStyle messageStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    wordWrap = true,
                    normal = { textColor = textColor }
                };
                float messageHeight = messageStyle.CalcHeight(new GUIContent(_currentNotification.message), contentRect.width);
                GUI.Label(new Rect(0, 25, contentRect.width, messageHeight), _currentNotification.message, messageStyle);

                // Action button
                if (!string.IsNullOrEmpty(_currentNotification.actionButtonText) && _currentNotification.onClickAction != null)
                {
                    float buttonY = 25 + messageHeight + 10;
                    if (GUI.Button(new Rect(0, buttonY, 100, 25), _currentNotification.actionButtonText))
                    {
                        if (_currentNotification.onClickAction != null)
                        {
                            _currentNotification.onClickAction.Invoke();
                        }
                        DismissNotification();
                    }
                }

                GUI.color = originalColor;
            }

            // Close button
            if (GUI.Button(new Rect(notificationRect.x + notificationRect.width - 25, notificationRect.y + 5, 20, 20), "×"))
            {
                DismissNotification();
            }
        }

        /// <summary>
        /// Calculates the height needed for a notification based on its content.
        /// </summary>
        private static float CalculateNotificationHeight(NotificationData notification)
        {
            float baseHeight = 60f;
            GUIStyle messageStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11
            };
            float messageHeight = messageStyle.CalcHeight(new GUIContent(notification.message), 380f);
            
            if (!string.IsNullOrEmpty(notification.actionButtonText))
            {
                messageHeight += 35f;
            }

            return baseHeight + messageHeight;
        }

        /// <summary>
        /// Gets the background color for a notification type.
        /// </summary>
        private static Color GetNotificationColor(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => new Color(0.2f, 0.8f, 0.2f),
                NotificationType.Warning => new Color(1f, 0.8f, 0.2f),
                NotificationType.Error => new Color(0.9f, 0.2f, 0.2f),
                _ => new Color(0.2f, 0.5f, 0.9f) // Info - blue
            };
        }

        /// <summary>
        /// Shows a success notification.
        /// </summary>
        public static void ShowSuccess(string title, string message, Action onClickAction = null) => Show(title, message, NotificationType.Success, onClickAction);

        /// <summary>
        /// Shows a warning notification.
        /// </summary>
        public static void ShowWarning(string title, string message, Action onClickAction = null) => Show(title, message, NotificationType.Warning, onClickAction);

        /// <summary>
        /// Shows an error notification.
        /// </summary>
        public static void ShowError(string title, string message, Action onClickAction = null) => Show(title, message, NotificationType.Error, onClickAction);

        /// <summary>
        /// Shows an info notification.
        /// </summary>
        public static void ShowInfo(string title, string message, Action onClickAction = null) => Show(title, message, NotificationType.Info, onClickAction);
    }
}

