using Android.Service.Autofill;
using Android.Text;
using Android.Views;
using Android.Views.Autofill;
using Fortress.Mobile.Core.Models;
using SysDebug = System.Diagnostics.Debug;

namespace Bit.Droid.Autofill
{
    public class FieldCollection
  {
        private List<Field> _passwordFields = null;
  private List<Field> _usernameFields = null;
   private List<Field> _creditCardFields = null;

        private HashSet<string> _ignoreSearchTerms = new HashSet<string> { "search", "find", "recipient", "edit" };
        private HashSet<string> _usernameTerms = new HashSet<string> { "email", "User ID", "Email", "phone", "username", "Username" };
        private HashSet<string> _passwordTerms = new HashSet<string> { "password", "Password", "pswd" };

        // ── Credit card terms ─────────────────────────────────────────────────
    private HashSet<string> _cardNumberTerms = new HashSet<string> {
  "cardnumber","card-number","card_number","ccnumber","cc-number","cc_number",
            "creditcard","credit-card","credit_card","cardnum","card-num","card_num",
   "pan","acct","account-number","accountnumber" };
        private HashSet<string> _cardCvvTerms = new HashSet<string> {
        "cvv","cvc","cvv2","cvc2","csc","cc-csc","securitycode","security-code","security_code",
   "cardcode","card-code","card_code","verification" };
   private HashSet<string> _cardExpiryTerms = new HashSet<string> {
            "expiry","expiration","exp-date","exp_date","expdate","cc-exp","ccexp",
            "exp-month","exp_month","expmonth","exp-year","exp_year","expyear",
       "cc-exp-month","cc-exp-year","expirymonth","expiryyear","mm/yy","mm-yy","valid" };
        private HashSet<string> _cardNameTerms = new HashSet<string> {
 "cc-name","ccname","card-name","cardname","cardholder","card-holder","nameoncard","name-on-card" };

        // ── Identity terms ────────────────────────────────────────────────────
        private static readonly HashSet<string> _identityFirstNameTerms = new(StringComparer.OrdinalIgnoreCase)
  { "firstname","first-name","first_name","fname","given-name","given_name" };
        private static readonly HashSet<string> _identityLastNameTerms = new(StringComparer.OrdinalIgnoreCase)
            { "lastname","last-name","last_name","lname","surname","family-name","family_name" };
    private static readonly HashSet<string> _identityPhoneTerms = new(StringComparer.OrdinalIgnoreCase)
     { "phone","mobile","cell","telephone","tel" };
        private static readonly HashSet<string> _identityAddressTerms = new(StringComparer.OrdinalIgnoreCase)
  { "address","street","addr","address1","address_1" };
        private static readonly HashSet<string> _identityPostalTerms = new(StringComparer.OrdinalIgnoreCase)
 { "zip","postal","postcode","post-code","zipcode" };
  private static readonly HashSet<string> _identityEmailTerms = new(StringComparer.OrdinalIgnoreCase)
    { "email","e-mail","emailaddress" };

      // New terms for state and address line 2
    private static readonly HashSet<string> _identityStateTerms = new(StringComparer.OrdinalIgnoreCase)
            { "state", "province", "region", "address-level1" };
     private static readonly HashSet<string> _identityAddress2Terms = new(StringComparer.OrdinalIgnoreCase)
 { "address2", "address-line2", "address_line2", "apt", "suite" };
        private static readonly HashSet<string> _identityAddress2HtmlNames = new(StringComparer.OrdinalIgnoreCase)
  { "address-line2", "address2", "apt", "suite" };
        private static readonly HashSet<string> _identityStateHtmlNames = new(StringComparer.OrdinalIgnoreCase)
       { "address-level1", "state", "province", "region" };
 private static readonly HashSet<string> _uaAddress2Hints = new(StringComparer.OrdinalIgnoreCase)
  { "ADDRESS_HOME_LINE2" };

        // ── Public collections ────────────────────────────────────────────────
        public List<AutofillId> AutofillIds { get; private set; } = new List<AutofillId>();
        public HashSet<string> Hints { get; private set; } = new HashSet<string>();
        public HashSet<string> FocusedHints { get; private set; } = new HashSet<string>();
   public HashSet<string> FieldTrackingIds { get; private set; } = new HashSet<string>();
        public List<Field> Fields { get; private set; } = new List<Field>();
        public IDictionary<string, List<Field>> HintToFieldsMap { get; private set; } = new Dictionary<string, List<Field>>();
      public List<AutofillId> IgnoreAutofillIds { get; private set; } = new List<AutofillId>();

