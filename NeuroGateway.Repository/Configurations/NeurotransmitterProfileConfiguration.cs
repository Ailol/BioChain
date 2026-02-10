using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository.Configurations;

public class NeurotransmitterProfileConfiguration : IEntityTypeConfiguration<NeurotransmitterProfile>
{
    public void Configure(EntityTypeBuilder<NeurotransmitterProfile> builder)
    {
        builder.ToTable("neurotransmitter_profile");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Personality)
            .WithMany(p => p.NeurotransmitterProfiles)
            .HasForeignKey(e => e.PersonalityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Neurotransmitter)
            .WithMany(nt => nt.NeurotransmitterProfiles)
            .HasForeignKey(e => e.NeurotransmitterId)
            .IsRequired();

        builder.HasOne(e => e.AnalyzedData)
            .WithMany(ad => ad.NeurotransmitterProfiles)
            .HasForeignKey(e => e.AnalyzedDataId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(e => e.ReasoningEmbedding)
            .HasColumnType("vector(4096)");

        builder.Property(e => e.IsClusterRepresentative)
            .HasDefaultValue(false);

        // Unique per (personality, chemical, analyzed input)
        builder.HasIndex(e => new { e.PersonalityId, e.NeurotransmitterId, e.AnalyzedDataId })
            .IsUnique();

        builder.HasIndex(e => e.PersonalityId);
        builder.HasIndex(e => e.AnalyzedDataId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
