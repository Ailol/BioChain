using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class PersonalityConfiguration : IEntityTypeConfiguration<Personality>
{
    public void Configure(EntityTypeBuilder<Personality> builder)
    {
        builder.ToTable("personality");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Person)
            .WithMany(p => p.Personalities)
            .HasForeignKey(e => e.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Topic)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Embedding)
            .HasColumnType("vector(4096)");

        // Composite unique constraint — one row per (person, topic)
        builder.HasIndex(e => new { e.PersonId, e.Topic })
            .IsUnique();

        builder.HasIndex(e => e.PersonId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
