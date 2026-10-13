using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// Helpers of the executive dashboard tests. A query string never repeats a key: a case that needs another `at`
/// or other dates is written in full, not appended to `October`.
public abstract class ExecutiveDashboardTestBase : CashDebtTestBase
{
    protected const string OctoberRange = "from=2026-10-01&to=2026-10-31";
    protected const string October = OctoberRange + "&at=2026-10-31";

    protected ExecutiveDashboardTestBase(PostgresFixture pg) : base(pg) { }

    /// An admin client whose working branch is the dataset's main branch.
    protected HttpClient MainClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    /// GET /api/dashboard/executive/{pathAndQuery}, success body read as T.
    protected static async Task<T> GetExecAsync<T>(HttpClient client, string pathAndQuery) =>
        await ReadDataAsync<T>(await GetExecRawAsync(client, pathAndQuery));

    protected static Task<HttpResponseMessage> GetExecRawAsync(HttpClient client, string pathAndQuery) =>
        client.GetAsync($"/api/dashboard/executive/{pathAndQuery}");
}
