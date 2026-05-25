using AuthenticationServices;
using Foundation;
using Fortress.iPhone.Autofill.Fortress.iPhone.Autofill;
using System;
using System.Collections.Generic;
using System.Linq;
using UIKit;
using CoreGraphics;

namespace Fortress.iPhone.Autofill
{
    public class CredentialListViewController : UIViewController
    {
        readonly List<AutofillCredential> _all;
        List<AutofillCredential> _filtered;
        readonly Action<AutofillCredential> _onSelected;

        UITableView? _table;
        UITextField? _searchField;
        bool _searchVisible;

        public CredentialListViewController(
            List<AutofillCredential> credentials,
            Action<AutofillCredential> onSelected)
        {
            try
            {
                ErrorLogger.LogInfo($"CredentialListViewController initialized with {credentials?.Count ?? 0} credentials");
                _all = credentials ?? new List<AutofillCredential>();
                _filtered = _all.ToList();
                _onSelected = onSelected;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CredentialListViewController constructor failed", ex);
                _all = new List<AutofillCredential>();
                _filtered = new List<AutofillCredential>();
                _onSelected = onSelected;
            }
        }

        public override void ViewDidLoad()
        {
            try
            {
                ErrorLogger.LogInfo($"ViewDidLoad started with {_all?.Count ?? 0} credentials");
                base.ViewDidLoad();
                
                View.BackgroundColor = UIColor.SystemBackground;
                
                SetupUI();
                
                ErrorLogger.LogInfo("ViewDidLoad completed successfully");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("ViewDidLoad failed", ex);
                ShowErrorState();
            }
        }

        private void SetupUI()
        {
            SetupSearchField();
            SetupTable();
            SetupConstraints();
        }

        private void SetupSearchField()
        {
            _searchField = new UITextField
            {
                Placeholder = "Search password...",
                BorderStyle = UITextBorderStyle.RoundedRect,
                Hidden = true,
                TranslatesAutoresizingMaskIntoConstraints = false,
                Font = UIFont.SystemFontOfSize(16),
                BackgroundColor = UIColor.SystemGray6
            };

            _searchField.EditingChanged += (_, __) => ApplySearch();
            View.AddSubview(_searchField);
        }

        private void SetupTable()
        {
            _table = new UITableView(CGRect.Empty, UITableViewStyle.Grouped)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                SeparatorStyle = UITableViewCellSeparatorStyle.SingleLine,
                SeparatorInset = new UIEdgeInsets(0, 60, 0, 0),
                BackgroundColor = UIColor.SystemBackground,
                ShowsVerticalScrollIndicator = true
            };

            _table.RegisterClassForCellReuse(typeof(CredentialCell), CredentialCell.Key);
            _table.Source = new CredentialSource(_filtered, _onSelected);
            
            View.AddSubview(_table);
        }

        private void SetupConstraints()
        {
            if (_searchField == null || _table == null) return;

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                // Search field constraints
                _searchField.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 8),
                _searchField.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 16),
                _searchField.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -16),
                _searchField.HeightAnchor.ConstraintEqualTo(44),

                // Table constraints
                _table.TopAnchor.ConstraintEqualTo(_searchField.BottomAnchor, 8),
                _table.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _table.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
                _table.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor)
            });
        }

        // MARK: - Public Methods
        
        public void NotifyCredentialsChanged()
        {
            InvokeOnMainThread(() =>
            {
                try
                {
                    if (_table?.Source is CredentialSource source)
                    {
                        source.UpdateCredentials(_filtered);
                        _table.ReloadData();
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to update credentials", ex);
                }
            });
        }

        // MARK: - Search Methods

        private void ApplySearch()
        {
            try
            {
                var searchText = _searchField?.Text?.Trim() ?? string.Empty;
                
                if (string.IsNullOrEmpty(searchText))
                {
                    _filtered = _all.ToList();
                }
                else
                {
                    _filtered = _all.Where(c => 
                        c.Domain.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        c.Username.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                if (_table?.Source is CredentialSource source)
                {
                    source.UpdateCredentials(_filtered);
                    _table.ReloadData();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("Search failed", ex);
            }
        }

        private void ShowErrorState()
        {
            View.BackgroundColor = UIColor.SystemRed;
            Title = "Error Loading";
            
            var errorLabel = new UILabel
            {
                Text = "Failed to load credentials",
                TextAlignment = UITextAlignment.Center,
                TextColor = UIColor.White,
                Font = UIFont.BoldSystemFontOfSize(18),
                TranslatesAutoresizingMaskIntoConstraints = false
            };
            
            View.AddSubview(errorLabel);
            
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                errorLabel.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                errorLabel.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor)
            });
        }
    }
}