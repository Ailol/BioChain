using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class PeptideProfileConfiguration : IEntityTypeConfiguration<PeptideProfile>
{
    public void Configure(EntityTypeBuilder<PeptideProfile> builder)
    {
        builder.ToTable("peptide_profile");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Personality)
            .WithMany(p => p.PeptideProfiles)
            .HasForeignKey(e => e.PersonalityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Peptide)
            .WithMany(p => p.PeptideProfiles)
            .HasForeignKey(e => e.PeptideId)
            .IsRequired();

        builder.HasIndex(e => new { e.PersonalityId, e.PeptideId })
            .IsUnique();

        builder.HasIndex(e => e.PersonalityId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
