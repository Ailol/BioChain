using BioChain.Repository;
using BioChain.Repository.Repositories;
using BioChain.Service;
using BioChain.Utils.Parsing;

namespace BioChain.Server.Api;

public static class AnalyzeApi
{
    public static RouteGroupBuilder MapAnalyzeApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyze").WithTags("Analyze");

        // Analyze text input
        group.MapPost("/", async (AnalyzeRequest req, AnalyzeService svc, IUserContext ctx,
            IPersonRepository persons) =>
        {
            if (!await persons.HasAccessAsync(req.PersonId, ctx.UserId))
                return Results.Forbid();

            var result = await svc.AnalyzeAsync(req.PersonId, req.Text, req.Kind);
            return Results.Ok(new
            {
                result.DataId,
                result.ProtocolsStored,
                result.LinesTotal,
            });
        });

        // Analyze document (PDF/DOCX via base64)
        group.MapPost("/document", async (AnalyzeDocumentRequest req, AnalyzeService svc,
            IUserContext ctx, IPersonRepository persons) =>
        {
            if (!await persons.HasAccessAsync(req.PersonId, ctx.UserId))
                return Results.Forbid();

            var text = DocumentExtractor.ExtractText(req.Content, req.DocumentType);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { error = "Could not extract text from document" });

            var result = await svc.AnalyzeAsync(req.PersonId, text, req.Kind);
            return Results.Ok(new
            {
                result.DataId,
                result.ProtocolsStored,
                result.LinesTotal,
            });
        });

        // Get analysis history for a person
        group.MapGet("/{personId:guid}", async (Guid personId, IUserContext ctx,
            IPersonRepository persons, IProtocolRepository protocols) =>
        {
            if (!await persons.HasAccessAsync(personId, ctx.UserId))
                return Results.Forbid();

            var list = await protocols.GetByPersonAsync(personId);
            return Results.Ok(new
            {
                count = list.Count,
                protocols = list.Select(p => new
                {
                    p.Id, p.Tag, p.Formula, p.Status, p.Phase,
                    createdAt = p.CreatedOnUtc,
                }),
            });
        });

        return group;
    }
}

public record AnalyzeRequest(Guid PersonId, string Text, string Kind);
public record AnalyzeDocumentRequest(Guid PersonId, string Content, string DocumentType, string Kind);
