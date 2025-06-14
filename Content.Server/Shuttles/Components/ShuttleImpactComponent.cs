using Content.Shared.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent]
    public sealed partial class ShuttleImpactComponent : Component
    {
        /// <summary>
        /// Минимальная разница скоростей между двумя телами, при которой происходит "удар" шаттла.
        /// </summary>
        [DataField("minimumImpactVelocity")]
        public int MinimumImpactVelocity = 21;

        /// <summary>
        /// Кинетическая энергия, необходимая для разрушения одной плитки.
        /// </summary>
        [DataField("tileBreakEnergy")]
        public float TileBreakEnergy = 2500;

        /// <summary>
        /// Кинетическая энергия, необходимая для создания искр.
        /// </summary>
        [DataField("sparkEnergy")]
        public float SparkEnergy = 2000;

        /// <summary>
        /// Коэффициент, на который уменьшается урон для живых существ.
        /// </summary>
        [DataField("mobDamageMultiplier")]
        public float MobDamageMultiplier = 0.1f;

        /// <summary>
        /// Максимальный урон, который может получить живое существо.
        /// </summary>
        [DataField("maxMobDamage")]
        public float MaxMobDamage = 300f;

        /// <summary>
        /// Время оглушения при столкновении.
        /// </summary>
        [DataField("stunTime")]
        public float StunTime = 3f;

        /// <summary>
        /// Длина области разрушения тайлов в направлении удара.
        /// </summary>
        [DataField("tileBreakLength")]
        public int TileBreakLength = 1;

        /// <summary>
        /// Ширина области разрушения тайлов перпендикулярно направлению удара.
        /// </summary>
        [DataField("tileBreakWidth")]
        public int TileBreakWidth = 1;
    }
}
