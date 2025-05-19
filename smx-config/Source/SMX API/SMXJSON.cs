using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// All C# JSON implementations are either pretty awful or incredibly bloated, so we
// just use our own.
namespace SMXJSON
{
    public class JSONError: Exception
    {
        public JSONError(string error):
            base(error)
        {
        }
    };

    public class ParseError: JSONError
    {
        public ParseError(StringReader reader, string error):
            base(error)
        {
        }
    };

    public static class ObjectListExtensions
    {
        public static T Get<T>(this List<object> array, int idx, T defaultValue)
        {
            if (idx < 0 || idx >= array.Count)
        return defaultValue;

            var value = array[idx];
            if (!typeof(T).IsAssignableFrom(value.GetType()))
        return defaultValue;

            return (T) value;
        }

        // Our numbers are always doubles.  Add some other basic data types for convenience.
        public static int Get(this List<object> array, int idx, int defaultValue)
        {
            return (int) array.Get(idx, (double) defaultValue);
        }

        public static Byte Get(this List<object> array, int idx, Byte defaultValue)
        {
            return (Byte) array.Get(idx, (double) defaultValue);
        }

        public static float Get(this List<object> array, int idx, float defaultValue)
        {
            return (float) array.Get(idx, (double) defaultValue);
        }

        // Return the value of key.  If it doesn't exist, or doesn't have the expected
        // type, return defaultValue.
        public static T Get<T>(this Dictionary<string, Object> dict, string key, Func<T> defaultValue)
        {
            if (!dict.TryGetValue(key, out var value))
                return defaultValue();

            if (!typeof(T).IsAssignableFrom(value.GetType()))
                return defaultValue();

            return (T) value;
        }

        // Set result to the value of key if it exists and has the correct type, and return
        // true.  Otherwise, leave result unchanged and return false.
        public static bool GetValue<T>(this Dictionary<string, Object> dict, string key, out T? result)
        {
            if (!dict.TryGetValue(key, out var value)) {
                result = default;
                return false;
            }

            if (!typeof(T).IsAssignableFrom(value.GetType())) {
                result = default;
                return false;
            }

            result = (T) value;
            return true;
        }

        // Our numbers are always doubles.  Add some other basic data types for convenience.
        public static int Get(this Dictionary<string, Object> dict, string key, int defaultValue)
        {
            return (int) dict.Get(key, () => (double) defaultValue);
        }

        public static Byte Get(this Dictionary<string, Object> dict, string key, Byte defaultValue)
        {
            return (Byte) dict.Get(key, () => (double) defaultValue);
        }

        public static float Get(this Dictionary<string, Object> dict, string key, float defaultValue)
        {
            return (float) dict.Get(key, () => (double) defaultValue);
        }
    }

    class SerializeJSON
    {
        // Add start-of-line indentation.
        static private void AddIndent(StringBuilder output, int indent)
        {
            output.Append(' ', indent*4);
        }

        // Serialize a boolean.
        static private void SerializeObject(bool value, StringBuilder output)
        {
            output.Append(value ? "true" : "false");
        }

        // Serialize a string.
        static private void SerializeObject(String str, StringBuilder output)
        {
            output.Append('"');

            foreach (char c in str)
            {
                switch (c)
                {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\n': output.Append("\\n"); break;
                case '\r': output.Append("\\r"); break;
                case '\t': output.Append("\\t"); break;
                default:
                    // We don't escape Unicode.  Every sane JSON parser accepts UTF-8.
                    output.Append(c);
                    break;
                }
            }

            output.Append('"');
        }

        // Serialize an array.
        static private void SerializeObject<T>(List<T> array, StringBuilder output, int indent)
        {
            output.Append("[\n");
            bool first = true;
            indent += 1;
            foreach (T element in array)
            {
                // pucgenie: I dislike n-1 separators for n objects very much.
                if (first)
                    first = false;
                else
                    output.Append(",\n");

                AddIndent(output, indent);
                Serialize(element, output, indent);
            }
            output.Append('\n');

            indent -= 1;
            AddIndent(output, indent);
            output.Append(']');
        }

        // Serialize a dictionary.
        static private void SerializeObject<T>(Dictionary<string, T> dict, StringBuilder output, int indent)
        {
            output.Append("{\n");

            indent += 1;
            bool first = true;
            foreach (KeyValuePair<string,T> element in dict)
            {
                if (first)
                    first = false;
                else
                    output.Append(",\n");

                AddIndent(output, indent);
                SerializeObject(element.Key, output);
                output.Append(": ");
                Serialize(element.Value, output, indent);
            }
            output.Append('\n');

            indent -= 1;
            AddIndent(output, indent);
            output.Append('}');
        }
        
