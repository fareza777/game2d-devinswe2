using System;
using UnityEngine;

namespace Oathfire.Controls
{
    /// <summary>
    /// Sprites from the tileset's own UI sheet used to dress the touch controls in the game's fantasy style.
    /// </summary>
    [Serializable]
    public class MobileUiSkin
    {
        [Tooltip("Ornate bronze ring drawn on top of every control.")]
        public Sprite ring;
        [Tooltip("Dark round plate behind icons; also used as the circular icon mask and cooldown fill.")]
        public Sprite disc;
        [Tooltip("Stone medallion used as the joystick knob.")]
        public Sprite medallion;
        [Tooltip("Soft radial glow shown when a control is pressed.")]
        public Sprite glow;
        public Sprite attackIcon;

        public bool IsComplete => ring && disc && medallion && glow && attackIcon;
    }
}
