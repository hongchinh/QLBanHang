namespace OrderMgmt.Application.Tt133;

public class Tt133ExportOptions
{
    public const string SectionName = "Tt133Export";

    /// The most sheets one export may hold; applies to explicit lists and to "all with activity" alike.
    public int MaxSheets { get; set; } = 300;
}
