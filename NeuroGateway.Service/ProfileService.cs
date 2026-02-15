using NeuroGateway.Repository;

namespace NeuroGateway.Service;

public class ProfileService(ProfileRepository profileRepo, PersonalityRepository personalityRepo)
{
    public Task<string?> GetCommunicationStyleAsync(string person) =>
        personalityRepo.GetCommunicationStyleAsync(person);

    public Task UpdateCommunicationStyleAsync(string person, string style) =>
        personalityRepo.UpdateCommunicationStyleAsync(person, style);

    public Task<List<(string Chemical, string Reasoning)>> GetProfileAsync(string person) =>
        profileRepo.GetByPersonAsync(person);

    public Task<List<(string Chemical, int Count)>> GetChemicalCountsAsync(string person) =>
        profileRepo.GetChemicalCountsAsync(person);
}
