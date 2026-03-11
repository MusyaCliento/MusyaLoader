## Strings for the drop-down window to manage your active account

account-drop-down-none-selected = No account selected
account-drop-down-not-logged-in = Not logged in
account-drop-down-log-out = Log out
account-drop-down-log-out-of = Log out of { $name }
account-drop-down-switch-account = Switch account:
account-drop-down-select-account = Select account:
account-drop-down-add-account = Add account

## Localization for the "add favorite server" dialog window

add-favorite-window-title = Add Favorite Server
add-favorite-window-address-invalid = Address is invalid
add-favorite-window-label-name = Name:
add-favorite-window-label-address = Address:
# 'Example' name shown as a watermark in the name input box
add-favorite-window-example-name = Honk Station

## Strings for the "connecting" menu that appears when connecting to a server.

connecting-title-connecting = Connecting…
connecting-title-content-bundle = Loading…
connecting-cancel = Cancel
connecting-status-none = Starting connection…
connecting-status-update-error =
    There was an error while downloading server content. If this persists try some of the following:
    - Try connecting to another game server to see if the problem persists.
    - Try disabling or enabling software such as VPNs, if you have any.

    If you are still having issues, first try contacting the server you are attempting to join before asking for support on the Official Space Station 14 Discord or Forums.

    Technical error: { $err }
connecting-status-update-error-no-engine-for-platform = This game is using an older version that does not support your current platform. Please try another server or try again later.
connecting-status-update-error-no-module-for-platform = This game requires additional functionality that is not yet supported on your current platform. Please try another server or try again later.
connecting-status-update-error-unknown = Unknown
connecting-status-updating = Updating: { $status }
connecting-status-connecting = Fetching connection info from server…
connecting-status-connection-failed = Failed to connect to server!
connecting-status-starting-client = Starting client…
connecting-status-not-a-content-bundle = File is not a valid content bundle!
connecting-status-client-crashed = Client seems to have crashed while starting. If this persists, please ask on Discord or GitHub for support.
connecting-update-status-checking-client-update = Checking for server content update…
connecting-update-status-downloading-engine = Downloading server content…
connecting-update-status-downloading-content = Downloading server content…
connecting-update-status-fetching-manifest = Fetching server manifest…
connecting-update-status-verifying = Verifying download integrity…
connecting-update-status-culling-engine = Clearing old content…
connecting-update-status-culling-content = Clearing old server content…
connecting-update-status-ready = Update done!
connecting-update-status-checking-engine-modules = Checking for additional dependencies…
connecting-update-status-downloading-engine-modules = Downloading extra dependencies…
connecting-update-status-committing-download = Synchronizing to disk…
connecting-update-status-loading-into-db = Storing assets in database…
connecting-update-status-loading-content-bundle = Loading content bundle…
connecting-update-status-unknown = You shouldn't see this

connecting-privacy-policy-text = This server requires that you accept its privacy policy before connecting.
connecting-privacy-policy-text-version-changed = This server has updated its privacy policy since the last time you played. You must accept the new version before connecting.
connecting-privacy-policy-view = View privacy policy
connecting-privacy-policy-accept = Accept (continue)
connecting-privacy-policy-decline = Decline (disconnect)

## Strings for the "direct connect" dialog window.

direct-connect-title = Direct Connect
direct-connect-text = Enter server address to connect:
direct-connect-connect = Connect
direct-connect-address-invalid = Address is invalid

## Strings for the "hub settings" dialog window.

