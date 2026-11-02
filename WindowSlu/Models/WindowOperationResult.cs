namespace WindowSlu.Models;

public class WindowOperationResult
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Unmatched { get; set; }
    public int Ambiguous { get; set; }
    public List<string> Details { get; } = new();
    public string Summary => $"{Succeeded} succeeded, {Failed} failed, {Unmatched} unmatched, {Ambiguous} ambiguous";
}
