using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class HormoneProfileConfiguration : IEntityTypeConfiguration<HormoneProfile>
{
    public void Configure(EntityTypeBuilder<HormoneProfile> builder)
    {
        builder.ToTable("hormone_profile");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Personality)
            .WithMany(p => p.HormoneProfiles)
            .HasForeignKey(e => e.PersonalityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Hormone)
            .WithMany(h => h.HormoneProfiles)
            .HasForeignKey(e => e.HormoneId)
            .IsRequired();

        builder.HasIndex(e => new { e.PersonalityId, e.HormoneId })
            .IsUnique();

        builder.HasIndex(e => e.PersonalityId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
