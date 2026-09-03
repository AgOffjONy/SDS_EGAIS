using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace StoreScanner.Models;

public partial class Store : ObservableObject
{
    [ObservableProperty] private int _storeNumber;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _region = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _format = string.Empty;
    [ObservableProperty] private string _mikrotikIp = string.Empty;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private ObservableCollection<Computer> _computers = new();

    public static string GetMikrotikIp(int storeNumber)
    {
        if (storeNumber <= 200)
        {
            // Магазины до 200: 192.168.{номер}.81
            return $"192.168.{storeNumber}.81";
        }
        else
        {
            // Магазины после 200 в подсетях 10.102, 10.103, 10.104, 10.105
            // №225 -> 10.102.25.1 (225-200=25)
            int offset = storeNumber - 200;
            int subnetIndex = (offset - 1) / 256; // 0, 1, 2, 3 для 10.102-105
            int thirdOctet = ((offset - 1) % 256) + 1;
            int fourthOctet = 1;
            return $"10.{102 + subnetIndex}.{thirdOctet}.{fourthOctet}";
        }
    }
}

public partial class Computer : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private string _utmStatus = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _utmErrors = new();
}
