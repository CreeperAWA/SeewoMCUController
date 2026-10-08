namespace SeewoMCUController.Ui;

/// <summary>控制台一行的内容与颜色。</summary>
internal readonly struct ConsoleRow
{
    public static readonly ConsoleRow Empty = new(string.Empty, ConsoleColor.Gray);

    public ConsoleRow(string text, ConsoleColor color)
    {
        Text = text ?? string.Empty;
        Color = color;
    }

    public string Text { get; }

    public ConsoleColor Color { get; }
}
