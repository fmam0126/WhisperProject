namespace WhisperProject.Models;

public class VadTimeMap
{
    public TimeSpan OriginalTimeStart { get; set; }
    public TimeSpan OriginalTimeEnd { get; set; }
    public TimeSpan ProcessedTimeStart { get; set; }
    public TimeSpan ProcessedTimeEnd { get; set; }
}