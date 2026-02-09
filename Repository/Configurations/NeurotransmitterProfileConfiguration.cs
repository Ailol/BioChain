using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

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

        builder.HasIndex(e => new { e.PersonalityId, e.NeurotransmitterId })
            .IsUnique();

        builder.HasIndex(e => e.PersonalityId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
