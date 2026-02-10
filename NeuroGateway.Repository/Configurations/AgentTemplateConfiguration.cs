using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository.Configurations;

public class AgentTemplateConfiguration : IEntityTypeConfiguration<AgentTemplate>
{
    public void Configure(EntityTypeBuilder<AgentTemplate> builder)
    {
        builder.ToTable("agent_template");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.Category)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.GroupName)
            .HasMaxLength(100);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Layer)
            .HasMaxLength(50);

        builder.Property(e => e.Role)
            .IsRequired()
            .HasMaxLength(200);

        // TEXT[] mapped natively by Npgsql EF Core
        builder.Property(e => e.Responsibilities);

        // TEXT (no max length — PhD-level prompts are long)
        builder.Property(e => e.Style)
            .IsRequired();

        builder.Property(e => e.MaxWords)
            .HasDefaultValue(200);

        builder.Property(e => e.IsSynthesizer)
            .HasDefaultValue(false);

        builder.Property(e => e.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(e => new { e.Category, e.GroupName, e.Name })
            .IsUnique();

        builder.HasIndex(e => e.Category);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
