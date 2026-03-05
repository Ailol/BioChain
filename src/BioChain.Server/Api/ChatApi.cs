using BioChain.Repository;
using BioChain.Repository.Repositories;
using BioChain.Service;
using Microsoft.Extensions.AI;

namespace BioChain.Server.Api;

public static class ChatApi
{
    public static RouteGroupBuilder MapChatApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        // Chat about a person's biochemical profile
        group.MapPost("/", async (ChatRequest req, BioChainChatService svc,
            IUserContext ctx, ISubjectRepository subjects) =>
        {
            if (!await subjects.HasAccessAsync(req.SubjectId, ctx.UserId))
                return Results.Forbid();

            // Convert history DTOs to ChatMessage
            List<ChatMessage>? history = null;
            if (req.History is { Count: > 0 })
            {
                history = req.History.Select(h =>
                    new ChatMessage(h.Role == "assistant" ? ChatRole.Assistant : ChatRole.User, h.Content)
                ).ToList();
            }

            var response = await svc.ChatAsync(req.SubjectId, req.Message, history);
            return Results.Ok(new ChatApiResponse(response.Text ?? ""));
        });

        return group;
    }
}

public record ChatRequest(Guid SubjectId, string Message, List<ChatHistoryItem>? History = null);
public record ChatHistoryItem(string Role, string Content);
public record ChatApiResponse(string Response);
