using Legado.Application.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legado.Infrastructure.Persistence.Configurations;

/// <summary>Perfis fixos do sistema, criados pela migration (ids estáveis para não gerar diffs).</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    /// <summary>Id fixo do perfil Admin.</summary>
    internal static readonly Guid AdminRoleId = new("0199a1b2-0000-7000-8000-000000000001");
    /// <summary>Id fixo do perfil User.</summary>
    internal static readonly Guid UserRoleId = new("0199a1b2-0000-7000-8000-000000000002");

    /// <summary>Insere os perfis Admin e User via dados de seed da migration.</summary>
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) =>
        builder.HasData(
            new IdentityRole<Guid>
            {
                Id = AdminRoleId,
                Name = Roles.Admin,
                NormalizedName = Roles.Admin.ToUpperInvariant(),
                ConcurrencyStamp = "8d3f6c1e-admin",
            },
            new IdentityRole<Guid>
            {
                Id = UserRoleId,
                Name = Roles.User,
                NormalizedName = Roles.User.ToUpperInvariant(),
                ConcurrencyStamp = "4b7e2a9d-user",
            });
}
