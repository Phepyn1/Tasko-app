using Microsoft.EntityFrameworkCore;
using Tasko.Application.Abstractions;
using Tasko.Domain;
using Tasko.Domain.Entities;
using Tasko.Infrastructure.Persistence;

namespace Tasko.Api.Seed;

/// <summary>
/// Dados de demonstração usados pelo perfil <c>demo</c> (provider InMemory).
/// Idempotente: se a conta marcadora já existe, nada é feito.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Senha única das contas de demonstração.</summary>
    public const string DemoPassword = "Demo1234";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed.Demo");
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        const string markerEmail = "ana@tasko.demo";
        if (await db.Users.AnyAsync(u => u.Email == markerEmail))
        {
            logger.LogInformation("Dados de demonstração já presentes.");
            return;
        }

        var now = clock.UtcNow;
        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);
        if (categories.Count == 0)
        {
            logger.LogWarning("Categorias ausentes: dados de demonstração não foram semeados.");
            return;
        }

        Guid Category(string name) => categories.TryGetValue(name, out var id)
            ? id
            : throw new InvalidOperationException($"Categoria de demonstração ausente: {name}.");

        // ------------------------------------------------------------------ usuários
        User NewUser(
            string name,
            string email,
            string presentation,
            string phone,
            string city,
            string neighborhood,
            string serviceRegion,
            string description,
            params RoleType[] roles)
        {
            var user = User.Create(name, email, hasher.Hash(DemoPassword), now);
            foreach (var role in roles)
            {
                user.AddRole(role, now);
            }

            user.EnsureProfile(now).Update(
                presentation, phone, city, description, neighborhood, serviceRegion, null, now);
            user.MarkEmailVerified(now);
            db.Users.Add(user);
            return user;
        }

        var ana = NewUser(
            "Ana Souza",
            "ana@tasko.demo",
            "Busco profissionais de confiança para pequenos reparos e organização da casa.",
            "11987650001",
            "São Paulo",
            "Vila Mariana",
            "Zona Sul",
            "Moro em apartamento na Vila Mariana e prefiro combinar horários pela manhã. Tenho duas crianças e um cachorro muito dócil.",
            RoleType.Requester);

        var carlos = NewUser(
            "Carlos Pereira",
            "carlos@tasko.demo",
            "Eletricista predial e residencial com 12 anos de experiência.",
            "11987650002",
            "São Paulo",
            "Moema",
            "Zona Sul e Centro",
            "Atendo emergências, instalações, quadros de distribuição e automação residencial. Emissão de nota fiscal e garantia de 90 dias.",
            RoleType.Provider);

        var mariana = NewUser(
            "Mariana Lima",
            "mariana@tasko.demo",
            "Diarista e organizadora profissional, referências verificadas.",
            "11987650003",
            "São Paulo",
            "Pinheiros",
            "Zona Oeste",
            "Trabalho com limpeza residencial, pós-obra e organização de armários. Levo meus próprios produtos e equipamentos.",
            RoleType.Provider);

        var joao = NewUser(
            "João Alves",
            "joao@tasko.demo",
            "Moro em Campinas e preciso de ajuda com montagem de móveis e reparos.",
            "19987650004",
            "Campinas",
            "Cambuí",
            "Campinas e região",
            "Acabei de me mudar e estou montando o apartamento aos poucos. Prefiro atendimento aos sábados pela manhã.",
            RoleType.Requester);

        var rafael = NewUser(
            "Rafael Nunes",
            "rafael@tasko.demo",
            "Pintor profissional com foco em acabamento fino e ambientes claros.",
            "11987650005",
            "São Paulo",
            "Tatuapé",
            "Zona Leste",
            "Executo pintura interna e externa, textura, grafiato e pequenos reparos de alvenaria. Orçamento sem compromisso.",
            RoleType.Provider);

        carlos.ReplaceSkills(
        [
            (Category("Elétrica"), "Instalação elétrica residencial"),
            (Category("Montagem de Móveis"), "Montagem de móveis")
        ], now);

        mariana.ReplaceSkills(
        [
            (Category("Limpeza"), "Limpeza residencial e pós-obra"),
            (Category("Jardinagem"), "Manutenção de jardim")
        ], now);

        rafael.ReplaceSkills(
        [
            (Category("Pintura"), "Pintura residencial"),
            (Category("Construção e Reformas"), "Pequenos reparos")
        ], now);

        // ------------------------------------------------------------------ publicações
        Publication NewPublication(
            Guid authorId,
            PublicationType type,
            string categoryName,
            string title,
            string description,
            string city,
            string? neighborhood,
            string serviceRegion,
            decimal? offeredPrice,
            decimal? budgetMin,
            decimal? budgetMax,
            DateTimeOffset? deadline,
            DateTimeOffset createdAt)
        {
            var input = new PublicationInput(
                type,
                Category(categoryName),
                title,
                description,
                city,
                neighborhood,
                serviceRegion,
                budgetMin,
                budgetMax,
                offeredPrice,
                deadline);

            var publication = Publication.Create(authorId, input, createdAt);
            db.Publications.Add(publication);
            return publication;
        }

        void History(
            Publication publication,
            Guid? changedBy,
            PublicationStatus from,
            PublicationStatus to,
            string note,
            DateTimeOffset at) =>
            db.PublicationStatusHistories.Add(
                PublicationStatusHistory.Create(publication.Id, changedBy, from, to, note, at));

        Proposal NewProposal(Publication publication, User proposer, string message, decimal? price, DateTimeOffset at)
        {
            var proposal = Proposal.Create(publication.Id, proposer.Id, message, price, at);
            db.Proposals.Add(proposal);
            return proposal;
        }

        Engagement Accept(Publication publication, Proposal proposal, DateTimeOffset at)
        {
            proposal.Accept(at);
            publication.StartNegotiation(at);
            History(publication, publication.AuthorId, PublicationStatus.Open, PublicationStatus.InNegotiation, "Proposta aceita", at);
            var engagement = Engagement.Create(publication, proposal, at);
            db.Engagements.Add(engagement);
            return engagement;
        }

        void Notify(
            User user,
            NotificationType type,
            string title,
            string message,
            string? referenceType,
            Guid? referenceId,
            DateTimeOffset at,
            bool read = false)
        {
            var notification = Notification.Create(user.Id, type, title, message, referenceType, referenceId, at);
            if (read)
            {
                notification.MarkAsRead(at);
            }

            db.Notifications.Add(notification);
        }

        // Oferta aberta do Carlos, com proposta pendente da Ana.
        var offerCarlos = NewPublication(
            carlos.Id,
            PublicationType.Offer,
            "Elétrica",
            "Instalação elétrica residencial e reparos",
            "Instalação de tomadas, interruptores, luminárias e revisão de quadro de distribuição. Atendo apartamentos e casas na Zona Sul com material de primeira linha e garantia.",
            "São Paulo",
            "Vila Mariana",
            "Zona Sul",
            180m,
            null,
            null,
            null,
            now.AddDays(-3));

        NewProposal(
            offerCarlos,
            ana,
            "Preciso trocar duas tomadas da sala e instalar um lustre. Consegue atender sábado pela manhã?",
            160m,
            now.AddDays(-1));

        Notify(
            carlos,
            NotificationType.ProposalReceived,
            "Nova proposta recebida",
            "Ana Souza enviou uma proposta para \"Instalação elétrica residencial e reparos\".",
            "publication",
            offerCarlos.Id,
            now.AddDays(-1));

        // Oferta aberta da Mariana, com proposta pendente do João.
        var offerMariana = NewPublication(
            mariana.Id,
            PublicationType.Offer,
            "Limpeza",
            "Diarista com experiência em limpeza pesada",
            "Limpeza completa de apartamento de 2 a 3 quartos, incluindo banheiros, cozinha e áreas comuns. Também faço pós-obra e organização de armários.",
            "São Paulo",
            "Pinheiros",
            "Zona Oeste",
            150m,
            null,
            null,
            null,
            now.AddDays(-5));

        NewProposal(
            offerMariana,
            joao,
            "Preciso de uma limpeza completa antes da mudança. O apartamento tem 75 m² e estará vazio.",
            140m,
            now.AddDays(-2));

        // Demanda aberta da Ana, com duas propostas pendentes.
        var requestAna = NewPublication(
            ana.Id,
            PublicationType.Request,
            "Elétrica",
            "Preciso de eletricista para trocar disjuntor",
            "O disjuntor da cozinha está desarmando com frequência. Preciso de diagnóstico e troca do componente, além de revisão da fiação antiga do imóvel.",
            "São Paulo",
            "Vila Mariana",
            "Zona Sul",
            null,
            100m,
            300m,
            now.AddDays(10),
            now.AddDays(-2));

        NewProposal(
            requestAna,
            carlos,
            "Faço o diagnóstico completo e a troca do disjuntor com material incluso. Posso atender amanhã às 9h.",
            220m,
            now.AddDays(-1));

        NewProposal(
            requestAna,
            rafael,
            "Tenho experiência com quadros antigos e posso levar um disjuntor reserva. Atendo no mesmo dia.",
            190m,
            now.AddHours(-20));

        Notify(
            ana,
            NotificationType.ProposalReceived,
            "Nova proposta recebida",
            "Carlos Pereira enviou uma proposta para \"Preciso de eletricista para trocar disjuntor\".",
            "publication",
            requestAna.Id,
            now.AddDays(-1));

        Notify(
            ana,
            NotificationType.ProposalReceived,
            "Nova proposta recebida",
            "Rafael Nunes enviou uma proposta para \"Preciso de eletricista para trocar disjuntor\".",
            "publication",
            requestAna.Id,
            now.AddHours(-20));

        // Oferta aberta do Rafael (vai receber uma denúncia de exemplo).
        var offerRafael = NewPublication(
            rafael.Id,
            PublicationType.Offer,
            "Pintura",
            "Pintura de apartamento e pequenos reparos",
            "Pintura interna de apartamentos com preparação de parede, massa corrida e duas demãos de tinta acrílica. Também executo pequenos reparos de alvenaria.",
            "São Paulo",
            "Tatuapé",
            "Zona Leste",
            900m,
            null,
            null,
            null,
            now.AddDays(-6));

        // Demanda aberta do João, com proposta pendente do Carlos.
        var requestJoao = NewPublication(
            joao.Id,
            PublicationType.Request,
            "Montagem de Móveis",
            "Montagem de guarda-roupa e estante",
            "Comprei um guarda-roupa de 6 portas e uma estante de parede. Preciso de montagem e fixação segura na parede do quarto.",
            "Campinas",
            "Cambuí",
            "Campinas e região",
            null,
            120m,
            250m,
            now.AddDays(7),
            now.AddDays(-1));

        NewProposal(
            requestJoao,
            carlos,
            "Monto os dois móveis no sábado pela manhã e levo as ferramentas de fixação. O valor inclui a instalação na parede.",
            200m,
            now.AddHours(-12));

        // Serviço concluído: demanda da Ana executada pelo Carlos, com avaliações mútuas.
        var completedRequest = NewPublication(
            ana.Id,
            PublicationType.Request,
            "Elétrica",
            "Instalação de chuveiro elétrico",
            "Comprei um chuveiro elétrico de 7500 W e preciso da instalação com disjuntor dedicado. O ponto de energia já existe no banheiro.",
            "São Paulo",
            "Vila Mariana",
            "Zona Sul",
            null,
            150m,
            350m,
            now.AddDays(-15),
            now.AddDays(-20));

        var completedProposal = NewProposal(
            completedRequest,
            carlos,
            "Instalo o chuveiro e o disjuntor dedicado, com teste de aquecimento e revisão do ponto. Garantia de 90 dias.",
            280m,
            now.AddDays(-19));

        var completedEngagement = Accept(completedRequest, completedProposal, now.AddDays(-18));
        completedRequest.MarkAgreed(now.AddDays(-17));
        History(completedRequest, completedRequest.AuthorId, PublicationStatus.InNegotiation, PublicationStatus.Agreed, "Combinação confirmada", now.AddDays(-17));
        completedEngagement.Complete(now.AddDays(-16));
        completedRequest.Complete(now.AddDays(-16));
        History(completedRequest, completedRequest.AuthorId, PublicationStatus.Agreed, PublicationStatus.Completed, "Serviço concluído", now.AddDays(-16));

        db.Reviews.Add(Review.Create(
            completedRequest,
            completedEngagement,
            ana.Id,
            carlos.Id,
            5,
            "Serviço impecável. O Carlos explicou tudo, deixou o local limpo e resolveu uma fiação antiga que eu nem sabia que estava ruim.",
            now.AddDays(-15)));

        db.Reviews.Add(Review.Create(
            completedRequest,
            completedEngagement,
            carlos.Id,
            ana.Id,
            5,
            "Cliente muito atenciosa, local organizado e combinamos tudo pelo chat da plataforma. Recomendo.",
            now.AddDays(-15)));

        Notify(
            carlos,
            NotificationType.EngagementCompleted,
            "Serviço concluído",
            "A contratação de \"Instalação de chuveiro elétrico\" foi concluída. Avalie a experiência.",
            "engagement",
            completedEngagement.Id,
            now.AddDays(-16),
            read: true);

        Notify(
            carlos,
            NotificationType.ReviewReceived,
            "Nova avaliação recebida",
            "Ana Souza avaliou você com nota 5.",
            "user",
            carlos.Id,
            now.AddDays(-15));

        Notify(
            ana,
            NotificationType.ReviewReceived,
            "Nova avaliação recebida",
            "Carlos Pereira avaliou você com nota 5.",
            "user",
            ana.Id,
            now.AddDays(-15),
            read: true);

        // Oferta do Carlos em negociação com a Ana (contato já liberado).
        var negotiatingOffer = NewPublication(
            carlos.Id,
            PublicationType.Offer,
            "Elétrica",
            "Manutenção elétrica preventiva em apartamento",
            "Revisão completa do quadro elétrico, teste de tomadas e identificação de circuitos. Ideal para apartamentos com mais de 10 anos.",
            "São Paulo",
            "Moema",
            "Zona Sul",
            320m,
            null,
            null,
            null,
            now.AddDays(-4));

        var negotiatingProposal = NewProposal(
            negotiatingOffer,
            ana,
            "Quero fazer a manutenção preventiva antes das festas de fim de ano. Pode ser na sexta à tarde?",
            300m,
            now.AddDays(-3));

        var competingProposal = NewProposal(
            negotiatingOffer,
            rafael,
            "Também faço revisão elétrica e posso levar um eletricista parceiro para agilizar.",
            280m,
            now.AddDays(-3));

        var negotiatingEngagement = Accept(negotiatingOffer, negotiatingProposal, now.AddDays(-3));
        competingProposal.Reject(now.AddDays(-3));

        Notify(
            ana,
            NotificationType.ProposalAccepted,
            "Proposta aceita",
            "Sua proposta para \"Manutenção elétrica preventiva em apartamento\" foi aceita. O contato direto foi liberado.",
            "engagement",
            negotiatingEngagement.Id,
            now.AddDays(-3));

        // ------------------------------------------------------------------ moderação
        db.Reports.Add(Report.Create(
            ana.Id,
            null,
            offerRafael.Id,
            ReportReason.Spam,
            "O anúncio promete pintura completa por um valor muito abaixo do mercado e pede pagamento antecipado por fora da plataforma.",
            now.AddHours(-30)));

        db.Reports.Add(Report.Create(
            mariana.Id,
            joao.Id,
            null,
            ReportReason.Other,
            "Perfil sem foto e com informações incompletas; pediu para negociar fora da plataforma.",
            now.AddHours(-8)));

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Dados de demonstração semeados. Contas: ana@tasko.demo, carlos@tasko.demo, mariana@tasko.demo, joao@tasko.demo, rafael@tasko.demo (senha {Password}).",
            DemoPassword);
    }
}
