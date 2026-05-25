using Microsoft.Maui.Graphics;

namespace Fortress.Controls
{
    /// <summary>
    /// Draws a circular countdown ring using MAUI GraphicsView.
 /// Track ring + coloured progress arc + centred countdown number.
    /// </summary>
    public class OtpRingDrawable : IDrawable
    {
        // ── Configurable ─────────────────────────────────────────────────────
        public Color ProgressColor { get; set; } = Color.FromArgb("#6366F1");
  public Color TrackColor { get; set; } = Color.FromArgb("#E2E8F0");
        public Color TextColor     { get; set; } = Color.FromArgb("#6366F1");
        public float StrokeWidth   { get; set; } = 4f;

   // ── Live values (set by the ViewModel tick) ───────────────────────────
        public double Progress  { get; set; }   // seconds remaining
  public double Maximum   { get; set; } = 30;

        // ── IDrawable ────────────────────────────────────────────────────────
     public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.Antialias = true;

 float cx   = dirtyRect.Center.X;
     float cy     = dirtyRect.Center.Y;
    float radius = (Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f) - (StrokeWidth / 2f);

      // ── Track (full circle) ───────────────────────────────────────────
            canvas.StrokeColor = TrackColor;
            canvas.StrokeSize  = StrokeWidth;
 canvas.DrawCircle(cx, cy, radius);

      // ── Progress arc ──────────────────────────────────────────────────
            double ratio      = Maximum > 0 ? Math.Clamp(Progress / Maximum, 0, 1) : 0;
       float  sweepAngle = (float)(ratio * 360.0);

 if (sweepAngle > 0)
  {
                canvas.StrokeColor    = ProgressColor;
          canvas.StrokeSize     = StrokeWidth;
                canvas.StrokeLineCap  = LineCap.Round;

        // MAUI arc: startAngle=0 is 3 o'clock; we start at -90 (12 o'clock)
       float startAngle = -90f;
             canvas.DrawArc(
          cx - radius, cy - radius,
     radius * 2,  radius * 2,
      startAngle,
        startAngle + sweepAngle,
            false, false);
 }

  // ── Centre number ─────────────────────────────────────────────────
  canvas.FontColor = TextColor;
            canvas.FontSize  = Math.Min(dirtyRect.Width, dirtyRect.Height) * 0.30f;
            canvas.DrawString(
       ((int)Math.Round(Progress)).ToString(),
       dirtyRect,
                HorizontalAlignment.Center,
         VerticalAlignment.Center);
        }
    }
}
