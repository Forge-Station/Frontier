
namespace Content.Shared._Forge.Sponsors;

public sealed class SponsorData
{
    public static readonly Dictionary<string, SponsorLevel> RolesMap = new()
    {
        { "1228412355705307148", SponsorLevel.Level1 }, // Бустер
        { "1388838190009290932", SponsorLevel.Level1 }, // Подмастерье Форжа
        { "1388839804375924736", SponsorLevel.Level2 }, // Оружейник
        { "1388839967475634176", SponsorLevel.Level3 }, // Мастер Кузни
        { "1388840103966933003", SponsorLevel.Level4 }, // Великий Кузнец
        { "1388840314860736512", SponsorLevel.Level5 }, // Архитектор Горна
        { "1388840456921550942", SponsorLevel.Level6 }, // Демиург Форжа
        { "1228303275833425992", SponsorLevel.Level6 }, // Руководитель Проекта
        { "1381007703425679522", SponsorLevel.Level6 }, // Помощник Рука
        { "1228659342668988416", SponsorLevel.Level4 }, // Старший Модер
        { "1351127483432570910", SponsorLevel.Level4 }, // ГГМ
        { "1227934528442728498", SponsorLevel.Level4 }, // Начкар
        { "1229422799362195577", SponsorLevel.Level4 }, // Старший ментор
        { "1257628115988119562", SponsorLevel.Level3 } // Смотритель Сервера
    };

    public static readonly Dictionary<SponsorLevel, string> SponsorColor = new()
    {
        { SponsorLevel.Level1, "#6bb9f0" },
        { SponsorLevel.Level2, "#8a9eff" },
        { SponsorLevel.Level3, "#bdbe6b" },
        { SponsorLevel.Level4, "#bdbe6b" },
        { SponsorLevel.Level5, "#ff9e2c" },
        { SponsorLevel.Level6, "#ffd700" }
    };

    public static readonly Dictionary<SponsorLevel, string> SponsorGhost = new()
    {
        { SponsorLevel.Level3, "MobObserver" },
        { SponsorLevel.Level4, "MobObserver" },
        { SponsorLevel.Level5, "MobObserver" },
        { SponsorLevel.Level6, "MobObserver" }
    };

    public static SponsorLevel ParseRoles(List<string> roles)
    {
        var highestRole = SponsorLevel.None;
        foreach (var role in roles)
        {
            if (RolesMap.ContainsKey(role))
                if ((byte) RolesMap[role] > (byte) highestRole)
                    highestRole = RolesMap[role];
        }

        return highestRole;
    }
}

public enum SponsorLevel : byte
{
    None = 0,
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    Level4 = 4,
    Level5 = 5,
    Level6 = 6
}
