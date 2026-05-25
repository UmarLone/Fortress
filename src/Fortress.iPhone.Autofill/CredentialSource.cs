using Foundation;
using System;
using System.Collections.Generic;
using UIKit;

namespace Fortress.iPhone.Autofill
{
    public class CredentialSource : UITableViewSource
    {
        List<AutofillCredential> _items;
        readonly Action<AutofillCredential> _onTap;

        public CredentialSource(
            List<AutofillCredential> items,
            Action<AutofillCredential> onTap)
        {
            try
            {
                _items = items ?? new List<AutofillCredential>();
                _onTap = onTap;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("CredentialSource constructor failed", ex);
                _items = new List<AutofillCredential>();
                _onTap = onTap;
            }
        }

        public void UpdateCredentials(List<AutofillCredential> newItems)
        {
            try
            {
                _items = newItems ?? new List<AutofillCredential>();
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("UpdateCredentials failed", ex);
            }
        }

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            try
            {
                return _items?.Count ?? 0;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("RowsInSection failed", ex);
                return 0;
            }
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            try
            {
                var cell = tableView.DequeueReusableCell(CredentialCell.Key) as CredentialCell;
                
                if (cell == null)
                {
                    cell = new CredentialCell(IntPtr.Zero);
                }

                if (indexPath.Row < (_items?.Count ?? 0))
                {
                    var credential = _items[indexPath.Row];
                    cell.Configure(credential);
                }
                else
                {
                    ErrorLogger.LogError($"Index out of range: {indexPath.Row} >= {_items?.Count ?? 0}");
                }

                return cell;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("GetCell failed", ex);
                
                // Return a basic cell as fallback
                var fallbackCell = new UITableViewCell(UITableViewCellStyle.Subtitle, "fallback");
                if (indexPath.Row < (_items?.Count ?? 0))
                {
                    var credential = _items[indexPath.Row];
                    fallbackCell.TextLabel.Text = credential.Domain;
                    fallbackCell.DetailTextLabel.Text = credential.Username;
                }
                else
                {
                    fallbackCell.TextLabel.Text = "Error loading credential";
                }
                return fallbackCell;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            try
            {
                tableView.DeselectRow(indexPath, true);
                
                if (indexPath.Row < (_items?.Count ?? 0))
                {
                    var credential = _items[indexPath.Row];
                    _onTap?.Invoke(credential);
                }
                else
                {
                    ErrorLogger.LogError($"Selected index out of range: {indexPath.Row}");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("RowSelected failed", ex);
            }
        }

        public override nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
        {
            try
            {
                if (indexPath.Row < (_items?.Count ?? 0))
                {
                    var credential = _items[indexPath.Row];
                    // Android-style heights: taller for OTP cells like Android row_credential.xml
                    return credential?.HasOtp == true ? 96 : 72; // Match Android minHeight="72dp"
                }
                return 72;
            }
            catch
            {
                return 72;
            }
        }

        public override string TitleForHeader(UITableView tableView, nint section)
        {
            var count = _items?.Count ?? 0;
            return count > 0 ? $"Found {count} password{(count == 1 ? "" : "s")}" : "No passwords found";
        }
    }
}