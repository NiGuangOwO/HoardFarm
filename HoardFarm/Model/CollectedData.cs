namespace HoardFarm.Model;

public class CollectedData
{
    public double Runtime { get; set; }
    public bool HoardFound { get; set; }
    public ushort TerritoryTyp { get; set; }
    public bool? HoardCollected { get; set; }
    public double? MoveTime { get; set; }
    public bool SafetyMode { get; set; }

    public bool IsValid()
    {
        var valid = Runtime > 0 && TerritoryTyp > 0;
        
        if (MoveTime.HasValue)
        {
            valid &= MoveTime.Value > 0;
        }

        return valid;
    }
}
