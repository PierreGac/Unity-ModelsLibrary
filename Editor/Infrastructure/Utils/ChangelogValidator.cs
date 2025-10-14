using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModelLibrary.Editor.Utils
{
    /// <summary>
    /// Utility class for validating changelog entries.
    /// Provides comprehensive validation rules for changelog content.
    /// </summary>
    public static class ChangelogValidator
    {
        // Validation Constants
        private const int __MIN_CHANGELOG_LENGTH = 10;
        private const int __MAX_CHANGELOG_LENGTH = 1000;
        private const int __MAX_LINE_LENGTH = 100;
        private const int __MAX_LINES = 20;

        /// <summary>
        /// Validates a changelog entry for completeness and quality.
        /// </summary>
        /// <param name="changelog">The changelog text to validate</param>
        /// <param name="isUpdateMode">Whether this is an update (requires non-empty changelog)</param>
        /// <returns>List of validation error messages, empty if valid</returns>
        public static List<string> ValidateChangelog(string changelog, bool isUpdateMode = false)
        {
            List<string> errors = new List<string>();

            if (string.IsNullOrWhiteSpace(changelog))
            {
                if (isUpdateMode)
                {
                    errors.Add("Changelog is required for updates");
                }
                return errors; // For new models, empty changelog is acceptable
            }

            string trimmedChangelog = changelog.Trim();

            // Check minimum length
            if (trimmedChangelog.Length < __MIN_CHANGELOG_LENGTH)
            {
                errors.Add($"Changelog must be at least {__MIN_CHANGELOG_LENGTH} characters long");
            }

            // Check maximum length
            if (trimmedChangelog.Length > __MAX_CHANGELOG_LENGTH)
            {
                errors.Add($"Changelog must not exceed {__MAX_CHANGELOG_LENGTH} characters");
            }

            // Check for meaningful content (not just repeated characters or spaces)
            if (IsMeaninglessContent(trimmedChangelog))
            {
                errors.Add("Changelog must contain meaningful content");
            }

            // Check line length
            string[] lines = trimmedChangelog.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length > __MAX_LINE_LENGTH)
                {
                    errors.Add($"Line {i + 1} exceeds maximum length of {__MAX_LINE_LENGTH} characters");
                }
            }

            // Check maximum number of lines
            if (lines.Length > __MAX_LINES)
            {
                errors.Add($"Changelog must not exceed {__MAX_LINES} lines");
            }

            // Check for invalid characters
            List<string> invalidCharErrors = ValidateCharacters(trimmedChangelog);
            errors.AddRange(invalidCharErrors);

            // Check for proper formatting
            List<string> formattingErrors = ValidateFormatting(trimmedChangelog);
            errors.AddRange(formattingErrors);

            return errors;
        }

        /// <summary>
        /// Validates changelog characters for invalid or problematic content.
        /// </summary>
        /// <param name="changelog">The changelog text to validate</param>
        /// <returns>List of character-related validation errors</returns>
        private static List<string> ValidateCharacters(string changelog)
        {
            List<string> errors = new List<string>();

            // Check for control characters (except newlines and tabs)
            for (int i = 0; i < changelog.Length; i++)
            {
                char c = changelog[i];
                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    errors.Add($"Changelog contains invalid control character at position {i + 1}");
                    break; // Only report the first invalid character
                }
            }

            // Check for excessive special characters
            int specialCharCount = 0;
            for (int i = 0; i < changelog.Length; i++)
            {
                char c = changelog[i];
                if (!char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != '.' && c != ',' && c != '!' && c != '?' && c != '-' && c != '_')
                {
                    specialCharCount++;
                }
            }

            double specialCharRatio = (double)specialCharCount / changelog.Length;
            if (specialCharRatio > 0.3) // More than 30% special characters
            {
                errors.Add("Changelog contains too many special characters");
            }

            return errors;
        }

        /// <summary>
        /// Validates changelog formatting and structure.
        /// </summary>
        /// <param name="changelog">The changelog text to validate</param>
        /// <returns>List of formatting-related validation errors</returns>
        private static List<string> ValidateFormatting(string changelog)
        {
            List<string> errors = new List<string>();

            // Check for proper sentence structure
            if (!changelog.EndsWith(".", StringComparison.Ordinal) &&
                !changelog.EndsWith("!", StringComparison.Ordinal) &&
                !changelog.EndsWith("?", StringComparison.Ordinal))
            {
                errors.Add("Changelog should end with proper punctuation (. ! ?)");
            }

            // Check for excessive whitespace
            if (changelog.Contains("  ") || changelog.Contains("\t\t"))
            {
                errors.Add("Changelog contains excessive whitespace");
            }

            // Check for proper capitalization
            if (changelog.Length > 0 && char.IsLower(changelog[0]))
            {
                errors.Add("Changelog should start with a capital letter");
            }

            // Check for repeated words
            List<string> repeatedWordErrors = CheckRepeatedWords(changelog);
            errors.AddRange(repeatedWordErrors);

            return errors;
        }

        /// <summary>
        /// Checks for repeated words in the changelog.
        /// </summary>
        /// <param name="changelog">The changelog text to check</param>
        /// <returns>List of repeated word errors</returns>
        private static List<string> CheckRepeatedWords(string changelog)
        {
            List<string> errors = new List<string>();

            // Split into words (simple approach)
            string[] words = Regex.Split(changelog.ToLowerInvariant(), @"\W+");

            for (int i = 0; i < words.Length - 1; i++)
            {
                if (string.IsNullOrEmpty(words[i]))
                {
                    continue;
                }

                int repeatCount = 1;
                for (int j = i + 1; j < words.Length; j++)
                {
                    if (words[i] == words[j])
                    {
                        repeatCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (repeatCount >= 3) // Same word repeated 3+ times
                {
                    errors.Add($"Word '{words[i]}' is repeated {repeatCount} times consecutively");
                    break; // Only report the first repeated word
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks if the changelog content is meaningful (not just repeated characters or spaces).
        /// </summary>
        /// <param name="changelog">The changelog text to check</param>
        /// <returns>True if the content is meaningless, false otherwise</returns>
        private static bool IsMeaninglessContent(string changelog)
        {
            if (string.IsNullOrEmpty(changelog))
            {
                return true;
            }

            // Check for repeated single character (more than 5 times)
            if (changelog.Length > 5)
            {
                char firstChar = changelog[0];
                int repeatCount = 1;
                for (int i = 1; i < changelog.Length; i++)
                {
                    if (changelog[i] == firstChar)
                    {
                        repeatCount++;
                    }
                    else
                    {
                        break;
                    }
                }
                if (repeatCount > 5) // More than 5 repeated characters
                {
                    return true;
                }
            }

            // Check for only whitespace and punctuation
            bool onlySpecialChars = true;
            for (int i = 0; i < changelog.Length; i++)
            {
                char c = changelog[i];
                if (char.IsLetterOrDigit(c))
                {
                    onlySpecialChars = false;
                    break;
                }
            }
            if (onlySpecialChars)
            {
                return true;
            }

            // Check for repeated words (like "asdf asdf asdf asdf")
            string[] words = Regex.Split(changelog.ToLowerInvariant(), @"\W+");
            if (words.Length > 2)
            {
                string firstWord = words[0];
                if (!string.IsNullOrEmpty(firstWord))
                {
                    int wordRepeatCount = 1;
                    for (int i = 1; i < words.Length; i++)
                    {
                        if (words[i] == firstWord)
                        {
                            wordRepeatCount++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (wordRepeatCount >= 3) // Same word repeated 3+ times
                    {
                        return true;
                    }
                }
            }

            // Check for very short meaningful content (less than 3 unique words)
            string meaningfulContent = Regex.Replace(changelog, @"[^\w]", " ");
            string[] uniqueWords = meaningfulContent.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (uniqueWords.Length < 3)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets validation suggestions for improving a changelog entry.
        /// </summary>
        /// <param name="changelog">The changelog text to analyze</param>
        /// <returns>List of improvement suggestions</returns>
        public static List<string> GetValidationSuggestions(string changelog)
        {
            List<string> suggestions = new List<string>();

            if (string.IsNullOrWhiteSpace(changelog))
            {
                suggestions.Add("• Provide a brief description of what changed");
                suggestions.Add("• Include the reason for the update");
                suggestions.Add("• Mention any new features or fixes");
                return suggestions;
            }

            string trimmedChangelog = changelog.Trim();

            // Length suggestions
            if (trimmedChangelog.Length < __MIN_CHANGELOG_LENGTH)
            {
                suggestions.Add("• Add more details about the changes made");
                suggestions.Add("• Explain the impact or benefit of the update");
            }
            else if (trimmedChangelog.Length > __MAX_CHANGELOG_LENGTH)
            {
                suggestions.Add("• Consider shortening the description");
                suggestions.Add("• Focus on the most important changes");
            }

            // Formatting suggestions
            if (!trimmedChangelog.EndsWith(".", StringComparison.Ordinal) &&
                !trimmedChangelog.EndsWith("!", StringComparison.Ordinal) &&
                !trimmedChangelog.EndsWith("?", StringComparison.Ordinal))
            {
                suggestions.Add("• End the description with proper punctuation");
            }

            if (trimmedChangelog.Length > 0 && char.IsLower(trimmedChangelog[0]))
            {
                suggestions.Add("• Start the description with a capital letter");
            }

            // Content suggestions
            if (IsMeaninglessContent(trimmedChangelog))
            {
                suggestions.Add("• Provide specific details about what was changed");
                suggestions.Add("• Include version numbers or feature names");
                suggestions.Add("• Explain the purpose of the update");
            }

            return suggestions;
        }

        /// <summary>
        /// Sanitizes a changelog entry by removing invalid characters and normalizing formatting.
        /// </summary>
        /// <param name="changelog">The changelog text to sanitize</param>
        /// <returns>Sanitized changelog text</returns>
        public static string SanitizeChangelog(string changelog)
        {
            if (string.IsNullOrEmpty(changelog))
            {
                return changelog;
            }

            // Remove control characters (except newlines and tabs) and replace with spaces
            string sanitized = "";
            for (int i = 0; i < changelog.Length; i++)
            {
                char c = changelog[i];
                if (char.IsControl(c))
                {
                    if (c == '\n' || c == '\r' || c == '\t')
                    {
                        // Keep newlines, carriage returns, and tabs
                        sanitized += c;
                    }
                    else
                    {
                        // Replace other control characters with spaces to maintain word separation
                        sanitized += " ";
                    }
                }
                else
                {
                    sanitized += c;
                }
            }

            // Normalize whitespace
            sanitized = Regex.Replace(sanitized, @"[ \t]+", " "); // Collapse multiple spaces/tabs into single space
            sanitized = Regex.Replace(sanitized, @"\n[ \t]+", "\n"); // Remove leading spaces/tabs after newlines
            sanitized = Regex.Replace(sanitized, @"\n{2,}", "\n"); // Collapse multiple newlines into single newline

            // Trim and ensure proper ending
            sanitized = sanitized.Trim();
            if (!string.IsNullOrEmpty(sanitized) && !sanitized.EndsWith(".", StringComparison.Ordinal) &&
                !sanitized.EndsWith("!", StringComparison.Ordinal) && !sanitized.EndsWith("?", StringComparison.Ordinal))
            {
                sanitized += ".";
            }

            return sanitized;
        }
    }
}
