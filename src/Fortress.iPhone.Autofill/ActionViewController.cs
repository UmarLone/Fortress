using MobileCoreServices;

namespace Fortress.iPhone.Autofill
{
    using Foundation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UIKit;

    namespace Fortress.iPhone.Autofill
    {
        public class CredentialListViewController : UIViewController
        {
            UITableView _table;
            UITextField _searchField;
            UIButton _searchButton;
            UIButton _addButton;
            UIButton _cancelButton;
            UILabel _emptyLabel;

            readonly List<AutofillCredential> _all;
            List<AutofillCredential> _filtered;
            readonly Action<AutofillCredential> _onSelected;

            public CredentialListViewController(
                List<AutofillCredential> credentials,
                Action<AutofillCredential> onSelected)
            {
                _all = credentials ?? new();
                _filtered = _all.ToList();
                _onSelected = onSelected;
            }
            void SetLeftLogoTitle()
            {
                // Logo - 32pt to match Android 32dp
                var logo = new UIImageView(UIImage.FromBundle("gklogoblue"))
                {
                    ContentMode = UIViewContentMode.ScaleAspectFit,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    logo.WidthAnchor.ConstraintEqualTo(32),
                    logo.HeightAnchor.ConstraintEqualTo(32)
                });

                // Letter-spaced title - matching Android: 20sp, bold, letterSpacing 0.12, color #606a7b
                var title = "FORTRESS";
                var titleLabel = new UILabel();

                var attributed = new NSMutableAttributedString(title);
                attributed.AddAttribute(
                    UIStringAttributeKey.KerningAdjustment,
                    NSNumber.FromFloat(2.4f),
                    new NSRange(0, title.Length));

                attributed.AddAttribute(
                    UIStringAttributeKey.Font,
                    UIFont.SystemFontOfSize(20, UIFontWeight.Bold),
                    new NSRange(0, title.Length));

                // Color #606a7b (gk_gray_700) to match Android
                attributed.AddAttribute(
                    UIStringAttributeKey.ForegroundColor,
                    UIColor.FromRGB(0x60, 0x6a, 0x7b),
                    new NSRange(0, title.Length));

                titleLabel.AttributedText = attributed;

                // Stack - 8pt spacing to match Android layout_marginEnd="8dp"
                var stack = new UIStackView(new UIView[] { logo, titleLabel })
                {
                    Axis = UILayoutConstraintAxis.Horizontal,
                    Alignment = UIStackViewAlignment.Center,
                    Spacing = 8
                };

                stack.LayoutMargins = new UIEdgeInsets(0, 8, 0, 0);
                stack.LayoutMarginsRelativeArrangement = true;

                var barButton = new UIBarButtonItem(stack);

                NavigationItem.LeftBarButtonItem = barButton;
            }

            public override void ViewDidLoad()
            {
                base.ViewDidLoad();

                View.BackgroundColor = UIColor.White;
                SetLeftLogoTitle();

                BuildHeader();
                BuildCancelButton();
                BuildTable();
                UpdateEmptyState();
            }

            #region Header