        public SaveDataType SaveType
        {
    get
            {
         if (FillableForCard)   return SaveDataType.CreditCard;
       if (FillableForLogin)  return SaveDataType.Password;
     return SaveDataType.Generic;
  }
        }

        // ── Password / Username ───────────────────────────────────────────────
    public List<Field> PasswordFields
   {
          get
  {
          if (_passwordFields != null) return _passwordFields;
       if (Hints.Any())
    {
        _passwordFields = new List<Field>();
        if (HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintPassword))
         {
             _passwordFields.AddRange(HintToFieldsMap[Android.Views.View.AutofillHintPassword]);
           if (_passwordFields.Any()) return _passwordFields;
     }
    }
        _passwordFields = Fields.Where(f => FieldIsPassword(f)).ToList();
                if (_passwordFields.Count > 1 && Fields.Count == 2)
            {
var hidden = _passwordFields
        .Where(f => f.InputType.HasFlag(InputTypes.TextVariationPassword) &&
        !f.InputType.HasFlag(InputTypes.TextVariationVisiblePassword))
          .ToList();
        if (hidden.Count == 1) _passwordFields = hidden;
      }
   if (!_passwordFields.Any())
          _passwordFields = Fields.Where(f => FieldHasPasswordTerms(f)).ToList();
             return _passwordFields;
            }
        }

        public List<Field> UsernameFields
  {
       get
{
       if (_usernameFields != null) return _usernameFields;
                _usernameFields = new List<Field>();
   if (Hints.Any())
       {
     if (HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintEmailAddress))
    _usernameFields.AddRange(HintToFieldsMap[Android.Views.View.AutofillHintEmailAddress]);
         if (HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintUsername))
     _usernameFields.AddRange(HintToFieldsMap[Android.Views.View.AutofillHintUsername]);
    if (_usernameFields.Any()) return _usernameFields;
        }
            foreach (var pf in PasswordFields)
        {
           var uf = Fields.TakeWhile(f => f.AutofillId != pf.AutofillId).LastOrDefault();
        if (uf != null) _usernameFields.Add(uf);
                }
                if (!_usernameFields.Any())
           _usernameFields = Fields.Where(f => FieldIsUsername(f)).ToList();
           return _usernameFields;
 }
        }

        // ── Credit card fields ────────────────────────────────────────────────
        public List<Field> CreditCardFields
   {
            get
            {
        if (_creditCardFields != null) return _creditCardFields;
                _creditCardFields = new List<Field>();

                foreach (var hint in new[] {
    Android.Views.View.AutofillHintCreditCardNumber,
       Android.Views.View.AutofillHintCreditCardSecurityCode,
            Android.Views.View.AutofillHintCreditCardExpirationMonth,
           Android.Views.View.AutofillHintCreditCardExpirationYear,
        Android.Views.View.AutofillHintCreditCardExpirationDate })
                {
         if (HintToFieldsMap.ContainsKey(hint))
      _creditCardFields.AddRange(HintToFieldsMap[hint]);
       }

    if (!_creditCardFields.Any())
     {
         foreach (var fld in Fields)
         if (FieldIsCreditCard(fld)) _creditCardFields.Add(fld);
              }
             return _creditCardFields;
   }
   }

        // ── Identity fields ───────────────────────────────────────────────────
        private static readonly HashSet<string> _identityFirstNameHtmlNames = new(StringComparer.OrdinalIgnoreCase)
            { "given-name", "firstname", "first-name", "first_name", "fname" };
        private static readonly HashSet<string> _identityLastNameHtmlNames = new(StringComparer.OrdinalIgnoreCase)
      { "family-name", "lastname", "last-name", "last_name", "lname", "surname" };
   private static readonly HashSet<string> _identityMiddleNameHtmlNames = new(StringComparer.OrdinalIgnoreCase)
       { "additional-name", "middlename", "middle-name", "middle_name", "mname" };
   private static readonly HashSet<string> _identityPhoneHtmlNames = new(StringComparer.OrdinalIgnoreCase)
            { "tel", "phone", "mobile", "cell", "telephone" };
        private static readonly HashSet<string> _identityAddressHtmlNames = new(StringComparer.OrdinalIgnoreCase)
      { "address-line1", "address-line2", "street-address", "address", "addr" };
        private static readonly HashSet<string> _identityPostalHtmlNames = new(StringComparer.OrdinalIgnoreCase)
            { "postal-code", "zip", "zipcode", "postcode" };
  private static readonly HashSet<string> _identityEmailHtmlNames = new(StringComparer.OrdinalIgnoreCase)
            { "email", "e-mail" };
      private static readonly HashSet<string> _identityCityHtmlNames = new(StringComparer.OrdinalIgnoreCase)
    { "address-level2", "city", "town" };

        // ua-autofill-hints values from Chrome's crowdsourcing
        private static readonly HashSet<string> _uaFirstNameHints = new(StringComparer.OrdinalIgnoreCase)
            { "NAME_FIRST" };
        private static readonly HashSet<string> _uaLastNameHints = new(StringComparer.OrdinalIgnoreCase)
            { "NAME_LAST" };
        private static readonly HashSet<string> _uaPhoneHints = new(StringComparer.OrdinalIgnoreCase)
        { "PHONE_HOME_CITY_AND_NUMBER", "PHONE_HOME_NUMBER", "PHONE_HOME_WHOLE_NUMBER" };
        private static readonly HashSet<string> _uaAddressHints = new(StringComparer.OrdinalIgnoreCase)
{ "ADDRESS_HOME_LINE1", "ADDRESS_HOME_LINE2", "ADDRESS_HOME_STREET_ADDRESS" };
        private static readonly HashSet<string> _uaPostalHints = new(StringComparer.OrdinalIgnoreCase)
          { "ADDRESS_HOME_ZIP" };
   private static readonly HashSet<string> _uaEmailHints = new(StringComparer.OrdinalIgnoreCase)
  { "EMAIL_ADDRESS" };
    private static readonly HashSet<string> _uaCityHints = new(StringComparer.OrdinalIgnoreCase)
       { "ADDRESS_HOME_CITY" };
        private static readonly HashSet<string> _uaStateHints = new(StringComparer.OrdinalIgnoreCase)
     { "ADDRESS_HOME_STATE" };

        private bool FieldMatchesIdentityFirst(Field f)
            => ValueContainsAnyTerms(f.IdEntry, _identityFirstNameTerms)
        || ValueContainsAnyTerms(f.Hint, _identityFirstNameTerms)
            || CheckHtmlAutocomplete(f, "given-name")
  || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityFirstNameHtmlNames)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaFirstNameHints)
|| ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-predictions-autofill-hints"), _uaFirstNameHints);

      private bool FieldMatchesIdentityLast(Field f)
            => ValueContainsAnyTerms(f.IdEntry, _identityLastNameTerms)
            || ValueContainsAnyTerms(f.Hint, _identityLastNameTerms)
          || CheckHtmlAutocomplete(f, "family-name")
    || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityLastNameHtmlNames)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaLastNameHints)
    || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-predictions-autofill-hints"), _uaLastNameHints);

        private bool FieldMatchesIdentityMiddle(Field f)
            => CheckHtmlAutocomplete(f, "additional-name")
  || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityMiddleNameHtmlNames)
    || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NAME_MIDDLE" });

        private bool FieldMatchesIdentityPhone(Field f)
        => ValueContainsAnyTerms(f.IdEntry, _identityPhoneTerms)
     || ValueContainsAnyTerms(f.Hint, _identityPhoneTerms)
            || CheckHtmlAutocomplete(f, "tel")
     || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityPhoneHtmlNames)
         || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaPhoneHints)
       || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaPhoneHints);

        private bool FieldMatchesIdentityAddress(Field f)
=> ValueContainsAnyTerms(f.IdEntry, _identityAddressTerms)
            || ValueContainsAnyTerms(f.Hint, _identityAddressTerms)
      || CheckHtmlAutocomplete(f, "address-line")
         || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityAddressHtmlNames)
     || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaAddressHints)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaAddressHints);

        private bool FieldMatchesIdentityAddress2(Field f)
  => ValueContainsAnyTerms(f.IdEntry, _identityAddress2Terms)
    || ValueContainsAnyTerms(f.Hint, _identityAddress2Terms)
      || CheckHtmlAutocomplete(f, "address-line2")
  || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityAddress2HtmlNames)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaAddress2Hints)
      || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaAddress2Hints);

        private bool FieldMatchesIdentityPostal(Field f)
            => ValueContainsAnyTerms(f.IdEntry, _identityPostalTerms)
            || ValueContainsAnyTerms(f.Hint, _identityPostalTerms)
            || CheckHtmlAutocomplete(f, "postal-code")
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityPostalHtmlNames)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaPostalHints)
      || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaPostalHints);

        private bool FieldMatchesIdentityEmail(Field f)
            => ValueContainsAnyTerms(f.IdEntry, _identityEmailTerms)