hub-settings-title = Hub Settings
hub-settings-button-done = Done
hub-settings-button-cancel = Cancel
hub-settings-button-reset = Reset
hub-settings-button-reset-tooltip = Reset to default settings
hub-settings-button-add-tooltip = Add hub
hub-settings-button-remove-tooltip = Remove hub
hub-settings-button-increase-priority-tooltip = Increase priority
hub-settings-button-decrease-priority-tooltip = Decrease priority
hub-settings-explanation = Here you can add extra hubs to fetch game servers from. You should only add hubs that you trust, as they can 'spoof' game servers from other hubs. The order of the hubs matters; if two hubs advertise the same game server the hub with the higher priority (higher in the list) will take precedence.
hub-settings-heading-default = Default
hub-settings-heading-custom = Custom
hub-settings-warning-invalid = Invalid hub (don't forget http(s)://)
hub-settings-warning-duplicate = Duplicate hubs

## Strings for the login screen

login-log-launcher = Log Launcher

## Error messages for login

login-error-invalid-credentials = Invalid login credentials
login-error-account-unconfirmed = The email address for this account still needs to be confirmed. Please confirm your email address before trying to log in
login-error-account-2fa-required = 2-factor authentication required
login-error-account-2fa-invalid = 2-factor authentication code invalid
login-error-account-account-locked = Account has been locked. Please contact us if you believe this to be in error.
login-error-unknown = Unknown error
login-errors-button-ok = Ok

## Strings for 2FA login

login-2fa-title = 2-factor authentication required
login-2fa-message = Please enter the authentication code from your app.
login-2fa-input-watermark = Authentication code
login-2fa-button-confirm = Confirm
login-2fa-button-recovery-code = Recovery code
login-2fa-button-cancel = Cancel

## Strings for the "login expired" view on login

login-expired-title = Login expired
login-expired-message =
    The session for this account has expired.
    Please re-enter your password.
login-expired-password-watermark = Password
login-expired-button-log-in = Log in
login-expired-button-log-out = Log out
login-expired-button-forgot-password = Forgot your password?

## Strings for the "forgot password" view on login

login-forgot-title = Forgot password?
login-forgot-message = If you've forgotten your password, you can enter the email address associated with your account here to receive a reset link.
login-forgot-email-watermark = Your email address
login-forgot-button-submit = Submit
login-forgot-button-back = Back to login
login-forgot-busy-sending = Sending email…
login-forgot-success-title = Reset email sent
login-forgot-success-message = A reset link has been sent to your email address.
login-forgot-error = Error

## Strings for the "login" view on login

login-login-title = Log in
login-login-username-watermark = Username or email
login-login-password-watermark = Password
login-login-show-password = Show Password
login-login-button-log-in = Log in
login-login-button-forgot = Forgot your password?
login-login-button-resend = Resend email confirmation
login-login-button-register = Don't have an account? Register!
login-login-busy-logging-in = Logging in…
login-login-error-title = Unable to log in

## Strings for the "register confirmation" view on login

login-confirmation-confirmation-title = Register confirmation
login-confirmation-confirmation-message = Please check your email to confirm your account. Once you have confirmed your account, press the button below to log in.
login-confirmation-button-confirm = I have confirmed my account
login-confirmation-button-cancel = Cancel
login-confirmation-busy = Logging in…

## Strings for the general main window layout of the launcher

main-window-title = Space Station 14 Launcher
main-window-header-link-discord = Discord
main-window-header-link-website = Website
main-window-out-of-date = Launcher out of date
main-window-out-of-date-desc =
    This launcher is out of date.
    Please download a new version from our website.
main-window-out-of-date-desc-steam =
    This launcher is out of date.
    Please allow Steam to update the game.
main-window-out-of-date-exit = Exit
main-window-out-of-date-download-manual = Download (manual)
main-window-early-access-title = Heads up!
main-window-early-access-desc = Space Station 14 is still very much in alpha. We hope you like what you see, and maybe even stick around, but make sure to keep your expectations modest for the time being.
main-window-early-access-accept = Got it!
main-window-intel-degrade-title = Intel 13th/14th Generation CPU detected.
main-window-intel-degrade-desc =
    The Intel 13th/14th generation CPUs are known to silently degrade permanently and die due to a microcode bug by Intel. We sadly can't tell if you are currently affected by this bug, so this warning appears for all users with these CPUs.

    We STRONGLY encourage you to update your motherboard's BIOS to the latest version to ensure prevention of further damage. If you are having stability issues/failing to start the game, downclock your CPU to get it stable again and use your warranty to ask about getting it replaced.

    We are not responsible to help with any issues that may arise from affected processors, unless you took the precautions and are sure your CPU is stable. This message will not appear again after you accept it.
main-window-intel-degrade-accept = I understand and have taken the necessary precautions.
main-window-rosetta-title = You are running the game using Rosetta 2!
main-window-rosetta-desc =
    You seem to be on an Apple Silicon Mac and are running the game using Rosetta 2. You may enjoy better performance and battery life by running the game natively.

    To do this, right click the launcher in Finder, select "Get Info" and uncheck "Open using Rosetta". After that, restart the launcher.

    If you are intentionally running the game using Rosetta 2, you can dismiss this message and it will not appear again. Although if you are doing this in an attempt to fix a problem, please make a bug report.
main-window-rosetta-accept = Continue
main-window-drag-drop-prompt = Drop to run game
main-window-busy-checking-update = Checking for launcher update…
main-window-busy-checking-login-status = Refreshing login status…
main-window-busy-checking-account-status = Checking account status
main-window-error-connecting-auth-server = Error connecting to authentication server
main-window-error-unknown = Unknown error occurred

## Long region names for server tag filters (shown in tooltip)

region-africa-central = Africa Central
region-africa-north = Africa North
region-africa-south = Africa South
region-antarctica = Antarctica
region-asia-east = Asia East
region-asia-north = Asia North
region-asia-south-east = Asia South East
region-central-america = Central America
region-europe-east = Europe East
region-europe-west = Europe West
region-greenland = Greenland
region-india = India
region-middle-east = Middle East
region-the-moon = The Moon
region-north-america-central = North America Central
region-north-america-east = North America East
region-north-america-west = North America West
region-oceania = Oceania
region-south-america-east = South America East
region-south-america-south = South America South
region-south-america-west = South America West

## Short region names for server tag filters (shown in filter check box)

region-short-africa-central = Africa Central
region-short-africa-north = Africa North
region-short-africa-south = Africa South
region-short-antarctica = Antarctica
region-short-asia-east = Asia East
region-short-asia-north = Asia North
region-short-asia-south-east = Asia South East
region-short-central-america = Central America
region-short-europe-east = Europe East
region-short-europe-west = Europe West
region-short-greenland = Greenland
region-short-india = India
region-short-middle-east = Middle East
region-short-the-moon = The Moon
region-short-north-america-central = NA Central
region-short-north-america-east = NA East
region-short-north-america-west = NA West
region-short-oceania = Oceania
region-short-south-america-east = SA East
region-short-south-america-south = SA South
region-short-south-america-west = SA West

## Strings for the "servers" tab

tab-servers-title = Servers
tab-servers-refresh = Refresh
filters = Filters ({ $filteredServers } / { $totalServers })
tab-servers-search-watermark = Search For Servers…
tab-servers-table-players = Players
tab-servers-table-name = Server Name
tab-servers-table-round-time = Time
tab-servers-table-map = Map
tab-servers-table-mode = Mode
tab-servers-table-ping = Ping
tab-servers-list-status-error = There was an error fetching the master server lists. Maybe try refreshing?
tab-servers-list-status-partial-error = Failed to fetch some of the server lists. Ensure your hub configuration is correct and try refreshing.
tab-servers-list-status-updating-master = Fetching master server list…
tab-servers-list-status-none-filtered = No servers match your search or filter settings.
tab-servers-list-status-none = There are no public servers. Ensure your hub configuration is correct.

## Strings for the server filters menu

filters-title = Filters
filters-title-language = Language
filters-title-region = Region
filters-title-rp = Role-play level
filters-title-player-count = Player count
filters-title-18 = 18+
filters-title-hub = Hub
filters-18-yes = Yes
filters-18-yes-desc = Yes
filters-18-no = No
filters-18-no-desc = No
filters-player-count-hide-empty = Hide empty
filters-player-count-hide-empty-desc = Servers with no players will not be shown
filters-player-count-hide-full = Hide full
filters-player-count-hide-full-desc = Servers that are full will not be shown
filters-player-count-minimum = Minimum:
filters-player-count-minimum-desc = Servers with less players will not be shown
filters-player-count-maximum = Maximum:
filters-player-count-maximum-desc = Servers with more players will not be shown
filters-unspecified-desc = Unspecified
filters-unspecified = Unspecified

## Server roleplay levels for the filters menu

filters-rp-none = None
filters-rp-none-desc = None
filters-rp-low = Low
filters-rp-low-desc = Low
filters-rp-medium = Medium
filters-rp-medium-desc = Medium
filters-rp-high = High
filters-rp-high-desc = High

## Strings for entries in the server list (including home page)

server-entry-connect = Connect
server-entry-add-favorite = Favorite
server-entry-remove-favorite = Unfavorite
server-entry-offline = OFFLINE
server-entry-player-count =
    { $players } / { $max ->
        [0] ∞
       *[1] { $max }
    }
server-entry-round-time = { $hours ->
 [0] { $mins }M
*[1] { $hours }H { $mins }M
}
server-entry-fetching = Fetching…
server-entry-description-offline = Unable to contact server
server-entry-description-fetching = Fetching server status…
server-entry-description-error = Error while fetching server description
server-entry-description-none = No server description provided
server-entry-status-lobby = Lobby
server-fetched-from-hub = Fetched from { $hub }
server-entry-raise = Raise to top

## Strings for the "Development" tab
## These aren't shown to users so they're not very important

tab-development-title = { "[" }DEV]
tab-development-title-override = { "[" }DEV (override active!!!)]
tab-development-disable-signing = Disable Engine Signature Checks
tab-development-disable-signing-desc = { "[" }DEV ONLY] Disables verification of engine signatures. DO NOT ENABLE UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING.
tab-development-enable-engine-override = Enable engine override
tab-development-enable-engine-override-desc = Override path to load engine zips from (release/ in RobustToolbox)

