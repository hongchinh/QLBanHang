namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Volume knobs of the sample dataset.
public static class SampleDataVolumes
{
    /// Opening stock in K1: weeks of planned sales.
    public const int OpeningWeeksK1Min = 3;
    public const int OpeningWeeksK1Max = 6;

    /// Opening stock in K2 of a product that is also in K1: a smaller amount.
    public const int OpeningWeeksK2SharedMin = 1;
    public const int OpeningWeeksK2SharedMax = 3;

    /// Opening stock in K2 of a K2-only product: the full range, as it can be sold from K2 only until bought.
    public const int OpeningWeeksK2OnlyMin = 3;
    public const int OpeningWeeksK2OnlyMax = 6;
}
