using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Course.Domain.Entities;
using Course.Domain.Enums;

namespace Course.Services.Utilities;

public static class InventoryIdFormatService
{
    public const int MaxFormatLength = 200;
    public const string DefaultFormat = "INV-{SEQ:0000}";

    public static bool TryParse(string? format, out List<InventoryIdElement> elements, out string? error)
    {
        elements = new List<InventoryIdElement>();
        error = null;

        if (string.IsNullOrWhiteSpace(format))
        {
            error = "ID format is required.";
            return false;
        }

        format = format.Trim();
        if (format.Length > MaxFormatLength)
        {
            error = "ID format is too long.";
            return false;
        }

        var buffer = new StringBuilder();
        var sortOrder = 1;

        for (var index = 0; index < format.Length; index++)
        {
            var current = format[index];
            if (current == '{')
            {
                if (index + 1 < format.Length && format[index + 1] == '{')
                {
                    buffer.Append('{');
                    index++;
                    continue;
                }

                if (buffer.Length > 0)
                {
                    if (!TryAddFixedText(elements, buffer.ToString(), ref sortOrder, out error))
                    {
                        return false;
                    }

                    buffer.Clear();
                }

                var closeIndex = format.IndexOf('}', index + 1);
                if (closeIndex == -1)
                {
                    error = "ID format contains an unclosed token.";
                    return false;
                }

                var token = format.Substring(index + 1, closeIndex - index - 1).Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    error = "ID format contains an empty token.";
                    return false;
                }

                if (!TryAddToken(elements, token, ref sortOrder, out error))
                {
                    return false;
                }

                index = closeIndex;
                continue;
            }

            if (current == '}' && index + 1 < format.Length && format[index + 1] == '}')
            {
                buffer.Append('}');
                index++;
                continue;
            }

            buffer.Append(current);
        }

        if (buffer.Length > 0)
        {
            if (!TryAddFixedText(elements, buffer.ToString(), ref sortOrder, out error))
            {
                return false;
            }
        }

        if (elements.Count == 0)
        {
            error = "ID format must include at least one token or text segment.";
            return false;
        }

        var hasUniqueToken = elements.Any(e => e.ElementType is InventoryIdElementType.Sequence 
                                                or InventoryIdElementType.Guid 
                                                or InventoryIdElementType.Random20Bit 
                                                or InventoryIdElementType.Random32Bit 
                                                or InventoryIdElementType.Random6Digit 
                                                or InventoryIdElementType.Random9Digit);
        
        if (!hasUniqueToken)
        {
            error = "ID format must include at least one unique token (e.g. {SEQ}, {GUID}, or {RAND6}) to ensure item IDs are unique.";
            return false;
        }

