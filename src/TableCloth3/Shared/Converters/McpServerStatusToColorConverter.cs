using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace TableCloth3.Shared.Converters;

public sealed class McpServerStatusToColorConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && 
            values[0] is bool isHealthy && 
            values[1] is bool isChecking)
        {
            if (isChecking)
                return Colors.Orange; // 확인 중일 때는 주황색
            
            return isHealthy ? Colors.Green : Colors.Red; // 정상일 때는 녹색, 오류일 때는 빨간색
        }
        
        return Colors.Gray; // 기본값
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}