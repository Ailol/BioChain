using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository.Configurations;

public class PersonalityConfiguration : IEntityTypeConfiguration<Personality>
{
    public void Configure(EntityTypeBuilder<Personality> builder)
    {
        builder.ToTable("personality");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Person)
            .WithOne(p => p.Personality)
            .HasForeignKey<Personality>(e => e.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // 1:1 with person — unique on person_id
        builder.HasIndex(e => e.PersonId)
            .IsUnique();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
