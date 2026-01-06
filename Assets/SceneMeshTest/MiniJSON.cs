// MiniJSON.cs
// Lightweight JSON parser/serializer for Unity.
// Public domain-style implementation widely used in Unity projects.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MiniJSON
{
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return Parser.Parse(json);
        }

        sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";

            StringReader _json;

            Parser(string jsonString)
            {
                _json = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                    return instance.ParseValue();
            }

            public void Dispose()
            {
                _json.Dispose();
                _json = null;
            }

            enum TOKEN
            {
                NONE, CURLY_OPEN, CURLY_CLOSE, SQUARED_OPEN, SQUARED_CLOSE,
                COLON, COMMA, STRING, NUMBER, TRUE, FALSE, NULL
            }

            object ParseValue()
            {
                switch (NextToken)
                {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.NUMBER: return ParseNumber();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARED_OPEN: return ParseArray();
                    case TOKEN.TRUE: return true;
                    case TOKEN.FALSE: return false;
                    case TOKEN.NULL: return null;
                    default: return null;
                }
            }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();

                // consume '{'
                _json.Read();

                while (true)
                {
                    switch (NextToken)
                    {
                        case TOKEN.NONE:
                            return null;

                        case TOKEN.COMMA:
                            continue;

                        case TOKEN.CURLY_CLOSE:
                            _json.Read();
                            return table;

                        default:
                            // key
                            var key = ParseString();
                            if (key == null) return null;

                            // colon
                            if (NextToken != TOKEN.COLON) return null;
                            _json.Read();

                            // value
                            table[key] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();

                // consume '['
                _json.Read();

                bool parsing = true;
                while (parsing)
                {
                    var token = NextToken;
                    switch (token)
                    {
                        case TOKEN.NONE:
                            return null;

                        case TOKEN.COMMA:
                            continue;

                        case TOKEN.SQUARED_CLOSE:
                            _json.Read();
                            parsing = false;
                            break;

                        default:
                            array.Add(ParseValue());
                            break;
                    }
                }

                return array;
            }

            string ParseString()
            {
                var sb = new StringBuilder();

                // consume opening quote
                _json.Read();

                bool parsing = true;
                while (parsing)
                {
                    if (_json.Peek() == -1) break;

                    char c = NextChar;
                    switch (c)
                    {
                        case '"':
                            parsing = false;
                            break;

                        case '\\':
                            if (_json.Peek() == -1) { parsing = false; break; }
                            c = NextChar;
                            switch (c)
                            {
                                case '"': sb.Append('"'); break;
                                case '\\': sb.Append('\\'); break;
                                case '/': sb.Append('/'); break;
                                case 'b': sb.Append('\b'); break;
                                case 'f': sb.Append('\f'); break;
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    sb.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;

                        default:
                            sb.Append(c);
                            break;
                    }
                }

                return sb.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord;

                if (number.IndexOf('.') != -1 || number.IndexOf('e') != -1 || number.IndexOf('E') != -1)
                {
                    if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return d;
                }
                else
                {
                    if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        return l;
                }

                // fallback
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double df))
                    return df;

                return 0d;
            }

            void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar))
                    _json.Read();
            }

            char PeekChar => Convert.ToChar(_json.Peek());
            char NextChar => Convert.ToChar(_json.Read());

            string NextWord
            {
                get
                {
                    var sb = new StringBuilder();
                    while (_json.Peek() != -1 && !IsWordBreak(PeekChar))
                        sb.Append(NextChar);
                    return sb.ToString();
                }
            }

            TOKEN NextToken
            {
                get
                {
                    EatWhitespace();
                    if (_json.Peek() == -1) return TOKEN.NONE;

                    char c = PeekChar;
                    switch (c)
                    {
                        case '{': return TOKEN.CURLY_OPEN;
                        case '}': return TOKEN.CURLY_CLOSE;
                        case '[': return TOKEN.SQUARED_OPEN;
                        case ']': return TOKEN.SQUARED_CLOSE;
                        case ',': return TOKEN.COMMA;
                        case '"': return TOKEN.STRING;
                        case ':': return TOKEN.COLON;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case '-':
                            return TOKEN.NUMBER;
                    }

                    string word = NextWord;
                    switch (word)
                    {
                        case "false": return TOKEN.FALSE;
                        case "true": return TOKEN.TRUE;
                        case "null": return TOKEN.NULL;
                    }

                    return TOKEN.NONE;
                }
            }

            static bool IsWordBreak(char c) => char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
        }
    }
}
