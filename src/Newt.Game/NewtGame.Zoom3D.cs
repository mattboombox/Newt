using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private int _viewZoomStep3D;
    private SoundEffect? _zoomInSound;
    private SoundEffect? _zoomOutSound;

    private float ViewFieldOfView3D => !_view3D
        ? MathHelper.PiOver4
        : 2 * MathF.Atan(MathF.Tan(MathHelper.PiOver4 / 2) / (1 << _viewZoomStep3D));

    private void CycleViewZoom3D()
    {
        if (!_view3D) return;
        _viewZoomStep3D = (_viewZoomStep3D + 1) % 3;
        try
        {
            (_viewZoomStep3D == 0 ? _zoomOutSound : _zoomInSound)?.Play(0.65f, 0, 0);
        }
        catch (NoAudioHardwareException)
        {
            // Zoom remains available on machines without an audio device.
        }
    }

    private void LoadZoomSounds()
    {
        try
        {
            using var zoomIn = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Sounds", "zoom-in.wav"));
            _zoomInSound = SoundEffect.FromStream(zoomIn);
            using var zoomOut = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Sounds", "zoom-out.wav"));
            _zoomOutSound = SoundEffect.FromStream(zoomOut);
        }
        catch (NoAudioHardwareException)
        {
            _zoomInSound?.Dispose();
            _zoomInSound = null;
            _zoomOutSound = null;
        }
    }
}
