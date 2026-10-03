# Моды для Mechabellum

MelonLoader-моды. Нужен MelonLoader в папке игры, запущенный хотя бы раз.

- [camera-mod](camera-mod/README.md) — MelperCamera: дальше отдаление, без тряски, настройки авто-камеры (`F6`).
- [scout-mod](scout-mod/README.md) — MelperScout: армии, урон прошлого боя и специалисты на расстановке (`F9`).

Клавиши стороннего мода BattleSuite (`Mechabellum.RoundHistory.dll`) заданы в
`<игра>\UserData\MechabellumRoundHistory\LiveUiConfig.json`: `F7` прячет и
показывает его панель урона, `F8` переключает урон за матч и за раунд. Наши
моды эти клавиши не используют.

## Собрать и установить все

Двойной щелчок по `deploy.bat` или:

```powershell
.\mods\deploy.ps1          # собрать и положить все DLL в <игра>\Mods
.\mods\deploy.ps1 -Run     # и запустить игру через Steam
```

Папка игры по умолчанию `D:\SteamLibrary\steamapps\common\Mechabellum`,
другую можно передать через `-GameRoot`. Если игра запущена, старая DLL
переименовывается в `.dll.old`, а новая загрузится после перезапуска игры.

Каждый мод можно собрать и по отдельности:
`dotnet build mods\scout-mod -c Release -p:Deploy=true`.