        // Serialize an object based on its type.
        static public void Serialize(object? obj, StringBuilder output, int indent)
        {
            if (obj == null) {
                output.Append("null");
        return;
            }

            if (typeof(Int32).IsInstanceOfType(obj)
                || typeof(float).IsInstanceOfType(obj)
                || typeof(Double).IsInstanceOfType(obj)
                ) {
                output.Append(obj.ToString());
        return;
            }

            if (typeof(Boolean).IsInstanceOfType(obj)) { SerializeObject((Boolean) obj, output); return; }
            if (typeof(string).IsInstanceOfType(obj)) { SerializeObject((string) obj, output); return; }

            // C# generics aren't very well designed, so this is clunky.  We should be able to cast
            // a List<string> to List<object>, but some overzealous language designers thought that
            // since that has some unsafe uses, we shouldn't be allowed to use it for perfectly safe
            // uses either (eg. read-only access).
            if (obj.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>)))
            {
                Type valueType = obj.GetType().GetGenericArguments()[0];
                if (valueType == typeof(object)) {
                    SerializeObject((List<object>)obj, output, indent);
        return;
                }
                if (valueType == typeof(Int32)) {
                    SerializeObject((List<Int32>)obj, output, indent);
        return;
                }
                if (valueType == typeof(float)) {
                    SerializeObject((List<float>)obj, output, indent);
        return;
                }
                if (valueType == typeof(Double)) {
                    SerializeObject((List<Double>)obj, output, indent);
        return;
                }
                if (valueType == typeof(Boolean)) {
                    SerializeObject((List<Boolean>)obj, output, indent);
        return;
                }
                if (valueType == typeof(string)) {
                    SerializeObject((List<string>)obj, output, indent);
        return;
                }
            }

