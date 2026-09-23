using System.Globalization;

namespace PCDoctor.Core.Formatting;

public static class ByteFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string FromBytes(long bytes)
    {
        if (bytes < 0)
        {
            return "n/a";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = value >= 100 || unit == 0 ? "0" : "0.##";
        return string.Create(CultureInfo.InvariantCulture, $"{value.ToString(format, CultureInfo.InvariantCulture)} {Units[unit]}");
    }

    public static string Percentage(double value)
        => string.Create(CultureInfo.InvariantCulture, $"{value:0.0}%");
}
