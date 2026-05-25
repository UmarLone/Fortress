using static Android.App.Assist.AssistStructure;
using Android.App.Assist;
using Android.Content;
using Android.OS;
using Fortress.Mobile.Core.Models;
using Fortress.Mobile.Core.Services;
using SysDebug = System.Diagnostics.Debug;

namespace Bit.Droid.Autofill
{
    public class Parser
    {
        public static HashSet<string> _excludedPackageIds = new HashSet<string>
        {
            "android"
        };
        public readonly AssistStructure _structure;
        private string _uri;
        private string _packageName;
        private string _website;

        public Parser(AssistStructure structure, Context applicationContext)
        {
            _structure = structure;
            ApplicationContext = applicationContext;
        }

        public Context ApplicationContext { get; set; }
        public FieldCollection FieldCollection { get; private set; } = new FieldCollection();

        public string Uri
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_uri))
                {
                    return _uri;
                }
                var websiteNull = string.IsNullOrWhiteSpace(Website);
                if (websiteNull && string.IsNullOrWhiteSpace(PackageName))
                {
                    _uri = null;
                }
                else if (!websiteNull)
                {
                    _uri = Website;
                }
                else
                {
                    _uri = string.Concat(Constants.AndroidAppProtocol, PackageName);
                }
                return _uri;
            }
        }

        public string PackageName
        {
            get => _packageName;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _packageName = _uri = null;
                }
                _packageName = value;
            }
        }

        public string Website
        {
            get => _website;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _website = _uri = null;
                }
                _website = value;
            }
        }

        public async Task<bool> ShouldAutofillAsync()
        {
            // Detailed logging for debugging
            SysDebug.WriteLine("═══════════════════════════════════════════════════════════");
            SysDebug.WriteLine("🔍 AUTOFILL DEBUG: ShouldAutofillAsync() called");
            SysDebug.WriteLine("═══════════════════════════════════════════════════════════");

            // Check URI
            SysDebug.WriteLine($"📍 URI: '{Uri ?? "NULL"}'");
            SysDebug.WriteLine($"   - IsNullOrWhiteSpace: {string.IsNullOrWhiteSpace(Uri)}");
            SysDebug.WriteLine($"   - Website: '{Website ?? "NULL"}'");
            SysDebug.WriteLine($"   - PackageName: '{PackageName ?? "NULL"}'");

            // Check blacklist
            var isBlacklisted = !string.IsNullOrWhiteSpace(Uri) && AutofillBuilder.BlacklistedUris.Contains(Uri);
            SysDebug.WriteLine($"🚫 Is Blacklisted: {isBlacklisted}");
            if (isBlacklisted)
            {
                SysDebug.WriteLine($"   - URI is in blacklist!");
            }

            // Check FieldCollection
            SysDebug.WriteLine($"📋 FieldCollection: {(FieldCollection == null ? "NULL" : "EXISTS")}");

            if (FieldCollection != null)
            {
                SysDebug.WriteLine($"   - Total Fields: {FieldCollection.Fields?.Count ?? 0}");
                SysDebug.WriteLine($"   - Hints: {string.Join(", ", FieldCollection.Hints ?? new HashSet<string>())}");
                SysDebug.WriteLine($"   - FocusedHints: {string.Join(", ", FieldCollection.FocusedHints ?? new HashSet<string>())}");

                // Log each field for debugging
                SysDebug.WriteLine("   📝 FIELDS DETECTED:");
                if (FieldCollection.Fields != null)
                {
                    foreach (var field in FieldCollection.Fields)
                    {
                        SysDebug.WriteLine($"      ─────────────────────────────");
                        SysDebug.WriteLine($"      ID: {field.Id}");
                        SysDebug.WriteLine($"      IdEntry: '{field.IdEntry ?? "NULL"}'");
                        SysDebug.WriteLine($"      Hint: '{field.Hint ?? "NULL"}'");
                        SysDebug.WriteLine($"      Hints: [{string.Join(", ", field.Hints ?? new List<string>())}]");
                        SysDebug.WriteLine($"      InputType: {field.InputType}");
                        SysDebug.WriteLine($"      AutofillType: {field.AutofillType}");
                        SysDebug.WriteLine($"      Focused: {field.Focused}");
                        SysDebug.WriteLine($"      Visible: {field.Visible}");
                        SysDebug.WriteLine($"  HtmlInfo Tag: '{field.HtmlInfo?.Tag ?? "NULL"}'");

                        // Check HTML attributes for autocomplete
                        if (field.HtmlInfo?.Attributes != null)
                        {
                            SysDebug.WriteLine($"      HTML Attributes:");
                            foreach (var attr in field.HtmlInfo.Attributes)
                            {
                                var key = attr.First as Java.Lang.String;
                                var val = attr.Second as Java.Lang.String;
                                SysDebug.WriteLine($"     {key}='{val}'");
                            }
                        }
                    }
                }

                // Check fillable conditions
                SysDebug.WriteLine("   🎯 FILLABLE CONDITIONS:");
                SysDebug.WriteLine($"      - FillableForLogin: {FieldCollection.FillableForLogin}");
                SysDebug.WriteLine($"      - FillableForCard: {FieldCollection.FillableForCard}");
                SysDebug.WriteLine($"      - FillableForIdentity: {FieldCollection.FillableForIdentity}");
                SysDebug.WriteLine($"      - Fillable (overall): {FieldCollection.Fillable}");

                // Check specific field types
                SysDebug.WriteLine($"      - UsernameFields count: {FieldCollection.UsernameFields?.Count ?? 0}");
                SysDebug.WriteLine($"      - PasswordFields count: {FieldCollection.PasswordFields?.Count ?? 0}");
                SysDebug.WriteLine($"      - CreditCardFields count: {FieldCollection.CreditCardFields?.Count ?? 0}");

                // Log focused fields
                var focusedFields = FieldCollection.Fields?.Where(f => f.Focused).ToList();
                SysDebug.WriteLine($"      - Focused fields: {focusedFields?.Count ?? 0}");
                if (focusedFields?.Any() == true)
                {
                    foreach (var f in focusedFields)
                    {
                        SysDebug.WriteLine($"    * IdEntry: '{f.IdEntry}', Hint: '{f.Hint}', Hints: [{string.Join(", ", f.Hints ?? new List<string>())}]");
                    }
                }
            }

            // Calculate final result
            var fillable = !string.IsNullOrWhiteSpace(Uri) &&
            !AutofillBuilder.BlacklistedUris.Contains(Uri) &&
            FieldCollection != null &&
                FieldCollection.Fillable;

            SysDebug.WriteLine("═══════════════════════════════════════════════════════════");
            SysDebug.WriteLine($"✅ FINAL RESULT: ShouldAutofill = {fillable}");

            if (!fillable)
            {
                SysDebug.WriteLine("❌ REASON FOR FALSE:");
                if (string.IsNullOrWhiteSpace(Uri))
                    SysDebug.WriteLine("   - URI is null or whitespace");
                if (!string.IsNullOrWhiteSpace(Uri) && AutofillBuilder.BlacklistedUris.Contains(Uri))
                    SysDebug.WriteLine("   - URI is blacklisted");
                if (FieldCollection == null)
                    SysDebug.WriteLine("   - FieldCollection is null");
                if (FieldCollection != null && !FieldCollection.Fillable)
                    SysDebug.WriteLine("   - FieldCollection.Fillable is false (no fillable fields detected)");
            }
            SysDebug.WriteLine("═══════════════════════════════════════════════════════════");

            return fillable;
        }

        public void Parse()
        {
            string titlePackageId = null;
            for (var i = 0; i < _structure.WindowNodeCount; i++)
            {
                var node = _structure.GetWindowNodeAt(i);
                if (i == 0)
                {
                    titlePackageId = GetTitlePackageId(node);
                }
                ParseNode(node.RootViewNode);
            }
            if (string.IsNullOrWhiteSpace(PackageName) && string.IsNullOrWhiteSpace(Website))
            {
                PackageName = titlePackageId;
            }
            if (!AutofillBuilder.TrustedBrowsers.Contains(PackageName) &&
                !AutofillBuilder.CompatBrowsers.Contains(PackageName))
            {
                Website = null;
            }
        }

        private void ParseNode(ViewNode node)
        {
            SetPackageAndDomain(node);
            var hints = node.GetAutofillHints();
            var isEditText = node.ClassName == "android.widget.EditText" || node?.HtmlInfo?.Tag == "input";
            if (isEditText || (hints?.Length ?? 0) > 0)
            {
                FieldCollection.Add(new Field(node));
            }
            else
            {
                FieldCollection.IgnoreAutofillIds.Add(node.AutofillId);
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                ParseNode(node.GetChildAt(i));
            }
        }

        private void SetPackageAndDomain(ViewNode node)
        {
            if (string.IsNullOrWhiteSpace(PackageName) && !string.IsNullOrWhiteSpace(node.IdPackage) &&
                !_excludedPackageIds.Contains(node.IdPackage))
            {
                PackageName = node.IdPackage;
            }
            if (string.IsNullOrWhiteSpace(Website) && !string.IsNullOrWhiteSpace(node.WebDomain))
            {
                var scheme = "http";
                if ((int)Build.VERSION.SdkInt >= 28)
                {
                    scheme = node.WebScheme;
                }
                Website = string.Format("{0}://{1}", scheme, node.WebDomain);
            }
        }

        private string GetTitlePackageId(WindowNode node)
        {
            if (node != null && !string.IsNullOrWhiteSpace(node.Title))
            {
                var slashPosition = node.Title.IndexOf('/');
                if (slashPosition > -1)
                {
                    var packageId = node.Title.Substring(0, slashPosition);
                    if (packageId.Contains("."))
                    {
                        return packageId;
                    }
                }
            }
            return null;
        }
    }
}
