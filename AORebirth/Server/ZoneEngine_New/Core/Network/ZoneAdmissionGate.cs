namespace ZoneEngine_New.Core.Network;

using AORebirth.Database.Dao;
using AORebirth.Interfaces.Persistence.Characters;
using SmokeLounge.AOtomation.Messaging.Messages.SystemMessages;

public interface IZoneAdmissionGate
{
    ZoneHandoffClaim Claim(ZoneLoginMessage message);

    /// <summary>Claim using the client's source address to pick the LAN/public redirect target.</summary>
    ZoneHandoffClaim Claim(ZoneLoginMessage message, System.Net.IPAddress? remote) => Claim(message);
}

/// <summary>Reuses the existing read-only account resolver; never hydrates an owned player.</summary>
public sealed class ZoneAdmissionGate(ICharacterDao characters) : IZoneAdmissionGate
{
    public ZoneHandoffClaim Claim(ZoneLoginMessage message) => Claim(message, null);

    public ZoneHandoffClaim Claim(ZoneLoginMessage message, System.Net.IPAddress? remote)
    {
        ZoneRedirectEndpoint target = ZoneRedirectEndpoint.Configured(remote);
        return ZoneHandoffStore.Configured().Claim(message.CharacterId, message.Cookie1, message.Cookie2,
            id => characters.LoadById(id)?.AccountUsername, target.Address.ToString(), target.Port);
    }
}
