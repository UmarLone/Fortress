using Android.App;
using Android.Content;
using Android.Service.QuickSettings;
using Fortress.Mobile.Core.Services;

namespace com.fortress.app
{
    /// <summary>
    /// Quick Settings tile — one-tap shortcut to FORTRESS autofill.
    /// Appears in the Android notification shade when the user adds the tile.
    /// Tapping it launches AutofillActivity directly, skipping the need to
    /// open the full app.
    /// </summary>
    [Android.App.Service(
               Permission = Android.Manifest.Permission.BindQuickSettingsTile,
       Exported = true,
       Label = "FORTRESS",
       Icon = "@drawable/logowhite")]
    [Android.App.IntentFilter(new[] { ActionQsTile })]
    public class AutofillTileService : TileService
    {
        public override void OnTileAdded()
        {
            base.OnTileAdded();
            Preferences.Default.Set("autofill_tile_added", true);
            UpdateTile();
        }

        public override void OnTileRemoved()
        {
            base.OnTileRemoved();
            Preferences.Default.Set("autofill_tile_added", false);
        }

        public override void OnStartListening()
        {
            base.OnStartListening();
            UpdateTile();
        }

        public override void OnStopListening()
        {
            base.OnStopListening();
        }

        public override void OnClick()
        {
            base.OnClick();
            LaunchAutofill();
        }

        private void UpdateTile()
        {
            var tile = QsTile;
            if (tile == null) return;
            tile.Label = "FORTRESS";
            tile.ContentDescription = "Fill passwords with FORTRESS";
            tile.State = PreferenceWrapper.Instance.IsApplicationLocked
         ? TileState.Inactive
      : TileState.Active;
            tile.UpdateTile();
        }

        private void LaunchAutofill()
        {
            var intent = new Intent(this, typeof(AutofillActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            intent.PutExtra("autofill", true);
            intent.PutExtra("tileFlow", true);
            StartActivityAndCollapse(intent);
        }
    }
}
