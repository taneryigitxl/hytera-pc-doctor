using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PCDoctor.Core.Enums;

namespace PCDoctor.App.Converters;

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter?.ToString() == "invert")
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = value is null || value is string text && string.IsNullOrWhiteSpace(text);
        return empty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Severity.Critical => "SeverityCriticalBrush",
            Severity.Warning => "SeverityWarningBrush",
            _ => "SeverityOkBrush"
        };

        return Application.Current?.TryFindResource(key) as Brush
               ?? new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class BooleanToStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = parameter?.ToString() == "test"
            ? value is true ? "Status.Passed" : "Status.Failed"
            : value is true ? "Status.Enabled" : "Status.Disabled";

        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class SeverityToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            Severity.Critical => "Severity.Critical",
            Severity.Warning => "Severity.Warning",
            _ => "Severity.Ok"
        };

        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}
