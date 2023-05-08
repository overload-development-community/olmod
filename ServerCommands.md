# Multiplayer Server Commands

Olmod adds the ability to send commands from any connected client to the dedicated server via the multiplayer chat functionality.
Most of the commands need special privileges. By default, the creator of a match has these chat command permissions,
see [the section on privilege management below](#privilege-management) for details.

## List of available Commands

 * `/KICK <player>`: Kick a player from the server. Player can still reconnect.
 * `/BAN <player>`: Ban player from the server, prevents the player from joining (or creating new matches), but does not immediately disconnect the client.
 * `/KICKBAN <player>`: Kick and Ban a player.
 * `/ANNOY <player>`: Annoy-Ban player: Player is put into spectator state on server, and also implies `/BLOCKCHAT` so player's chat messages are suppressed.
 * `/UNBAN [<player>]`: Unban specific player or all banned players.
 * `/UNANNOY [<player>]`: Remove the annoy-bans for a player or all annoy-banned players. Note that an annoy-banned player needs to re-join before the all of the effects are removed. This implies `/UNBLOCKCHAT`.
 * `/BLOCKCHAT <player>`: Block all chat messages from the specified player on the server, do not relay them to the other clients. This is a ban and will still apply if a player re-connects.
 * `/UNBLOCKCHAT [<player>]`: Remove the block-chat ban for a player or all players.
 * `/START [seconds]`: Only in lobby: Set the lobby countdown to the specified number of seconds (default: 5).
 * `/END`: End the match immediately
 * `/EXTEND [seconds]`: Only in lobby: Adjust the lobby countdown by the specified number of seconds (default: 180). Negative values are allowed to shorten the countdown.
 * `/GIVEPERM <player>`: grant a player the chat command permissions.
 * `/REVOKEPERM [<player>]`: revoke chat command permissions from a player or all players.
 * `/AUTH password`: a server operator can also start the server with the commandline argument `-chatCommandPassword serverPassword`. Any Player knowing this password can get to authenticated state with this command. If no `serverPassword` is set, this command always fails. Note that the password check is **not** case-sensitive.
 * `/STATUS` or `/INFO`: short info about chat command and ban status. No permission required for this command.
 * `/SAY`: send a message to all players which are not blocked for chat
 * `/TEST <player>`: Test player name selection. No permission required for this command.
 * `/SWITCHTEAM [<player>]`: Switch the team a player is in. If used without an argument, it affects the player sending this command. Switching teams for players other than yourself requires chat command permissions.
 * `/LISTPLAYERS [connectionId1 [connectionId2]]`: List players by their connection ID. If no arguments are given, all players are listed. If a single argument is given, it is treated as a connection ID and only
   the player on that ID is queried. If two arguments are given (separated by a single space character), they are treated as a range of connection IDs. Since the chat history shows at most 8 entries, you can split it into multiple queries that way.

### Player Selection and Name Matching

The argument `<player>` may either be a string pattern to match for a player name, or a connection ID, when the prefix `CONN:` or `C:` is given (like `CONN:2`, use `/LISTPLAYERS` command to get the IDs).
You can always use the `/TEST` command to find out which player a specific `<player>` argument would select. The selection by connection ID is useful when there are player names with characters which 
can't be typed in your current language.

Player names are matched to the `<player>` pattern as follows:
 * If the name matches completely, that player is selected
 * If only one player name contains the pattern, that player is selected.
 * If several player names contain the pattern:
   * The one player where the pattern matches earlier in the name is selected
   * It the pattern starts at the same position for multiply players, the one with the least number of extra characters in the rest of the not-matched part of the string is selected
 * The `<player>` pattern contain `?` as wildcard character, matching every possible character (including non-typable unicode ones). You can use the `/TEST` command to check which player would be selected.
 * Spaces are allowed in the `<player>` pattern. Everything after the first space character separating the command from the argument is used as-is.

## Privilege Management

The person who initially created the match has always permission to use the chat commands. Further players can be added by using the `/GIVEPERM` command, 
and revoked with `/REVOKEPERM`. If the server operator specified a chat command password, any player knowing the password can also self-authenticate 
via `/AUTH` command.

### Persistence of bans and privileges

The bans and command privileges stay effective until a new game is created on the server _by a different, unbanned person_.
Bans and permissions are not persistently stored - if the server process is restarted, all bans and permissions are reset.

If a match is created by the same creator as before, or a player who has chat command privileges, the match creator is informed
by the server that all bans and permissions were kept. If a new match is created by any other player, the match creator is informed
that all bans and permissions were reset _if_ any bans and permissions were active before.

Players can always use the `/STATUS` command to check whether bans or chat permissions are active. This command does not require any permissions.

## Command Line Options for controlling chat commands

Server operators can further control the chat commands via commandline arguments:
 * `-disableChatCommands` will disable all chat commands on this server
 * `-trustedPlayerIds id1[,...,idN]` will add the specified player IDs (sepatated by comma, colon or semicolon) as _trusted players_. Trusted Players always have chat command permission, and can not be kicked or banned from the server. Note that the player IDs are **not** player names. Server operators can find the player IDs in the server log file.
 * `-chatCommandPassword serverPassword` will allow players knowing the password to authenticate themselves with the `/AUTH` command.

