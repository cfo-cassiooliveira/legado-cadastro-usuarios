using Legado.Infrastructure.Persistence;

namespace Legado.Infrastructure.Seeding;

/// <summary>Usuários fictícios de demonstração. Endereços ilustrativos (logradouros reais públicos), nomes inventados.</summary>
internal static class DemoUsers
{
    /// <summary>Login do administrador de demonstração.</summary>
    public const string AdminUserName = "admin";

    /// <summary>Cria a lista de usuários fictícios (o primeiro é o administrador).</summary>
    public static IReadOnlyList<ApplicationUser> Create() =>
    [
        User(AdminUserName, "Administrador Legado", "admin@legado.example", "+5516991000000",
            Address("01001000", "Praça da Sé", "1", "Sé", "São Paulo", 3550308, "SP")),
        User("ana.souza", "Ana Paula Souza", "ana.souza@legado.example", "+5511991110001",
            Address("01310100", "Avenida Paulista", "1000", "Bela Vista", "São Paulo", 3550308, "SP")),
        User("bruno.lima", "Bruno Henrique Lima", "bruno.lima@legado.example", "+5521992220002",
            Address("22070011", "Avenida Atlântica", "1702", "Copacabana", "Rio de Janeiro", 3304557, "RJ")),
        User("carla.mendes", "Carla Mendes", "carla.mendes@legado.example", "+5531993330003",
            Address("30130010", "Avenida Afonso Pena", "1212", "Centro", "Belo Horizonte", 3106200, "MG")),
        User("diego.rocha", "Diego Rocha Alves", "diego.rocha@legado.example", "+5541994440004",
            Address("80010000", "Rua XV de Novembro", "300", "Centro", "Curitiba", 4106902, "PR")),
        User("elisa.martins", "Elisa Martins", "elisa.martins@legado.example", "+5516995550005",
            Address("14010000", "Rua General Osório", "45", "Centro", "Ribeirão Preto", 3543402, "SP")),
        User("fabio.nunes", "Fábio Nunes", "fabio.nunes@legado.example", "+5551996660006",
            Address("90010150", "Rua dos Andradas", "1001", "Centro Histórico", "Porto Alegre", 4314902, "RS")),
        User("gabriela.dias", "Gabriela Dias Costa", "gabriela.dias@legado.example", "+5585997770007",
            Address("60060000", "Rua Pedro Pereira", "88", "Centro", "Fortaleza", 2304400, "CE")),
        User("henrique.melo", "Henrique Melo", "henrique.melo@legado.example", "+5581998880008",
            Address("50030230", "Avenida Rio Branco", "15", "Recife", "Recife", 2611606, "PE")),
        User("isabela.ferraz", "Isabela Ferraz", "isabela.ferraz@legado.example", "+5561999990009",
            Address("70040010", "SBN Quadra 1", "10", "Asa Norte", "Brasília", 5300108, "DF")),
        User("joao.pereira", "João Pedro Pereira", "joao.pereira@legado.example", "+5571991230010",
            Address("40020000", "Avenida Sete de Setembro", "500", "Centro", "Salvador", 2927408, "BA")),
    ];

    private static ApplicationUser User(string userName, string fullName, string email, string phone, UserAddress address) => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = userName,
        FullName = fullName,
        Email = email,
        EmailConfirmed = true,
        PhoneNumber = phone,
        Address = address,
    };

    private static UserAddress Address(string postalCode, string street, string number, string district, string city, int ibgeCode, string state) => new()
    {
        PostalCode = postalCode,
        Street = street,
        Number = number,
        District = district,
        City = city,
        CityIbgeCode = ibgeCode,
        State = state,
    };
}
