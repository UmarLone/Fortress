namespace Fortress.Core.Models
{
    public sealed class VaultHealthConfig
  {
        public int MaxPasswordAgeDays { get; init; } = 365;
  public int MinAcceptableLength { get; init; } = 8;
        public int StrongLengthThreshold { get; init; } = 14;
   public int DeductionPerWeakPassword { get; init; } = 4;
 public int DeductionPerReusedPassword { get; init; } = 5;
        public int DeductionPerOldPassword { get; init; } = 2;
     public int DeductionPerMissing2FA { get; init; } = 3;
        public int DeductionPerBreachedAccount { get; init; } = 10;
  public int DeductionPerEmptyPassword { get; init; } = 8;
        public int DeductionPerUsernameAsPassword { get; init; } = 6;
        public int MaxDeductionPerCategory { get; init; } = 30;
        public int ExcellentThreshold { get; init; } = 90;
       public int GoodThreshold { get; init; } = 75;
        public int AtRiskThreshold { get; init; } = 50;
    }
}
