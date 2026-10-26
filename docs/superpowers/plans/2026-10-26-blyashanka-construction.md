# Blyashanka Construction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить детали и полный YAML-граф сборки Бляшанки, завершающийся вооружённым мехом с перенесённой энергоячейкой.

**Architecture:** Все новые ресурсы изолируются в `_Sunrise`: отдельный RSI, прототипы деталей и отдельный construction graph. Существующий `BuildMech` переносит только батарею, поэтому граф создаёт технический вариант Бляшанки с двумя предустановленными мех-модулями РПД, но без `ContainerFill` батареи.

**Tech Stack:** SS14 YAML prototypes, Fluent localization, RSI/PNG assets, `Content.YAMLLinter`.

**Spec:** `docs/superpowers/specs/2026-10-26-blyashanka-construction-design.md`

## Global Constraints

- Не изменять C#.
- Все новые прототипы и ассеты размещать под `_Sunrise` и использовать префикс `Sunrise` для новых prototype/tag ID.
- Сохранить имена sprite states `banka_*` и `jar*` из исходных файлов.
- Не создавать `jar12`; после обработки проводов визуал остаётся `jar11`, следующий визуал — `jar13`.
- Финальный оружейный шаг принимает `WeaponLightMachineGunRPD` и `SunriseWeaponMechCombatRpd` в любой комбинации.
- Не добавлять рецепты техфаба и комплектный ящик.
- Добавить отдельный `MechBreakageBlyashanka` без C# и переключить на него базовый прототип Бляшанки.

## Review Focus

- В RSI существуют все состояния, на которые ссылаются прототипы и visualizer, кроме намеренно неиспользуемого `jar12`.
- `PartAssembly` требует ровно четыре уникальные ноги и корректно отображает каждый слой `+o`.
- Оба типа РПД имеют общий тег, а граф требует ровно два предмета с этим тегом.
- Итоговый прототип не создаёт вторую батарею и получает два мех-модуля РПД.
- Разрушение Бляшанки использует только её spawner-прототипы частей и совместимый spawner плат Gygax.
- Все новые FTL-ключи соответствуют prototype ID и существуют в `en-US` и `ru-RU`.

---

### Task 1: RSI деталей и стадий сборки

**Files:**
- Create: `Resources/Textures/_Sunrise/Objects/Specific/Mech/blyashanka_construction.rsi/meta.json`
- Create: `Resources/Textures/_Sunrise/Objects/Specific/Mech/blyashanka_construction.rsi/*.png`

**Interfaces:**
- Consumes: PNG из `C:\Users\kaise\Downloads\banka` и `части тела`.
- Produces: states `banka_harness`, `banka_harness+o`, четыре пары состояний ног, `banka_armor`, `jar2`–`jar11`, `jar13`–`jar46`.

- [ ] Скопировать исходные PNG без преобразования пикселей и создать RSI metadata размером 32x32, лицензией CC-BY-SA-4.0 и атрибуцией из существующего `blyashanka.rsi`.
- [ ] Проверить JSON и убедиться, что список файлов совпадает со списком states, а `jar1`/`jar12` отсутствуют.
- [ ] Зафиксировать изменения отдельным коммитом ассетов.

### Task 2: Детали, теги и локализация

**Files:**
- Create: `Resources/Prototypes/_Sunrise/Entities/Objects/Specific/Mech/blyashanka_construction.yml`
- Modify: `Resources/Prototypes/_Sunrise/tags.yml`
- Modify: `Resources/Prototypes/_Sunrise/Entities/Objects/Weapons/Guns/LMGs/lmgs.yml`
- Modify: `Resources/Prototypes/_Sunrise/Entities/Objects/Specific/Mech/Weapons/Gun/combat.yml`
- Modify: `Resources/Prototypes/_Sunrise/Entities/Objects/Specific/Mech/mechs.yml`
- Modify: `Resources/Prototypes/_Sunrise/Entities/Objects/Specific/Mech/mech_breakage.yml`
- Create: `Resources/Locale/en-US/_prototypes/_sunrise/entities/objects/specific/mech/blyashanka_construction.ftl`
- Create: `Resources/Locale/ru-RU/_prototypes/_sunrise/entities/objects/specific/mech/blyashanka_construction.ftl`

**Interfaces:**
- Consumes: states из Task 1 и существующие прототипы `SunriseMechBlyashanka`, `WeaponLightMachineGunRPD`, `SunriseWeaponMechCombatRpd`.
- Produces: каркас, четыре ноги, шасси, бронеплиты, assembly/tag IDs и технический вооружённый результат без встроенной батареи.
- Produces: отдельный `MechBreakageBlyashanka` и spawner-прототипы собственных частей.

- [ ] Добавить теги для четырёх ног, бронеплит и допустимого оружия Бляшанки.
- [ ] Добавить общий оружейный тег на обычный и меховый РПД.
- [ ] Создать прототипы деталей с `ItemMapper`, `PartAssembly`, `Construction`, `MechAssemblyVisuals` и `battery-container` по образцу Колобка.
- [ ] Создать технический результат сборки, наследующий `SunriseMechBlyashanka`, с двумя `SunriseWeaponMechCombatRpd` и без `ContainerFill` батареи.
- [ ] Создать spawner-прототипы каркаса, четырёх ног и бронеплит Бляшанки, затем `MechBreakageBlyashanka` с ними и `MechGygaxBoardSpawnerLow`.
- [ ] Заменить у `SunriseMechBlyashanka` родителя `MechBreakageGygax` на `MechBreakageBlyashanka`.
- [ ] Добавить согласованную английскую и русскую локализацию всех новых сущностей.
- [ ] Проверить уникальность prototype/tag/FTL ID и ссылки на RSI states.
- [ ] Зафиксировать изменения отдельным коммитом прототипов деталей.

### Task 3: Граф сборки и проверка

**Files:**
- Create: `Resources/Prototypes/_Sunrise/Recipes/Construction/Graphs/mechs/blyashanka_construction.yml`
- Modify: `Resources/Locale/en-US/_strings/_sunrise/construction/steps.ftl`
- Modify: `Resources/Locale/ru-RU/_strings/_sunrise/construction/steps.ftl`

**Interfaces:**
- Consumes: assembly ID, armor tag, weapon tag, chassis and result prototype from Task 2.
- Produces: полный construction graph `SunriseBlyashanka` со стадиями из спецификации.

- [ ] Реализовать шаги от `jar2` до `jar14`, пропуская visualizer update для отсутствующего `jar12`.
- [ ] Реализовать шаги `jar15`–`jar40` с указанными количествами стали, манипуляторов, проводов и вспышек, платами Gygax, бронестеклом, энергоячейкой и баллоном.
- [ ] Реализовать `jar41`–`jar45` через бронеплиты и четыре применения `Anchoring`.
- [ ] Реализовать два последовательных оружейных шага с общим тегом; первый не меняет визуал, второй устанавливает `jar46` и завершает сборку техническим вооружённым прототипом.
- [ ] Добавить локализованные названия новых construction steps.
- [ ] Запустить `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build`; если бинарник отсутствует, сначала выполнить Release build по репозиторному правилу.
- [ ] Проверить `git diff --check`, итоговый diff и отсутствие запущенных процессов тестирования.
- [ ] Зафиксировать проверенную реализацию отдельным коммитом.

