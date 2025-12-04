using MilkiBotFramework.Data;

namespace MilkiBotFramework.Services;

public interface ISensitiveScanService
{
    Task<List<SensitiveScanEntry>> GetScanResultsAsync(params IEnumerable<string> messageContent);
    string? SanitizeString(string? originalString, SensitiveScanEntry scanResult);
}