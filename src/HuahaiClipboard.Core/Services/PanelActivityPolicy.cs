using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.Core.Services;

public readonly record struct PanelActivity(
    bool AnimateLiquidReflection,
    bool AnimatePetals);

public static class PanelActivityPolicy
{
    public static PanelActivity Resolve(
        bool isPanelVisible,
        bool reduceMotion,
        PetalLevel petalLevel)
    {
        var animateAmbientEffects = isPanelVisible && !reduceMotion;
        return new PanelActivity(
            animateAmbientEffects,
            animateAmbientEffects && petalLevel != PetalLevel.Off);
    }
}