## Strings for the "home" tab

tab-home-title = Home
tab-home-favorite-servers = Favorite Servers
tab-home-add-favorite = Add favorite
tab-home-refresh = Refresh
tab-home-direct-connect = Direct connect to server
tab-home-run-content-bundle = Run content bundle/replay
tab-home-go-to-servers-tab = Go to the servers tab
tab-home-favorites-guide = Mark servers as favorite for easy access here

## Strings for the "news" tab

tab-news-title = News
tab-news-recent-news = Recent News:
tab-news-pulling-news = Pulling news…

## Strings for the "options" tab

tab-options-title = Options
tab-options-flip = { "*" }flip
tab-options-clear-engines = Clear engines
tab-options-clear-content = Clear server content
tab-options-clear-content-close-client = Close running clients first
tab-options-open-log-directory = Open log directory
tab-options-account-settings = Account Settings
tab-options-account-settings-desc = You can manage your account settings, such as changing email or password, through our website.
tab-options-compatibility-mode = Compatibility Mode
tab-options-compatibility-mode-desc = This forces the game to use a different graphics backend, which is less likely to suffer from driver bugs. Try this if you are experiencing graphical issues or crashes.
tab-options-log-client = Log Client
tab-options-log-client-desc = Enables logging of any game client output. Useful for developers.
tab-options-log-launcher = Log Launcher
tab-options-log-launcher-desc = Enables logging of the launcher. Useful for developers. (requires launcher restart)
tab-options-verbose-launcher-logging = Verbose Launcher Logging
tab-options-verbose-launcher-logging-desc = For when the developers are *very* stumped with your problem. (requires launcher restart)
tab-options-seasonal-branding = Seasonal Branding
tab-options-seasonal-branding-desc = Whatever temporally relevant icons and logos we can come up with.
tab-options-disable-signing = Disable Engine Signature Checks
tab-options-disable-signing-desc = { "[" }DEV ONLY] Disables verification of engine signatures. DO NOT ENABLE UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING.
tab-options-hub-settings = Hub Settings
tab-options-hub-settings-desc = Change what hub server or servers you would like to use to fetch the server list.
tab-options-desc-incompatible = This option is incompatible with your platform and has been disabled.

