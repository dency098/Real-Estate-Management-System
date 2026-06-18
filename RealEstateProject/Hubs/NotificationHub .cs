using Microsoft.AspNetCore.SignalR;

namespace RealEstateProject.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendRequest(string user, string title, string type)
        {
            await Clients.All.SendAsync("ReceiveRequest", user, title, type);
        }
    }
}