namespace HamiChap_MES_ERP.Services;

public class SessionInactivityService
{
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    public TimeSpan Timeout { get; set; } =
        TimeSpan.FromMinutes(15);

    public void RecordActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    public bool IsExpired()
    {
        return DateTime.UtcNow - _lastActivityUtc >= Timeout;
    }

    public DateTime LastActivityUtc =>
        _lastActivityUtc;
}