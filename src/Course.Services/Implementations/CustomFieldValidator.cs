using System.Text.Json;
using System.Text.RegularExpressions;
using Course.Domain.Entities;
using Course.Domain.Enums;

namespace Course.Services.Implementations;

public static class CustomFieldValidator
{
    public static List<string> Validate(CustomField field, string? value)
    {
        var errors = new List<string>();
        var isBlank = string.IsNullOrWhiteSpace(value);

        if (field.IsRequired && isBlank)
        {
            errors.Add($"'{field.Name}' is required.");
            return errors;
        }

        if (isBlank)
        {
            return errors;
        }

        var settings = ParseSettings(field.SettingsJson);

        switch (field.FieldType)
        {
            case InventoryFieldType.SingleLineText:
            case InventoryFieldType.MultiLineText:
                ValidateStringValue(field.Name, value!, settings, errors);
                break;

            case InventoryFieldType.Number:
                ValidateNumberValue(field.Name, value!, settings, errors);
                break;

            case InventoryFieldType.Boolean:
                if (!bool.TryParse(value, out _) && value != "0" && value != "1")
                {
                    errors.Add($"'{field.Name}' must be a boolean value (true/false).");
                }
                break;

            case InventoryFieldType.Url:
            case InventoryFieldType.Link:
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    errors.Add($"'{field.Name}' must be a valid URL (http or https).");
                }
                ValidateStringValue(field.Name, value!, settings, errors);
                break;

            case InventoryFieldType.OneFromList:
                var options = GetSelectOptions(settings);
                if (options.Count > 0 && !options.Contains(value!, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"'{field.Name}' must be one of: {string.Join(", ", options)}.");
                }
                break;
        }

        return errors;
    }

    public static List<string> GetSelectOptions(string? settingsJson)
    {
        var settings = ParseSettings(settingsJson);
        return GetSelectOptions(settings);
    }

    private static List<string> GetSelectOptions(Dictionary<string, JsonElement> settings)
    {
        if (settings.TryGetValue("Options", out var optionsElement) ||
            settings.TryGetValue("options", out optionsElement))
        {
            if (optionsElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in optionsElement.EnumerateArray())
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        list.Add(text);
                    }
                }
                return list;
            }
        }
        return new List<string>();
    }

    private static void ValidateStringValue(string fieldName, string value,
        Dictionary<string, JsonElement> settings, List<string> errors)
    {
        if (TryGetInt(settings, "MinLength", out var minLength) && value.Length < minLength)
        {
            errors.Add($"'{fieldName}' must be at least {minLength} characters.");
        }

        if (TryGetInt(settings, "MaxLength", out var maxLength) && value.Length > maxLength)
        {
            errors.Add($"'{fieldName}' must be at most {maxLength} characters.");
        }

        if (TryGetString(settings, "Regex", out var pattern) && !string.IsNullOrWhiteSpace(pattern))
        {
            try
            {
                if (!Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    errors.Add($"'{fieldName}' does not match the required pattern.");
                }
            }
            catch (RegexParseException)
            {
                // Invalid regex in settings — skip validation
            }
        }
    }

    private static void ValidateNumberValue(string fieldName, string value,
        Dictionary<string, JsonElement> settings, List<string> errors)
    {
        if (!decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            errors.Add($"'{fieldName}' must be a valid number.");
            return;
        }

        if (TryGetDecimal(settings, "MinValue", out var minValue) && number < minValue)
        {
            errors.Add($"'{fieldName}' must be at least {minValue}.");
        }

        if (TryGetDecimal(settings, "MaxValue", out var maxValue) && number > maxValue)
        {
            errors.Add($"'{fieldName}' must be at most {maxValue}.");
        }
    }

    private static Dictionary<string, JsonElement> ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, JsonElement>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                   ?? new Dictionary<string, JsonElement>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static bool TryGetInt(Dictionary<string, JsonElement> settings, string key, out int value)
    {
        value = 0;
        // Check both PascalCase and camelCase
        if (!settings.TryGetValue(key, out var element) &&
            !settings.TryGetValue(char.ToLowerInvariant(key[0]) + key[1..], out element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
        {
            return true;
        }
        return false;
    }

    private static bool TryGetDecimal(Dictionary<string, JsonElement> settings, string key, out decimal value)
    {
        value = 0;
        if (!settings.TryGetValue(key, out var element) &&
            !settings.TryGetValue(char.ToLowerInvariant(key[0]) + key[1..], out element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value))
        {
            return true;
        }
        return false;
    }

    private static bool TryGetString(Dictionary<string, JsonElement> settings, string key, out string? value)
    {
        value = null;
        if (!settings.TryGetValue(key, out var element) &&
            !settings.TryGetValue(char.ToLowerInvariant(key[0]) + key[1..], out element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return value != null;
        }
        return false;
    }
}
