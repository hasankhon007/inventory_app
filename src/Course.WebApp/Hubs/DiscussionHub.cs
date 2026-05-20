using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Course.WebApp.Hubs;

[Authorize]
public class DiscussionHub : Hub
{
}
