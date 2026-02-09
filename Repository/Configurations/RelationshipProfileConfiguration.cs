using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class RelationshipProfileConfiguration : IEntityTypeConfiguration<RelationshipProfile>
{
    public void Configure(EntityTypeBuilder<RelationshipProfile> builder)
    {
        builder.ToTable("relationship_profile");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        // No cascade — don't lose profiles on person delete
        builder.HasOne(e => e.Person)
            .WithMany(p => p.RelationshipProfiles)
            .HasForeignKey(e => e.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RelationshipType)
            .WithMany(rt => rt.RelationshipProfiles)
            .HasForeignKey(e => e.RelationshipTypeId)
            .IsRequired();

        builder.Property(e => e.CompatibilityVector)
            .HasColumnType("vector(4096)");

        // Composite unique constraint
        builder.HasIndex(e => new { e.PersonId, e.RelationshipTypeId })
            .IsUnique();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
