using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class HormoneConfiguration : IEntityTypeConfiguration<Hormone>
{
    public void Configure(EntityTypeBuilder<Hormone> builder)
    {
        builder.ToTable("hormone");

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
