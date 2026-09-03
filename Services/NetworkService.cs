using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using HtmlAgilityPack;

namespace StoreScanner.Services;

public interface INetworkService
{
    Task<bool> PingAsync(string ipAddress, int timeout = 2000);
    Task<string?> ResolveHostnameAsync(string hostname);
    Task<(bool isAvailable, List<string> errors)> CheckUtmStatusAsync(string ipAddress);
}

public class NetworkService : INetworkService
{
    public async Task<bool> PingAsync(string ipAddress, int timeout = 2000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeout);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> ResolveHostnameAsync(string hostname)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(hostname);
            foreach (var ip in entry.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch
        {
            // Hostname not resolved
        }
        return null;
    }

    public async Task<(bool isAvailable, List<string> errors)> CheckUtmStatusAsync(string ipAddress)
    {
        var errors = new List<string>();
        
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            
            var url = $"http://{ipAddress}:8080/app";
            var html = await client.GetStringAsync(url);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // Ищем все mat-icon элементы в блоке ng-star-inserted
            var targetDiv = doc.DocumentNode.SelectSingleNode("//div[@class='ng-star-inserted']");
            
            if (targetDiv != null)
            {
                var matIcons = targetDiv.SelectNodes(".//mat-icon");
                
                if (matIcons != null && matIcons.Count == 8)
                {
                    bool allOk = true;
                    foreach (var icon in matIcons)
                    {
                        var iconText = icon.InnerText?.Trim().ToLower();
                        
                        // Проверяем на ошибки: clear или error_outline
                        if (iconText == "clear" || iconText == "error_outline")
                        {
                            allOk = false;
                            // Ищем ошибку в родительском элементе ci-utm-info-list-item
                            var infoListItem = icon.Ancestors("div").FirstOrDefault(a => a.HasClass("ci-utm-info-list-item"));
                            if (infoListItem != null)
                            {
                                var errorDiv = infoListItem.SelectSingleNode(".//div[@class='ci-utm-info-list-item-text']");
                                if (errorDiv != null)
                                {
                                    var errorText = errorDiv.InnerText?.Trim();
                                    if (!string.IsNullOrEmpty(errorText))
                                        errors.Add(errorText);
                                }
                            }
                        }
                        else if (iconText != "check")
                        {
                            // Неожиданное значение иконки
                            allOk = false;
                        }
                    }
                    
                    if (allOk)
                        return (true, new List<string>());
                    else
                        return (false, errors);
                }
                else
                {
                    // Количество иконок не равно 8
                    errors.Add($"Ожидается 8 индикаторов, найдено: {matIcons?.Count ?? 0}");
                    return (false, errors);
                }
            }
            else
            {
                // Не нашли целевой блок
                errors.Add("Не удалось найти блок статуса УТМ");
                return (false, errors);
            }
        }
        catch (HttpRequestException httpEx)
        {
            errors.Add($"Ошибка HTTP: {httpEx.Message}");
            return (false, errors);
        }
        catch (TaskCanceledException)
        {
            errors.Add("Превышено время ожидания ответа от УТМ");
            return (false, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Ошибка проверки УТМ: {ex.Message}");
            return (false, errors);
        }
    }
}
