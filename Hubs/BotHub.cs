using System.Threading.Tasks;

using Microsoft.AspNet.SignalR;

namespace ClintonFrankland
{

    public class BotHub : Hub
    {

        public async Task RequestGroupInvite(GroupInvite invite)
        {
            await Clients.Others.NewRequest;
        }

        public class GroupInvite
        {
            public string GroupId { get; set; }
            public string RoleId { get; set; }
            public string AgentId { get; set; }
        }

    }
}