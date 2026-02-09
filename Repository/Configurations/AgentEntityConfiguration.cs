using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class AgentEntityConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("agent");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Group)
            .WithMany(g => g.Agents)
            .HasForeignKey(e => e.GroupId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Role)
            .IsRequired()
            .HasMaxLength(100);

        // TEXT[] mapped natively by Npgsql EF Core
        builder.Property(e => e.Responsibilities)
            .IsRequired();

        builder.Property(e => e.Style)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.MaxWords)
            .HasDefaultValue(200);

        builder.Property(e => e.IsSynthesizer)
            .HasDefaultValue(false);

        builder.Property(e => e.SortOrder)
            .HasDefaultValue(0);

        // Composite unique constraint
        builder.HasIndex(e => new { e.GroupId, e.Name })
            .IsUnique();

        builder.HasIndex(e => e.GroupId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