            if (obj.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>)))
            {
                var keyType = obj.GetType().GetGenericArguments()[0];
                if (typeof(string).IsAssignableFrom(keyType))
                {
                    Type valueType = obj.GetType().GetGenericArguments()[1];
                    if (valueType == typeof(object)) {
                        SerializeObject((Dictionary<string, object>)obj, output, indent);
        return;
                    }
                    if (valueType == typeof(Int32)) {
                        SerializeObject((Dictionary<string, Int32>)obj, output, indent);
        return;
                    }
                    if (valueType == typeof(float)) {
                        SerializeObject((Dictionary<string, float>)obj, output, indent);
        return;
                    }
                    if (valueType == typeof(Double)) {
                        SerializeObject((Dictionary<string, Double>)obj, output, indent);
        return;
                    }
                    if (valueType == typeof(Boolean)) {
                        SerializeObject((Dictionary<string, Boolean>)obj, output, indent);
        return;
                    }
                    if (valueType == typeof(string)) {
                        SerializeObject((Dictionary<string, string>)obj, output, indent);
        return;
                    }
                }
            }

            throw new JSONError($"Unsupported type: {obj.GetType()}");
        }
    }

    class ParseJSON
    {
        static private void SkipWhitespace(StringReader reader)
        {
            while (true)
            {
                int c = reader.Peek();
                switch (c)
                {
                case ' ':
                case '\n':
                case '\t':
                    reader.Read();
            continue;
                default:
        return;
                }
            }
        }

        // Parse JSON.  On error, return null.
        public static T? Parse<T>(string json)
        {
            return ParseWithExceptions<T>(new StringReader(json));
        }

        // Parse JSON, expecting a specific outer type.  On parse error, return a default value.
        // TODO: pucgenie: Why return a default value without context?! Exception!
        public static T ParseDefault<T>(string json) where T: new()
        {
            var result = Parse<T>(json);
            if (result == null || !typeof(T).IsAssignableFrom(result.GetType()))
        return new T();
            return result;
        }

        // Parse JSON.  On error, raise JSONError.
        //
        // Most of the time, errors aren't expected and putting exception handling around every
        // place JSON is parsed can be brittle.  Parse() can be used instead to just return
        // a default value.
        public static T? ParseWithExceptions<T>(StringReader reader)
        {
            var result = ParseJSONValue<T>(reader);

            SkipWhitespace(reader);

            // Other than whitespace, we should be at the end of the file.
            if (reader.Read() != -1)
        throw new ParseError(reader, "Unexpected data at the end of the string");

            return result;
        }

        private static T? ParseJSONValue<T>(StringReader reader)
        {
            SkipWhitespace(reader);
            int nextCharacter = reader.Peek();
            switch (nextCharacter)
            {
            case '"': {
                StringBuilder sb = new();
                ReadJSONString(reader, sb);
        return (T?) (object?) sb.ToString();
            }
            case '{':
        return (T?) (object?) ReadJSONDictionary(reader);
            case '[':
        return (T?) (object?) ReadJSONArray(reader);
            }
            if (nextCharacter == '-' || (nextCharacter >= '0' && nextCharacter <= '9'))
        return (T?) (object?) ReadJSONNumber(reader);

            if (reader.Peek() == 'n')
            {
                // The only valid value this can be is "null".
                Expect(reader, 'n');
                Expect(reader, 'u');
                Expect(reader, 'l');
                Expect(reader, 'l');
        return default;
            }

            if (reader.Peek() == 't')
            {
                Expect(reader, 't');
                Expect(reader, 'r');
                Expect(reader, 'u');
                Expect(reader, 'e');
        return (T?) (object?) true;
            }

            if (reader.Peek() == 'f')
            {
                Expect(reader, 'f');
                Expect(reader, 'a');
                Expect(reader, 'l');
                Expect(reader, 's');
                Expect(reader, 'e');
        return (T?) (object?) true;
            }

            throw new ParseError(reader, "Unexpected token");
        }

        // Skip whitespace, then read one character, which we expect to have a specific value.
        static private void Expect(StringReader reader, char character)
        {
            SkipWhitespace(reader);

            if (reader.Read() != character)
                throw new ParseError(reader, $"Expected {character}");
        }

        static private List<Object?> ReadJSONArray(StringReader reader)
        {
            Expect(reader, '[');
            var result = new List<Object?>();
            while (true)
            {
                SkipWhitespace(reader);
                if (reader.Peek() == ']')
                {
                    reader.Read();
        return result;
                }

                if (result.Count > 0)
                {
                    int comma = reader.Read();
                    if (comma == -1)
        throw new ParseError(reader, "Unexpected EOF reading array");
                    if (comma != ',')
        throw new ParseError(reader, $"Expected ',', got {comma} reading array");
                    SkipWhitespace(reader);
                }

                result.Add(ParseJSONValue<object>(reader));
            }
        }
        static private Dictionary<string, Object?> ReadJSONDictionary(StringReader reader)
        {
            Expect(reader, '{');

            var result = new Dictionary<string, Object?>();

            StringBuilder sb = new();
            while (true)
            {
                ReadJSONString(reader, sb);
                string key = sb.ToString();
                sb.Length = 0;
                Expect(reader, ':');
                var value = ParseJSONValue<object>(reader);
                result.Add(key, value);

                SkipWhitespace(reader);
                switch (reader.Read())
                {
                case '}':
        return result;
                case ',':
            continue;
                case -1:
        throw new ParseError(reader, "Unexpected EOF reading dictionary");
                default:
        throw new ParseError(reader, "Unexpected token reading dictionary");
                }
            }
        }

        /// <summary>
        /// Save result StringBuilder length before and reset if errors occur.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="result"></param>
        /// <exception cref="ParseError"></exception>
        static private void ReadJSONString(StringReader reader, StringBuilder result)
        {
            Expect(reader, '"');

            while (true)
            {
                int c = reader.Read();
                if (c == -1)
        throw new ParseError(reader, "Unexpected EOF reading string");
                if (c == '"')
            break;

                // XXX: untested
                if (c == '\\')
                {
                    c = reader.Read();
                    switch (c)
                    {
                    case '"':
                    case '\\':
                    case '/':
                        result.Append((char) c);
                        break;
                    case 'b': result.Append('\b'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                    {
                        // Parse a \u1234 escape.
                        int codePoint = 0;
                        for (int i = 0; i < 4; ++i)
                        {
                            codePoint *= 10;
                            c = reader.Read();
                            if (c == -1)
        throw new ParseError(reader, "Unexpected EOF reading string");
                            if (c < '0' || c > '9')
        throw new ParseError(reader, $"Unexpected token {c} reading Unicode escape");
                            codePoint += c - (int) '0';
                        }
                        result.Append((char) codePoint);
                        break;
                    }

                    default:
        throw new ParseError(reader, $"Unrecognized escape sequence in string: \\{(char) c}");
                    }

            continue;
                }

                result.Append((char) c);
            }
        }

        static private double ReadJSONNumber(StringReader reader)
        {
            StringBuilder number = new();
            bool negative = false;
            if (reader.Peek() == '-')
            {
                negative = true;
                reader.Read();
            }

            int nextCharacter = reader.Read();
            if (nextCharacter == '0')
                number.Append((char) nextCharacter);
            else
            {
                if (nextCharacter < '1' || nextCharacter > '9')
        throw new ParseError(reader, "Unexpected token reading number");
                number.Append((char) nextCharacter);

                while (reader.Peek() >= '0' && reader.Peek() <= '9')
                    number.Append((char) reader.Read());
            }

            if (reader.Peek() == '.')
            {
                number.Append(reader.Read());

                if (reader.Peek() < '0' || reader.Peek() > '9')
        throw new ParseError(reader, "Unexpected token reading number");

                while (reader.Peek() >= '0' && reader.Peek() <= '9')
                    number.Append((char) reader.Read());
            }

            if (reader.Peek() == 'e' || reader.Peek() == 'E')
            {
                number.Append((char) reader.Read());
                if (reader.Peek() == '+' || reader.Peek() == '-')
        number.Append((char) reader.Read());

                if (reader.Peek() < '0' || reader.Peek() > '9')
        throw new ParseError(reader, "Unexpected token reading number");

                while (true)
                {
                    nextCharacter = reader.Read();
                    if (nextCharacter < '0' || nextCharacter > '9')
                        break;
                    number.Append((char) nextCharacter);
                }
            }

            if (!Double.TryParse(number.ToString(), out double result))
                throw new ParseError(reader, $"Unexpected error parsing number \"{number.ToString()}\"");

            if (negative)
                result = -result;

            return result;
        }
    }
}