## For the language selection menu.

# Text on the button that opens the menu.
language-selector-label = Language
# "Save" button.
language-selector-save = Save
# "Cancel" button.
language-selector-cancel = Cancel
language-selector-help-translate = Want to help translate? You can!
language-selector-system-language = System language ({ $languageName })
# Used for contents of each language button.
language-selector-language = { $languageName } ({ $englishName })

## Miscellaneous

# Generic "Done!" message used for some buttons.
button-done = Done!


# Marsey

marsey-Header-Game = Game
marsey-Explicitly-disallow-HWID = Explicitly disallow HWID
marsey-Explicitly-disallow-HWID-1 = [Patchless] HWId2 - Opt out of sending your HWId to the server.
marsey-Explicitly-disallow-HWID-2 = Servers may require a HWId in the future, as HWId2 works (sort of) on Linux.
marsey-Compatibility-Mode-Short = Compatibility Mode
marsey-Compatibility-Mode = This uses OpenGL ES 2 (via ANGLE if necessary), which is less likely to suffer from driver bugs. Try this if you are experiencing graphical issues or crashes.
marsey-Experimental-Performance-Options-Short = Experimental Performance Options
marsey-Experimental-Performance-Options = Experimental .NET 6 environment variables that enable full Dynamic PGO. Disable if you experience issues.
marsey-Server-List-View = Server List Display
marsey-Server-List-Show-Time = Show round time
marsey-Server-List-Show-Players = Show players
marsey-Server-List-Show-Map = Show map
marsey-Server-List-Show-Mode = Show mode
marsey-Server-List-Show-Ping = Show ping
marsey-Server-List-View-Desc = These options control what fields are shown in Home and Servers lists. Ping colors: green good, yellow medium, red high.
marsey-Hub-Settings = Hub Settings
marsey-Hub-Settings-Desc = Change what hub server or servers you would like to use to fetch the server list.
marsey-Account-Settings-Short = Account Settings
marsey-Account-Settings-Desc = You can manage your account settings, such as changing email or password, through our website.
tab-options-clear-engines = Clear engines
tab-options-clear-content = Clear server content

