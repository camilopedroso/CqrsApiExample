using CqrsApiExample.Domain.UserAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CqrsApiExample.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", Schemas.Accounts);

        // Ids are Guid v7, time-ordered in .NET but not in SQL Server, which sorts uniqueidentifier
        // by its last bytes. A clustered key on them would split pages on every insert, so the key
        // is non-clustered and rows are clustered by creation time (also the list order).
        builder.HasKey(u => u.Id).IsClustered(false);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.HasIndex(u => u.CreatedAt).IsClustered();

        builder.Property(u => u.Name)
               .HasMaxLength(User.NameMaxLength)
               .IsRequired();

        builder.Property(u => u.Surname)
               .HasMaxLength(User.SurnameMaxLength)
               .IsRequired();

        builder.Property(u => u.Email)
               .HasMaxLength(User.EmailMaxLength)
               .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