        return true;
    }

    public static string ToFormatString(IEnumerable<InventoryIdElement>? elements)
    {
        if (elements == null)
        {
            return DefaultFormat;
        }

        var ordered = elements.OrderBy(element => element.SortOrder).ToList();
        if (ordered.Count == 0)
        {
            return DefaultFormat;
        }

        var builder = new StringBuilder();
        foreach (var element in ordered)
        {
            builder.Append(element.ElementType switch
            {
                InventoryIdElementType.FixedText => EscapeFixedText(element.FixedText ?? string.Empty),
                InventoryIdElementType.Sequence => BuildToken("SEQ", element.Format),
                InventoryIdElementType.DateTime => BuildToken("DATE", element.Format),
                InventoryIdElementType.Guid => "{GUID}",
                InventoryIdElementType.Random20Bit => "{RAND20}",
                InventoryIdElementType.Random32Bit => "{RAND32}",
                InventoryIdElementType.Random6Digit => "{RAND6}",
                InventoryIdElementType.Random9Digit => "{RAND9}",
                _ => string.Empty
            });
        }

        return builder.ToString();
    }

    public static bool TryBuildCustomId(
        IEnumerable<InventoryIdElement> elements,
        int? sequenceNumber,
        DateTimeOffset now,
        out string customId,
        out string? error)
    {
        customId = string.Empty;
        error = null;

        var ordered = elements.OrderBy(element => element.SortOrder).ToList();
        if (ordered.Count == 0)
        {
            error = "ID format is not configured for this inventory.";
            return false;
        }

        var builder = new StringBuilder();
        foreach (var element in ordered)
        {
            try
            {
                switch (element.ElementType)
                {
                    case InventoryIdElementType.FixedText:
                        builder.Append(element.FixedText ?? string.Empty);
                        break;
                    case InventoryIdElementType.Sequence:
                        if (!sequenceNumber.HasValue)
                        {
                            error = "Sequence number is required for this ID format.";
                            return false;
                        }

                        builder.Append(sequenceNumber.Value.ToString(
                            string.IsNullOrWhiteSpace(element.Format) ? "D" : element.Format,
                            CultureInfo.InvariantCulture));
                        break;
                    case InventoryIdElementType.DateTime:
                        builder.Append(now.ToString(
                            string.IsNullOrWhiteSpace(element.Format) ? "yyyyMMdd" : element.Format,
                            CultureInfo.InvariantCulture));
                        break;
                    case InventoryIdElementType.Guid:
                        builder.Append(Guid.NewGuid().ToString("N"));
                        break;
                    case InventoryIdElementType.Random20Bit:
                        builder.Append(RandomNumberGenerator.GetInt32(1 << 20).ToString("X5", CultureInfo.InvariantCulture));
                        break;
                    case InventoryIdElementType.Random32Bit:
                        builder.Append(RandomNumberGenerator.GetInt32(int.MaxValue).ToString("X8", CultureInfo.InvariantCulture));
                        break;
                    case InventoryIdElementType.Random6Digit:
                        builder.Append(RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture));
                        break;
                    case InventoryIdElementType.Random9Digit:
                        builder.Append(RandomNumberGenerator.GetInt32(1_000_000_000).ToString("D9", CultureInfo.InvariantCulture));
                        break;
                }
            }
            catch (FormatException)
            {
                error = "ID format contains an invalid token format.";
                return false;
            }
            catch (ArgumentException)
            {
                error = "ID format contains an invalid token format.";
                return false;
            }
        }

        customId = builder.ToString();
        if (string.IsNullOrWhiteSpace(customId))
        {
            error = "Generated ID is empty.";
            return false;
        }

        if (customId.Length > 120)
        {
            error = "Generated ID exceeds the maximum length.";
            return false;
        }

        return true;
    }

    private static bool TryAddFixedText(List<InventoryIdElement> elements, string text, ref int sortOrder, out string? error)
    {
        error = null;
        if (text.Length > 200)
        {
            error = "Fixed text segments cannot exceed 200 characters.";
            return false;
        }

        elements.Add(new InventoryIdElement
        {
            ElementType = InventoryIdElementType.FixedText,
            FixedText = text,
            SortOrder = sortOrder++
        });

        return true;
    }

    private static bool TryAddToken(List<InventoryIdElement> elements, string token, ref int sortOrder, out string? error)
    {
        error = null;
        var parts = token.Split(':', 2, StringSplitOptions.TrimEntries);
        var name = parts[0].Trim().ToUpperInvariant();
        var format = parts.Length > 1 ? parts[1].Trim() : null;

        InventoryIdElement element;
        switch (name)
        {
            case "SEQ":
                if (!TryNormalizeFormat(format, out var sequenceFormat, out error))
                {
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.Sequence,
                    Format = sequenceFormat
                };
                break;
            case "DATE":
                if (!TryNormalizeFormat(format, out var dateFormat, out error))
                {
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.DateTime,
                    Format = dateFormat
                };
                break;
            case "GUID":
                if (!string.IsNullOrWhiteSpace(format))
                {
                    error = "GUID token does not accept a format.";
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.Guid
                };
                break;
            case "RAND20":
                if (!string.IsNullOrWhiteSpace(format))
                {
                    error = "RAND20 token does not accept a format.";
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.Random20Bit
                };
                break;
            case "RAND32":
                if (!string.IsNullOrWhiteSpace(format))
                {
                    error = "RAND32 token does not accept a format.";
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.Random32Bit
                };
                break;
            case "RAND6":
                if (!string.IsNullOrWhiteSpace(format))
                {
                    error = "RAND6 token does not accept a format.";
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.Random6Digit
                };
                break;
            case "RAND9":
                if (!string.IsNullOrWhiteSpace(format))
                {
                    error = "RAND9 token does not accept a format.";
                    return false;
                }

                element = new InventoryIdElement
                {
                    ElementType = InventoryIdElementType.Random9Digit
                };
                break;
            default:
                error = "Unknown token in ID format.";
                return false;
        }

        element.SortOrder = sortOrder++;
        elements.Add(element);
        return true;
    }

    private static bool TryNormalizeFormat(string? format, out string? normalized, out string? error)
    {
        error = null;
        normalized = null;

        if (string.IsNullOrWhiteSpace(format))
        {
            return true;
        }

        if (format.Length > 100)
        {
            error = "Token format cannot exceed 100 characters.";
            return false;
        }

        normalized = format;
        return true;
    }

    private static string BuildToken(string name, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return $"{{{name}}}";
        }

        return $"{{{name}:{format}}}";
    }

    private static string EscapeFixedText(string text)
    {
        return text.Replace("{", "{{").Replace("}", "}}");
    }
}
