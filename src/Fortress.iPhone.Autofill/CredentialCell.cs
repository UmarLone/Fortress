using Foundation;
using System;
using System.Threading.Tasks;
using UIKit;
using CoreGraphics;
using CoreAnimation;

namespace Fortress.iPhone.Autofill
{
    public class CredentialCell : UITableViewCell
    {
        public static readonly NSString Key = new(nameof(CredentialCell));

        UIView? _iconContainer;
        UIImageView? _iconImageView;
        UILabel? _domainLabel;
        UILabel? _usernameLabel;
        UILabel? _otpLabel;
        UIView? _circularProgress;
        CAShapeLayer? _progressLayer;
        CAShapeLayer? _trackLayer;
        UIButton? _optionsButton;
        UIView? _divider;

        public CredentialCell(IntPtr handle) : base(handle)
        {
            try
            {
                SetupCell();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CredentialCell constructor failed", ex);
                SetupFallbackCell();
            }
        }
        
        private void SetupCell()
        {
            SelectionStyle = UITableViewCellSelectionStyle.Default;
            BackgroundColor = UIColor.SystemBackground;

            // Icon Container (circular like Android MaterialCardView)
            _iconContainer = new UIView
            {
                BackgroundColor = UIColor.White,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            _iconContainer.Layer.CornerRadius = 20;
            _iconContainer.Layer.BorderWidth = 1;
            _iconContainer.Layer.BorderColor = UIColor.LightGray.CGColor;
            ContentView.AddSubview(_iconContainer);

            // Icon (lock like Android)
            _iconImageView = new UIImageView(UIImage.GetSystemImage("lock.fill"))
            {
                TintColor = UIColor.SystemGray,
                ContentMode = UIViewContentMode.ScaleAspectFit,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            _iconContainer.AddSubview(_iconImageView);

            // Domain label (bold like Android)
            _domainLabel = new UILabel
            {
                Font = UIFont.BoldSystemFontOfSize(15),
                TextColor = UIColor.Label,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            ContentView.AddSubview(_domainLabel);

            // Username label (smaller, gray like Android)
            _usernameLabel = new UILabel
            {
                Font = UIFont.SystemFontOfSize(13),
                TextColor = UIColor.SystemGray,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            ContentView.AddSubview(_usernameLabel);

            // OTP Label (large, bold, primary color like Android textColor="@color/colorPrimary")
            _otpLabel = new UILabel
            {
                Font = UIFont.BoldSystemFontOfSize(22),
                TextColor = UIColor.FromRGB(64, 124, 202), // Android #407cca
                TranslatesAutoresizingMaskIntoConstraints = false,
                Hidden = true
            };
            ContentView.AddSubview(_otpLabel);

            // Circular Progress Container (like Android CircularProgressIndicator)
            _circularProgress = new UIView
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                Hidden = true
            };
            ContentView.AddSubview(_circularProgress);

            SetupCircularProgress();

            // Options Button (three dots like Android ic_more_vert)
            _optionsButton = new UIButton(UIButtonType.System)
            {
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            // Use ellipsis icon rotated 90 degrees to get vertical dots like Android
            var ellipsisImage = UIImage.GetSystemImage("ellipsis");
            _optionsButton.SetImage(ellipsisImage, UIControlState.Normal);
            _optionsButton.Transform = CoreGraphics.CGAffineTransform.MakeRotation((nfloat)(Math.PI / 2)); // 90 degrees
            _optionsButton.TintColor = UIColor.FromRGB(102, 102, 102); // #666666 like Android
            _optionsButton.ShowsMenuAsPrimaryAction = true;
            // Menu will be set when configuring cell with credential
            ContentView.AddSubview(_optionsButton);
            
            // Horizontal divider (like Android divider at bottom)
            _divider = new UIView
            {
                BackgroundColor = UIColor.FromRGB(224, 224, 224), // #E0E0E0 like Android
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            ContentView.AddSubview(_divider);

            SetupConstraints();
        }

        // Android primary color #407cca
        private static UIColor PrimaryColor => UIColor.FromRGB(64, 124, 202);
        
        private void SetupCircularProgress()
        {
            if (_circularProgress == null) return;

            // Track layer (background circle like Android trackColor="#E0E0E0")
            _trackLayer = new CAShapeLayer();
            var center = new CGPoint(16, 16);
            var radius = 14f;
            var circlePath = UIBezierPath.FromArc(center, radius, 0, (nfloat)(2 * Math.PI), true);
            _trackLayer.Path = circlePath.CGPath;
            _trackLayer.FillColor = UIColor.Clear.CGColor;
            _trackLayer.StrokeColor = UIColor.FromRGB(224, 224, 224).CGColor; // #E0E0E0
            _trackLayer.LineWidth = 4;
            _circularProgress.Layer.AddSublayer(_trackLayer);

            // Progress layer (foreground circle like Android indicatorColor="@color/colorPrimary")
            _progressLayer = new CAShapeLayer();
            _progressLayer.Path = circlePath.CGPath;
            _progressLayer.FillColor = UIColor.Clear.CGColor;
            _progressLayer.StrokeColor = PrimaryColor.CGColor;
            _progressLayer.LineWidth = 4;
            _progressLayer.LineCap = CAShapeLayer.CapRound;
            _progressLayer.StrokeEnd = 0;
            _circularProgress.Layer.AddSublayer(_progressLayer);
        }

        private UIMenu CreateOptionsMenu(AutofillCredential? credential)
        {
            var copyUsernameAction = UIAction.Create(
                "Copy Username",
                UIImage.GetSystemImage("person.circle"),
                null,
                _ => CopyUsername());
            
            var copyPasswordAction = UIAction.Create(
                "Copy Password",
                UIImage.GetSystemImage("key.fill"),
                null,
                _ => CopyPassword());
            
            if (credential?.HasOtp == true && !string.IsNullOrEmpty(credential.Code))
            {
                var copyOtpAction = UIAction.Create(
                    "Copy OTP",
                    UIImage.GetSystemImage("lock.circle"),
                    null,
                    _ => CopyOtp());
                return UIMenu.Create(new[] { copyOtpAction, copyUsernameAction, copyPasswordAction });
            }
            return UIMenu.Create(new[] { copyUsernameAction, copyPasswordAction });
        }
        
        private void CopyOtp()
        {
            var credential = GetCredential();
            if (credential?.HasOtp == true && !string.IsNullOrEmpty(credential.Code))
            {
                UIPasteboard.General.String = credential.Code;
                ShowCopyFeedback($"OTP code copied");
            }
        }
        
        private void CopyUsername()
        {
            var credential = GetCredential();
            if (!string.IsNullOrEmpty(credential?.Username))
            {
                UIPasteboard.General.String = credential.Username;
                ShowCopyFeedback("Username copied");
            }
        }
        
        private void CopyPassword()
        {
            var credential = GetCredential();
            if (!string.IsNullOrEmpty(credential?.Password))
            {
                UIPasteboard.General.String = credential.Password;
                ShowCopyFeedback("Password copied");
            }
        }
        
        private void ShowCopyFeedback(string message)
        {
            var viewController = this.FindViewController();
            if (viewController?.View == null) return;
            
            // Create snackbar-style toast
            var snackbar = new UIView
            {
                BackgroundColor = UIColor.FromRGB(50, 50, 50),
                Alpha = 0,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            snackbar.Layer.CornerRadius = 8;
            
            var label = new UILabel
            {
                Text = message,
                TextColor = UIColor.White,
                Font = UIFont.SystemFontOfSize(14),
                TextAlignment = UITextAlignment.Center,
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            snackbar.AddSubview(label);
            viewController.View.AddSubview(snackbar);
            
            // Setup constraints
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                snackbar.BottomAnchor.ConstraintEqualTo(viewController.View.SafeAreaLayoutGuide.BottomAnchor, -20),
                snackbar.CenterXAnchor.ConstraintEqualTo(viewController.View.CenterXAnchor),
                snackbar.HeightAnchor.ConstraintEqualTo(44),
                snackbar.LeadingAnchor.ConstraintGreaterThanOrEqualTo(viewController.View.LeadingAnchor, 20),
                snackbar.TrailingAnchor.ConstraintLessThanOrEqualTo(viewController.View.TrailingAnchor, -20),
                
                label.LeadingAnchor.ConstraintEqualTo(snackbar.LeadingAnchor, 16),
                label.TrailingAnchor.ConstraintEqualTo(snackbar.TrailingAnchor, -16),
                label.CenterYAnchor.ConstraintEqualTo(snackbar.CenterYAnchor)
            });
            
            // Animate in
            UIView.Animate(0.3, () => snackbar.Alpha = 1, () =>
            {
                // Auto-dismiss after 2 seconds
                Task.Delay(2000).ContinueWith(_ =>
                {
                    InvokeOnMainThread(() =>
                    {
                        UIView.Animate(0.3, () => snackbar.Alpha = 0, () => snackbar.RemoveFromSuperview());
                    });
                });
            });
        }

        private void SetupConstraints()
        {
            if (_iconContainer == null || _iconImageView == null || _domainLabel == null || 
                _usernameLabel == null || _otpLabel == null || _circularProgress == null || 
                _optionsButton == null) return;

            // Icon container constraints (40x40, like Android)
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _iconContainer.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 16),
                _iconContainer.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor, 12),
                _iconContainer.WidthAnchor.ConstraintEqualTo(40),
                _iconContainer.HeightAnchor.ConstraintEqualTo(40)
            });

            // Icon constraints (24x24, centered like Android)
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _iconImageView.CenterXAnchor.ConstraintEqualTo(_iconContainer.CenterXAnchor),
                _iconImageView.CenterYAnchor.ConstraintEqualTo(_iconContainer.CenterYAnchor),
                _iconImageView.WidthAnchor.ConstraintEqualTo(24),
                _iconImageView.HeightAnchor.ConstraintEqualTo(24)
            });

