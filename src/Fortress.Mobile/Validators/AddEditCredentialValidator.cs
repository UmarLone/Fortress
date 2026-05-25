using FluentValidation;
using Fortress.ViewModels;

namespace Fortress.Validators
{
    /// <summary>Validates the AddEditCredentialPage form fields.</summary>
    public class AddEditCredentialValidator : AbstractValidator<AddEditCredentialPageViewModel>
    {
        public AddEditCredentialValidator()
        {
            RuleFor(x => x.FormDomain)
                 .NotEmpty()
            .WithMessage("Website or app name is required");

            RuleFor(x => x.FormUsername)
                .NotEmpty()
                .WithMessage("Username or email is required");

            // Password only required in add mode – edit leaves it blank to keep existing
            RuleFor(x => x.FormPassword)
                .NotEmpty()
        .When(x => !x.IsEditMode)
         .WithMessage("Password is required");

            RuleFor(x => x.FormOtpSecret)
   .NotEmpty()
          .When(x => x.FormHasOtp)
           .WithMessage("OTP secret is required when 2FA is enabled");
        }
    }
}
