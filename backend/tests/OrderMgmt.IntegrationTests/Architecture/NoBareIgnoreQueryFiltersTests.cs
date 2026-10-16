using System.Text.RegularExpressions;
using FluentAssertions;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Architecture;

public class NoBareIgnoreQueryFiltersTests
{
    [Fact]
    public void Src_has_no_bare_IgnoreQueryFilters()
    {
        var bare = new Regex(@"IgnoreQueryFilters\(\s*\)");
        var hits = SourceTree.SourceFiles()
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => bare.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(SourceTree.BackendRoot(), x.f)}:{x.i + 1}")
            .ToList();
        hits.Should().BeEmpty("backend/src names the filter: IgnoreQueryFilters([QueryFilters.SoftDelete]), " +
            "or IgnoreQueryFilters(new[] { QueryFilters.SoftDelete }) inside an expression tree");
    }
}