            // Domain label constraints (like Android domain TextView)
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _domainLabel.LeadingAnchor.ConstraintEqualTo(_iconContainer.TrailingAnchor, 12),
                _domainLabel.TopAnchor.ConstraintEqualTo(_iconContainer.TopAnchor),
                _domainLabel.TrailingAnchor.ConstraintLessThanOrEqualTo(_optionsButton.LeadingAnchor, -8)
            });

            // Username label constraints (like Android username TextView)
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _usernameLabel.LeadingAnchor.ConstraintEqualTo(_iconContainer.TrailingAnchor, 12),
                _usernameLabel.TopAnchor.ConstraintEqualTo(_domainLabel.BottomAnchor, 2),
                _usernameLabel.TrailingAnchor.ConstraintLessThanOrEqualTo(_optionsButton.LeadingAnchor, -8)
            });

            // OTP label constraints (under username, left aligned like Android)
            // Note: No bottom constraint here - row height is determined by GetHeightForRow
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _otpLabel.LeadingAnchor.ConstraintEqualTo(_iconContainer.TrailingAnchor, 12),
                _otpLabel.TopAnchor.ConstraintEqualTo(_usernameLabel.BottomAnchor, 8),
                _otpLabel.TrailingAnchor.ConstraintLessThanOrEqualTo(_circularProgress.LeadingAnchor, -12)
            });

            // Circular progress constraints (32x32, vertically centered with OTP label)
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _circularProgress.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),
                _circularProgress.CenterYAnchor.ConstraintEqualTo(_otpLabel.CenterYAnchor),
                _circularProgress.WidthAnchor.ConstraintEqualTo(32),
                _circularProgress.HeightAnchor.ConstraintEqualTo(32)
            });

            // Options button constraints (like Android optionsButton)
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                _optionsButton.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -16),
                _optionsButton.TopAnchor.ConstraintEqualTo(_iconContainer.TopAnchor),
                _optionsButton.WidthAnchor.ConstraintEqualTo(40),
                _optionsButton.HeightAnchor.ConstraintEqualTo(40)
            });
            
            // Divider constraints (like Android divider at bottom, marginStart="56dp")
            if (_divider != null)
            {
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    _divider.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 68), // 16 + 40 + 12 = 68
                    _divider.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor),
                    _divider.BottomAnchor.ConstraintEqualTo(ContentView.BottomAnchor),
                    _divider.HeightAnchor.ConstraintEqualTo(1)
                });
            }
        }

        private void SetupFallbackCell()
        {
            // Fallback for any errors
            TextLabel!.Text = "Loading...";
        }

        private AutofillCredential? _currentCredential;

        public void Configure(AutofillCredential credential)
        {
            try
            {
                _currentCredential = credential;
                
                // Update menu based on credential (shows/hides Copy OTP option)
                if (_optionsButton != null)
                {
                    _optionsButton.Menu = CreateOptionsMenu(credential);
                }
                
                if (_domainLabel != null)
                    _domainLabel.Text = credential.Domain;
                
                if (_usernameLabel != null)
                    _usernameLabel.Text = credential.Username;
                
                // Load icon from URL (like Android uses IconUri from CredentialMapper)
                LoadIconAsync(credential.IconUri, credential.FallbackIcon);

                if (credential.HasOtp && !string.IsNullOrEmpty(credential.Data))
                {
                    // Generate OTP code immediately (same as Android)
                    var totp = OtpHelper.GenerateOtp(credential.Data);
                    credential.Code = totp.Code;
                    credential.Progress = totp.RemainingSeconds;
                    
                    if (_otpLabel != null)
                    {
                        _otpLabel.Hidden = false;
                        _otpLabel.Text = totp.Code;
                    }
                    
                    if (_circularProgress != null)
                    {
                        _circularProgress.Hidden = false;
                        UpdateCircularProgress(totp.RemainingSeconds);
                    }
                }
                else
                {
                    if (_otpLabel != null)
                        _otpLabel.Hidden = true;
                    
                    if (_circularProgress != null)
                        _circularProgress.Hidden = true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Configure failed", ex);
                TextLabel!.Text = credential.Domain;
            }
        }
        
        /// <summary>
        /// Loads the icon from URL asynchronously (like Android Glide/Picasso)
        /// </summary>
        private async void LoadIconAsync(string? iconUri, string? fallbackIcon)
        {
            try
            {
                if (_iconImageView == null) return;
                
                // Set default lock icon first
                SetDefaultIcon();
                
                if (string.IsNullOrEmpty(iconUri))
                {
                    return;
                }
                
                // Check if it's a URL (web icon) or local asset
                if (iconUri.StartsWith("http"))
                {
                    try
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(10);
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "Fortress-iOS-Autofill/1.0");
                        
                        var response = await httpClient.GetAsync(iconUri);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var imageData = await response.Content.ReadAsByteArrayAsync();
                            
                            if (imageData != null && imageData.Length > 0)
                            {
                                var nsData = NSData.FromArray(imageData);
                                var image = UIImage.LoadFromData(nsData);
                                
                                if (image != null)
                                {
                                    // Update on main thread
                                    InvokeOnMainThread(() =>
                                    {
                                        if (_iconImageView != null)
                                        {
                                            _iconImageView.Image = image;
                                            _iconImageView.TintColor = null; // Remove tint for actual images
                                            _iconImageView.ContentMode = UIViewContentMode.ScaleAspectFit;
                                            
                                            // Make icon container circular with the image
                                            _iconImageView.Layer.CornerRadius = 12;
                                            _iconImageView.ClipsToBounds = true;
                                        }
                                    });
                                    return;
                                }
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Icon request timed out - use default
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        // HTTP error - use default
                    }
                }
                else
                {
                    // Try loading as bundled asset
                    var bundledImage = UIImage.FromBundle(iconUri);
                    if (bundledImage != null)
                    {
                        InvokeOnMainThread(() =>
                        {
                            if (_iconImageView != null)
                            {
                                _iconImageView.Image = bundledImage;
                                _iconImageView.TintColor = null;
                            }
                        });
                        return;
                    }
                }
                
                // If we get here, icon loading failed - keep default
            }
            catch (Exception ex)
            {
                // Silently fail - keep default lock icon
            }
        }
        
        private void SetDefaultIcon()
        {
            if (_iconImageView == null) return;
            
            _iconImageView.Image = UIImage.GetSystemImage("lock.fill");
            _iconImageView.TintColor = UIColor.SystemGray;
            _iconImageView.Layer.CornerRadius = 0;
            _iconImageView.ClipsToBounds = false;
        }

        private void UpdateCircularProgress(int secondsRemaining)
        {
            if (_progressLayer == null) return;

            var clamped = Math.Max(0, Math.Min(30, secondsRemaining));
            var progress = clamped / 30.0;

            // Remove any existing animation and set value directly for accurate 1-second sync
            _progressLayer.RemoveAllAnimations();
            _progressLayer.StrokeEnd = (nfloat)progress;
            _progressLayer.StrokeColor = PrimaryColor.CGColor;
        }

        
        // Public method to update progress without reconfiguring the whole cell
        public void UpdateProgress(int secondsRemaining)
        {
            if (_currentCredential != null && _currentCredential.HasOtp)
            {
                UpdateCircularProgress(secondsRemaining);
            }
        }

        // Public method to update OTP code and progress (like Android Tick())
        public void UpdateOtp(string code, int secondsRemaining)
        {
            if (_currentCredential != null && _currentCredential.HasOtp)
            {
                if (_otpLabel != null && !string.IsNullOrEmpty(code))
                {
                    _otpLabel.Text = code;
                }
                UpdateCircularProgress(secondsRemaining);
            }
        }

        private AutofillCredential? GetCredential()
        {
            return _currentCredential;
        }

        protected override void Dispose(bool disposing)
        {
            // No event handlers to clean up since we use UIMenu
            base.Dispose(disposing);
        }
    }
}