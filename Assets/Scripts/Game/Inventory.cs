public class Inventory
{
    public const int MaxMedkits = 5;

    public int medkits = 1;

    public void Reset()
    {
        medkits = 1;
    }

    public bool AddMedkit()
    {
        if (medkits >= MaxMedkits)
            return false;
        medkits++;
        return true;
    }
}