            void BuildHeader()
            {
                var header = new UIView
                {
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                var logo = new UIImageView(UIImage.FromBundle("gklogoblue"))
                {
                    ContentMode = UIViewContentMode.ScaleAspectFit,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                _searchField = new UITextField
                {
                    Placeholder = "Search",
                    BorderStyle = UITextBorderStyle.RoundedRect,
                    ClearButtonMode = UITextFieldViewMode.WhileEditing,
                    ReturnKeyType = UIReturnKeyType.Done,
                    Hidden = true,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                _searchField.EditingChanged += (_, __) => ApplySearch();
                _searchField.ShouldReturn = _ =>
                {
                    _searchField.ResignFirstResponder();
                    return true;
                };

                // Cancel button inside search field
                var cancelButton = UIButton.FromType(UIButtonType.System);
                cancelButton.SetTitle("Cancel", UIControlState.Normal);
                cancelButton.TouchUpInside += (_, __) =>
                {
                    _searchField.Text = string.Empty;
                    _searchField.Hidden = true;
                    _searchField.ResignFirstResponder();

                    _filtered = _all.ToList();
                    ReloadTable();
                };

                cancelButton.SizeToFit();
                _searchField.RightView = cancelButton;
                _searchField.RightViewMode = UITextFieldViewMode.WhileEditing;

                _searchButton = UIButton.FromType(UIButtonType.System);
                _searchButton.SetImage(
                    UIImage.GetSystemImage("magnifyingglass"),
                    UIControlState.Normal);

                _searchButton.Enabled = _all.Any();
                _searchButton.TouchUpInside += (_, __) =>
                {
                    _searchField.Hidden = false;
                    _searchField.BecomeFirstResponder();
                };

                _addButton = UIButton.FromType(UIButtonType.System);
                _addButton.SetImage(
                    UIImage.GetSystemImage("plus"),
                    UIControlState.Normal);

                _addButton.TouchUpInside += (_, __) =>
                {
                    ExtensionContext.OpenUrl(new NSUrl("fortress://add"), null);
                    ExtensionContext.CancelRequest(null);
                };

                // Title label - matching Android: 20sp, bold, letterSpacing 0.12, color #606a7b
                var titleLabel = new UILabel();
                var title = "FORTRESS";
                var attributed = new NSMutableAttributedString(title);
                attributed.AddAttribute(
                    UIStringAttributeKey.KerningAdjustment,
                    NSNumber.FromFloat(2.4f),
                    new NSRange(0, title.Length));
                attributed.AddAttribute(
                    UIStringAttributeKey.Font,
                    UIFont.SystemFontOfSize(20, UIFontWeight.Bold),
                    new NSRange(0, title.Length));
                attributed.AddAttribute(
                    UIStringAttributeKey.ForegroundColor,
                    UIColor.FromRGB(0x60, 0x6a, 0x7b),
                    new NSRange(0, title.Length));
                titleLabel.AttributedText = attributed;

                // Create vertical separators
                UIView CreateSeparator()
                {
                    var separator = new UIView
                    {
                        BackgroundColor = UIColor.Separator,
                        TranslatesAutoresizingMaskIntoConstraints = false
                    };
                    separator.WidthAnchor.ConstraintEqualTo(1).Active = true;
                    separator.HeightAnchor.ConstraintEqualTo(24).Active = true;
                    return separator;
                }

                var separator1 = CreateSeparator();
                var separator2 = CreateSeparator();

                var stack = new UIStackView(new UIView[]
                {
                logo,
                titleLabel,
                separator1,
                _searchField,
                _searchButton,
                separator2,
                _addButton
                })
                {
                    Axis = UILayoutConstraintAxis.Horizontal,
                    Spacing = 8,
                    Alignment = UIStackViewAlignment.Center,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                header.AddSubview(stack);
                View.AddSubview(header);

                NSLayoutConstraint.ActivateConstraints(new[]
                {
                header.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
                header.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor, 16),
                header.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor, -16),
                header.HeightAnchor.ConstraintEqualTo(56),

                stack.TopAnchor.ConstraintEqualTo(header.TopAnchor),
                stack.BottomAnchor.ConstraintEqualTo(header.BottomAnchor),
                stack.LeadingAnchor.ConstraintEqualTo(header.LeadingAnchor),
                stack.TrailingAnchor.ConstraintEqualTo(header.TrailingAnchor),

                logo.WidthAnchor.ConstraintEqualTo(32),
                logo.HeightAnchor.ConstraintEqualTo(32),

                _searchButton.WidthAnchor.ConstraintEqualTo(40),
                _addButton.WidthAnchor.ConstraintEqualTo(40),
            });
            }

            #endregion

            #region Cancel Button

            void BuildCancelButton()
            {
                _cancelButton = new UIButton(UIButtonType.Custom)
                {
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                // #407cca background with white text
                _cancelButton.SetTitle("Cancel", UIControlState.Normal);
                _cancelButton.SetTitleColor(UIColor.White, UIControlState.Normal);
                _cancelButton.BackgroundColor = UIColor.FromRGB(0x40, 0x7c, 0xca);
                _cancelButton.TitleLabel.Font = UIFont.SystemFontOfSize(16, UIFontWeight.Semibold);
                _cancelButton.Layer.CornerRadius = 8;
                _cancelButton.ClipsToBounds = true;

                _cancelButton.TouchUpInside += (_, __) =>
                {
                    ExtensionContext?.CancelRequest(null);
                };

                View.AddSubview(_cancelButton);

                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    _cancelButton.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                    _cancelButton.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor, -20),
                    _cancelButton.HeightAnchor.ConstraintEqualTo(40),
                    _cancelButton.WidthAnchor.ConstraintGreaterThanOrEqualTo(120)
                });
            }

