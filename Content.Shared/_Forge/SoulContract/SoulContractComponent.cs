using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Forge.SoulContract
{
    [RegisterComponent]
    public sealed partial class SoulContractComponent: Component
    {
        [DataField]
        public int Reward;

        [DataField]
        public TimeSpan UseTime = TimeSpan.FromSeconds(5);
    }

    [Serializable, NetSerializable]
    public sealed partial class SoulContractDoAfterEvent : SimpleDoAfterEvent
    {
    }
}
