using OrderMgmt.Domain.Common;

namespace OrderMgmt.Application.CashDebt.Ledger;

/// The 422 confirmation (P10) of a save, cancel or delete that would remove settlements of other documents.
public static class SettlementRemovalConfirmation
{
    public const string Code = "DEBT_SETTLEMENTS_WILL_BE_REMOVED";

    private const string Message = "Chứng từ đã được đối trừ. Lưu sẽ bỏ các đối trừ sau.";

    public static ConfirmationRequiredException Create(IDictionary<string, string[]> details) =>
        new(Code, Message, details, canConfirm: true);

    /// A confirmation that carries the token of the settlements it lists.
    public static ConfirmationRequiredException Create(IDictionary<string, string[]> details,
        IEnumerable<Guid> settlementIds) =>
        new(Code, Message, details, canConfirm: true, TokenOf(settlementIds));

    /// The token of a set of settlements: the distinct ids in Guid order, "N" format, comma-separated.
    public static string TokenOf(IEnumerable<Guid> ids) =>
        string.Join(',', ids.Distinct().Order().Select(id => id.ToString("N")));

    /// True when the confirmed token lists every settlement the save would remove. An absent token means the caller
    /// sent no token (unchanged behaviour), so it covers everything; an unparsable token covers nothing.
    public static bool Covers(string? token, IEnumerable<Guid> ids)
    {
        if (token is null) return true;
        var listed = Parse(token);
        return listed is not null && ids.All(listed.Contains);
    }

    /// One confirmation for two sources saved together (a stock voucher and its automatic voucher): the details of
    /// both, merged by the other document's code. The token is the union of the ids of both tokens (an absent or
    /// unparsable token adds none); it is null only when neither source has a token.
    public static ConfirmationRequiredException Merge(ConfirmationRequiredException first,
        ConfirmationRequiredException second)
    {
        var details = first.Details.Concat(second.Details)
            .GroupBy(d => d.Key)
            .ToDictionary(g => g.Key, g => g.SelectMany(d => d.Value).ToArray());
        if (first.Token is null && second.Token is null) return Create(details);
        var ids = (Parse(first.Token) ?? new HashSet<Guid>()).Union(Parse(second.Token) ?? new HashSet<Guid>());
        return Create(details, ids);
    }

    /// The ids listed in a token, or null when the token is null or any part is not an "N" format Guid.
    private static HashSet<Guid>? Parse(string? token)
    {
        if (token is null) return null;
        var ids = new HashSet<Guid>();
        foreach (var part in token.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParseExact(part, "N", out var id)) return null;
            ids.Add(id);
        }
        return ids;
    }
}
