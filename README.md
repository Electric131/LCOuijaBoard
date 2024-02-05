# Quick TLDR
Do you want the ability to speak to the dead?
Look no further! Dead players can now speak though ouija boards.

# Features
Adds a UI message system that is used with a ouija board that can be found.

Press 'o' while dead to enable the message popup.
With the UI open, you can type any message (up to 10 characters).
(Spaces will not be counted and will not be written)

You can also use the following shortcut messages (1 movement vs 3/2/7 respectively)
Shortcut messages included:
- y / yes
- n / no
- bye / goodbye

# Contact/Find
[Thunderstore](https://thunderstore.io/c/lethal-company/p/Electric131/OuijaBoard/)

**PLEASE REPORT BUGS TO GITHUB!**

[Github](https://github.com/Electric131/LCOuijaBoard)

[Unofficial Discord](https://discord.gg/lethal-company) [Forum Post](https://discord.com/channels/1169792572382773318/1186411214390181908)

(btw I would love it if you sent clips of the Ouija Board in action in the forum post <3)

# Changelog / Patchnotes

## v0.1.0
First beta build.
- Basic spawning and networking
- Ability to use /ouija (Now Deprecated)
- No check for if player dead

## v0.1.1
- README updated.. that's all

## v1.0.0
- OFFICIAL RELEASE!!
- I decided to add a github url for issues and possible pull requests.
- Changed system to use a UI instead of chat
- No responsive feedback for message not sending (will fix in later UI changes)

## v1.0.1
- Fixed the tooltip on the Ouija Board item

## v1.0.2
- Fixed UI not opening
- Removed attempt to keep menu open on Enter

## v1.1.0
- UI closes automatically when you respawn
- Config options for scrap / shop items as well as weight / cost respectively.
- Fixed minor bugs

## v1.2.0
- Scrap vs store versions of Ouija Board items are separated
- Player insanity increases when near a moving Ouija Board
- Updated importance of any word (not just yes/no/goodbye)

## v1.2.1
- Proper invalid character checking
- Character limit fixed to not include spaces

## v1.3.0 - DO NOT USE
- Fixed scrap spawn being tied to store config (thanks karmaBonfire)
- New config option to make enemies hear the sliding sound (Ex. Dogs)
- Paddle stays where it is longer (moves then stays there longer)
- Fixed sliding sound being global
- LEFT DEBUG VAR ON (DO NOT USE THIS VERSION)

## v1.3.1
- Disabled dev var ._.

## v1.4.0
- Fixed timing of movements scaling to player count (now constant)
- Added text in Text UI to show if boards are on cooldown before you hit enter

## v1.5.0
- Bypass interact checks when UI is open by making game temporarily think player is alive (WORKS WITH SPECTATE ENEMIES!!!)

## v1.5.1
- Config option to change menu keybind. (brought up by demosche)

## v1.5.2
- Added config options for min and max price
- Fixed error logging when player was invalid

## v1.5.3
- Fixes compatibility with SpectateEnemies (Due to the changed input method)