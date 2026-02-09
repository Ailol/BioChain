namespace Repository.Entities;

public class Neurotransmitter
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Navigation properties
    public ICollection<NeurotransmitterProfile> NeurotransmitterProfiles { get; set; } = [];
}
