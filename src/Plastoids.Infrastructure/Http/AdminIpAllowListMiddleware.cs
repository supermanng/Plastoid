using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Plastoids.Infrastructure.Http;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>IPs or CIDR ranges allowed to call /api/v1/admin/*. Empty → loopback only.</summary>
    public string[] IpAllowList { get; set; } = [];
}

/// <summary>Build prompt §2.10: staff endpoints under /api/v1/admin/* require an IP allow-list in addition to the staff policy.</summary>
public sealed class AdminIpAllowListMiddleware(RequestDelegate next, IOptions<AdminOptions> options)
{
    public const string PathPrefix = "/api/v1/admin";
    private readonly (IPAddress Network, int Prefix)[] _allowed = options.Value.IpAllowList.Select(Parse).ToArray();

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(PathPrefix) && !IsAllowed(context.Connection.RemoteIpAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://docs.plastoids.ng/errors/admin_ip_not_allowed",
                title = "admin_ip_not_allowed",
                status = 403,
                detail = "This address may not call staff endpoints.",
                code = "admin_ip_not_allowed",
            });
            return;
        }

        await next(context);
    }

    private bool IsAllowed(IPAddress? remote)
    {
        if (remote is null)
        {
            // Only the in-memory TestServer has no connection address; Kestrel always sets one. Treat it as loopback.
            remote = IPAddress.Loopback;
        }

        if (remote.IsIPv4MappedToIPv6)
        {
            remote = remote.MapToIPv4();
        }

        if (_allowed.Length == 0)
        {
            return IPAddress.IsLoopback(remote);
        }

        return _allowed.Any(a => Matches(remote, a.Network, a.Prefix));
    }

    private static (IPAddress, int) Parse(string entry)
    {
        var parts = entry.Split('/', 2);
        var ip = IPAddress.Parse(parts[0].Trim());
        var prefix = parts.Length == 2 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
        return (ip, prefix);
    }

    private static bool Matches(IPAddress address, IPAddress network, int prefix)
    {
        if (address.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var a = address.GetAddressBytes();
        var n = network.GetAddressBytes();
        var fullBytes = prefix / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (a[i] != n[i])
            {
                return false;
            }
        }

        var remaining = prefix % 8;
        if (remaining == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remaining));
        return (a[fullBytes] & mask) == (n[fullBytes] & mask);
    }
}
