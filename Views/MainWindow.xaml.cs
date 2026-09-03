using System.Windows;
using System.Windows.Data;
using StoreScanner.ViewModels;
using StoreScanner.Services;

namespace StoreScanner.Views;

public partial class MainWindow : Window
{
    public MainWindow(INetworkService networkService)
    {
        InitializeComponent();
        DataContext = new MainViewModel(networkService);
    }
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Если параметр "Invert", то инвертируем
            if (parameter is string param && param == "Invert")
                return !boolValue;
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
