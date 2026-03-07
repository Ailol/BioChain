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
            ISubjectRepository subjects) =>
        {
            if (!await subjects.HasAccessAsync(req.SubjectId, ctx.UserId))
                return Results.Forbid();

            var result = await svc.AnalyzeAsync(req.SubjectId, req.Text, req.Kind);
            return Results.Ok(new
            {
                result.StimuliId,
                result.AnalysesStored,
                result.LinesTotal,
            });
        });

        // Analyze document (PDF/DOCX via base64)
        group.MapPost("/document", async (AnalyzeDocumentRequest req, AnalyzeService svc,
            IUserContext ctx, ISubjectRepository subjects) =>
        {
            if (!await subjects.HasAccessAsync(req.SubjectId, ctx.UserId))
                return Results.Forbid();

            var text = DocumentExtractor.ExtractText(req.Content, req.DocumentType);
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { error = "Could not extract text from document" });

            var result = await svc.AnalyzeAsync(req.SubjectId, text, req.Kind);
            return Results.Ok(new
            {
                result.StimuliId,
                result.AnalysesStored,
                result.LinesTotal,
            });
        });

        // Get analysis history for a person
        group.MapGet("/{personId:guid}", async (Guid personId, IUserContext ctx,
            ISubjectRepository subjects, IAnalysisRepository analyses) =>
        {
            if (!await subjects.HasAccessAsync(personId, ctx.UserId))
                return Results.Forbid();

            var list = await analyses.GetByPersonAsync(personId);
            return Results.Ok(new
            {
                count = list.Count,
                analyses = list.Select(p => new
                {
                    p.Id, p.Tag, p.Formula, p.Status, p.Phase,
                    createdAt = p.CreatedOnUtc,
                }),
            });
        });

        return group;
    }
}

public record AnalyzeRequest(Guid SubjectId, string Text, string Kind);
public record AnalyzeDocumentRequest(Guid SubjectId, string Content, string DocumentType, string Kind);
