using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository.Configurations;

public class AnalyzedDataConfiguration : IEntityTypeConfiguration<AnalyzedData>
{
    public void Configure(EntityTypeBuilder<AnalyzedData> builder)
    {
        builder.ToTable("analyzed_data");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Person)
            .WithMany(p => p.AnalyzedData)
            .HasForeignKey(e => e.PersonId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Content)
            .IsRequired();

        builder.Property(e => e.SourceType)
            .HasMaxLength(30);

        builder.Property(e => e.Embedding)
            .HasColumnType("vector(4096)");

        builder.HasIndex(e => e.PersonId);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
