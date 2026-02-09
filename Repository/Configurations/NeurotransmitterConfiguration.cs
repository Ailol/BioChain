using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class NeurotransmitterConfiguration : IEntityTypeConfiguration<Neurotransmitter>
{
    public void Configure(EntityTypeBuilder<Neurotransmitter> builder)
    {
        builder.ToTable("neurotransmitter");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.Name).IsUnique();
    }
}
