using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> builder)
    {
        builder.ToTable("pipeline");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Person)
            .WithMany(p => p.Pipelines)
            .HasForeignKey(e => e.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.RelationshipType)
            .WithMany(rt => rt.Pipelines)
            .HasForeignKey(e => e.RelationshipTypeId)
            .IsRequired(false);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(e => new { e.PersonId, e.Name })
            .IsUnique();

        builder.HasIndex(e => e.PersonId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
