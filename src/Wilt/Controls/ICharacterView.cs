using Wilt.Models;

namespace Wilt.Controls;

/// <summary>
/// Common surface both character rigs (<see cref="CharacterControl"/>, the
/// standing photo-based rig, and <see cref="DeskCharacterControl"/>, the
/// seated vector rig) implement, so OverlayWindow can drive whichever pose
/// the user picked (PRD-inspired "Could have": alternate poses) without
/// caring which concrete control is active.
/// </summary>
public interface ICharacterView
{
    CharacterSkin Skin { get; set; }
    bool WhimsyEmbellishments { get; set; }

    /// <summary>Called every timer tick (~30fps) to drive the continuous Energy-driven base pose.</summary>
    void ApplyState(double energy, EnergyState state, bool animateImmediately = false);

    /// <summary>
    /// Starts/stops this rig's internal blink/action-clip timers. The
    /// inactive pose (not currently shown) should be inactive so it isn't
    /// burning CPU or scheduling animations nobody sees.
    /// </summary>
    void SetActive(bool active);
}
