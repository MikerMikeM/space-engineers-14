using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Server.ServerStatus;
using Robust.Shared.Player;

public sealed partial class AdminListEndpoint : EntitySystem
{
    [Dependency] private IStatusHost _statusHost = default!;
    [Dependency] private IAdminManager _adminManager = default!;

    private const string CommandPath = "/status/admins";
    private const string DefaultRank = "Администратор";

    public override void Initialize()
    {
        base.Initialize();
        _statusHost.AddHandler(HandleRequest);
    }

    private async Task<bool> HandleRequest(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Get || context.Url.AbsolutePath != CommandPath)
            return false;

        var admins = _adminManager.ActiveAdmins
            .Select(session =>
            {
                var data = _adminManager.GetAdminData(session);
                return new
                {
                    name = session.Name,
                    rank = string.IsNullOrWhiteSpace(data?.Title) ? DefaultRank : data!.Title
                };
            })
            .ToArray();

        var json = JsonSerializer.Serialize(new { admins });

        await context.RespondAsync(json, HttpStatusCode.OK);
        return true;
    }
}
