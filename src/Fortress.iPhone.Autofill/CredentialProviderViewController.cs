using AuthenticationServices;
using Foundation;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UIKit;
using CoreGraphics;
using ObjCRuntime;


namespace Fortress.iPhone.Autofill
{
  
    [Register("CredentialProviderViewController")]
    
    public  class CredentialProviderViewController : ASCredentialProviderViewController
    {
        
        // Storyboard outlets
        [Foundation.Outlet("logoImageView")]
        public UIKit.UIImageView? LogoImageView { get; set; }

        [Foundation.Outlet("titleLabel")]
        public UIKit.UILabel? TitleLabel { get; set; }

        [Foundation.Outlet("subtitleLabel")]
        public UIKit.UILabel? SubtitleLabel { get; set; }

        [Foundation.Outlet("loadingIndicator")]
        public UIKit.UIActivityIndicatorView? LoadingIndicator { get; set; }

        [Foundation.Outlet("cancelButton")]
        public UIKit.UIButton? CancelButton { get; set; }
        
        public CredentialProviderViewController(IntPtr handle) : base(handle)
        {
            // Keep constructor minimal for fast extension startup
        }

        List<AutofillCredential>? _credentials;
        List<AutofillCredential>? _filteredCredentials;
        UIView? _headerContainer;
        UIView? _searchContainer;
        UITextField? _searchTextField;
        UITableView? _credentialsTable;
        CredentialSource? _credentialSource;
        NSLayoutConstraint? _tableTopConstraint;
        UIButton? _floatingCancelButton;
        bool _isSearchVisible = false;
        string _currentDomain = string.Empty; // Store the current target app domain

