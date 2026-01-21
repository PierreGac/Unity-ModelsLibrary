using System;
using UnityEditor;
using UnityEngine;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Centralized GUIStyles for a consistent, polished UI across editor windows.
    /// </summary>
    public static class UIStyles
    {
        private static bool _initialized;
        private static GUIStyle _titleLabel;
        private static GUIStyle _sectionHeader;
        private static GUIStyle _cardBox;
        private static GUIStyle _mutedLabel;
        private static GUIStyle _tagPill;
        private static GUIStyle _toolbarButton;
        private static GUIStyle _toolbarPopup;
        private static GUIStyle _buttonPrimary;
        private static GUIStyle _buttonSecondary;
        private static GUIStyle _buttonSuccess;
        private static GUIStyle _buttonDanger;
        private static GUIStyle _buttonSmall;

        /// <summary>Large title label for prominent headings.</summary>
        public static GUIStyle TitleLabel
        {
            get
            {
                EnsureStyles();
                return _titleLabel;
            }
        }

        /// <summary>Section header label for sub-sections.</summary>
        public static GUIStyle SectionHeader
        {
            get
            {
                EnsureStyles();
                return _sectionHeader;
            }
        }

        /// <summary>Card-like container for grouping content.</summary>
        public static GUIStyle CardBox
        {
            get
            {
                EnsureStyles();
                return _cardBox;
            }
        }

        /// <summary>Muted label for secondary information.</summary>
        public static GUIStyle MutedLabel
        {
            get
            {
                EnsureStyles();
                return _mutedLabel;
            }
        }

        /// <summary>Pill style for tags and badges.</summary>
        public static GUIStyle TagPill
        {
            get
            {
                EnsureStyles();
                return _tagPill;
            }
        }

        /// <summary>Toolbar button style for consistent toolbars.</summary>
        public static GUIStyle ToolbarButton
        {
            get
            {
                EnsureStyles();
                return _toolbarButton;
            }
        }

        /// <summary>Toolbar popup style for consistent toolbars.</summary>
        public static GUIStyle ToolbarPopup
        {
            get
            {
                EnsureStyles();
                return _toolbarPopup;
            }
        }

        /// <summary>Primary button style for important actions (blue).</summary>
        public static GUIStyle ButtonPrimary
        {
            get
            {
                EnsureStyles();
                return _buttonPrimary;
            }
        }

        /// <summary>Secondary button style for standard actions (gray).</summary>
        public static GUIStyle ButtonSecondary
        {
            get
            {
                EnsureStyles();
                return _buttonSecondary;
            }
        }

        /// <summary>Success button style for positive actions (green).</summary>
        public static GUIStyle ButtonSuccess
        {
            get
            {
                EnsureStyles();
                return _buttonSuccess;
            }
        }

        /// <summary>Danger button style for destructive actions (red).</summary>
        public static GUIStyle ButtonDanger
        {
            get
            {
                EnsureStyles();
                return _buttonDanger;
            }
        }

        /// <summary>Small button style for compact layouts.</summary>
        public static GUIStyle ButtonSmall
        {
            get
            {
                EnsureStyles();
                return _buttonSmall;
            }
        }

        private static void EnsureStyles()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _titleLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = UIConstants.FONT_SIZE_TITLE
            };

            _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = UIConstants.FONT_SIZE_SECTION
            };

            _mutedLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = UIConstants.FONT_SIZE_MUTED,
                wordWrap = true
            };

            _cardBox = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_STANDARD),
                margin = new RectOffset(
                    UIConstants.PADDING_SMALL,
                    UIConstants.PADDING_SMALL,
                    UIConstants.PADDING_SMALL,
                    UIConstants.PADDING_SMALL)
            };

            _tagPill = new GUIStyle(EditorStyles.miniLabel)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_SMALL,
                    UIConstants.PADDING_SMALL,
                    UIConstants.PADDING_EXTRA_SMALL,
                    UIConstants.PADDING_EXTRA_SMALL),
                fontStyle = FontStyle.Bold
            };

            _toolbarButton = new GUIStyle(EditorStyles.toolbarButton);
            _toolbarPopup = new GUIStyle(EditorStyles.toolbarPopup);

            // Primary button (blue, for important actions)
            _buttonPrimary = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_STANDARD),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UIConstants.COLOR_BUTTON_PRIMARY_TEXT },
                hover = { textColor = UIConstants.COLOR_BUTTON_PRIMARY_TEXT },
                active = { textColor = UIConstants.COLOR_BUTTON_PRIMARY_TEXT }
            };

            // Secondary button (gray, for standard actions)
            _buttonSecondary = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_STANDARD),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UIConstants.COLOR_BUTTON_SECONDARY_TEXT },
                hover = { textColor = UIConstants.COLOR_BUTTON_SECONDARY_TEXT },
                active = { textColor = UIConstants.COLOR_BUTTON_SECONDARY_TEXT }
            };

            // Success button (green, for positive actions)
            _buttonSuccess = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_STANDARD),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };

            // Danger button (red, for destructive actions)
            _buttonDanger = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_LARGE,
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_STANDARD),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };

            // Small button (compact version)
            _buttonSmall = new GUIStyle(EditorStyles.miniButton)
            {
                padding = new RectOffset(
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_STANDARD,
                    UIConstants.PADDING_SMALL,
                    UIConstants.PADDING_SMALL),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = UIConstants.COLOR_BUTTON_SECONDARY_TEXT },
                hover = { textColor = UIConstants.COLOR_BUTTON_SECONDARY_TEXT },
                active = { textColor = UIConstants.COLOR_BUTTON_SECONDARY_TEXT }
            };
        }

        /// <summary>
        /// Draws a button with primary styling (blue background, white text).
        /// </summary>
        public static bool DrawPrimaryButton(string text, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            Color originalColor = GUI.color;
            GUI.backgroundColor = UIConstants.COLOR_BUTTON_PRIMARY_BG;
            GUI.color = UIConstants.COLOR_BUTTON_PRIMARY_TEXT;
            bool result = GUILayout.Button(text, ButtonPrimary, options);
            GUI.backgroundColor = originalBg;
            GUI.color = originalColor;
            return result;
        }

        /// <summary>
        /// Draws a button with secondary styling (gray background, white text).
        /// </summary>
        public static bool DrawSecondaryButton(string text, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            Color originalColor = GUI.color;
            GUI.backgroundColor = UIConstants.COLOR_BUTTON_SECONDARY_BG;
            GUI.color = UIConstants.COLOR_BUTTON_SECONDARY_TEXT;
            bool result = GUILayout.Button(text, ButtonSecondary, options);
            GUI.backgroundColor = originalBg;
            GUI.color = originalColor;
            return result;
        }

        /// <summary>
        /// Draws a button with success styling (green background, white text).
        /// </summary>
        public static bool DrawSuccessButton(string text, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            Color originalColor = GUI.color;
            GUI.backgroundColor = UIConstants.COLOR_BUTTON_SUCCESS_BG;
            GUI.color = Color.white;
            bool result = GUILayout.Button(text, ButtonSuccess, options);
            GUI.backgroundColor = originalBg;
            GUI.color = originalColor;
            return result;
        }

        /// <summary>
        /// Draws a button with danger styling (red background, white text).
        /// </summary>
        public static bool DrawDangerButton(string text, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            Color originalColor = GUI.color;
            GUI.backgroundColor = UIConstants.COLOR_BUTTON_DANGER_BG;
            GUI.color = Color.white;
            bool result = GUILayout.Button(text, ButtonDanger, options);
            GUI.backgroundColor = originalBg;
            GUI.color = originalColor;
            return result;
        }

        /// <summary>
        /// Draws a page header with optional subtitle and spacing.
        /// </summary>
        public static void DrawPageHeader(string title, string subtitle = null)
        {
            EditorGUILayout.LabelField(title, TitleLabel);
            if (!string.IsNullOrEmpty(subtitle))
            {
                EditorGUILayout.LabelField(subtitle, MutedLabel);
            }
            EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
        }

        /// <summary>
        /// Draws a section header with consistent spacing.
        /// </summary>
        public static void DrawSectionHeader(string title)
        {
            EditorGUILayout.LabelField(title, SectionHeader);
            EditorGUILayout.Space(UIConstants.SPACING_SMALL);
        }

        /// <summary>
        /// Creates a card-like vertical scope for grouping content.
        /// </summary>
        public static EditorGUILayout.VerticalScope BeginCard()
        {
            return new EditorGUILayout.VerticalScope(CardBox);
        }

        /// <summary>
        /// Draws a consistent empty state with optional primary/secondary actions.
        /// </summary>
        public static void DrawEmptyState(string title, string message, string primaryLabel, Action primaryAction, string secondaryLabel = null, Action secondaryAction = null)
        {
            EditorGUILayout.Space(UIConstants.SPACING_LARGE);
            using (EditorGUILayout.VerticalScope cardScope = BeginCard())
            {
                EditorGUILayout.LabelField(title, TitleLabel);
                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);
                EditorGUILayout.HelpBox(message, MessageType.Info);
                EditorGUILayout.Space(UIConstants.SPACING_DEFAULT);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(primaryAction == null))
                    {
                        if (DrawPrimaryButton(primaryLabel, GUILayout.Width(UIConstants.BUTTON_WIDTH_MEDIUM), GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                        {
                            primaryAction?.Invoke();
                        }
                    }

                    if (!string.IsNullOrEmpty(secondaryLabel))
                    {
                        using (new EditorGUI.DisabledScope(secondaryAction == null))
                        {
                            if (DrawSecondaryButton(secondaryLabel, GUILayout.Width(UIConstants.BUTTON_WIDTH_MEDIUM), GUILayout.Height(UIConstants.BUTTON_HEIGHT_LARGE)))
                            {
                                secondaryAction?.Invoke();
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>
        /// Draws a status badge using TagPill style (for installed/update status).
        /// </summary>
        /// <param name="text">Badge text to display.</param>
        /// <param name="textColor">Text color for the badge.</param>
        /// <param name="backgroundColor">Background color for the badge.</param>
        public static void DrawStatusBadge(string text, Color textColor, Color backgroundColor)
        {
            GUIStyle badgeStyle = new GUIStyle(TagPill)
            {
                normal = { textColor = textColor },
                alignment = TextAnchor.MiddleCenter
            };

            Color originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;

            using (new EditorGUILayout.HorizontalScope("box", GUILayout.ExpandWidth(false)))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(text, badgeStyle);
                GUILayout.FlexibleSpace();
            }

            GUI.backgroundColor = originalBgColor;
        }
    }
}
