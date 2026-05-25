using FluentValidation;
using Fortress.ViewModels;

namespace Fortress.Validators
{
    public class CreditCardValidator : AbstractValidator<AddEditCreditCardPageViewModel>
    {
        public CreditCardValidator()
        {
            RuleFor(x => x.CardName)
      .NotEmpty().WithMessage("Card label is required");

            RuleFor(x => x.CardHolder)
      .NotEmpty().WithMessage("Cardholder name is required");

            RuleFor(x => x.CardNumber)
            .NotEmpty().WithMessage("Card number is required")
                .Must((vm, n) =>
          {
              // Strip spaces/formatting before checking length
              var digits = new string((n ?? string.Empty).Where(char.IsDigit).ToArray());
              var expected = vm.CardNetwork == "Amex" ? 15 : 16;
              return digits.Length == expected;
          })
              .WithMessage(vm => $"{vm.CardNetwork} cards require {(vm.CardNetwork == "Amex" ? 15 : 16)} digits")
         .Must(n =>
               {
                   var digits = new string((n ?? string.Empty).Where(char.IsDigit).ToArray());
                   return PassesLuhn(digits);
               })
            .WithMessage("Card number is not valid")
              .When((vm, n) =>
              {
                  var digits = new string((vm.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
                  var expected = vm.CardNetwork == "Amex" ? 15 : 16;
                  return digits.Length == expected;
              }, ApplyConditionTo.CurrentValidator);

            // ExpiryMonth: must be 2 digits, value 01–12
            RuleFor(x => x.ExpiryMonth)
           .NotEmpty().WithMessage("Month required")
   .Length(2).WithMessage("Enter 2-digit month (MM)")
   .Must(m => int.TryParse(m, out var v) && v >= 1 && v <= 12)
    .WithMessage("Month must be 01–12");

            // ExpiryYear: must be 2 digits
            RuleFor(x => x.ExpiryYear)
         .NotEmpty().WithMessage("Year required")
        .Length(2).WithMessage("Enter 2-digit year (YY)")
   // Only check expiry when month is also valid – prevents confusing "expired" error
   .Must((vm, y) => IsExpiryValid(vm.ExpiryMonth, y))
       .WithMessage("Card has expired")
     .When(vm => vm.ExpiryMonth?.Length == 2
       && int.TryParse(vm.ExpiryMonth, out var m)
    && m >= 1 && m <= 12,
         ApplyConditionTo.CurrentValidator);

            RuleFor(x => x.Cvv)
            .NotEmpty().WithMessage("CVV is required")
             .Must((vm, cvv) =>
            {
                var expected = vm.CardNetwork == "Amex" ? 4 : 3;
                return (cvv?.Length ?? 0) == expected;
            })
        .WithMessage(vm => $"{vm.CardNetwork} CVV must be {(vm.CardNetwork == "Amex" ? 4 : 3)} digits");
        }

        private static bool IsExpiryValid(string? month, string? year)
        {
            if (!int.TryParse(month, out var m)) return false;
            if (!int.TryParse(year, out var y)) return false;
            if (m < 1 || m > 12) return false;
            var fullYear = 2000 + y;
            var expDate = new DateTime(fullYear, m, DateTime.DaysInMonth(fullYear, m));
            return expDate >= DateTime.Today;
        }

        private static bool PassesLuhn(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return false;
            int sum = 0; bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                if (!char.IsDigit(digits[i])) return false;
                int n = digits[i] - '0';
                if (alt) { n *= 2; if (n > 9) n -= 9; }
                sum += n;
                alt = !alt;
            }
            return sum % 10 == 0;
        }
    }
}
