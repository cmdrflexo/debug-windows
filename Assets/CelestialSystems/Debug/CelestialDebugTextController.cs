/*
 * Builds configurable TextMeshPro runtime diagnostics from a pasted template and explicitly assigned Celestial Systems objects.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialDebugTextController : MonoBehaviour
    {
        [Serializable]
        private sealed class SourceBinding
        {
            [SerializeField]
            private string alias;

            [SerializeField]
            private UnityEngine.Object source;

            public string Alias => alias;

            public UnityEngine.Object Source => source;
        }

        private sealed class Token
        {
            public string Literal;
            public string Expression;
            public string Format;
            public string Fallback;
            public SourceBinding Binding;
            public MemberInfo[] Members;
            public string Error;

            public bool IsLiteral => Literal != null;
        }

        [Header("Output")]
        [SerializeField]
        private TMP_Text targetText;

        [SerializeField]
        [Min(0.0f)]
        private float refreshIntervalSeconds = 0.1f;

        [Header("Available Sources")]
        [SerializeField]
        private SourceBinding[] sources = Array.Empty<SourceBinding>();

        [Header("Display Code")]
        [SerializeField]
        [TextArea(6, 30)]
        private string displayCode =
            "<b>Celestial Debug</b>\n" +
            "Altitude: {surface.AnchorAltitudeMeters:N1|--} m\n" +
            "Face: {surface.AnchorAddress.Face|--}";

        [Header("Runtime")]
        [SerializeField]
        private string lastConfigurationError;

        private readonly List<Token> tokens = new List<Token>();
        private readonly StringBuilder outputBuilder = new StringBuilder(512);

        private string compiledDisplayCode;
        private int compiledSourceSignature;
        private double nextRefreshTime;

        public string DisplayCode => displayCode;

        private void Reset()
        {
            targetText = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            InvalidateTemplate();
            RefreshText();
        }

        private void Update()
        {
            if (Time.unscaledTimeAsDouble < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime =
                Time.unscaledTimeAsDouble +
                refreshIntervalSeconds;

            RefreshText();
        }

        public void SetDisplayCode(string newDisplayCode)
        {
            displayCode = newDisplayCode ?? string.Empty;
            InvalidateTemplate();
            RefreshText();
        }

        private void RefreshText()
        {
            if (targetText == null)
            {
                return;
            }

            EnsureTemplateCompiled();
            outputBuilder.Clear();

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token.IsLiteral)
                {
                    outputBuilder.Append(token.Literal);
                    continue;
                }

                if (!TryEvaluate(token, out var value))
                {
                    outputBuilder.Append(token.Fallback);
                    continue;
                }

                if (TryFormatValue(value, token.Format, out var formattedValue))
                {
                    outputBuilder.Append(formattedValue);
                }
                else
                {
                    outputBuilder.Append(token.Fallback);
                }
            }

            targetText.text = outputBuilder.ToString();
        }

        private void EnsureTemplateCompiled()
        {
            var sourceSignature = CalculateSourceSignature();

            if (compiledDisplayCode == displayCode &&
                compiledSourceSignature == sourceSignature)
            {
                return;
            }

            CompileTemplate(sourceSignature);
        }

        private void CompileTemplate(int sourceSignature)
        {
            tokens.Clear();
            lastConfigurationError = string.Empty;

            compiledDisplayCode = displayCode ?? string.Empty;
            compiledSourceSignature = sourceSignature;

            var bindings = BuildBindingLookup();
            var literal = new StringBuilder();

            for (var index = 0; index < compiledDisplayCode.Length; index++)
            {
                var character = compiledDisplayCode[index];

                if (character == '{' &&
                    index + 1 < compiledDisplayCode.Length &&
                    compiledDisplayCode[index + 1] == '{')
                {
                    literal.Append('{');
                    index++;
                    continue;
                }

                if (character == '}' &&
                    index + 1 < compiledDisplayCode.Length &&
                    compiledDisplayCode[index + 1] == '}')
                {
                    literal.Append('}');
                    index++;
                    continue;
                }

                if (character != '{')
                {
                    literal.Append(character);
                    continue;
                }

                var closingBrace = compiledDisplayCode.IndexOf('}', index + 1);

                if (closingBrace < 0)
                {
                    literal.Append(compiledDisplayCode, index, compiledDisplayCode.Length - index);
                    RecordConfigurationError("Display code contains an unmatched opening brace.");
                    break;
                }

                AddLiteralToken(literal);

                var expression = compiledDisplayCode.Substring(
                    index + 1,
                    closingBrace - index - 1);

                tokens.Add(CreateValueToken(expression, bindings));
                index = closingBrace;
            }

            AddLiteralToken(literal);
        }

        private Dictionary<string, SourceBinding> BuildBindingLookup()
        {
            var bindings = new Dictionary<string, SourceBinding>(
                StringComparer.OrdinalIgnoreCase);

            if (sources == null)
            {
                return bindings;
            }

            for (var i = 0; i < sources.Length; i++)
            {
                var binding = sources[i];

                if (binding == null ||
                    string.IsNullOrWhiteSpace(binding.Alias))
                {
                    continue;
                }

                var alias = binding.Alias.Trim();

                if (!bindings.TryAdd(alias, binding))
                {
                    RecordConfigurationError(
                        $"Source alias '{alias}' is assigned more than once.");
                }
            }

            return bindings;
        }

        private Token CreateValueToken(
            string rawExpression,
            Dictionary<string, SourceBinding> bindings)
        {
            var token = new Token
            {
                Expression = rawExpression,
                Fallback = "--"
            };

            var expression = rawExpression.Trim();
            var fallbackSeparator = expression.IndexOf('|');

            if (fallbackSeparator >= 0)
            {
                token.Fallback = expression.Substring(fallbackSeparator + 1);
                expression = expression.Substring(0, fallbackSeparator).Trim();
            }

            var formatSeparator = expression.IndexOf(':');

            if (formatSeparator >= 0)
            {
                token.Format = expression.Substring(formatSeparator + 1).Trim();
                expression = expression.Substring(0, formatSeparator).Trim();
            }

            var pathParts = expression.Split('.');

            if (pathParts.Length < 2 ||
                string.IsNullOrWhiteSpace(pathParts[0]))
            {
                return SetTokenError(
                    token,
                    $"Token '{{{rawExpression}}}' must use alias.Member syntax.");
            }

            if (!bindings.TryGetValue(pathParts[0].Trim(), out var binding))
            {
                return SetTokenError(
                    token,
                    $"Token '{{{rawExpression}}}' uses unknown source alias '{pathParts[0]}'.");
            }

            token.Binding = binding;

            if (binding.Source == null)
            {
                return SetTokenError(
                    token,
                    $"Source alias '{binding.Alias}' has no assigned object.");
            }

            var members = new MemberInfo[pathParts.Length - 1];
            var currentType = binding.Source.GetType();

            for (var i = 1; i < pathParts.Length; i++)
            {
                var memberName = pathParts[i].Trim();
                var member = FindReadableMember(currentType, memberName);

                if (member == null)
                {
                    return SetTokenError(
                        token,
                        $"'{currentType.Name}' has no public readable field or property named '{memberName}'.");
                }

                members[i - 1] = member;
                currentType = GetMemberType(member);
            }

            token.Members = members;
            return token;
        }

        private Token SetTokenError(Token token, string error)
        {
            token.Error = error;
            RecordConfigurationError(error);
            return token;
        }

        private void RecordConfigurationError(string error)
        {
            if (string.IsNullOrEmpty(lastConfigurationError))
            {
                lastConfigurationError = error;
            }
        }

        private static MemberInfo FindReadableMember(Type type, string name)
        {
            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.Public;

            var property = type.GetProperty(name, Flags);

            if (property != null &&
                property.CanRead &&
                property.GetIndexParameters().Length == 0)
            {
                return property;
            }

            return type.GetField(name, Flags);
        }

        private static Type GetMemberType(MemberInfo member)
        {
            if (member is PropertyInfo property)
            {
                return property.PropertyType;
            }

            return ((FieldInfo)member).FieldType;
        }

        private static bool TryEvaluate(Token token, out object value)
        {
            value = null;

            if (token.Error != null ||
                token.Binding == null ||
                token.Binding.Source == null ||
                token.Members == null)
            {
                return false;
            }

            object current = token.Binding.Source;

            try
            {
                for (var i = 0; i < token.Members.Length; i++)
                {
                    if (IsNull(current))
                    {
                        return false;
                    }

                    var member = token.Members[i];

                    current = member is PropertyInfo property
                        ? property.GetValue(current)
                        : ((FieldInfo)member).GetValue(current);
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (IsNull(current))
            {
                return false;
            }

            value = current;
            return true;
        }

        private static bool IsNull(object value)
        {
            if (value == null)
            {
                return true;
            }

            return value is UnityEngine.Object unityObject &&
                   unityObject == null;
        }

        private static bool TryFormatValue(
            object value,
            string format,
            out string formattedValue)
        {
            try
            {
                if (string.IsNullOrEmpty(format))
                {
                    formattedValue = value.ToString();
                    return true;
                }

                if (value is IFormattable formattable)
                {
                    formattedValue = formattable.ToString(
                        format,
                        CultureInfo.InvariantCulture);
                    return true;
                }

                var formattedToString = value.GetType().GetMethod(
                    "ToString",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null);

                if (formattedToString != null)
                {
                    formattedValue = (string)formattedToString.Invoke(
                        value,
                        new object[] { format });
                    return true;
                }

                formattedValue = value.ToString();
                return true;
            }
            catch (Exception)
            {
                formattedValue = null;
                return false;
            }
        }

        private void AddLiteralToken(StringBuilder literal)
        {
            if (literal.Length == 0)
            {
                return;
            }

            tokens.Add(new Token
            {
                Literal = literal.ToString()
            });

            literal.Clear();
        }

        private int CalculateSourceSignature()
        {
            unchecked
            {
                var signature = 17;

                if (sources == null)
                {
                    return signature;
                }

                for (var i = 0; i < sources.Length; i++)
                {
                    var binding = sources[i];

                    signature = signature * 31 +
                        (binding?.Alias != null
                            ? StringComparer.OrdinalIgnoreCase.GetHashCode(binding.Alias)
                            : 0);
                    signature = signature * 31 +
                        (binding?.Source != null
                            ? binding.Source.GetInstanceID()
                            : 0);
                }

                return signature;
            }
        }

        private void InvalidateTemplate()
        {
            compiledDisplayCode = null;
            compiledSourceSignature = 0;
        }
    }
}
