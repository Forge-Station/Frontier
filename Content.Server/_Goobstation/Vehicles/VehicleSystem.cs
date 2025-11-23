// SPDX-FileCopyrightText: 2022 DrSmugleaf
// SPDX-FileCopyrightText: 2022 Leon Friedrich
// SPDX-FileCopyrightText: 2022 Rane
// SPDX-FileCopyrightText: 2023 AJCM-git
// SPDX-FileCopyrightText: 2023 Daniil Sikinami
// SPDX-FileCopyrightText: 2024 checkraze
// SPDX-FileCopyrightText: 2024 metalgearsloth
// SPDX-FileCopyrightText: 2025 HacksLua
// SPDX-FileCopyrightText: 2025 ScyronX
// SPDX-FileCopyrightText: 2025 starch
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Mono.Radar; //Lua mod
using Content.Shared.Buckle.Components; //Lua mod
using Content.Shared._Goobstation.Vehicle;
using Content.Shared._Goobstation.Vehicle.Components;
using Content.Shared._NF.Radar; //Lua mod

namespace Content.Server.Vehicle;

public sealed class VehicleSystem : SharedVehicleSystem
{
    //Lua start
    protected override void OnStrapped(EntityUid uid, VehicleComponent component, ref StrappedEvent args)
    {
        base.OnStrapped(uid, component, ref args);

        var blip = EnsureComp<RadarBlipComponent>(uid);
        blip.RadarColor = Color.Cyan;
        blip.Scale = 0.5f;
        blip.VisibleFromOtherGrids = true;
    }

    protected override void OnUnstrapped(EntityUid uid, VehicleComponent component, ref UnstrappedEvent args)
    {
        RemComp<RadarBlipComponent>(uid);

        base.OnUnstrapped(uid, component, ref args);
    }
    //Lua end
}
