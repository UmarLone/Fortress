using System.Collections.ObjectModel;
using Wpf.Ui.Abstractions.Controls;

namespace Fortress.Windows.Desktop.ViewModels.Pages
{
    public partial class PasswordGeneratorViewModel : ObservableObject, INavigationAware
    {
       [ObservableProperty] private string _generatedPassword = string.Empty;
     [ObservableProperty] private int _passwordLength = 20;
     [ObservableProperty] private bool _includeUppercase = true;
     [ObservableProperty] private bool _includeLowercase = true;
       [ObservableProperty] private bool _includeNumbers = true;
        [ObservableProperty] private bool _includeSymbols = true;
        [ObservableProperty] private bool _excludeAmbiguous = true;
[ObservableProperty] private int _strengthScore;
    [ObservableProperty] private string _strengthLabel = string.Empty;
      [ObservableProperty] private string _strengthColor = "#94A3B8";
        [ObservableProperty] private ObservableCollection<string> _history = new();

    private const string Upper   = "ABCDEFGHJKLMNPQRSTUVWXYZ";
      private const string Lower   = "abcdefghjkmnpqrstuvwxyz";
     private const string Digits  = "23456789";
  private const string Symbols = "!@#$%^&*()-_=+[]{}|;:,.<>?";
  private const string AmbiguousUpper  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string AmbiguousLower  = "abcdefghijklmnopqrstuvwxyz";
      private const string AmbiguousDigits = "0123456789";

  public Task OnNavigatedToAsync() { if (string.IsNullOrEmpty(GeneratedPassword)) GenerateNewPassword(); return Task.CompletedTask; }
      public Task OnNavigatedFromAsync() => Task.CompletedTask;

     [RelayCommand]
  private void GenerateNewPassword()
 {
   var chars = BuildCharset();
     if (chars.Length == 0) { GeneratedPassword = "Select at least one character type"; return; }

      var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
   var bytes = new byte[PasswordLength * 4];
  rng.GetBytes(bytes);

      var result = new char[PasswordLength];
      for (int i = 0; i < PasswordLength; i++)
      {
          var idx = BitConverter.ToUInt32(bytes, i * 4) % (uint)chars.Length;
           result[i] = chars[(int)idx];
     }

    GeneratedPassword = new string(result);
  UpdateStrength(GeneratedPassword);
  History.Insert(0, GeneratedPassword);
            if (History.Count > 10) History.RemoveAt(History.Count - 1);
   }

        [RelayCommand]
        private void CopyPassword()
     {
       if (!string.IsNullOrEmpty(GeneratedPassword))
System.Windows.Clipboard.SetText(GeneratedPassword);
     }

   [RelayCommand]
  private void UsePassword(string? pw)
  {
    if (!string.IsNullOrEmpty(pw))
  System.Windows.Clipboard.SetText(pw);
  }

        partial void OnPasswordLengthChanged(int value)    { if (!string.IsNullOrEmpty(GeneratedPassword)) GenerateNewPassword(); }
   partial void OnIncludeUppercaseChanged(bool value)  { GenerateNewPassword(); }
        partial void OnIncludeLowercaseChanged(bool value) { GenerateNewPassword(); }
      partial void OnIncludeNumbersChanged(bool value)  { GenerateNewPassword(); }
        partial void OnIncludeSymbolsChanged(bool value)   { GenerateNewPassword(); }
  partial void OnExcludeAmbiguousChanged(bool value)  { GenerateNewPassword(); }

  private string BuildCharset()
     {
  var sb = new System.Text.StringBuilder();
   if (IncludeUppercase) sb.Append(ExcludeAmbiguous ? Upper : AmbiguousUpper);
       if (IncludeLowercase) sb.Append(ExcludeAmbiguous ? Lower : AmbiguousLower);
if (IncludeNumbers)   sb.Append(ExcludeAmbiguous ? Digits : AmbiguousDigits);
      if (IncludeSymbols)   sb.Append(Symbols);
      return sb.ToString();
   }

   private void UpdateStrength(string pw)
  {
    if (string.IsNullOrEmpty(pw)) { StrengthScore = 0; StrengthLabel = "None"; StrengthColor = "#94A3B8"; return; }
    int score = 0;
     score += pw.Length >= 8  ? 20 : 0;
   score += pw.Length >= 12 ? 15 : 0;
         score += pw.Length >= 16 ? 15 : 0;
    score += pw.Length >= 20 ? 10 : 0;
    score += pw.Any(char.IsUpper)  ? 10 : 0;
      score += pw.Any(char.IsLower)  ? 10 : 0;
    score += pw.Any(char.IsDigit)  ? 10 : 0;
     score += pw.Any(c => Symbols.Contains(c)) ? 10 : 0;
  StrengthScore = Math.Min(100, score);
  (StrengthLabel, StrengthColor) = StrengthScore switch
     {
         >= 90 => ("Very Strong", "#22C55E"),
   >= 70 => ("Strong",     "#84CC16"),
  >= 50 => ("Fair",      "#F59E0B"),
    >= 30 => ("Weak",      "#F97316"),
       _     => ("Very Weak",  "#EF4444"),
     };
 }
    }
}
