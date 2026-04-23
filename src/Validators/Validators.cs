using FluentValidation;
using InsureZen.Models.DTOs;

namespace InsureZen.Validators;

public class ClaimCreateRequestValidator : AbstractValidator<ClaimCreateRequest>
{
    public ClaimCreateRequestValidator()
    {
        RuleFor(x => x.InsuranceCompany).NotEmpty().WithMessage("insuranceCompany is required.");
        RuleFor(x => x.StandardizedData).NotNull().WithMessage("standardizedData is required.");
    }
}

public class MakerAssignRequestValidator : AbstractValidator<MakerAssignRequest>
{
    public MakerAssignRequestValidator()
    {
        RuleFor(x => x.MakerId).NotEmpty().WithMessage("makerId is required.");
    }
}

public class MakerReviewRequestValidator : AbstractValidator<MakerReviewRequest>
{
    private static readonly string[] ValidValues = ["APPROVE", "REJECT"];

    public MakerReviewRequestValidator()
    {
        RuleFor(x => x.Recommendation)
            .NotEmpty()
            .Must(v => ValidValues.Contains(v?.ToUpper()))
            .WithMessage("recommendation must be APPROVE or REJECT.");
    }
}

public class CheckerAssignRequestValidator : AbstractValidator<CheckerAssignRequest>
{
    public CheckerAssignRequestValidator()
    {
        RuleFor(x => x.CheckerId).NotEmpty().WithMessage("checkerId is required.");
    }
}

public class CheckerReviewRequestValidator : AbstractValidator<CheckerReviewRequest>
{
    private static readonly string[] ValidValues = ["APPROVE", "REJECT"];

    public CheckerReviewRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty()
            .Must(v => ValidValues.Contains(v?.ToUpper()))
            .WithMessage("decision must be APPROVE or REJECT.");
    }
}