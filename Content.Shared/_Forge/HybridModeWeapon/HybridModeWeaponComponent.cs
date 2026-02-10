using Content.Shared._Forge.FireModes;
using Content.Shared.Access;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Forge.TripleModeWeapon
{
    [RegisterComponent, NetworkedComponent]
    [Access(typeof(HybridModeWeaponSystem))]
    [AutoGenerateComponentState]
    public sealed partial class HybridModeWeaponComponent: Component
    {
        [DataField("fireModes", required: true)]
        [AutoNetworkedField]
        public Dictionary<ProtoId<EntityPrototype>, string> FireModes = default!;

        [DataField("proto", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string Prototype = default!; 

        [DataField]
        [AutoNetworkedField]
        public int CurrentFireMode;
    }
}
