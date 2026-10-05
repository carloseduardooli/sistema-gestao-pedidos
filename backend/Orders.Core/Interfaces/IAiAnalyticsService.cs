namespace Orders.Core.Interfaces;

public interface IAiAnalyticsService
{
    Task<string> AskAboutOrdersAsync(string userQuestion);
}
