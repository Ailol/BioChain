using BioChain.Repository;

namespace BioChain.Service;

public class ProfileService(ObservationRepository observationRepo, PersonalityRepository personalityRepo)
{
    public Task<string?> GetCommunicationStyleAsync(string person) =>
        personalityRepo.GetCommunicationStyleAsync(person);

    public Task UpdateCommunicationStyleAsync(string person, string style) =>
        personalityRepo.UpdateCommunicationStyleAsync(person, style);

    public Task<List<(string Signal, string Formula)>> GetProfileAsync(string person) =>
        observationRepo.GetByPersonAsync(person);

    public Task<List<(string Signal, int Count)>> GetSignalCountsAsync(string person) =>
        observationRepo.GetSignalCountsAsync(person);
}
