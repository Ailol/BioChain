using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository.Configurations;

public class AgentGroupConfiguration : IEntityTypeConfiguration<AgentGroup>
{
    public void Configure(EntityTypeBuilder<AgentGroup> builder)
    {
        builder.ToTable("agent_group");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.HasOne(e => e.Person)
            .WithMany(p => p.AgentGroups)
            .HasForeignKey(e => e.PersonId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Partial unique indexes are defined in init.sql (WHERE person_id IS [NOT] NULL)
        // EF can't express partial indexes natively, so we skip HasIndex here

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
