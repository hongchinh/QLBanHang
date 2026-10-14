namespace OrderMgmt.Infrastructure.Excel.Tt133;

public enum Tt133CellKind { Text, Date, Money, Quantity }

/// Symbol: "A", "B", "1", "2"… Group: the upper-tier caption shared by neighbouring columns, null = no group.
public sealed record Tt133Column(string Symbol, string Header, double Width, Tt133CellKind Kind = Tt133CellKind.Text, string? Group = null);
