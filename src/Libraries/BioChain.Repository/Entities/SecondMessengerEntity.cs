namespace BioChain.Repository.Entities;

public class SecondMessengerEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = "";            // cAMP, cGMP, IP3, DAG, Ca2+, PKA, PKC, CREB, MAPK
    public string Label { get; set; } = "";
    public string MessengerType { get; set; } = "";  // messenger, kinase, transcription_factor, phosphoprotein
    public string Config { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
