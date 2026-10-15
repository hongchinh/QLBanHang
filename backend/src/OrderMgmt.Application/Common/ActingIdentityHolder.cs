using OrderMgmt.Application.Common.Interfaces;

namespace OrderMgmt.Application.Common;

public sealed class ActingIdentityHolder : IActingIdentity
{
    public ActingIdentity? Current { get; private set; }

    // One identity per scope: AppDbContext reads the user id at SaveChanges, so reuse would mis-attribute CreatedBy.
    public void Set(ActingIdentity identity)
    {
        if (Current is not null) throw new InvalidOperationException("The acting identity is already set for this scope.");
        Current = identity;
    }
}