## Marsey Options Tab - Safety Tab

marsey-Header-Safety = Safety
marsey-Hide-Level = Hide Level
marsey-HideLevel-Desc = Sets degree to which Marsey hides itself.
marsey-HideLevel-Disabled = Hidesey is disabled. Servers with engine version 183.0.0 or above will crash the client.
marsey-HideLevel-Dublicit = Patcher is hidden from the game. Patches remain visible to allow administrators to inspect which patches are being used.
marsey-HideLevel-Normal = Patcher and patches are hidden.
marsey-HideLevel-Explicit = Patcher and patches are hidden. Separate patch logging is disabled.
marsey-HideLevel-Unconditional = Patcher, patches are hidden. Separate patch logging, Subversion is disabled.
marsey-HideLevel-Unknown = Unknown hide level.

marsey-Launcher-Behavior = Launcher Behavior
marsey-Disable-Auto-Login = Disable Automatic Login
marsey-Disable-Auto-Login-Desc = Do not log in into last active account when starting the launcher.

marsey-Game-Behavior = Game Behavior
marsey-Disable-RPC = Disable RPC
marsey-Disable-RPC-Desc = Does not let Discord RPC initialize, hiding your username and server from your profile.
marsey-Fake-RPC = Fake RPC Username
marsey-Fake-RPC-Desc = Changes the username on Discord Rich Presence.
marsey-RPC-Username-Desc = Set your username below. This username will be shown in the discord rich presence activity when hovering on the big icon.
marsey-Set-RPC-Username-Button = Set username

marsey-Disable-Redial = Disable Redial
marsey-Disable-Redial-Desc = Does not let game admins (or the game itself) to reconnect you to another station.

marsey-HWID = HWID
marsey-Force-HWID = Force HWID
marsey-Force-HWID-Desc = Force change HWID when joining a server.
marsey-Bind-HWID = Bind hwid to account
marsey-Bind-HWID-Desc = Bind HWID to your account info
marsey-Change-HWID = Change your HWID. Can be set to be empty or any hexadecimal string.
marsey-Set-HWID = Set HWID
marsey-Generate-Random = Generate random

marsey-Patching = Patching
marsey-Except-On-Patch-Fail = Except On Patch Fail
marsey-Except-On-Patch-Fail-Desc = Exits client if any patch fails to apply. Useful when you need all patches applied or debugging a patch.
marsey-Whitelist-RemoteExecuteCommand = Whitelist RemoteExecuteCommand
marsey-Whitelist-RemoteExecuteCommand-Desc = Allows only whitelisted commands to use RemoteExecuteCommand.
marsey-Whitelist-RemoteExecuteCommand-Warn = May break functions in game. Your mileage may vary.

