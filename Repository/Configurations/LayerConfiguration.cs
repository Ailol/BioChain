using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Repository.Entities;

namespace Repository.Configurations;

public class LayerConfiguration : IEntityTypeConfiguration<Layer>
{
    public void Configure(EntityTypeBuilder<Layer> builder)
    {
        builder.ToTable("layer");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .UseIdentityByDefaultColumn();

        builder.HasOne(e => e.Pipeline)
            .WithMany(p => p.Layers)
            .HasForeignKey(e => e.PipelineId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Agent)
            .WithMany()
            .HasForeignKey(e => e.AgentId)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsSynthesizer)
            .HasDefaultValue(false);

        builder.Property(e => e.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(e => new { e.PipelineId, e.SortOrder })
            .IsUnique();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
