using OrderMgmt.Application.Common.Interfaces;

namespace OrderMgmt.Application.Common;

public sealed class ActingIdentityHolder : IActingIdentity
{
    public ActingIdentity? Current { get; private set; }

    public void Set(ActingIdentity identity) => Current = identity;
}
