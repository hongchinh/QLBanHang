namespace OrderMgmt.Application.Common;

/// <summary>
/// ILike patterns built from user input. Every ILike on an escaped pattern must pass <see cref="EscapeCharacter"/>:
/// without an escape argument Npgsql sends ESCAPE '' (no escape character), the added backslashes stay literal and a
/// search containing %, _ or \ misses the rows that contain it.
/// </summary>
public static class LikePattern
{
    public const string EscapeCharacter = "\\";

    public static string Escape(string input) =>
        input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
