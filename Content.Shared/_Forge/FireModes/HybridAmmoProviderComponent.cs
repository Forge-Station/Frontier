using Content.Shared._Forge.TripleModeWeapon;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Forge.FireModes
{

    /// <summary>
    /// Allows battery weapons to fire different types of projectiles
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    [Access(typeof(HybridModeWeaponSystem), typeof(SharedGunSystem))]
    public sealed partial class HybridAmmoProviderComponent : AmmoProviderComponent
    {
        /// <summary>
        /// How much battery it costs to fire once.
        /// </summary>
        [DataField("fireCost"), ViewVariables(VVAccess.ReadWrite)]
        public float FireCost = 100;

        [ViewVariables(VVAccess.ReadWrite)]
        public int Shots;

        [ViewVariables(VVAccess.ReadWrite)]
        public int Capacity;

        [ViewVariables(VVAccess.ReadWrite), DataField("soundAutoEject")]
        public SoundSpecifier? SoundAutoEject = new SoundPathSpecifier("/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg");

        /// <summary>
        /// Should the magazine automatically eject when empty.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField("autoEject")]
        public bool AutoEject = false;
    }
}
