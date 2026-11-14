using Legado.Domain.Addresses;
using Legado.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legado.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento Fluent do usuário: tamanhos, índice único de e-mail e colunas de endereço.</summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    private const int PhoneMaxLength = 20;
    private const int StateLength = 2;

    /// <summary>Índice único (filtrado) do id do usuário no Keycloak.</summary>
    public const string KeycloakUserIdIndex = "KeycloakUserIdIndex";

    /// <summary>Aplica o mapeamento da entidade <see cref="ApplicationUser"/>.</summary>
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(FullName.MaxLength).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(PhoneMaxLength);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        // O Identity cria EmailIndex não único; tornamos único para garantir a regra mesmo sob concorrência.
        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        // Uma conta do Keycloak corresponde a no máximo uma conta do Legado (expand: colunas anuláveis).
        builder.HasIndex(u => u.KeycloakUserId)
            .HasDatabaseName(KeycloakUserIdIndex)
            .IsUnique()
            .HasFilter("[KeycloakUserId] IS NOT NULL");

        builder.ComplexProperty(u => u.Address, address =>
        {
            address.Property(a => a.PostalCode).HasColumnName("AddressPostalCode")
                .HasMaxLength(PostalCode.Length).IsFixedLength().IsUnicode(false).IsRequired();
            address.Property(a => a.Street).HasColumnName("AddressStreet").HasMaxLength(Address.StreetMaxLength).IsRequired();
            address.Property(a => a.Number).HasColumnName("AddressNumber").HasMaxLength(Address.NumberMaxLength).IsRequired();
            address.Property(a => a.Complement).HasColumnName("AddressComplement").HasMaxLength(Address.ComplementMaxLength);
            address.Property(a => a.District).HasColumnName("AddressDistrict").HasMaxLength(Address.DistrictMaxLength).IsRequired();
            address.Property(a => a.City).HasColumnName("AddressCity").HasMaxLength(Address.CityMaxLength).IsRequired();
            address.Property(a => a.CityIbgeCode).HasColumnName("AddressCityIbgeCode").IsRequired();
            address.Property(a => a.State).HasColumnName("AddressState")
                .HasMaxLength(StateLength).IsFixedLength().IsUnicode(false).IsRequired();
        });
    }
}
