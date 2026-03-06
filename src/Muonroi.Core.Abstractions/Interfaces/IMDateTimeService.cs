namespace Muonroi.Core.Abstractions.Interfaces;

public interface IMDateTimeService
{
    DateTime Now();
    DateTime UtcNow();
    DateTime Today();
    DateTime UtcToday();
    double NowTs();
    double UtcNowTs();
}