|| ValueContainsAnyTerms(f.Hint, _identityEmailTerms)
    || CheckHtmlAutocomplete(f, "email")
          || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityEmailHtmlNames)
     || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaEmailHints)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaEmailHints);

        private bool FieldMatchesIdentityCity(Field f)
   => CheckHtmlAutocomplete(f, "address-level2")
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityCityHtmlNames)
     || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaCityHints)
 || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaCityHints);

        private bool FieldMatchesIdentityState(Field f)
            => ValueContainsAnyTerms(f.IdEntry, _identityStateTerms)
            || ValueContainsAnyTerms(f.Hint, _identityStateTerms)
   || CheckHtmlAutocomplete(f, "address-level1")
     || ValueContainsAnyTerms(GetHtmlAttribute(f, "name"), _identityStateHtmlNames)
            || ValueContainsAnyTerms(GetHtmlAttribute(f, "ua-autofill-hints"), _uaStateHints)
     || ValueContainsAnyTerms(GetHtmlAttribute(f, "crowdsourcing-autofill-hints"), _uaStateHints);

        public List<Field> IdentityFirstNameFields =>
        HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintName)
  ? HintToFieldsMap[Android.Views.View.AutofillHintName].ToList()
       : Fields.Where(FieldMatchesIdentityFirst).ToList();

        public List<Field> IdentityLastNameFields =>
 Fields.Where(FieldMatchesIdentityLast).ToList();

        public List<Field> IdentityMiddleNameFields =>
       Fields.Where(FieldMatchesIdentityMiddle).ToList();

        public List<Field> IdentityPhoneFields =>
    HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintPhone)
      ? HintToFieldsMap[Android.Views.View.AutofillHintPhone].ToList()
             : Fields.Where(FieldMatchesIdentityPhone).ToList();

        public List<Field> IdentityAddressFields =>
        HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintPostalAddress)