        public override void PrepareCredentialList(
            ASCredentialServiceIdentifier[] serviceIdentifiers)
        {
            try
            {
                _currentDomain = serviceIdentifiers?.FirstOrDefault()?.Identifier ?? "unknown";

                InvokeOnMainThread(async () =>
                {
                    try
                    {
                        // Check if app is locked using shared preferences
                        if (IsAppLocked())
                        {
                            ShowLockedUI();
                            return;
                        }

                        // Load credentials from shared storage
                        _credentials = await LoadCredentialsFromSharedStorageAsync(_currentDomain);
                        
                        // If no credentials found, show option to add
                        if (_credentials == null || _credentials.Count == 0)
                        {
                            // Fall back to fake credentials for now during development
                            // TODO: Remove this once main app writes to shared storage
                            //_credentials = await LoadFakeCredentialsAsync(_currentDomain);
                        }
                        
                        // Hide the loading UI
                        HideLoadingUI();
                        
                        // Show credential list
                        ShowCredentialList();
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("❌ Failed to present credential list", ex);
                        ShowSimpleAlert("Extension Error", $"Failed to load UI: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("❌ PrepareCredentialList failed completely", ex);
                ShowSimpleAlert("Extension Startup Error", $"Extension failed to start: {ex.Message}");
            }
        }

        // MARK: - Shared Storage Methods
        
        private bool IsAppLocked()
        {
            // TODO: Enable lock check once SharedPreferences is fully implemented
            // For now, always return false to allow access
            //return false;
            try
            {
                return SharedPreferences.Instance.IsLocked();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("IsAppLocked check failed", ex);
                return true; // Fail safe - assume locked
            }
        }
        
        private async Task<List<AutofillCredential>> LoadCredentialsFromSharedStorageAsync(string domain)
        {
            try
            {
                var credentials = await SharedCredentialStorage.Instance.GetMatchingCredentialsAsync(domain);
                return credentials;
            }
            catch (Exception ex)
            {
                // Log detailed exception info for debugging
                var errorMsg = $"Type: {ex.GetType().Name}\nMessage: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMsg += $"\n\nInner: {ex.InnerException.GetType().Name}\n{ex.InnerException.Message}";
                }
                ErrorLogger.LogError($"Failed to load credentials from shared storage. {errorMsg}", ex);
                
                // Show alert for debugging
                InvokeOnMainThread(() =>
                {
                    var alert = UIAlertController.Create("Shared Storage Error", errorMsg, UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    PresentViewController(alert, true, null);
                });
                
                return new List<AutofillCredential>();
            }
        }
        
        // MARK: - Fake Methods for UI Development (will be removed)
        
        private async Task<List<AutofillCredential>> LoadFakeCredentialsAsync(string domain)
        {
            // Simulate loading delay
           
            
            return new List<AutofillCredential>
            {
                new AutofillCredential("github.com", "john.doe@email.com", "SuperSecret123!")
                {
                    HasOtp = true,
                    Code = "123456",
                    Progress = 25 // seconds remaining
                },
               
            };
        }
        
        // MARK: - UI Helper Methods
        
        private void HideLoadingUI()
        {
            if (LoadingIndicator != null)
            {
                LoadingIndicator.StopAnimating();
                LoadingIndicator.Hidden = true;
            }
            
            if (TitleLabel != null)
                TitleLabel.Text = "Select a Credential";
                
            if (SubtitleLabel != null)
                SubtitleLabel.Text = "Choose credentials for this app";
        }
        
        private void ShowCredentialList()
        {
            try
            {
                // Hide any existing UI
                View!.Subviews.ToList().ForEach(v => v.RemoveFromSuperview());
                
                // Set up the main view like Android AutofillBottomSheetFragment
                View!.BackgroundColor = UIColor.SystemBackground;
                
                // Create Fortress header like Android
                SetupAndroidStyleHeader();
                
                // Create credentials table
                SetupCredentialsTable();
                
                // Start OTP timer for live updates
                StartOtpTimerIfNeeded();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show credential list", ex);
                ShowSimpleAlert("Error", "Failed to display credentials");
            }
        }
        
        private void SetupAndroidStyleHeader()
        {
            // Android primary color #407cca
            var primaryColor = UIColor.FromRGB(64, 124, 202);
            
            // Create header container like Android LinearLayout
            var headerContainer = new UIView
            {
                BackgroundColor = UIColor.SystemBackground,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            View!.AddSubview(headerContainer);
            
            // Fortress logo (like Android)
            var logoImage = UIImage.FromBundle("gklogoblue");
            var logoImageView = new UIImageView(logoImage ?? UIImage.GetSystemImage("shield.fill"))
            {
                ContentMode = UIViewContentMode.ScaleAspectFit,
                TintColor = primaryColor,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            headerContainer.AddSubview(logoImageView);
            
            // Fortress title (like Android TextView)
            var titleLabel = new UILabel
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            
            // Create attributed string with letter spacing
            var attributedTitle = new NSMutableAttributedString("FORTRESS");
            attributedTitle.AddAttribute(UIStringAttributeKey.KerningAdjustment, NSNumber.FromFloat(2.0f), new NSRange(0, attributedTitle.Length));
            attributedTitle.AddAttribute(UIStringAttributeKey.Font, UIFont.BoldSystemFontOfSize(22), new NSRange(0, attributedTitle.Length));
            attributedTitle.AddAttribute(UIStringAttributeKey.ForegroundColor, UIColor.Label, new NSRange(0, attributedTitle.Length));
            titleLabel.AttributedText = attributedTitle;
            
            headerContainer.AddSubview(titleLabel);
            
            // Search button (like Android ImageButton with bg_primary_circle)
            var searchButton = new UIButton(UIButtonType.System)
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            searchButton.SetImage(UIImage.GetSystemImage("magnifyingglass"), UIControlState.Normal);
            searchButton.TintColor = UIColor.White;
            searchButton.BackgroundColor = primaryColor;
            searchButton.Layer.CornerRadius = 20;
            searchButton.TouchUpInside += OnSearchTapped;
            headerContainer.AddSubview(searchButton);
            
            // Add button (like Android ImageButton with bg_primary_circle)
            var addButton = new UIButton(UIButtonType.System)
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            addButton.SetImage(UIImage.GetSystemImage("plus"), UIControlState.Normal);
            addButton.TintColor = UIColor.White;
            addButton.BackgroundColor = primaryColor;
            addButton.Layer.CornerRadius = 20;
            addButton.TouchUpInside += OnAddTapped;
            headerContainer.AddSubview(addButton);
            
            // Setup constraints like Android layout
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                // Header container
                headerContainer.TopAnchor.ConstraintEqualTo(View!.SafeAreaLayoutGuide.TopAnchor),
                headerContainer.LeadingAnchor.ConstraintEqualTo(View!.LeadingAnchor),
                headerContainer.TrailingAnchor.ConstraintEqualTo(View!.TrailingAnchor),
                headerContainer.HeightAnchor.ConstraintEqualTo(60),
                
                // Logo (left side like Android)
                logoImageView!.LeadingAnchor.ConstraintEqualTo(headerContainer.LeadingAnchor, 16),
                logoImageView.CenterYAnchor.ConstraintEqualTo(headerContainer.CenterYAnchor),
                logoImageView.WidthAnchor.ConstraintEqualTo(32),
                logoImageView.HeightAnchor.ConstraintEqualTo(32),
                
                // Title (next to logo like Android)
                titleLabel.LeadingAnchor.ConstraintEqualTo(logoImageView!.TrailingAnchor, 8),
                titleLabel.CenterYAnchor.ConstraintEqualTo(headerContainer.CenterYAnchor),
                
                // Add button (far right now that cancel is moved)
                addButton.TrailingAnchor.ConstraintEqualTo(headerContainer.TrailingAnchor, -16),
                addButton.CenterYAnchor.ConstraintEqualTo(headerContainer.CenterYAnchor),
                addButton.WidthAnchor.ConstraintEqualTo(40),
                addButton.HeightAnchor.ConstraintEqualTo(40),
                
                // Search button (next to add button like Android)
                searchButton.TrailingAnchor.ConstraintEqualTo(addButton.LeadingAnchor, -8),
                searchButton.CenterYAnchor.ConstraintEqualTo(headerContainer.CenterYAnchor),
                searchButton.WidthAnchor.ConstraintEqualTo(40),
                searchButton.HeightAnchor.ConstraintEqualTo(40)
            });
            
            // Store reference for later use
            _headerContainer = headerContainer;
            
            // Setup search container (initially hidden)
            SetupSearchBar();
            
            // Setup floating cancel button at bottom center
            SetupFloatingCancelButton();
        }
        
        private void SetupSearchBar()
        {
            var primaryColor = UIColor.FromRGB(64, 124, 202);
            
            // Search container
            _searchContainer = new UIView
            {
                BackgroundColor = UIColor.SystemBackground,
                TranslatesAutoresizingMaskIntoConstraints = false,
                Alpha = 0,
                Hidden = true
            };
            View!.AddSubview(_searchContainer);
            
            // Search text field with modern styling
            _searchTextField = new UITextField
            {
                Placeholder = "Search passwords...",
                BorderStyle = UITextBorderStyle.None,
                BackgroundColor = UIColor.SecondarySystemBackground,
                Font = UIFont.SystemFontOfSize(16),
                ClearButtonMode = UITextFieldViewMode.WhileEditing,
                ReturnKeyType = UIReturnKeyType.Search,
                AutocorrectionType = UITextAutocorrectionType.No,
                AutocapitalizationType = UITextAutocapitalizationType.None,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            _searchTextField.Layer.CornerRadius = 10;
            
            // Add search icon as left view
            var searchIcon = new UIImageView(UIImage.GetSystemImage("magnifyingglass"))
            {
                TintColor = UIColor.PlaceholderText,
                ContentMode = UIViewContentMode.ScaleAspectFit
            };
            var leftContainer = new UIView(new CoreGraphics.CGRect(0, 0, 36, 20));
            searchIcon.Frame = new CoreGraphics.CGRect(12, 0, 20, 20);
            leftContainer.AddSubview(searchIcon);
            _searchTextField.LeftView = leftContainer;
            _searchTextField.LeftViewMode = UITextFieldViewMode.Always;
            
            // Add padding on right
            _searchTextField.RightView = new UIView(new CoreGraphics.CGRect(0, 0, 12, 20));
            _searchTextField.RightViewMode = UITextFieldViewMode.UnlessEditing;
            
            _searchContainer.AddSubview(_searchTextField);
            
            // Cancel search button
            var cancelSearchButton = new UIButton(UIButtonType.System)
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            cancelSearchButton.SetTitle("Cancel", UIControlState.Normal);
            cancelSearchButton.SetTitleColor(primaryColor, UIControlState.Normal);
            cancelSearchButton.TouchUpInside += OnCancelSearchTapped;
            _searchContainer.AddSubview(cancelSearchButton);
            
            // Wire up text changed event
            _searchTextField.EditingChanged += OnSearchTextChanged;
            _searchTextField.ShouldReturn = (textField) =>
            {
                textField.ResignFirstResponder();
                return true;
            };
            
            // Setup constraints
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _searchContainer.TopAnchor.ConstraintEqualTo(_headerContainer!.BottomAnchor),
                _searchContainer.LeadingAnchor.ConstraintEqualTo(View!.LeadingAnchor),
                _searchContainer.TrailingAnchor.ConstraintEqualTo(View!.TrailingAnchor),
                _searchContainer.HeightAnchor.ConstraintEqualTo(56),
                
                _searchTextField.LeadingAnchor.ConstraintEqualTo(_searchContainer.LeadingAnchor, 16),
                _searchTextField.CenterYAnchor.ConstraintEqualTo(_searchContainer.CenterYAnchor),
                _searchTextField.HeightAnchor.ConstraintEqualTo(40),
                
                cancelSearchButton.LeadingAnchor.ConstraintEqualTo(_searchTextField.TrailingAnchor, 12),
                cancelSearchButton.TrailingAnchor.ConstraintEqualTo(_searchContainer.TrailingAnchor, -16),
                cancelSearchButton.CenterYAnchor.ConstraintEqualTo(_searchContainer.CenterYAnchor),
                cancelSearchButton.WidthAnchor.ConstraintEqualTo(60)
            });
        }
        
        private void SetupFloatingCancelButton()
        {
            // Android primary color #407cca
            var primaryColor = UIColor.FromRGB(64, 124, 202);
            
            // Create floating cancel button at bottom center
            var cancelButton = new UIButton(UIButtonType.Custom)
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            
            // Style as a pill-shaped button with "Cancel" text
            cancelButton.SetTitle("Cancel", UIControlState.Normal);
            cancelButton.SetTitleColor(primaryColor, UIControlState.Normal);
            cancelButton.BackgroundColor = UIColor.White;
            cancelButton.TitleLabel!.Font = UIFont.SystemFontOfSize(16, UIFontWeight.Medium);
            cancelButton.Layer.CornerRadius = 22;
            cancelButton.Layer.MasksToBounds = false;
            cancelButton.Layer.ShadowColor = UIColor.Black.CGColor;
            cancelButton.Layer.ShadowOffset = new CoreGraphics.CGSize(0, 2);
            cancelButton.Layer.ShadowOpacity = 0.3f;
            cancelButton.Layer.ShadowRadius = 4;
            cancelButton.ClipsToBounds = false;
            cancelButton.TouchUpInside += OnCancelTapped;
            
            View!.AddSubview(cancelButton);
            
            // Setup constraints for bottom center floating position
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                cancelButton.CenterXAnchor.ConstraintEqualTo(View!.CenterXAnchor),
                cancelButton.BottomAnchor.ConstraintEqualTo(View!.SafeAreaLayoutGuide.BottomAnchor, -20),
                cancelButton.WidthAnchor.ConstraintEqualTo(120),
                cancelButton.HeightAnchor.ConstraintEqualTo(44)
            });
            
            // Store reference so we can bring to front later
            _floatingCancelButton = cancelButton;
        }

        public override void PrepareInterfaceForExtensionConfiguration()
        {
            // This is called when user enables the extension in iOS Settings
            // Save the enabled status so the main app knows
            try
            {
                ErrorLogger.LogInfo("PrepareInterfaceForExtensionConfiguration called - Extension is being enabled");
                SharedPreferences.Instance.SetAutofillEnabled(true);
                
                // Update UI to show success message
                InvokeOnMainThread(() =>
                {
                    if (TitleLabel != null)
                        TitleLabel.Text = "AutoFill Enabled!";
                    if (SubtitleLabel != null)
                        SubtitleLabel.Text = "Fortress is now ready to autofill your passwords";
                    if (LoadingIndicator != null)
                        LoadingIndicator.StopAnimating();
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PrepareInterfaceForExtensionConfiguration failed", ex);
            }
        }

        #region Save Credential (iOS 18+)
        
        /// <summary>
        /// Called by iOS when a website/app wants to save a password credential.
        /// Opens main app to handle the save via OnAutofillRequest.
        /// Available on iOS 18+
        /// </summary>
        [Export("prepareInterfaceForSavePasswordRequest:")]
        public void PrepareInterfaceForSavePasswordRequest(NSObject savePasswordRequest)
        {
            try
            {
                ErrorLogger.LogInfo($"PrepareInterfaceForSavePasswordRequest called");
                
                // Extract credential info using KVC since the types may not be fully bound
                var domain = GetPropertyValue<string>(savePasswordRequest, "credentialIdentity.serviceIdentifier.identifier") ?? "Unknown";
                var credential = GetPropertyValue<NSObject>(savePasswordRequest, "credential");
                var username = GetPropertyValue<string>(credential, "user") ?? "";
                var password = GetPropertyValue<string>(credential, "password") ?? "";
                
                ErrorLogger.LogInfo($"Save request - Domain: {domain}, Username: {username}");
                
                InvokeOnMainThread(() =>
                {
                    // Open main app with the credential data - it will handle UI via OnAutofillRequest
                    OpenMainAppForSave(domain, username, password);
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PrepareInterfaceForSavePasswordRequest failed", ex);
                var error = new NSError(new NSString("ASExtensionErrorDomain"), 
                    Convert.ToInt32(ASExtensionErrorCode.Failed), null);
                ExtensionContext?.CancelRequest(error);
            }
        }
        
        /// <summary>
        /// Called by iOS to attempt saving a password without user interaction.
        /// We always require user interaction so the main app can confirm the save.
        /// </summary>
        [Export("performWithoutUserInteractionIfPossibleForSavePasswordRequest:")]
        public void PerformWithoutUserInteractionIfPossibleForSavePasswordRequest(NSObject savePasswordRequest)
        {
            try
            {
                ErrorLogger.LogInfo("PerformWithoutUserInteractionIfPossibleForSavePasswordRequest called");
                
                // Always require user interaction - main app needs to show add/update credential page
                var error = new NSError(new NSString("ASExtensionErrorDomain"), 
                    Convert.ToInt32(ASExtensionErrorCode.UserInteractionRequired), null);
                ExtensionContext?.CancelRequest(error);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("PerformWithoutUserInteractionIfPossibleForSavePasswordRequest failed", ex);
                var error = new NSError(new NSString("ASExtensionErrorDomain"), 
                    Convert.ToInt32(ASExtensionErrorCode.Failed), null);
                ExtensionContext?.CancelRequest(error);
            }
        }
        
        /// <summary>
        /// Helper to get nested property values using KVC
        /// </summary>
        private T? GetPropertyValue<T>(NSObject? obj, string keyPath) where T : class
        {
            if (obj == null) return null;
            try
            {
                var value = obj.ValueForKeyPath(new NSString(keyPath));
                if (value is T typedValue)
                    return typedValue;
                if (value is NSString nsString && typeof(T) == typeof(string))
                    return nsString.ToString() as T;
                return value?.ToString() as T;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Opens the main Fortress app to handle saving the credential.
        /// The main app's OnAutofillRequest will show the Add/Update credential page.
        /// </summary>
        private void OpenMainAppForSave(string domain, string username, string password)
        {
            try
            {
                ErrorLogger.LogInfo($"Opening main app to save credential for {domain}");
                
                // Build URL with credential data
                var url = new NSUrl($"fortress://autofill?action=save&domain={Uri.EscapeDataString(domain)}&name={Uri.EscapeDataString(ExtractFriendlyName(domain) ?? domain)}&username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}");

                // Try to open main app
                var opened = this.OpenUrlViaResponderChain(url);
                
                if (opened)
                {
                    ErrorLogger.LogInfo("Opened main app to complete save");
                    
                    // Complete the extension request successfully
                    var credentialExtensionContext = ExtensionContext as ASCredentialProviderExtensionContext;
                    credentialExtensionContext?.CompleteRequest(new NSExtensionItem[] { }, null);
                }
                else
                {
                    ErrorLogger.LogError("Failed to open main app via responder chain");
                    ShowSimpleAlert("Error", "Could not open Fortress. Please open the app manually to save this credential.");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OpenMainAppForSave failed", ex);
                var error = new NSError(new NSString("ASExtensionErrorDomain"), 
                    Convert.ToInt32(ASExtensionErrorCode.Failed), null);
                ExtensionContext?.CancelRequest(error);
            }
        }
        
        #endregion

        async void OnCredentialSelected(AutofillCredential cred)
        {
            try
            {
                ErrorLogger.LogInfo($"Credential selected: {cred?.Username ?? "null"}");
                
                if (cred == null)
                {
                    ErrorLogger.LogError("Selected credential is null");
                    ShowErrorAndCancel("Invalid Selection", "The selected credential is invalid.");
                    return;
                }
                
                if (string.IsNullOrEmpty(cred.Username) || string.IsNullOrEmpty(cred.Password))
                {
                    ErrorLogger.LogError($"Invalid credential data - Username: {!string.IsNullOrEmpty(cred.Username)}, Password: {!string.IsNullOrEmpty(cred.Password)}");
                    ShowErrorAndCancel("Invalid Credential", "The credential data is incomplete.");
                    return;
                }
                
                // Log credential usage (like Android OnCredentialUsed)
                // This will be processed by the main app on next launch
                _ = CredentialUsageLogger.LogCredentialUsedAsync(cred.Id, cred.CredentialType, cred.Domain);
                
                // Copy OTP to clipboard if credential has OTP and show snackbar briefly
                if (cred.HasOtp && !string.IsNullOrEmpty(cred.Code))
                {
                    UIPasteboard.General.String = cred.Code;
                    ShowSnackbar("OTP copied to clipboard");
                    
                    // Delay to let user see the snackbar before extension closes
                    await Task.Delay(500);
                }
                
                var passwordCredential = new ASPasswordCredential(cred.Username, cred.Password);
                ExtensionContext.CompleteRequest(passwordCredential, null);
                ErrorLogger.LogInfo("Successfully completed credential request");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("OnCredentialSelected failed", ex);
                ShowErrorAndCancel("Error", "Failed to provide credential. Please try again.");
            }
        }
        
        public override void ViewDidLoad()
        {
            try
            {
                base.ViewDidLoad();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ViewDidLoad failed", ex);
                base.ViewDidLoad();
            }
        }
        
        public override void ViewWillAppear(bool animated)
        {
            try
            {
                base.ViewWillAppear(animated);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ViewWillAppear failed", ex);
                base.ViewWillAppear(animated);
            }
        }
        
        public override void ViewDidAppear(bool animated)
        {
            try
            {
                base.ViewDidAppear(animated);
                StartOtpTimerIfNeeded();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ViewDidAppear failed", ex);
                base.ViewDidAppear(animated);
            }
        }
        
        public override void ViewWillDisappear(bool animated)
        {
            try
            {
                StopOtpTimer();
                base.ViewWillDisappear(animated);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ViewWillDisappear failed", ex);
                base.ViewWillDisappear(animated);
            }
        }





        private NSTimer? _otpTimer;

        private void StartOtpTimerIfNeeded()
        {
            try
            {
                if (_credentials == null) return;
                
                var otpCredentials = _credentials.Where(c => c.HasOtp && !string.IsNullOrEmpty(c.Data)).ToList();
                if (!otpCredentials.Any()) return;

                // Generate initial OTP codes immediately
                foreach (var c in otpCredentials)
                {
                    var totp = OtpHelper.GenerateOtp(c.Data);
                    c.Progress = totp.RemainingSeconds;
                    c.Code = totp.Code;
                }

                // Create timer to update OTP every second (like Android)
                _otpTimer = NSTimer.CreateRepeatingScheduledTimer(1, _ =>
                {
                    try
                    {
                        InvokeOnMainThread(() =>
                        {
                            foreach (var c in _credentials.Where(x => x.HasOtp && !string.IsNullOrEmpty(x.Data)))
                            {
                                // Generate real OTP using same logic as Android
                                var totp = OtpHelper.GenerateOtp(c.Data );
                                c.Progress = totp.RemainingSeconds;
                                c.Code = totp.Code;
                            }
                            
                            // Update visible cells without reloading entire table
                            if (_credentialsTable != null)
                            {
                                foreach (var cell in _credentialsTable.VisibleCells)
                                {
                                    if (cell is CredentialCell credCell)
                                    {
                                        var indexPath = _credentialsTable.IndexPathForCell(cell);
                                        if (indexPath != null && indexPath.Row < _filteredCredentials?.Count)
                                        {
                                            var cred = _filteredCredentials[indexPath.Row];
                                            credCell.UpdateOtp(cred.Code, cred.Progress);
                                        }
                                    }
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        ErrorLogger.LogError("OTP timer tick failed", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("StartOtpTimerIfNeeded failed", ex);
            }
        }

        private void StopOtpTimer()
        {
            try
            {
                if (_otpTimer != null)
                {
                    _otpTimer.Invalidate();
                    _otpTimer = null;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("StopOtpTimer failed", ex);
                _otpTimer = null;
            }
        }
        private void ShowErrorAndCancel(string title, string message)
        {
            try
            {
                ErrorLogger.LogError($"Showing error alert: {title} - {message}");
                
                var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
                
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ =>
                {
                    ExtensionContext.CancelRequest(null!);
                }));
                
                // Add a "View Logs" option for debugging
                alert.AddAction(UIAlertAction.Create("View Logs", UIAlertActionStyle.Default, _ =>
                {
                    ShowLogsAlert();
                }));
                
                InvokeOnMainThread(() =>
                {
                    PresentViewController(alert, true, null);
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show error alert", ex);
                // Fallback - just cancel the request
                ExtensionContext.CancelRequest(null!);
            }
        }
        
        private void ShowLogsAlert()
        {
            try
            {
                var logs = ErrorLogger.GetLogContents();
                var alert = UIAlertController.Create("Extension Logs", logs, UIAlertControllerStyle.Alert);
                
                alert.AddAction(UIAlertAction.Create("Close", UIAlertActionStyle.Default, null));
                alert.AddAction(UIAlertAction.Create("Clear Logs", UIAlertActionStyle.Destructive, _ =>
                {
                    ErrorLogger.ClearLogs();
                }));
                
                InvokeOnMainThread(() =>
                {
                    PresentViewController(alert, true, null);
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show logs alert", ex);
            }
        }

        private void ShowLockedUI()
        {
            try
            {
                ErrorLogger.LogInfo("App is locked - showing locked UI");
                
                // Hide any existing UI
                View!.Subviews.ToList().ForEach(v => v.RemoveFromSuperview());
                
                View!.BackgroundColor = UIColor.SystemBackground;
                
                // Android primary color #407cca
                var primaryColor = UIColor.FromRGB(64, 124, 202);
                
                // Create main container for centering
                var containerView = new UIView
                {
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                View!.AddSubview(containerView);
                
                // Fortress logo
                var logoImage = UIImage.FromBundle("gklogoblue");
                var lockIcon = new UIImageView(logoImage ?? UIImage.GetSystemImage("lock.shield.fill"))
                {
                    ContentMode = UIViewContentMode.ScaleAspectFit,
                    TintColor = primaryColor,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                containerView.AddSubview(lockIcon);
                
                // Fortress title with letter spacing
                var titleLabel = new UILabel
                {
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                var attributedTitle = new NSMutableAttributedString("FORTRESS");
                attributedTitle.AddAttribute(UIStringAttributeKey.KerningAdjustment, NSNumber.FromFloat(2.0f), new NSRange(0, attributedTitle.Length));
                attributedTitle.AddAttribute(UIStringAttributeKey.Font, UIFont.BoldSystemFontOfSize(24), new NSRange(0, attributedTitle.Length));
                attributedTitle.AddAttribute(UIStringAttributeKey.ForegroundColor, UIColor.Label, new NSRange(0, attributedTitle.Length));
                titleLabel.AttributedText = attributedTitle;
                titleLabel.TextAlignment = UITextAlignment.Center;
                containerView.AddSubview(titleLabel);
                
                // Locked message
                var messageLabel = new UILabel
                {
                    Text = "Fortress is locked.\nPlease unlock the app to access your passwords",
                    TextColor = UIColor.SecondaryLabel,
                    Font = UIFont.SystemFontOfSize(16),
                    TextAlignment = UITextAlignment.Center,
                    Lines = 0,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                containerView.AddSubview(messageLabel);
                
                // Unlock button (styled like Android primary button)
                var unlockButton = new UIButton(UIButtonType.System)
                {
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                unlockButton.SetTitle("Unlock Fortress", UIControlState.Normal);
                unlockButton.SetTitleColor(UIColor.White, UIControlState.Normal);
                unlockButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(17);
                unlockButton.BackgroundColor = primaryColor;
                unlockButton.Layer.CornerRadius = 12;
                unlockButton.TouchUpInside += (s, e) => OpenMainApp();
                containerView.AddSubview(unlockButton);
                
                // Cancel button (secondary style)
                var cancelButton = new UIButton(UIButtonType.System)
                {
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                cancelButton.SetTitle("Cancel", UIControlState.Normal);
                cancelButton.SetTitleColor(primaryColor, UIControlState.Normal);
                cancelButton.TitleLabel!.Font = UIFont.SystemFontOfSize(17);
                cancelButton.TouchUpInside += (s, e) => ExtensionContext?.CancelRequest(null!);
                containerView.AddSubview(cancelButton);
                
                // Setup constraints
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    // Container centered in view
                    containerView.CenterXAnchor.ConstraintEqualTo(View!.CenterXAnchor),
                    containerView.CenterYAnchor.ConstraintEqualTo(View!.CenterYAnchor, -40),
                    containerView.LeadingAnchor.ConstraintEqualTo(View!.LeadingAnchor, 32),
                    containerView.TrailingAnchor.ConstraintEqualTo(View!.TrailingAnchor, -32),
                    
                    // Lock icon at top of container
                    lockIcon.TopAnchor.ConstraintEqualTo(containerView.TopAnchor),
                    lockIcon.CenterXAnchor.ConstraintEqualTo(containerView.CenterXAnchor),
                    lockIcon.WidthAnchor.ConstraintEqualTo(80),
                    lockIcon.HeightAnchor.ConstraintEqualTo(80),
                    
                    // Title below lock icon
                    titleLabel.TopAnchor.ConstraintEqualTo(lockIcon.BottomAnchor, 20),
                    titleLabel.CenterXAnchor.ConstraintEqualTo(containerView.CenterXAnchor),
                    
                    // Message below title
                    messageLabel.TopAnchor.ConstraintEqualTo(titleLabel.BottomAnchor, 12),
                    messageLabel.LeadingAnchor.ConstraintEqualTo(containerView.LeadingAnchor),
                    messageLabel.TrailingAnchor.ConstraintEqualTo(containerView.TrailingAnchor),
                    
                    // Unlock button below message
                    unlockButton.TopAnchor.ConstraintEqualTo(messageLabel.BottomAnchor, 32),
                    unlockButton.LeadingAnchor.ConstraintEqualTo(containerView.LeadingAnchor),
                    unlockButton.TrailingAnchor.ConstraintEqualTo(containerView.TrailingAnchor),
                    unlockButton.HeightAnchor.ConstraintEqualTo(50),
                    
                    // Cancel button below unlock button
                    cancelButton.TopAnchor.ConstraintEqualTo(unlockButton.BottomAnchor, 12),
                    cancelButton.CenterXAnchor.ConstraintEqualTo(containerView.CenterXAnchor),
                    cancelButton.BottomAnchor.ConstraintEqualTo(containerView.BottomAnchor)
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show locked UI", ex);
                ExtensionContext?.CancelRequest(null!);
            }
        }
        
        private void CopyOtpToClipboard(AutofillCredential cred)
        {
            try
            {
                ErrorLogger.LogInfo($"CopyOtpToClipboard called for credential: {cred?.Username}");
                
                //if (cred == null || !cred.HasOtp || string.IsNullOrWhiteSpace(cred.Data))
                //{
                //    ErrorLogger.LogInfo("OTP copy skipped - invalid credential or no OTP data");
                //    return;
                //}

                //try
                //{
                //    var totp = OtpHelper.GenerateOtp(cred.Data);

                //    if (string.IsNullOrWhiteSpace(totp.Code))
                //    {
                //        ErrorLogger.LogError("Generated OTP code is empty");
                //        return;
                //    }

                //    UIPasteboard.General.String = totp.Code;
                //    ErrorLogger.LogInfo("OTP copied to clipboard successfully");
                //}
                //catch (Exception ex)
                //{
                //    ErrorLogger.LogError("Failed to generate or copy OTP", ex);
                //}
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CopyOtpToClipboard failed", ex);
                // ❌ Never throw in an extension - Apple will silently kill it
            }
        }
        
      
        
        private void ShowSimpleAlert(string title, string message)
        {
            try
            {
                ErrorLogger.LogInfo($"📱 Showing alert: {title} - {message}");
                
                InvokeOnMainThread(() =>
                {
                    var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
                    
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ =>
                    {
                        ErrorLogger.LogInfo("User dismissed alert, cancelling extension");
                        ExtensionContext.CancelRequest(null!);
                    }));
                    
                    // Add "View Logs" button to see what happened
                    alert.AddAction(UIAlertAction.Create("View Logs", UIAlertActionStyle.Default, _ =>
                    {
                        ShowLogsInAlert();
                    }));
                    
                    PresentViewController(alert, true, () =>
                    {
                        ErrorLogger.LogInfo("Alert presented successfully");
                    });
                });
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show simple alert", ex);
                ExtensionContext.CancelRequest(null!);
            }
        }
        
        private void ShowSnackbar(string message)
        {
            InvokeOnMainThread(() =>
            {
                if (View == null) return;
                
                // Create snackbar container
                var snackbar = new UIView
                {
                    BackgroundColor = UIColor.FromRGB(50, 50, 50),
                    Alpha = 0,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                snackbar.Layer.CornerRadius = 8;
                
                // Add label
                var label = new UILabel
                {
                    Text = message,
                    TextColor = UIColor.White,
                    Font = UIFont.SystemFontOfSize(14, UIFontWeight.Medium),
                    TextAlignment = UITextAlignment.Center,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };
                snackbar.AddSubview(label);
                View.AddSubview(snackbar);
                
                // Setup constraints
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    snackbar.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 20),
                    snackbar.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                    snackbar.HeightAnchor.ConstraintEqualTo(44),
                    snackbar.LeadingAnchor.ConstraintGreaterThanOrEqualTo(View.LeadingAnchor, 20),
                    snackbar.TrailingAnchor.ConstraintLessThanOrEqualTo(View.TrailingAnchor, -20),
                    
                    label.LeadingAnchor.ConstraintEqualTo(snackbar.LeadingAnchor, 16),
                    label.TrailingAnchor.ConstraintEqualTo(snackbar.TrailingAnchor, -16),
                    label.CenterYAnchor.ConstraintEqualTo(snackbar.CenterYAnchor)
                });
                
                // Animate in
                UIView.Animate(0.3, () =>
                {
                    snackbar.Alpha = 1;
                });
                
                // Auto dismiss after 2 seconds
                Task.Delay(2000).ContinueWith(_ =>
                {
                    InvokeOnMainThread(() =>
                    {
                        UIView.Animate(0.3, () =>
                        {
                            snackbar.Alpha = 0;
                        }, () =>
                        {
                            snackbar.RemoveFromSuperview();
                        });
                    });
                });
            });
        }
        
        private void ShowLogsInAlert()
        {
            try
            {
                var logs = ErrorLogger.GetLogContents();
                var alert = UIAlertController.Create("Extension Logs", logs, UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("Close", UIAlertActionStyle.Default, null));
                PresentViewController(alert, true, null);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show logs alert", ex);
            }
        }
        
        // IBAction methods required by the storyboard
        [Export("cancel:")]
        public void Cancel(NSObject sender)
        {
            Console.WriteLine("🚫 [AUTOFILL] Cancel button tapped");
            ErrorLogger.LogInfo("Cancel button tapped - cancelling request");
            ExtensionContext.CancelRequest(null!);
        }
        
        [Export("passwordSelected:")]
        public void PasswordSelected(NSObject sender)
        {
            Console.WriteLine("🔑 [AUTOFILL] Password selected button tapped");
            ErrorLogger.LogInfo("Password selected button tapped - providing example password");
            
            // Provide a test credential
            var testUsername = "testuser@example.com";
            var testPassword = "TestPassword123!";
            
            CompleteRequest(testUsername, testPassword);
        }
        
        // Helper method to complete the extension request
        private void CompleteRequest(string username, string password)
        {
            try
            {
                ErrorLogger.LogInfo($"CompleteRequest called with username: {username}");
                var passwordCredential = new ASPasswordCredential(username, password);
                ExtensionContext.CompleteRequest(passwordCredential, null);
                ErrorLogger.LogInfo("Successfully completed credential request");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CompleteRequest failed", ex);
                ExtensionContext.CancelRequest(null!);
            }
        }
        
        private void SetupCredentialsTable()
        {
            // Create table view like Android RecyclerView
            var tableView = new UITableView
            {
                BackgroundColor = UIColor.SystemBackground,
                SeparatorStyle = UITableViewCellSeparatorStyle.None,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            
            // Register cell like Android adapter
            tableView.RegisterClassForCellReuse(typeof(CredentialCell), CredentialCell.Key);
            
            // Initialize filtered credentials with all credentials
            _filteredCredentials = _credentials ?? new List<AutofillCredential>();
            
            // Set data source
            _credentialSource = new CredentialSource(_filteredCredentials, OnCredentialSelected);
            tableView.Source = _credentialSource;
            
            View!.AddSubview(tableView);
            
            // Setup constraints
            if (_headerContainer != null)
            {
                _tableTopConstraint = tableView.TopAnchor.ConstraintEqualTo(_headerContainer.BottomAnchor, 8);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    _tableTopConstraint,
                    tableView!.LeadingAnchor.ConstraintEqualTo(View!.LeadingAnchor),
                    tableView!.TrailingAnchor.ConstraintEqualTo(View!.TrailingAnchor),
                    tableView!.BottomAnchor.ConstraintEqualTo(View!.BottomAnchor)
                });
            }
            
            _credentialsTable = tableView;
            
            // Bring floating cancel button to front after table is added
            if (_floatingCancelButton != null)
            {
                View!.BringSubviewToFront(_floatingCancelButton);
            }
        }
        
        // MARK: - Action Methods (Android-style)
        
        private void OnAddTapped(object? sender, EventArgs e)
        {
            ErrorLogger.LogInfo("Add button tapped - opening main app to add credential");
            OpenMainAppForAddCredential();
        }
        
        private void OnSearchTapped(object? sender, EventArgs e)
        {
            ErrorLogger.LogInfo("Search button tapped");
            ToggleSearchBar(true);
        }
        
        private void OnCancelSearchTapped(object? sender, EventArgs e)
        {
            ErrorLogger.LogInfo("Cancel search tapped");
            ToggleSearchBar(false);
        }
        
        private void ToggleSearchBar(bool show)
        {
            if (_searchContainer == null || _searchTextField == null) return;
            
            _isSearchVisible = show;
            
            if (show)
            {
                _searchContainer.Hidden = false;
                _searchTextField.Text = string.Empty;
                
                // Update table constraint to be below search bar
                if (_tableTopConstraint != null && _searchContainer != null)
                {
                    _tableTopConstraint.Active = false;
                    _tableTopConstraint = _credentialsTable?.TopAnchor.ConstraintEqualTo(_searchContainer.BottomAnchor, 8);
                    _tableTopConstraint?.Active = true;
                }
                
                UIView.Animate(0.3, () =>
                {
                    _searchContainer!.Alpha = 1;
                    View!.LayoutIfNeeded();
                }, () =>
                {
                    _searchTextField!.BecomeFirstResponder();
                });
            }
            else
            {
                _searchTextField.ResignFirstResponder();
                _searchTextField.Text = string.Empty;
                
                // Reset filter to show all credentials
                FilterCredentials(string.Empty);
                
                UIView.Animate(0.3, () =>
                {
                    _searchContainer!.Alpha = 0;
                    
                    // Update table constraint back to header
                    if (_tableTopConstraint != null && _headerContainer != null)
                    {
                        _tableTopConstraint.Active = false;
                        _tableTopConstraint = _credentialsTable?.TopAnchor.ConstraintEqualTo(_headerContainer.BottomAnchor, 8);
                        _tableTopConstraint?.Active = true;
                    }
                    
                    View!.LayoutIfNeeded();
                }, () =>
                {
                    _searchContainer!.Hidden = true;
                });
            }
        }
        
        private void OnSearchTextChanged(object? sender, EventArgs e)
        {
            var searchText = _searchTextField?.Text ?? string.Empty;
            FilterCredentials(searchText);
        }
        
        private void FilterCredentials(string searchText)
        {
            if (_credentials == null || _credentialSource == null || _credentialsTable == null) return;
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredCredentials = _credentials;
            }
            else
            {
                var lowerSearch = searchText.ToLowerInvariant();
                _filteredCredentials = _credentials
                    .Where(c => 
                        (c.Domain?.ToLowerInvariant().Contains(lowerSearch) ?? false) ||
                        (c.Username?.ToLowerInvariant().Contains(lowerSearch) ?? false))
                    .ToList();
            }
            
            _credentialSource.UpdateCredentials(_filteredCredentials);
            _credentialsTable.ReloadData();
            
            ErrorLogger.LogInfo($"Filtered credentials: {_filteredCredentials.Count} of {_credentials.Count}");
        }
        
        private void OnCancelTapped(object? sender, EventArgs e)
        {
            ErrorLogger.LogInfo("Cancel button tapped - dismissing extension");
            ExtensionContext.CancelRequest(null!);
        }
        
        private void OpenMainApp()
        {
            OpenMainAppWithContext(isAddContext: false);
        }
        
        private void OpenMainAppForAddCredential()
        {
            OpenMainAppWithContext(isAddContext: true);
        }
        
        private void OpenMainAppWithContext(bool isAddContext)
        {
            try
            {
                // Build URL with query parameters like Android does with Intent extras
                // Format: fortress://autofill?action=add&domain=example.com&name=Example
                var urlBuilder = new System.Text.StringBuilder("fortress://autofill");

                var queryParams = new List<string>();
                
                if (isAddContext)
                {
                    queryParams.Add("action=add");
                }
                else
                {
                    queryParams.Add("action=unlock");
                }
                
                // Add the current domain/package info
                if (!string.IsNullOrEmpty(_currentDomain))
                {
                    var encodedDomain = Uri.EscapeDataString(_currentDomain);
                    queryParams.Add($"domain={encodedDomain}");
                    
                    // Extract a friendly name from the domain
                    var friendlyName = ExtractFriendlyName(_currentDomain);
                    if (!string.IsNullOrEmpty(friendlyName))
                    {
                        var encodedName = Uri.EscapeDataString(friendlyName);
                        queryParams.Add($"name={encodedName}");
                    }
                }
                
                if (queryParams.Count > 0)
                {
                    urlBuilder.Append("?");
                    urlBuilder.Append(string.Join("&", queryParams));
                }
                
                var urlString = urlBuilder.ToString();
                ErrorLogger.LogInfo($"Opening main app with URL: {urlString}");
                
                var url = new NSUrl(urlString);
                
                // Try the responder chain approach first - works on all iOS versions
                // This walks up the responder chain to find UIApplication
                ErrorLogger.LogInfo("Attempting to open URL via responder chain...");
                
                var opened = this.OpenUrlViaResponderChain(url);
                ErrorLogger.LogInfo($"OpenUrlViaResponderChain result: {opened}");
                
                if (opened)
                {
                    ErrorLogger.LogInfo("Successfully opened main app via responder chain");
                    // Small delay then dismiss extension
                    Task.Delay(100).ContinueWith(_ =>
                    {
                        InvokeOnMainThread(() =>
                        {
                            ExtensionContext?.CancelRequest(null!);
                        });
                    });
                    return;
                }
                
                // Fallback: Try iOS 17+ ASCredentialProviderExtensionContext.openURL API
                var credentialExtensionContext = ExtensionContext as ASCredentialProviderExtensionContext;
                
                ErrorLogger.LogInfo($"Responder chain failed, trying iOS 17+ API...");
                ErrorLogger.LogInfo($"credentialExtensionContext is null: {credentialExtensionContext == null}");
                ErrorLogger.LogInfo($"iOS version (17.0+): {UIDevice.CurrentDevice.CheckSystemVersion(17, 0)}");
                
                if (credentialExtensionContext != null && UIDevice.CurrentDevice.CheckSystemVersion(17, 0))
                {
                    var selector = new ObjCRuntime.Selector("openURL:completion:");
                    var respondsToSelector = credentialExtensionContext.RespondsToSelector(selector);
                    ErrorLogger.LogInfo($"RespondsToSelector(openURL:completion:): {respondsToSelector}");
                    
                    if (respondsToSelector)
                    {
                        ErrorLogger.LogInfo("Calling OpenUrl via P/Invoke...");
                        
                        try
                        {
                            credentialExtensionContext.OpenUrl(url, (success) =>
                            {
                                InvokeOnMainThread(() =>
                                {
                                    ErrorLogger.LogInfo($"OpenUrl completion: {success}");
                                    if (success)
                                    {
                                        Task.Delay(100).ContinueWith(_ =>
                                        {
                                            InvokeOnMainThread(() =>
                                            {
                                                ExtensionContext?.CancelRequest(null!);
                                            });
                                        });
                                    }
                                    else
                                    {
                                        ShowOpenAppInstructionsAlert(isAddContext);
                                    }
                                });
                            });
                            return;
                        }
                        catch (Exception ex)
                        {
                            ErrorLogger.LogError($"OpenUrl P/Invoke failed", ex);
                        }
                    }
                }
                
                // All methods failed - show manual instructions
                ErrorLogger.LogInfo("All URL opening methods failed, showing manual instructions");
                ShowOpenAppInstructionsAlert(isAddContext);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Error opening main app", ex);
                ShowSimpleAlert("Error", "Failed to open main app");
            }
        }
        
        /// <summary>
        /// Shows an alert with instructions to open the main app manually
        /// Used when OpenUrl is not available (iOS 16) or fails
        /// </summary>
        private void ShowOpenAppInstructionsAlert(bool isAddContext)
        {
            try
            {
                var title = isAddContext ? "Add New Password" : "Unlock Fortress";
                var message = isAddContext
                    ? "Please open the Fortress app to add a new password, then return here."
                    : "Please open the Fortress app and unlock it, then return here to use autofill.";
                
                var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
                
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ =>
                {
                    // Cancel the extension request so user can go open the app
                    ExtensionContext.CancelRequest(null!);
                }));
                
                // Add View Logs button to debug why OpenUrl failed
                alert.AddAction(UIAlertAction.Create("View Logs", UIAlertActionStyle.Default, _ =>
                {
                    ShowLogsAlert();
                }));
                
                PresentViewController(alert, true, null);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Failed to show open app instructions", ex);
                ExtensionContext.CancelRequest(null!);
            }
        }
        
        /// <summary>
        /// Extracts a user-friendly name from a domain/URL
        /// </summary>
        private string ExtractFriendlyName(string domain)
        {
            try
            {
                // Remove scheme
                if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    domain = domain.Substring(7);
                else if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    domain = domain.Substring(8);
                
                // Remove www
                if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    domain = domain.Substring(4);
                
                // Remove path
                var slashIndex = domain.IndexOf('/');
                if (slashIndex > 0)
                    domain = domain.Substring(0, slashIndex);
                
                // Get the main domain part (e.g., "github" from "github.com")
                var parts = domain.Split('.');
                if (parts.Length >= 2)
                {
                    // Capitalize the first letter
                    var name = parts[0];
                    if (!string.IsNullOrEmpty(name))
                    {
                        return char.ToUpper(name[0]) + name.Substring(1);
                    }
                }
                
                return domain;
            }
            catch
            {
                return domain;
            }
        }
    }
}
