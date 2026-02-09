using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class PeptideConfiguration : IEntityTypeConfiguration<Peptide>
{
    public void Configure(EntityTypeBuilder<Peptide> builder)
    {
        builder.ToTable("peptide");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.Name).IsUnique();

        builder.Property(e => e.Embedding)
            .HasColumnType("vector(4096)");
    }
}
