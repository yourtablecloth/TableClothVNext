using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace TableCloth3.Spork.Converters;

public sealed class Int32EqualsConverter : MarkupExtension, IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int intValue &&
        parameter is string text &&
        int.TryParse(text, out var paramterValue) &&
        intValue == paramterValue;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;
}
