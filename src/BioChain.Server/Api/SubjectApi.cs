using BioChain.Repository;
using BioChain.Repository.Entities;
using BioChain.Repository.Repositories;

namespace BioChain.Server.Api;

public static class SubjectApi
{
    public static RouteGroupBuilder MapSubjectApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subjects").WithTags("Subjects");

        // List subjects for current user
        group.MapGet("/", async (ISubjectRepository repo, IUserContext ctx) =>
        {
            var subjects = await repo.GetByOwnerAsync(ctx.UserId);
            return Results.Ok(subjects.Select(s => new SubjectDto(s.Id, s.Name, s.Kind, s.CreatedOnUtc)));
        });

        // Create a new subject
        group.MapPost("/", async (CreateSubjectRequest req, ISubjectRepository repo, IUserContext ctx) =>
        {
            var name = req.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest("Name is required.");

            // Check for duplicate name per owner
            var existing = await repo.GetByOwnerAndNameAsync(ctx.UserId, name);
            if (existing is not null)
                return Results.Ok(new SubjectDto(existing.Id, existing.Name, existing.Kind, existing.CreatedOnUtc));

            var entity = await repo.CreateAsync(new SubjectEntity
            {
                Id = Guid.NewGuid(),
                OwnerId = ctx.UserId,
                Name = name,
                Kind = "person",
                Namespace = "biochain",
                CreatedOnUtc = DateTimeOffset.UtcNow,
            });

            return Results.Created($"/api/subjects/{entity.Id}",
                new SubjectDto(entity.Id, entity.Name, entity.Kind, entity.CreatedOnUtc));
        });

        return group;
    }
}

public record CreateSubjectRequest(string? Name);
public record SubjectDto(Guid Id, string Name, string Kind, DateTimeOffset CreatedAt);
