using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mycology_ECS.Utils
{
    public static class SimpleJson
    {
        public static object Parse(string json)
        {
            if (json == null) return null;
            var parser = new Parser(json);
            return parser.ParseValue();
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s)
            {
                _s = s;
                _i = 0;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (_i >= _s.Length) return null;

                var c = _s[_i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't' || c == 'f') return ParseBool();
                if (c == 'n') return ParseNull();
                return ParseNumber();
            }

            private Dictionary<string, object> ParseObject()
            {
                Expect('{');
                SkipWhitespace();

                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                if (TryConsume('}')) return obj;

                while (true)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    var value = ParseValue();
                    obj[key] = value;
                    SkipWhitespace();

                    if (TryConsume('}')) break;
                    Expect(',');
                }

                return obj;
            }

            private List<object> ParseArray()
            {
                Expect('[');
                SkipWhitespace();

                var list = new List<object>();
                if (TryConsume(']')) return list;

                while (true)
                {
                    var v = ParseValue();
                    list.Add(v);
                    SkipWhitespace();

                    if (TryConsume(']')) break;
                    Expect(',');
                }

                return list;
            }

            private string ParseString()
            {
                Expect('"');

                var chars = new List<char>();
                while (_i < _s.Length)
                {
                    var c = _s[_i++];
                    if (c == '"')
                    {
                        return new string(chars.ToArray());
                    }

                    if (c != '\\')
                    {
                        chars.Add(c);
                        continue;
                    }

                    if (_i >= _s.Length) break;

                    var esc = _s[_i++];
                    switch (esc)
                    {
                        case '"': chars.Add('"'); break;
                        case '\\': chars.Add('\\'); break;
                        case '/': chars.Add('/'); break;
                        case 'b': chars.Add('\b'); break;
                        case 'f': chars.Add('\f'); break;
                        case 'n': chars.Add('\n'); break;
                        case 'r': chars.Add('\r'); break;
                        case 't': chars.Add('\t'); break;
                        case 'u':
                            chars.Add(ParseUnicodeEscape());
                            break;
                        default:
                            chars.Add(esc);
                            break;
                    }
                }

                return new string(chars.ToArray());
            }

            private char ParseUnicodeEscape()
            {
                if (_i + 4 > _s.Length) return '?';
                var hex = _s.Substring(_i, 4);
                _i += 4;
                if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                {
                    return (char)code;
                }
                return '?';
            }

            private object ParseNumber()
            {
                SkipWhitespace();
                var start = _i;

                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;

                var hasDot = false;
                if (_i < _s.Length && _s[_i] == '.')
                {
                    hasDot = true;
                    _i++;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }

                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    hasDot = true;
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }

                var token = _s.Substring(start, _i - start);
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    if (!hasDot)
                    {
                        if (d >= int.MinValue && d <= int.MaxValue) return (int)d;
                        if (d >= long.MinValue && d <= long.MaxValue) return (long)d;
                    }
                    return d;
                }

                return 0d;
            }

            private bool ParseBool()
            {
                SkipWhitespace();
                if (Match("true")) return true;
                if (Match("false")) return false;
                return false;
            }

            private object ParseNull()
            {
                SkipWhitespace();
                Match("null");
                return null;
            }

            private bool Match(string token)
            {
                SkipWhitespace();
                if (_i + token.Length > _s.Length) return false;

                for (var j = 0; j < token.Length; j++)
                {
                    if (_s[_i + j] != token[j]) return false;
                }

                _i += token.Length;
                return true;
            }

            private void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        _i++;
                        continue;
                    }
                    break;
                }
            }

            private void Expect(char c)
            {
                SkipWhitespace();
                if (_i >= _s.Length || _s[_i] != c)
                {
                    throw new FormatException("Invalid JSON");
                }
                _i++;
            }

            private bool TryConsume(char c)
            {
                SkipWhitespace();
                if (_i < _s.Length && _s[_i] == c)
                {
                    _i++;
                    return true;
                }
                return false;
            }
        }
    }
}
