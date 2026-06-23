using Microsoft.AspNetCore.SignalR;

namespace MEP_Management_System.Hubs;

public class InventoryHub : Hub
{
    // Basic hub for inventory real-time updates. Keep lightweight; authorization is enforced at controller level.
}
