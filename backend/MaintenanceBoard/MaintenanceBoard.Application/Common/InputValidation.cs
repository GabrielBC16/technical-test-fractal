using MaintenanceBoard.Application.Common.Exceptions;

namespace MaintenanceBoard.Application.Common;

internal static class InputValidation
{
    public static string RequiredText(string? value, string field, int maximumLength, bool trim = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(field, $"{field} is required.");
        }

        CheckLength(value, field, maximumLength);
        return trim ? value.Trim() : value;
    }

    public static string? OptionalText(string? value, string field, int maximumLength)
    {
        CheckLength(value, field, maximumLength);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static void CheckLength(string? value, string field, int maximumLength)
    {
        if (value?.Length > maximumLength)
        {
            throw new RequestValidationException(field, $"{field} must not exceed {maximumLength} characters.");
        }
    }

    public static void PositiveId(int value, string field)
    {
        if (value <= 0)
        {
            throw new RequestValidationException(field, $"{field} must be a positive integer.");
        }
    }
}