            #endregion

            #region Table + Empty State

            void BuildTable()
            {
                _table = new UITableView
                {
                    TranslatesAutoresizingMaskIntoConstraints = false,
                    RowHeight = 72,
                    SeparatorInset = new UIEdgeInsets(0, 56, 0, 0)
                };

                _emptyLabel = new UILabel
                {
                    Text = "No Passwords Found",
                    TextColor = UIColor.Gray,
                    Font = UIFont.SystemFontOfSize(16, UIFontWeight.Medium),
                    TextAlignment = UITextAlignment.Center,
                    Lines = 0,
                    TranslatesAutoresizingMaskIntoConstraints = false
                };

                // Create a container view to center the label
                var emptyContainer = new UIView();
                emptyContainer.AddSubview(_emptyLabel);
                
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    _emptyLabel.CenterXAnchor.ConstraintEqualTo(emptyContainer.CenterXAnchor),
                    _emptyLabel.CenterYAnchor.ConstraintEqualTo(emptyContainer.CenterYAnchor),
                    _emptyLabel.LeadingAnchor.ConstraintGreaterThanOrEqualTo(emptyContainer.LeadingAnchor, 20),
                    _emptyLabel.TrailingAnchor.ConstraintLessThanOrEqualTo(emptyContainer.TrailingAnchor, -20)
                });

                _table.BackgroundView = emptyContainer;

                _table.RegisterClassForCellReuse(
                    typeof(CredentialCell),
                    CredentialCell.Key);

                _table.Source = new CredentialSource(_filtered, _onSelected);

                View.AddSubview(_table);

                NSLayoutConstraint.ActivateConstraints(new[]
                {
                _table.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 56),
                _table.BottomAnchor.ConstraintEqualTo(_cancelButton.TopAnchor, -10),
                _table.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _table.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
            });
            }

            void ReloadTable()
            {
                _table.Source = new CredentialSource(_filtered, _onSelected);
                _table.ReloadData();
                UpdateEmptyState();
            }

            void UpdateEmptyState()
            {
                var hasItems = _filtered.Any();
                _emptyLabel.Hidden = hasItems;
                _table.SeparatorStyle = hasItems
                    ? UITableViewCellSeparatorStyle.SingleLine
                    : UITableViewCellSeparatorStyle.None;
            }

            #endregion

            #region Search

            void ApplySearch()
            {
                var q = _searchField.Text?.ToLowerInvariant() ?? "";

                _filtered = string.IsNullOrWhiteSpace(q)
                    ? _all.ToList()
                    : _all.Where(c =>
                            c.Domain.ToLowerInvariant().Contains(q) ||
                            c.Username.ToLowerInvariant().Contains(q))
                        .ToList();

                ReloadTable();
            }

            #endregion
        }

       
    }
     
}
