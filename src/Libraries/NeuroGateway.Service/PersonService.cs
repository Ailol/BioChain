using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class PersonService(PersonRepository personRepo, PersonalityRepository personalityRepo)
{
    public async Task<(Guid PersonId, int PersonalityId)> EnsureAsync(string name)
    {
        var personId = await personRepo.EnsureExistsAsync(name);
        var personalityId = await personalityRepo.EnsureExistsAsync(personId);
        return (personId, personalityId);
    }

    public Task<List<string>> ListAsync() => personRepo.ListAsync();

    public Task<Guid?> FindAsync(string name) => personRepo.GetIdAsync(name);

    public Task<List<string>> FindSimilarAsync(string name) => personRepo.FindSimilarAsync(name);
}