## Marsey Options Tab - Logging Tab

marsey-Header-Logging = Logging
marsey-Logging-Game = Game
marsey-Open-Log-Directory = Open log directory
marsey-Log-Client = Log Client
marsey-Log-Client-Desc = Enables logging of any game client output. Useful for developers.
marsey-Log-Launcher = Log Launcher
marsey-Log-Launcher-Desc = Enables logging of the launcher. Useful for developers. (requires launcher restart)
marsey-Verbose-Launcher-Logging = Verbose Launcher Logging
marsey-Verbose-Launcher-Logging-Desc = For when the developers are *very* stumped with your problem. (requires launcher restart)
marsey-Log-Patches = Log Patcher
marsey-Log-Patches-Desc = Write MarseyLogger output to log.
marsey-Enable-Launcher-Patcher-Logging = Enable launcher-patcher logging
marsey-Enable-Launcher-Patcher-Logging-Desc = Write MarseyLogger output to launcher's stdout.
marsey-Enable-Loader-Debug-Logs = Enable Loader Debug Logs
marsey-Enable-Loader-Debug-Logs-Desc = Enable harmony debug mode, outputting IL code to desktop and providing marsey debug logs.
marsey-Log-Trace-MarseyLogger = Log Trace MarseyLogger messages
marsey-Log-Trace-MarseyLogger-Desc = Write MarseyLogger trace logs to stdout.
marsey-Separate-Logging = Separate Game/Patcher Logs
marsey-Separate-Logging-Desc = Log patcher output to client.marsey.log instead of client.stdout.log.
marsey-Dump-CVars = Dump CVars

## Marsey Options Tab - Guest Tab

account-drop-down-guest-account = Guest
marsey-Header-Guest = Guest
marsey-Guest-Username = Set your guest username.
marsey-Guest-Username-Watermark = Enter guest username
marsey-Guest-Username-Desc = This name will be used for guest sessions
marsey-Set-Guest-Username = Set guest username

## Marsey Options Tab - Misc Tab

marsey-Header-Misc = Misc
marsey-Patchless = Run patchless
marsey-Patchless-Desc = Disables any patching except hiding harmony, essentially acting like a killswitch. Useful when game breaks due to the launcher itself.
marsey-Skip-Privacy-Policy = Skip Privacy Policy
marsey-Skip-Privacy-Policy-Desc = Skip showing the privacy policy screen on connect.
marsey-Bypass-AntiHarmony = Bypass anti-Harmony protection
marsey-Bypass-AntiHarmony-Desc = Skips the Robust GameController anti-Harmony static check that can crash the client by filling memory.
marsey-Bypass-AntiHarmony = Bypass Commit e495159
marsey-Bypass-AntiHarmony-Desc = Bypassing this "Memory Corruption" from wizards

marsey-Patches = Patches
marsey-Dump-Resources = Dump Resources
marsey-Dump-Resources-Desc = Dumps everything client facing off a server and disables itself.
marsey-Open-Dump-Directory = Open dump directory

marsey-Backports = Enable backports
marsey-Backports-Desc = Apply fixes relevant for the fork and/or engine version if available.
marsey-Disable-Global-Backports = Disable global backports
marsey-Disable-Global-Backports-Desc = Disable available backports targeting any engine version.

marsey-Resource-Packs = Resource packs
marsey-Resource-Override = Resource Pack Strict Override
marsey-Resource-Override-Desc = [DEV] Disables Resource Pack fork target checks.

marsey-Title-Manager = Title manager
marsey-Randomize-Window-Titles = Randomize window titles
marsey-Randomize-Window-Titles-Desc = Use a random title, otherwise "MusyaLoader"
marsey-Randomize-Header-Images = Randomize header images
marsey-Randomize-Header-Images-Desc = Use a random header image, otherwise stick to default MusyaLoader
marsey-Randomize-Connection-Messages = Randomize connection messages
marsey-Randomize-Connection-Messages-Desc = Use random, (un)funny messages instead of connection status ones

marsey-Usernames = Usernames
marsey-Change-Username = Change your account's name to something else. This does not change your in-game username, requires a restart.
marsey-Set-Username = Set Username

