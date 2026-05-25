using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Bumptech.Glide;
using Bumptech.Glide.Request;
using Fortress.Mobile.Core.Models;
using Google.Android.Material.Card;
using Google.Android.Material.ProgressIndicator;
using AndroidColor = Android.Graphics.Color;
using AndroidImageButton = Android.Widget.ImageButton;
using View = Android.Views.View;
using Resource = Microsoft.Maui.Resource;

namespace Bit.Droid.Autofill
{
    public class CredentialViewHolder : RecyclerView.ViewHolder
    {
        private readonly TextView _domain;
        private readonly TextView _username;
        private readonly TextView _initialsText;
        private readonly MaterialCardView _initialsContainer;
        private readonly ImageView _icon;
        private readonly MaterialCardView _iconContainer;
        private readonly LinearLayout _aiBadge;
        private readonly TextView _aiBadgeText;
        private readonly MaterialCardView _otpBadge;
        private readonly TextView _otpText;
        private readonly CircularProgressIndicator _otpProgress;
        private readonly AndroidImageButton _optionsButton;
        private CredentialView _credential;

        private static readonly int[] AvatarBgColors =
            {
   AndroidColor.ParseColor("#DBEAFE"),
            AndroidColor.ParseColor("#DCFCE7"),
          AndroidColor.ParseColor("#FEF3C7"),
    AndroidColor.ParseColor("#EDE9FE"),
     AndroidColor.ParseColor("#FCE7F3"),
   AndroidColor.ParseColor("#CFFAFE"),
        };
        private static readonly int[] AvatarFgColors =
         {
       AndroidColor.ParseColor("#1D4ED8"),
    AndroidColor.ParseColor("#15803D"),
      AndroidColor.ParseColor("#B45309"),
     AndroidColor.ParseColor("#6D28D9"),
       AndroidColor.ParseColor("#BE185D"),
   AndroidColor.ParseColor("#0E7490"),
 };

        public CredentialViewHolder(View itemView, Action<CredentialView> onSelected, Action<CredentialView> onOptions)
           : base(itemView)
        {
            _domain = itemView.FindViewById<TextView>(Resource.Id.domain);
            _username = itemView.FindViewById<TextView>(Resource.Id.username);
            _initialsText = itemView.FindViewById<TextView>(Resource.Id.initialsText);
            _initialsContainer = itemView.FindViewById<MaterialCardView>(Resource.Id.initialsContainer);
            _icon = itemView.FindViewById<ImageView>(Resource.Id.siteIcon);
            _iconContainer = itemView.FindViewById<MaterialCardView>(Resource.Id.iconContainer);
            _aiBadge = itemView.FindViewById<LinearLayout>(Resource.Id.aiBadge);
            _aiBadgeText = itemView.FindViewById<TextView>(Resource.Id.aiBadgeText);
            _otpBadge = itemView.FindViewById<MaterialCardView>(Resource.Id.otpBadge);
            _otpText = itemView.FindViewById<TextView>(Resource.Id.otpText);
            _otpProgress = itemView.FindViewById<CircularProgressIndicator>(Resource.Id.otpProgress);
            _optionsButton = itemView.FindViewById<AndroidImageButton>(Resource.Id.optionsButton);

            itemView.Click += (_, __) => { if (_credential != null) onSelected(_credential); };
            _optionsButton.Click += (_, __) => { if (_credential != null) onOptions?.Invoke(_credential); };
        }

        public void Bind(CredentialView credential)
        {
            _credential = credential;
            _domain.Text = credential.Domain;
            _username.Text = credential.Username;
            SetInitialsAvatar(credential.Domain);
            LoadFavicon(credential.IconUri?.Trim());
            BindOtp(credential);
            BindAiBadge(credential);
        }

        private void SetInitialsAvatar(string? domain)
        {
            var initial = string.IsNullOrWhiteSpace(domain)
          ? "?"
            : domain.Trim()[0].ToString().ToUpperInvariant();
            var idx = Math.Abs((domain ?? string.Empty).GetHashCode()) % AvatarBgColors.Length;
            _initialsText.Text = initial;
            _initialsContainer.SetCardBackgroundColor(new AndroidColor(AvatarBgColors[idx]));
            _initialsText.SetTextColor(new AndroidColor(AvatarFgColors[idx]));
            _initialsContainer.Visibility = ViewStates.Visible;
        }

        private void LoadFavicon(string? iconUri)
        {
            if (string.IsNullOrWhiteSpace(iconUri) ||
       (!iconUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
   !iconUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            {
                _iconContainer.Visibility = ViewStates.Gone;
                _icon.Visibility = ViewStates.Gone;
                return;
            }

            _iconContainer.Visibility = ViewStates.Visible;
            _icon.Visibility = ViewStates.Visible;

            Glide.With(ItemView.Context)
                   .Load(iconUri)
                .Apply(RequestOptions.CircleCropTransform()
              .Placeholder(Resource.Drawable.ic_lock)
              .Error(Resource.Drawable.ic_lock))
            .Into(_icon);
        }

        private void BindOtp(CredentialView credential)
        {
            if (!credential.HasOtp)
            {
                _otpBadge.Visibility = ViewStates.Gone;
                if (_otpProgress != null)
                    _otpProgress.Visibility = ViewStates.Gone;
                return;
            }

            _otpBadge.Visibility = ViewStates.Visible;

            var code = credential.Code;
            _otpText.Text = string.IsNullOrEmpty(code)
         ? "--- ---"
              : (code.Length == 6 ? $"{code[..3]} {code[3..]}" : code);

            if (_otpProgress != null)
            {
                _otpProgress.Visibility = ViewStates.Visible;
                int max = credential.Duration > 0 ? credential.Duration : 30;
                int progress = Math.Clamp((int)credential.Progress, 0, max);
                _otpProgress.Max = max;
                // SetProgressCompat(value, animated) is the correct API for CircularProgressIndicator
                _otpProgress.SetProgressCompat(progress, false);
            }
        }

        private void BindAiBadge(CredentialView credential)
        {
            if (_aiBadge == null) return;

 // Show "Verified by AI" for login credentials that have a strong password
   // and are not flagged for risk. Card/Identity types get a different badge.
    var credType = credential.CredentialType ?? string.Empty;

   if (credType is "CreditCard" or "Address" or "Identity")
            {
                _aiBadge.Visibility = ViewStates.Visible;
      if (_aiBadgeText != null) _aiBadgeText.Text = "Auto-detected";
            }
            else if (credential.PasswordStrengthLevel >= 3 || credential.HasOtp)
       {
                // Strong or has 2FA — show verified badge
      _aiBadge.Visibility = ViewStates.Visible;
      if (_aiBadgeText != null) _aiBadgeText.Text = credential.HasOtp ? "AI verified · 2FA" : "Verified by AI";
         }
            else if (credential.PasswordStrengthLevel > 0 && credential.PasswordStrengthLevel < 3)
            {
         // Weak password — show warning badge
           _aiBadge.Visibility = ViewStates.Visible;
     if (_aiBadgeText != null) _aiBadgeText.Text = "Weak password";

                // Swap colours to amber
                var badgeCard = (MaterialCardView)_aiBadge.GetChildAt(0);
badgeCard?.SetCardBackgroundColor(new AndroidColor(AndroidColor.ParseColor("#FEF3C7")));
        if (_aiBadgeText != null) _aiBadgeText.SetTextColor(new AndroidColor(AndroidColor.ParseColor("#92400E")));
            }
       else
   {
 _aiBadge.Visibility = ViewStates.Gone;
            }
        }
    }
}
