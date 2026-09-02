namespace FieldSim.Core;

public static class VillageGridReference
{
    public const int GridSize = 13;

    private static readonly string[] RowNames =
    [
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf",
        "Hotel", "India", "Juliett", "Kilo", "Lima", "Mike"
    ];

    public static string RowName(int row)
    {
        if (row < 0 || row >= GridSize) throw new ArgumentOutOfRangeException(nameof(row));
        return RowNames[row];
    }

    public static string RowCode(int row) => RowName(row)[0].ToString();

    public static string CellLabel(GridPoint point)
    {
        Validate(point);
        return $"{RowName(point.Y)}-{point.X + 1:00}";
    }

    public static string ShortCellLabel(GridPoint point)
    {
        Validate(point);
        return $"{RowCode(point.Y)}{point.X + 1:00}";
    }

    public static string FullLabel(GridPoint point, int keypad)
    {
        if (keypad is < 1 or > 9) throw new ArgumentOutOfRangeException(nameof(keypad));
        return $"{CellLabel(point)} / KP{keypad}";
    }

    public static int KeypadFromNormalizedPosition(double x, double y)
    {
        x = Math.Clamp(x, 0.0, 0.999999);
        y = Math.Clamp(y, 0.0, 0.999999);
        var column = Math.Clamp((int)(x * 3.0), 0, 2);
        var rowFromTop = Math.Clamp((int)(y * 3.0), 0, 2);
        var rowFromBottom = 2 - rowFromTop;
        return rowFromBottom * 3 + column + 1;
    }

    private static void Validate(GridPoint point)
    {
        if (point.X < 0 || point.X >= GridSize || point.Y < 0 || point.Y >= GridSize)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
    }
}
