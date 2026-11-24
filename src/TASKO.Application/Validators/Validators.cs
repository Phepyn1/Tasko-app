using FluentValidation;
using Tasko.Application.Contracts;
using Tasko.Domain;

namespace Tasko.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 120).WithMessage("Nome deve ter entre 2 e 120 caracteres.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(180);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha deve ter ao menos 8 caracteres.")
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("A senha deve conter ao menos uma letra.")
            .Matches("[0-9]").WithMessage("A senha deve conter ao menos um número.");
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Informe ao menos um papel: solicitante ou prestador.");
        RuleForEach(x => x.Roles).IsInEnum();
        RuleFor(x => x.Roles)
            .Must(roles => roles is null || !roles.Contains(RoleType.Moderator))
            .WithMessage("O papel de moderador não pode ser solicitado no cadastro.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha deve ter ao menos 8 caracteres.")
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("A senha deve conter ao menos uma letra.")
            .Matches("[0-9]").WithMessage("A senha deve conter ao menos um número.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Presentation).MaximumLength(500);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.City).MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Neighborhood).MaximumLength(120);
        RuleFor(x => x.ServiceRegion).MaximumLength(200);
        RuleFor(x => x.ProfilePhotoUrl).MaximumLength(500);
    }
}

public sealed class UpdateSkillsRequestValidator : AbstractValidator<UpdateSkillsRequest>
{
    public UpdateSkillsRequestValidator()
    {
        RuleFor(x => x.Skills).NotNull();
        RuleForEach(x => x.Skills).ChildRules(skill =>
        {
            skill.RuleFor(s => s.CategoryId).NotEmpty();
            skill.RuleFor(s => s.SkillName).NotEmpty().Length(2, 80);
        });
    }
}

public sealed class CreatePublicationRequestValidator : AbstractValidator<CreatePublicationRequest>
{
    public CreatePublicationRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().Length(5, 150);
        RuleFor(x => x.Description).NotEmpty().Length(20, 4000);
        RuleFor(x => x.City).NotEmpty().Length(2, 120);
        RuleFor(x => x.ServiceRegion).NotEmpty().Length(2, 120);
        RuleFor(x => x.Neighborhood).MaximumLength(120);
        RuleFor(x => x.BudgetMin).GreaterThanOrEqualTo(0).When(x => x.BudgetMin.HasValue);
        RuleFor(x => x.BudgetMax).GreaterThanOrEqualTo(0).When(x => x.BudgetMax.HasValue);
        RuleFor(x => x.OfferedPrice).GreaterThanOrEqualTo(0).When(x => x.OfferedPrice.HasValue);
        RuleFor(x => x)
            .Must(x => !x.BudgetMin.HasValue || !x.BudgetMax.HasValue || x.BudgetMin <= x.BudgetMax)
            .WithMessage("Orçamento mínimo não pode ser maior que o máximo.");

        When(x => x.Type == PublicationType.Request, () =>
        {
            RuleFor(x => x.Deadline).NotNull().WithMessage("Solicitações de demanda exigem prazo.");
            RuleFor(x => x.Deadline).GreaterThan(DateTimeOffset.UtcNow).WithMessage("O prazo deve ser futuro.");
            RuleFor(x => x.OfferedPrice).Null().WithMessage("Solicitações usam orçamento, não preço ofertado.");
        });

        When(x => x.Type == PublicationType.Offer, () =>
        {
            RuleFor(x => x.BudgetMin).Null().WithMessage("Ofertas de serviço usam preço ofertado.");
            RuleFor(x => x.BudgetMax).Null().WithMessage("Ofertas de serviço usam preço ofertado.");
        });
    }
}

public sealed class UpdatePublicationRequestValidator : AbstractValidator<UpdatePublicationRequest>
{
    public UpdatePublicationRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().Length(5, 150);
        RuleFor(x => x.Description).NotEmpty().Length(20, 4000);
        RuleFor(x => x.City).NotEmpty().Length(2, 120);
        RuleFor(x => x.ServiceRegion).NotEmpty().Length(2, 120);
        RuleFor(x => x.Neighborhood).MaximumLength(120);
        RuleFor(x => x.BudgetMin).GreaterThanOrEqualTo(0).When(x => x.BudgetMin.HasValue);
        RuleFor(x => x.BudgetMax).GreaterThanOrEqualTo(0).When(x => x.BudgetMax.HasValue);
        RuleFor(x => x.OfferedPrice).GreaterThanOrEqualTo(0).When(x => x.OfferedPrice.HasValue);
        RuleFor(x => x)
            .Must(x => !x.BudgetMin.HasValue || !x.BudgetMax.HasValue || x.BudgetMin <= x.BudgetMax)
            .WithMessage("Orçamento mínimo não pode ser maior que o máximo.");
    }
}

public sealed class CreateProposalRequestValidator : AbstractValidator<CreateProposalRequest>
{
    public CreateProposalRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().Length(5, 2000);
        RuleFor(x => x.ProposedPrice).GreaterThanOrEqualTo(0).When(x => x.ProposedPrice.HasValue);
    }
}

public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.ReviewedUserId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public sealed class CreateReportRequestValidator : AbstractValidator<CreateReportRequest>
{
    public CreateReportRequestValidator()
    {
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x)
            .Must(x => x.ReportedUserId.HasValue || x.PublicationId.HasValue)
            .WithMessage("Informe o perfil ou a publicação denunciada.");
    }
}

public sealed class ResolveReportRequestValidator : AbstractValidator<ResolveReportRequest>
{
    public ResolveReportRequestValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
