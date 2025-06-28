using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Ahelp rate limit values are accounted in periods of this size (seconds).
    ///     After the period has passed, the count resets.
    /// </summary>
    /// <seealso cref="AhelpRateLimitCount"/>
    public static readonly CVarDef<float> AhelpRateLimitPeriod =
        CVarDef.Create("ahelp.rate_limit_period", 2f, CVar.SERVERONLY);

    /// <summary>
    ///     How many ahelp messages are allowed in a single rate limit period.
    /// </summary>
    /// <seealso cref="AhelpRateLimitPeriod"/>
    public static readonly CVarDef<int> AhelpRateLimitCount =
        CVarDef.Create("ahelp.rate_limit_count", 10, CVar.SERVERONLY);

    /// <summary>
    ///     Should the administrator's position be displayed in ahelp.
    ///     If it is is false, only the admin's ckey will be displayed in the ahelp.
    /// </summary>
    /// <seealso cref="AdminUseCustomNamesAdminRank"/>
    /// <seealso cref="AhelpAdminPrefixWebhook"/>
    public static readonly CVarDef<bool> AhelpAdminPrefix =
        CVarDef.Create("ahelp.admin_prefix", true, CVar.SERVERONLY); // Frontier-Change

    /// <summary>
    ///     Should the administrator's position be displayed in the webhook.
    ///     If it is is false, only the admin's ckey will be displayed in webhook.
    /// </summary>
    /// <seealso cref="AdminUseCustomNamesAdminRank"/>
    /// <seealso cref="AhelpAdminPrefix"/>
    public static readonly CVarDef<bool> AhelpAdminPrefixWebhook =
        CVarDef.Create("ahelp.admin_prefix_webhook", false, CVar.SERVERONLY);

    // Frontier-Change-Start
    /// <summary>
    ///     The color of the names of admins. This is the fallback color for admins.
    /// </summary>
    public static readonly CVarDef<string> AdminBwoinkColor =
        CVarDef.Create("admin.admin_bwoink_color", "red", CVar.SERVERONLY);

    /// <summary>
    ///     Use the admin's Admin OOC color in bwoinks.
    ///     If either the ooc color or this is not set, uses the admin.admin_bwoink_color value.
    /// </summary>
    public static readonly CVarDef<bool> UseAdminOOCColorInBwoinks =
        CVarDef.Create("admin.bwoink_use_admin_ooc_color", true, CVar.SERVERONLY);
    // Frontier-Change-End
}
