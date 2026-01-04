<div class="header" align="center">
<img alt="Frontier Station" height="300" src="https://github.com/Forge-Station/Frontier/blob/master/Resources/Textures/_NF/Logo/logo.png?raw=true" />
</div>

**Corvax Forge Frontier** — это русскоязычное ответвление **Frontier Station**, форка [Space Station 14](https://github.com/space-wizards/space-station-14), работающего на движке [Robust Toolbox](https://github.com/space-wizards/RobustToolbox) (C#).

В этой сборке представлены собственные наработки, адаптации и контент, созданный русскоязычным комьюнити.
Если вы хотите разместить сервер или разрабатывать контент для **Corvax Forge Frontier**, используйте этот репозиторий. Он включает **RobustToolbox** и контент-пак для создания новых дополнений.

## Ссылки

<div class="header" align="center">

[Discord](https://discord.gg/7wDwSPde58) | [Steam](https://store.steampowered.com/app/1255460/Space_Station_14/) | [Boosty](https://boosty.to/corvaxforge) | [Вики](https://station14.ru/wiki/%D0%9F%D0%BE%D1%80%D1%82%D0%B0%D0%BB:Frontier)

</div>

## Документация / Вики

Актуальная информация о контенте доступна на [Вики](https://station14.ru/wiki/%D0%9F%D0%BE%D1%80%D1%82%D0%B0%D0%BB:Frontier).

## Сборка

1. Клонируйте репозиторий:
```sh
git clone https://github.com/Forge-Station/Frontier.git
```
2. Инициализируйте подмодули и движок:
```sh
cd Frontier
python RUN_THIS.py
```
3. Соберите решение:
```sh
dotnet build
```

[Подробнее о сборке](https://docs.spacestation14.com/en/general-development/setup.html).

## Лицензия

Правовые аспекты описаны в [LEGAL.md](https://github.com/Forge-Station/Frontier/blob/master/LEGAL.md), включая атрибуцию авторов.

- **Код:** Основная лицензия — MIT.
- **Ассеты:** Большинство под лицензией [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Проверяйте метаданные (например, [crowbar.rsi](https://github.com/Forge-Station/Frontier/blob/master/Resources/Textures/Objects/Tools/crowbar.rsi/meta.json)).
- **Некоммерческий контент:** Некоторые ассеты используют [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) — их необходимо удалить для коммерческого использования.

**Особое разрешение:**
Код из **Emberfall** перелицензирован под MIT с [согласия MilonPL](https://github.com/Forge-Station/Frontier/pull/3607) ([коммит](https://github.com/Forge-Station/Frontier/commit/2fca06eaba205ae6fe3aceb8ae2a0594f0effee0)).
