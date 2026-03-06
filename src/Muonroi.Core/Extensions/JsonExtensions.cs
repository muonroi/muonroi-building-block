namespace Muonroi.Core.Extensions;

public static class JsonExtensions
{
    private static readonly JsonSerializerOptions _option = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false
    };

    public static string Serialize(this object obj)
    {
        try
        {
            return JsonSerializer.Serialize(obj, _option); // MBB002-exempt: static-class boundary
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