## Marsey Patches Tab
marsey-Patches-Tab = Patches
marsey-Header-Patches = Patches
marsey-Header-ResourcePacks = Resource Packs

marsey-Enabled = Enabled
marsey-Disabled = Disabled

marsey-OpenModDir = Open Marsey directory
marsey-RecheckMods = Recheck
marsey-Button-Toggle = Toggle

marsey-Patches-Unsandboxed = Patches are unsandboxed, run at own risk.
marsey-patch-type-marsey = Marsey patch
marsey-patch-type-subverter = Subverter patch
marsey-resourcepack-type = Resource pack
marsey-patch-description = Description

marsey-Header-Themes = Themes
marsey-Theme = Theme
marsey-Theme-Desc = Choose a launcher theme.
marsey-Theme-Dark = Dark
marsey-Theme-Light = Eye Burner 3000
marsey-Theme-DarkRed = Dark Red
marsey-Theme-DarkPurple = Dark Purple
marsey-Theme-MidnightBlue = Midnight Blue
marsey-Theme-EmeraldDusk = Emerald Dusk
marsey-Theme-CopperNight = Copper Night
marsey-Theme-Custom = Custom
marsey-Theme-Custom-Colors = Custom theme colors
marsey-Theme-Custom-Colors-Desc = Pick your own colors for background, accent and text.
marsey-Theme-Custom-Background = Background color
marsey-Theme-Custom-Accent = Accent color
marsey-Theme-Custom-Text = Text color
marsey-Theme-Custom-Popup = Popup color
marsey-Theme-Custom-Gradient-Start = Gradient start color
marsey-Theme-Custom-Gradient-End = Gradient end color
marsey-Theme-Custom-Reset = Reset
marsey-Theme-Custom-Import = Import .json
marsey-Theme-Custom-Export = Export .json
marsey-Theme-Gradient = Gradient background
marsey-Theme-Gradient-Desc = Adds a soft diagonal gradient to make flat areas look more alive.
marsey-Theme-Decor = Decorative pattern
marsey-Theme-Decor-Desc = Enables the striped texture in tab and login backgrounds.
marsey-Theme-Font = Font
marsey-Theme-Font-Desc = Choose one of the built-in fonts or load your own .ttf/.otf font file.
marsey-Theme-Font-Selected = Custom font: { $font }
marsey-Theme-Font-Fallback = Font file not supported. Using { $font }
marsey-Theme-Font-Invalid = Font file not supported. Using default
marsey-Theme-Font-Installed = Installed for current user: { $font }
marsey-Theme-Font-Install = Font file not supported. Opening install window for { $font }
marsey-Theme-Font-Install-Restart = Font file not supported. Opening install window for { $font }. Restart the launcher after installing.
marsey-Theme-Font-Restart-Title = Restart Required
marsey-Theme-Font-Restart-Body = Install the font and restart the launcher to apply it.\n\nFont: { $font }
marsey-Theme-Font-Load = Load custom font

## Launcher self-update
launcher-updates-tab-title = Updates
launcher-updates-header = Launcher Updates
launcher-updates-auto = Automatic updates
launcher-updates-auto-desc = Disabled by default. If enabled, update starts automatically.
launcher-updates-notify = Show update notifications
launcher-updates-notify-desc = Enabled by default. Shows update prompt window.
launcher-updates-allow-prerelease = Allow pre-release updates
launcher-updates-allow-prerelease-desc = If enabled, launcher can offer versions from the Pre-Release tag.
launcher-updates-repo = GitHub repository
launcher-updates-repo-placeholder = https://github.com/MusyaCliento/MusyaLoader
launcher-updates-repo-desc = Supported: https://github.com/owner/repo, github.com/owner/repo, .../.git
launcher-updates-list-title = Available versions
launcher-updates-list-refresh = Refresh
launcher-updates-filter-label = Filter:
launcher-updates-filter-all = All
launcher-updates-filter-release-only = Release only
launcher-updates-filter-prerelease-only = Pre-release only
launcher-updates-list-loading = Loading versions...
launcher-updates-list-empty = No versions found in Release / Pre-Release.
launcher-updates-list-count = Found versions: {$count}
launcher-updates-list-channel-release = Release
launcher-updates-list-channel-prerelease = Pre-Release
launcher-updates-list-open = Open update page
launcher-updates-list-install-selected = Install selected

