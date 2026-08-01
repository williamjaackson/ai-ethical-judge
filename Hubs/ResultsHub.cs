using Microsoft.AspNetCore.SignalR;

namespace ResultsGraphApi.Hubs
{
    /// <summary>
    /// Clients (the graph page) connect here and listen for the "ResultsUpdated" event,
    /// which the controller/watcher broadcast every time a new JSON file is read.
    /// </summary>
    public class ResultsHub : Hub
    {
    }
}
