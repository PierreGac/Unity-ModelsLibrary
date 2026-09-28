using System.Text;

namespace ModelLibrary.Editor.Serialization
{
    /// <summary>
    /// Rewrites dictionary objects that JsonUtility cannot read into entry-list JSON.
    /// </summary>
    internal static class DictionaryJsonMigration
    {
        private const string EXTRA_FIELD = "extra";
        private const string EXTRA_ENTRIES_FIELD = "extraEntries";
        private const string IMPORTER_FIELD = "modelImporters";
        private const string IMPORTER_ENTRIES_FIELD = "modelImporterEntries";
        private const string VERSIONS_FIELD = "versions";
        private const string VERSION_ENTRIES_FIELD = "versionEntries";
        private const string VALUE_PROPERTY = "value";
        private const string VERSIONS_PROPERTY = "versions";

        /// <summary>
        /// Converts object-form <c>extra</c> and <c>modelImporters</c> fields into entry lists.
        /// </summary>
        public static string PrepareModelMeta(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            json = RewriteObjectMap(json, EXTRA_FIELD, EXTRA_ENTRIES_FIELD, VALUE_PROPERTY, JsonMapValueKind.String);
            json = RewriteObjectMap(json, IMPORTER_FIELD, IMPORTER_ENTRIES_FIELD, VALUE_PROPERTY, JsonMapValueKind.Object);
            return json;
        }

        /// <summary>
        /// Converts an object-form <c>versions</c> field into an entry list.
        /// </summary>
        public static string PrepareModelIndex(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            return RewriteObjectMap(json, VERSIONS_FIELD, VERSION_ENTRIES_FIELD, VERSIONS_PROPERTY, JsonMapValueKind.Array);
        }

        private static string RewriteObjectMap(string json, string sourceField, string targetField, string valueProperty, JsonMapValueKind valueKind)
        {
            if (ContainsKey(json, targetField))
            {
                return json;
            }

            if (!TryFindObjectField(json, sourceField, out int fieldStart, out int fieldEnd, out int objectStart, out int objectEnd))
            {
                return json;
            }

            string entries = BuildEntries(json, objectStart, objectEnd, valueProperty, valueKind);
            return json.Substring(0, fieldStart) + "\"" + targetField + "\":[" + entries + "]" + json.Substring(fieldEnd);
        }

        private static string BuildEntries(string json, int objectStart, int objectEnd, string valueProperty, JsonMapValueKind valueKind)
        {
            StringBuilder builder = new StringBuilder();
            int index = objectStart + 1;
            bool first = true;
            while (index < objectEnd)
            {
                index = SkipWhitespace(json, index);
                if (index >= objectEnd || json[index] == '}')
                {
                    break;
                }

                if (json[index] == ',')
                {
                    index++;
                    continue;
                }

                if (!TryReadString(json, index, out string key, out int keyEnd))
                {
                    break;
                }

                index = SkipWhitespace(json, keyEnd);
                if (index >= json.Length || json[index] != ':')
                {
                    break;
                }

                index = SkipWhitespace(json, index + 1);
                if (!TryReadValue(json, index, valueKind, out string rawValue, out string textValue, out int valueEnd))
                {
                    break;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append("{\"key\":\"");
                builder.Append(EscapeJson(key));
                builder.Append("\",\"");
                builder.Append(valueProperty);
                builder.Append("\":");
                if (valueKind == JsonMapValueKind.String)
                {
                    builder.Append('"');
                    builder.Append(EscapeJson(textValue));
                    builder.Append('"');
                }
                else
                {
                    builder.Append(rawValue);
                }

                builder.Append('}');
                index = valueEnd;
            }

            return builder.ToString();
        }

        private static bool TryFindObjectField(string json, string fieldName, out int fieldStart, out int fieldEnd, out int objectStart, out int objectEnd)
        {
            fieldStart = 0;
            fieldEnd = 0;
            objectStart = 0;
            objectEnd = 0;
            int index = 0;
            while (index < json.Length)
            {
                if (json[index] != '"')
                {
                    index++;
                    continue;
                }

                int keyStart = index;
                if (!TryReadString(json, index, out string key, out int keyEnd))
                {
                    return false;
                }

                index = SkipWhitespace(json, keyEnd);
                if (key == fieldName && index < json.Length && json[index] == ':')
                {
                    int valueStart = SkipWhitespace(json, index + 1);
                    if (valueStart < json.Length && json[valueStart] == '{')
                    {
                        if (!TryReadBlock(json, valueStart, '{', '}', out int blockEnd))
                        {
                            return false;
                        }

                        fieldStart = keyStart;
                        fieldEnd = blockEnd;
                        objectStart = valueStart;
                        objectEnd = blockEnd;
                        return true;
                    }
                }

                index = keyEnd;
            }

            return false;
        }

        private static bool ContainsKey(string json, string fieldName)
        {
            int index = 0;
            while (index < json.Length)
            {
                if (json[index] != '"')
                {
                    index++;
                    continue;
                }

                if (!TryReadString(json, index, out string key, out int keyEnd))
                {
                    return false;
                }

                int afterKey = SkipWhitespace(json, keyEnd);
                if (key == fieldName && afterKey < json.Length && json[afterKey] == ':')
                {
                    return true;
                }

                index = keyEnd;
            }

            return false;
        }

        private static bool TryReadValue(string json, int start, JsonMapValueKind valueKind, out string rawValue, out string textValue, out int end)
        {
            rawValue = null;
            textValue = null;
            end = start;
            if (start >= json.Length)
            {
                return false;
            }

            if (valueKind == JsonMapValueKind.String)
            {
                if (!TryReadString(json, start, out textValue, out end))
                {
                    return false;
                }

                rawValue = textValue;
                return true;
            }

            char open = valueKind == JsonMapValueKind.Object ? '{' : '[';
            char close = valueKind == JsonMapValueKind.Object ? '}' : ']';
            if (json[start] != open || !TryReadBlock(json, start, open, close, out end))
            {
                return false;
            }

            rawValue = json.Substring(start, end - start);
            return true;
        }

        private static bool TryReadBlock(string json, int start, char open, char close, out int end)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                }
                else if (current == open)
                {
                    depth++;
                }
                else if (current == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i + 1;
                        return true;
                    }
                }
            }

            end = start;
            return false;
        }

        private static bool TryReadString(string json, int start, out string value, out int end)
        {
            value = null;
            end = start;
            if (start >= json.Length || json[start] != '"')
            {
                return false;
            }

            StringBuilder builder = new StringBuilder();
            bool escaped = false;
            for (int i = start + 1; i < json.Length; i++)
            {
                char current = json[i];
                if (escaped)
                {
                    builder.Append(Unescape(current));
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                {
                    value = builder.ToString();
                    end = i + 1;
                    return true;
                }

                builder.Append(current);
            }

            return false;
        }

        private static char Unescape(char escaped)
        {
            switch (escaped)
            {
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                default:
                    return escaped;
            }
        }

        private static string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                switch (current)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(current);
                        break;
                }
            }

            return builder.ToString();
        }

        private static int SkipWhitespace(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            return index;
        }

        private enum JsonMapValueKind
        {
            String,
            Object,
            Array
        }
    }
}