launcher-proxy-header = SOCKS5 Proxy
launcher-proxy-enabled = Enable proxy
launcher-proxy-enabled-desc = Applies to launcher HTTP requests. Restart launcher after changes.
launcher-proxy-host = Proxy host
launcher-proxy-port = Proxy port
launcher-proxy-username = Username (optional)
launcher-proxy-password = Password (optional)
launcher-proxy-loader = Apply proxy to loader
launcher-proxy-loader-desc = Passes proxy via ALL_PROXY/HTTP_PROXY/HTTPS_PROXY to SS14.Loader process.
launcher-proxy-udp-relay = Route game UDP through local relay (experimental)
launcher-proxy-udp-relay-desc = Starts SS14.ProxyService and rewrites connect-address to localhost via SOCKS5 UDP ASSOCIATE.
tab-proxy-title = Proxy
tab-proxy-header = Proxy Profiles
tab-proxy-guard = Block game launch if proxy test fails
tab-proxy-launcher = Proxy launcher
tab-proxy-game = Proxy game (requires UDP test, experimental)
tab-proxy-game-info = Routes game traffic through SS14.ProxyService and SOCKS5 UDP relay. Requires SOCKS5 server support for UDP ASSOCIATE. If UDP is not supported, connection may fail or loop on "Connecting...".
tab-proxy-updates = Proxy updates (GitHub, releases, etc.)
tab-proxy-bypass = Bypass regional restrictions for Robust builds
tab-proxy-add = Add
tab-proxy-edit = Edit
tab-proxy-remove = Remove
tab-proxy-save = Save
tab-proxy-test-selected = Test selected
tab-proxy-test-all = Test all
tab-proxy-col-name = Name
tab-proxy-col-protocol = Protocol
tab-proxy-col-hostport = Host:Port
tab-proxy-col-host = Host
tab-proxy-col-port = Port
tab-proxy-col-user = Username
tab-proxy-col-pass = Password
tab-proxy-col-tcp = Proxy RTT
tab-proxy-col-connect = TCP ping
tab-proxy-col-udp = UDP test
proxy-status-empty = No proxy profiles configured.
proxy-status-loaded = Loaded profiles: {$count}
proxy-status-added = Profile added.
proxy-status-removed = Profile removed.
proxy-status-saved = Saved profiles: {$count}
proxy-status-active = Active profile: {$name}
proxy-status-testing = Running proxy tests...
proxy-status-tested = Proxy tests completed.
proxy-test-running = Running...
proxy-test-ok = OK
proxy-test-fail = Fail
tab-proxy-dialog-title = Proxy profile
tab-proxy-dialog-name = Name
tab-proxy-dialog-host = Host
tab-proxy-dialog-port = Port
tab-proxy-dialog-user = Username
tab-proxy-dialog-pass = Password
tab-proxy-dialog-show-pass = Show
tab-proxy-dialog-save = Save
tab-proxy-dialog-cancel = Cancel
tab-proxy-service-debug = Debug ProxyService (show console window)
tab-proxy-service-independent = Keep ProxyService running after launcher closes
launcher-updates-rate-limit = GitHub API rate limit exceeded. Try again later or set a GitHub token/proxy.
proxy-unavailable-title = Selected proxy is unavailable
proxy-unavailable-message = Could not connect to proxy {$host}:{$port}.\nError: {$error} will be disabled for this session. The proxy will be disabled for this session. Select the correct one and restart the launcher.
proxy-unavailable-ok = OK
proxy-unavailable-settings = Settings

launcher-update-overlay-title = Launcher update available
launcher-update-overlay-version = New version: {$version}
launcher-update-overlay-notes = Release notes:
launcher-update-overlay-install = Install
launcher-update-overlay-open-release = Open release
launcher-update-overlay-skip = Skip
launcher-update-error-macos-manual = Built-in installation is disabled on macOS. Manual update only.
launcher-update-error-unsupported-platform = Auto-install is not supported on this platform.
launcher-update-progress-preparing = Preparing download...