? HintToFieldsMap[Android.Views.View.AutofillHintPostalAddress].ToList()
    : Fields.Where(FieldMatchesIdentityAddress).ToList();

      public List<Field> IdentityAddress2Fields =>
     Fields.Where(FieldMatchesIdentityAddress2).ToList();

      public List<Field> IdentityPostalFields =>
          HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintPostalCode)
       ? HintToFieldsMap[Android.Views.View.AutofillHintPostalCode].ToList()
      : Fields.Where(FieldMatchesIdentityPostal).ToList();

        public List<Field> IdentityEmailFields =>
  HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintEmailAddress)
             ? HintToFieldsMap[Android.Views.View.AutofillHintEmailAddress].ToList()
                : Fields.Where(FieldMatchesIdentityEmail).ToList();

        public List<Field> IdentityCityFields =>
     Fields.Where(FieldMatchesIdentityCity).ToList();

 public List<Field> IdentityStateFields =>
            Fields.Where(FieldMatchesIdentityState).ToList();

        // ── Fillable detection ────────────────────────────────────────────────

        // Login: hint, focused field, OR password field present
        public bool FillableForLogin =>
            FocusedHintsContain(new[] {
           Android.Views.View.AutofillHintUsername,
   Android.Views.View.AutofillHintEmailAddress,
      Android.Views.View.AutofillHintPassword })
            || UsernameFields.Any(f => f.Focused)
   || PasswordFields.Any(f => f.Focused)
          || PasswordFields.Any();

    // Card: focused hint, focused card field, OR card fields present with no password field
        public bool FillableForCard =>
            FocusedHintsContain(new[] {
 Android.Views.View.AutofillHintCreditCardNumber,
Android.Views.View.AutofillHintCreditCardExpirationMonth,
       Android.Views.View.AutofillHintCreditCardExpirationYear,
            Android.Views.View.AutofillHintCreditCardSecurityCode })
      || CreditCardFields.Any(f => f.Focused)
            || (CreditCardFields.Any() && !PasswordFields.Any());

        // Identity: focused hint, focused identity field, OR 2+ distinct identity field types with no password field
        public bool FillableForIdentity =>
            FocusedHintsContain(new[] {
           Android.Views.View.AutofillHintPhone,
         Android.Views.View.AutofillHintPostalAddress,
     Android.Views.View.AutofillHintPostalCode })
 || IdentityFirstNameFields.Any(f => f.Focused)
  || IdentityLastNameFields.Any(f => f.Focused)
            || IdentityAddressFields.Any(f => f.Focused)
            || IdentityPhoneFields.Any(f => f.Focused)
        || IdentityCityFields.Any(f => f.Focused)
            || (!PasswordFields.Any() && (
       (IdentityFirstNameFields.Any() && IdentityLastNameFields.Any())
     || (IdentityFirstNameFields.Any() && IdentityAddressFields.Any())
          || (IdentityFirstNameFields.Any() && IdentityPhoneFields.Any())
    || (IdentityLastNameFields.Any() && IdentityAddressFields.Any())
        || (IdentityAddressFields.Any() && IdentityPostalFields.Any())
   || (IdentityAddressFields.Any() && IdentityCityFields.Any())
     || (IdentityPhoneFields.Any() && IdentityAddressFields.Any())));

        public bool Fillable => FillableForCard || FillableForIdentity || FillableForLogin;

     // ── Mutation ──────────────────────────────────────────────────────────
        public void Add(Field field)
        {
        if (field == null || FieldTrackingIds.Contains(field.TrackingId)) return;
          _passwordFields = _usernameFields = _creditCardFields = null;
          FieldTrackingIds.Add(field.TrackingId);
          Fields.Add(field);
            AutofillIds.Add(field.AutofillId);
       if (field.Hints != null)
{
     foreach (var hint in field.Hints)
            {
 Hints.Add(hint);
      if (field.Focused) FocusedHints.Add(hint);
            if (!HintToFieldsMap.ContainsKey(hint))
          HintToFieldsMap.Add(hint, new List<Field>());
         HintToFieldsMap[hint].Add(field);
            }
            }
  }

        // ── Save helpers ──────────────────────────────────────────────────────
        public SavedItem GetSavedItem()
    {
            if (SaveType == SaveDataType.Password)
     {
                var passwordField = PasswordFields.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.TextValue));
                if (passwordField == null) return null;
                var savedItem = new SavedItem
 {
            Type = CipherType.Login,
     Login = new SavedItem.LoginItem { Password = GetFieldValue(passwordField) }
        };
      var usernameField = Fields.TakeWhile(f => f.AutofillId != passwordField.AutofillId).LastOrDefault();
  savedItem.Login.Username = GetFieldValue(usernameField);
   return savedItem;
            }
   else if (SaveType == SaveDataType.CreditCard)
          {
      return new SavedItem
       {
       Type = CipherType.Card,
             Card = new SavedItem.CardItem
       {
Number   = GetFieldValue(Android.Views.View.AutofillHintCreditCardNumber),
   Name     = GetFieldValue(Android.Views.View.AutofillHintName),
   ExpMonth = GetFieldValue(Android.Views.View.AutofillHintCreditCardExpirationMonth, true),
 ExpYear  = GetFieldValue(Android.Views.View.AutofillHintCreditCardExpirationYear),
               Code     = GetFieldValue(Android.Views.View.AutofillHintCreditCardSecurityCode),
}
         };
  }
    return null;
        }

 public AutofillId[] GetOptionalSaveIds()
   {
            if (SaveType == SaveDataType.Password)
  return UsernameFields.Select(f => f.AutofillId).ToArray();
            if (SaveType == SaveDataType.CreditCard)
      {
         var list = new List<Field>();
              foreach (var hint in new[] {
       Android.Views.View.AutofillHintCreditCardSecurityCode,
         Android.Views.View.AutofillHintCreditCardExpirationYear,
       Android.Views.View.AutofillHintCreditCardExpirationMonth,
    Android.Views.View.AutofillHintCreditCardExpirationDate,
        Android.Views.View.AutofillHintName })
                {
      if (HintToFieldsMap.ContainsKey(hint))
     list.AddRange(HintToFieldsMap[hint]);
  }
       return list.Select(f => f.AutofillId).ToArray();
            }
    return Array.Empty<AutofillId>();
    }

        public AutofillId[] GetRequiredSaveFields()
        {
   if (SaveType == SaveDataType.Password)
return PasswordFields.Select(f => f.AutofillId).ToArray();
  if (SaveType == SaveDataType.CreditCard &&
    HintToFieldsMap.ContainsKey(Android.Views.View.AutofillHintCreditCardNumber))
   return HintToFieldsMap[Android.Views.View.AutofillHintCreditCardNumber]
   .Select(f => f.AutofillId).ToArray();
            return Array.Empty<AutofillId>();
        }

        // ── Private helpers ───────────────────────────────────────────────────
     private bool FocusedHintsContain(IEnumerable<string> hints) =>
      hints.Any(h => FocusedHints.Contains(h));

        private string GetFieldValue(string hint, bool monthValue = false)
        {
            if (!HintToFieldsMap.ContainsKey(hint)) return null;
        foreach (var field in HintToFieldsMap[hint])
     {
     var val = GetFieldValue(field, monthValue);
        if (!string.IsNullOrWhiteSpace(val)) return val;
            }
    return null;
        }

        private string GetFieldValue(Field field, bool monthValue = false)
        {
 if (field == null) return null;
            if (!string.IsNullOrWhiteSpace(field.TextValue))
            {
     if (field.AutofillType == AutofillType.List && field.ListValue.HasValue && monthValue)
                {
         if (field.AutofillOptions.Count == 13) return field.ListValue.ToString();
                  if (field.AutofillOptions.Count == 12) return (field.ListValue + 1).ToString();
          }
      return field.TextValue;
     }
     if (field.DateValue.HasValue) return field.DateValue.Value.ToString();
            if (field.ToggleValue.HasValue) return field.ToggleValue.Value.ToString();
     return null;
        }

        private bool FieldIsPassword(Field f)
     {
            var inputTypePassword = f.InputType.HasFlag(InputTypes.TextVariationPassword) ||
   f.InputType.HasFlag(InputTypes.TextVariationVisiblePassword) ||
      f.InputType.HasFlag(InputTypes.TextVariationWebPassword);
            if (inputTypePassword && f.InputType.HasFlag(InputTypes.TextVariationPassword) &&
           f.InputType.HasFlag(InputTypes.TextFlagMultiLine))
 inputTypePassword = false;
        if (!inputTypePassword && f.HtmlInfo?.Tag == "input" && (f.HtmlInfo.Attributes?.Any() ?? false))
    {
    foreach (var a in f.HtmlInfo.Attributes)
       {
           var key = a.First as Java.Lang.String;
            var val = a.Second as Java.Lang.String;
              if (key?.ToString() == "type" && val?.ToString() == "password") return true;
      }
          }
            return inputTypePassword
       && !ValueContainsAnyTerms(f.IdEntry, _ignoreSearchTerms)
     && !ValueContainsAnyTerms(f.Hint, _ignoreSearchTerms)
     && !FieldIsUsername(f);
 }

        private bool FieldHasPasswordTerms(Field f) =>
     ValueContainsAnyTerms(f.IdEntry, _passwordTerms) || ValueContainsAnyTerms(f.Hint, _passwordTerms);

    private bool FieldIsUsername(Field f) =>
            f.InputType.HasFlag(InputTypes.TextVariationWebEmailAddress) || FieldHasUsernameTerms(f);

   private bool FieldHasUsernameTerms(Field f) =>
            ValueContainsAnyTerms(f.IdEntry, _usernameTerms) || ValueContainsAnyTerms(f.Hint, _usernameTerms);

        private bool FieldIsCreditCard(Field f) =>
       FieldIsCreditCardNumber(f) || FieldIsCvv(f) || FieldIsExpiry(f) || FieldIsCardName(f);

        private bool FieldIsCreditCardNumber(Field f)
        {
            if (ValueContainsAnyTerms(f.IdEntry, _cardNumberTerms) || ValueContainsAnyTerms(f.Hint, _cardNumberTerms))
     return true;
            var htmlName = GetHtmlAttribute(f, "name");
            var htmlId   = GetHtmlAttribute(f, "id");
            var uaHints  = GetHtmlAttribute(f, "ua-autofill-hints");
            if (ValueContainsAnyTerms(htmlName, _cardNumberTerms) || ValueContainsAnyTerms(htmlId, _cardNumberTerms)
        || CheckHtmlAutocomplete(f, "cc-number") || uaHints?.Contains("CREDIT_CARD_NUMBER") == true)
       return true;
     var isNumeric = f.InputType.HasFlag(InputTypes.ClassNumber) || f.InputType.HasFlag(InputTypes.ClassPhone);
    return isNumeric && (ValueContainsAnyTerms(f.IdEntry, new HashSet<string> { "card" })
          || ValueContainsAnyTerms(f.Hint,    new HashSet<string> { "card" })
 || ValueContainsAnyTerms(htmlName,  new HashSet<string> { "card" })
             || ValueContainsAnyTerms(htmlId,    new HashSet<string> { "card" }));
        }

        private bool FieldIsCvv(Field f)
        {
   if (ValueContainsAnyTerms(f.IdEntry, _cardCvvTerms) || ValueContainsAnyTerms(f.Hint, _cardCvvTerms))
  return true;
  var htmlName = GetHtmlAttribute(f, "name");
      var htmlId   = GetHtmlAttribute(f, "id");
      var uaHints  = GetHtmlAttribute(f, "ua-autofill-hints");
     return ValueContainsAnyTerms(htmlName, _cardCvvTerms) || ValueContainsAnyTerms(htmlId, _cardCvvTerms)
                || CheckHtmlAutocomplete(f, "cc-csc")
                || uaHints?.Contains("CREDIT_CARD_VERIFICATION_CODE") == true;
        }

        private bool FieldIsExpiry(Field f)
  {
        if (ValueContainsAnyTerms(f.IdEntry, _cardExpiryTerms) || ValueContainsAnyTerms(f.Hint, _cardExpiryTerms))
            return true;
            var htmlName = GetHtmlAttribute(f, "name");
    var htmlId   = GetHtmlAttribute(f, "id");
      var uaHints  = GetHtmlAttribute(f, "ua-autofill-hints");
            return ValueContainsAnyTerms(htmlName, _cardExpiryTerms) || ValueContainsAnyTerms(htmlId, _cardExpiryTerms)
           || CheckHtmlAutocomplete(f, "cc-exp")
         || uaHints?.Contains("CREDIT_CARD_EXP") == true;
        }

        private bool FieldIsCardName(Field f)
        {
            if (ValueContainsAnyTerms(f.IdEntry, _cardNameTerms) || ValueContainsAnyTerms(f.Hint, _cardNameTerms))
                return true;
            var htmlName = GetHtmlAttribute(f, "name");
 var htmlId   = GetHtmlAttribute(f, "id");
      var uaHints  = GetHtmlAttribute(f, "ua-autofill-hints");
     return ValueContainsAnyTerms(htmlName, _cardNameTerms) || ValueContainsAnyTerms(htmlId, _cardNameTerms)
           || CheckHtmlAutocomplete(f, "cc-name")
         || uaHints?.Contains("CREDIT_CARD_NAME") == true;
      }

  private string GetHtmlAttribute(Field f, string attributeName)
     {
     if (f.HtmlInfo?.Attributes == null) return null;
     foreach (var attr in f.HtmlInfo.Attributes)
       {
    var key = attr.First as Java.Lang.String;
      var val = attr.Second as Java.Lang.String;
         if (key != null && val != null &&
   key.ToString().Equals(attributeName, StringComparison.OrdinalIgnoreCase))
             return val.ToString();
   }
 return null;
     }

    private bool CheckHtmlAutocomplete(Field f, string autocompleteValue)
   {
         if (f.HtmlInfo?.Tag != "input" || !(f.HtmlInfo.Attributes?.Any() ?? false)) return false;
        foreach (var attr in f.HtmlInfo.Attributes)
{
                var key = attr.First as Java.Lang.String;
    var val = attr.Second as Java.Lang.String;
   if (key?.ToString().ToLowerInvariant() == "autocomplete" &&
            val?.ToString().ToLowerInvariant().Contains(autocompleteValue) == true)
          return true;
       }
        return false;
}

        private bool ValueContainsAnyTerms(string value, HashSet<string> terms)
        {
 if (string.IsNullOrWhiteSpace(value)) return false;
            var lowerValue = value.ToLowerInvariant();
         return terms.Any(t => lowerValue.Contains(t));
        }
    }
}