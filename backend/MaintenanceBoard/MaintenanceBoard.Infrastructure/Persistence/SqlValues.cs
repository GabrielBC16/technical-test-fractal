using System.Data;

namespace MaintenanceBoard.Infrastructure.Persistence;

internal static class SqlValues
{
    // SQL datetime2 has no timezone metadata. Database timestamps are UTC by contract.
    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static TEnum ParseEnum<TEnum>(string value) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: false, out var result) &&
            Enum.IsDefined(result) && string.Equals(result.ToString(), value, StringComparison.Ordinal))
        {
            return result;
        }

        throw new DataException($"The database contains an unsupported {typeof(TEnum).Name} value.");
    }
}
