using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using StoreScanner.Models;
using StoreScanner.Services;

namespace StoreScanner.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INetworkService _networkService;
    private readonly string _dataFilePath = "stores.json";

    [ObservableProperty] private ObservableCollection<Store> _stores = new();
    [ObservableProperty] private ObservableCollection<string> _regions = new();
    [ObservableProperty] private ObservableCollection<string> _cities = new();
    [ObservableProperty] private ObservableCollection<string> _formats = new();
    [ObservableProperty] private string? _selectedRegion;
    [ObservableProperty] private string? _selectedCity;
    [ObservableProperty] private string? _selectedFormat;
    [ObservableProperty] private int _newStoreNumber;
    [ObservableProperty] private string _newStoreName = string.Empty;
    [ObservableProperty] private string _newStoreRegion = string.Empty;
    [ObservableProperty] private string _newStoreCity = string.Empty;
    [ObservableProperty] private string _newStoreFormat = "Хороший";
    [ObservableProperty] private string _newComputerName = string.Empty;
    [ObservableProperty] private int _selectedStoreForComputer;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<Store> _pendingStores = new();

    public MainViewModel(INetworkService networkService)
    {
        _networkService = networkService;
        LoadData();
        UpdateFilters();
    }

    public ObservableCollection<Store> FilteredStores
    {
        get
        {
            var filtered = Stores;
            if (!string.IsNullOrEmpty(SelectedRegion))
                filtered = new ObservableCollection<Store>(filtered.Where(s => s.Region == SelectedRegion).ToList());
            if (!string.IsNullOrEmpty(SelectedCity))
                filtered = new ObservableCollection<Store>(filtered.Where(s => s.City == SelectedCity).ToList());
            if (!string.IsNullOrEmpty(SelectedFormat))
                filtered = new ObservableCollection<Store>(filtered.Where(s => s.Format == SelectedFormat).ToList());
            return filtered;
        }
    }

    [RelayCommand]
    private void AddStore()
    {
        if (NewStoreNumber <= 0 || string.IsNullOrEmpty(NewStoreName))
            return;

        var store = new Store
        {
            StoreNumber = NewStoreNumber,
            Name = NewStoreName,
            Region = NewStoreRegion,
            City = NewStoreCity,
            Format = NewStoreFormat,
            MikrotikIp = Store.GetMikrotikIp(NewStoreNumber)
        };

        Stores.Add(store);
        UpdateFilters();
        SaveData();
    }

    [RelayCommand]
    private void AddComputer()
    {
        var store = Stores.FirstOrDefault(s => s.StoreNumber == SelectedStoreForComputer);
        if (store == null || string.IsNullOrEmpty(NewComputerName))
            return;

        var computer = new Computer
        {
            Name = NewComputerName
        };
        store.Computers.Add(computer);
        SaveData();
    }

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        StatusMessage = "Начало сканирования...";

        try
        {
            foreach (var store in Stores)
            {
                StatusMessage = $"Проверка магазина {store.Name}...";
                store.IsAvailable = await _networkService.PingAsync(store.MikrotikIp);
                
                if (store.IsAvailable)
                {
                    // Определяем подсеть для поиска компьютеров
                    string subnetBase;
                    if (store.StoreNumber <= 200)
                        subnetBase = $"192.168.{store.StoreNumber}";
                    else
                    {
                        var ipParts = store.MikrotikIp.Split('.');
                        subnetBase = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}";
                    }

                    // Сканируем подсеть в поисках компьютеров EGAIS-DEB или EGIAS-DEB
                    for (int i = 1; i <= 254; i++)
                    {
                        try
                        {
                            var ipToCheck = $"{subnetBase}.{i}";
                            var reply = await _networkService.PingAsync(ipToCheck, 1000);
                            
                            if (reply)
                            {
                                try
                                {
                                    var entry = await System.Net.Dns.GetHostEntryAsync(ipToCheck);
                                    var hostname = entry.HostName.ToUpper();
                                    
                                    if (hostname.Contains("EGAIS-DEB") || hostname.Contains("EGIAS-DEB"))
                                    {
                                        var existingComputer = store.Computers.FirstOrDefault(c => c.IpAddress == ipToCheck);
                                        if (existingComputer == null)
                                        {
                                            var computer = new Computer 
                                            { 
                                                Name = entry.HostName, 
                                                IpAddress = ipToCheck 
                                            };
                                            store.Computers.Add(computer);
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }

                    // Проверяем доступность найденных компьютеров и УТМ
                    foreach (var computer in store.Computers.ToList())
                    {
                        if (!string.IsNullOrEmpty(computer.IpAddress))
                        {
                            computer.IsAvailable = await _networkService.PingAsync(computer.IpAddress);
                            
                            if (computer.IsAvailable)
                            {
                                var (utmAvailable, utmErrors) = await _networkService.CheckUtmStatusAsync(computer.IpAddress);
                                
                                if (utmAvailable)
                                {
                                    computer.UtmStatus = "УТМ доступен";
                                    computer.UtmErrors.Clear();
                                }
                                else
                                {
                                    computer.UtmStatus = "УТМ недоступен";
                                    computer.UtmErrors.Clear();
                                    foreach (var error in utmErrors)
                                        computer.UtmErrors.Add(error);
                                }
                            }
                            else
                            {
                                computer.UtmStatus = "Компьютер недоступен";
                                computer.UtmErrors.Clear();
                            }
                        }
                    }
                }
            }
            StatusMessage = "Сканирование завершено";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void ConfirmPendingStores()
    {
        foreach (var store in PendingStores)
            Stores.Add(store);
        PendingStores.Clear();
        UpdateFilters();
        SaveData();
    }

    private void UpdateFilters()
    {
        Regions = new ObservableCollection<string>(Stores.Select(s => s.Region).Distinct().OrderBy(r => r));
        Cities = new ObservableCollection<string>(Stores.Select(s => s.City).Distinct().OrderBy(c => c));
        Formats = new ObservableCollection<string>(Stores.Select(s => s.Format).Distinct().OrderBy(f => f));
        OnPropertyChanged(nameof(FilteredStores));
    }

    private void SaveData()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Stores, options);
            File.WriteAllText(_dataFilePath, json);
        }
        catch { }
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                var stores = JsonSerializer.Deserialize<ObservableCollection<Store>>(json);
                if (stores != null)
                {
                    Stores = stores;
                    foreach (var store in Stores)
                        store.PropertyChanged += (s, e) => OnPropertyChanged(nameof(FilteredStores));
                }
            }
        }
        catch { }
    }
}
