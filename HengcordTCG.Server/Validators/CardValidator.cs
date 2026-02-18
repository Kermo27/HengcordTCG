using FluentValidation;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.Validators;

public class CardValidator : AbstractValidator<Card>
{
    public CardValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Card name is required")
            .MaximumLength(100).WithMessage("Card name cannot exceed 100 characters");

        RuleFor(c => c.Attack)
            .InclusiveBetween(0, 100).WithMessage("Attack must be between 0 and 100");

        RuleFor(c => c.Defense)
            .InclusiveBetween(0, 100).WithMessage("Defense must be between 0 and 100");

        RuleFor(c => c.Health)
            .InclusiveBetween(0, 999).WithMessage("Health must be between 0 and 999");

        RuleFor(c => c.LightCost)
            .InclusiveBetween(0, 7).WithMessage("Light cost must be between 0 and 7");

        RuleFor(c => c.Speed)
            .InclusiveBetween(0, 20).WithMessage("Speed must be between 0 and 20");

        RuleFor(c => c.MinDamage)
            .InclusiveBetween(0, 20).WithMessage("Min damage must be between 0 and 20");

        RuleFor(c => c.MaxDamage)
            .InclusiveBetween(0, 20).WithMessage("Max damage must be between 0 and 20");

        RuleFor(c => c)
            .Must(c => c.MinDamage <= c.MaxDamage)
            .WithMessage("Min damage cannot be greater than max damage");

        RuleFor(c => c.CounterStrike)
            .InclusiveBetween(0, 50).WithMessage("Counter strike must be between 0 and 50");

        RuleFor(c => c.ImagePath)
            .MaximumLength(255).WithMessage("Image path cannot exceed 255 characters")
            .When(c => !string.IsNullOrEmpty(c.ImagePath));

        RuleFor(c => c.AbilityText)
            .MaximumLength(500).WithMessage("Ability text cannot exceed 500 characters")
            .When(c => !string.IsNullOrEmpty(c.AbilityText));

        RuleFor(c => c.AbilityId)
            .MaximumLength(100).WithMessage("Ability ID cannot exceed 100 characters")
            .When(c => !string.IsNullOrEmpty(c.AbilityId));

        RuleFor(c => c.CardType)
            .IsInEnum().WithMessage("Invalid card type");

        RuleFor(c => c.Rarity)
            .IsInEnum().WithMessage("Invalid rarity");
    }
}
