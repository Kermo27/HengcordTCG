using FluentValidation;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.Validators;

public class PackValidator : AbstractValidator<PackType>
{
    public PackValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Pack name is required")
            .MaximumLength(50).WithMessage("Pack name cannot exceed 50 characters");

        RuleFor(p => p.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be non-negative");

        RuleFor(p => p.ChanceCommon)
            .InclusiveBetween(0, 100).WithMessage("Common chance must be between 0 and 100");

        RuleFor(p => p.ChanceRare)
            .InclusiveBetween(0, 100).WithMessage("Rare chance must be between 0 and 100");

        RuleFor(p => p.ChanceLegendary)
            .InclusiveBetween(0, 100).WithMessage("Legendary chance must be between 0 and 100");

        RuleFor(p => p)
            .Must(p => p.ChanceCommon + p.ChanceRare + p.ChanceLegendary == 100)
            .WithMessage("Rarity chances must sum to 100");
    }
}
