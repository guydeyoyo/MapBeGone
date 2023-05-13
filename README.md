# MapBeGone

Allows fine control over the removal of maps from the game with ServerSync for no map and semi-map worlds.

Allows you to:
* Use or remove the minimap.
* Use or remove the large map.
* Use or remove opening large map via cartography table.
* Use or remove opening large map via vegvisir stones.
* Use or remove writing changes to cartography table.
* Allow or deny click-to-ping on the large map and cartography table.
* Allow or deny zoom of minimap (if minimap enabled).
* Show or hide biome name on minimap (if minimap enabled).
* Show or hide wind direction on minimap (if minimap enabled).
* Enable or disable forced sharing of player map positions.
* Manage discovery radius when not "attached" to a boat.
* Manage discovery radius when sitting, holding fast, or using rudder on a boat.
* ServerSync for all of these individual options.


## Configuration
A configuration file ``BepInEx\config\Yoyo.MapBeGone.cfg`` is created after starting the game. You can adjust the values in this configuration file using a standard text editor or *Config Editor* in r2modman. All of the configuration can be changed on-the-fly or controlled via a dedicated server.


## Server Synchronization
All configuration options are synchronized with the server, if enabled server-side.


## Installation
Unzip the contents to your ``BepInEx\plugins`` folder.


## Thanks

* [Azumatt](https://valheim.thunderstore.io/package/Azumatt/) for their videos and examples.
* [blaxxun-boop](https://github.com/blaxxun-boop) for ServerSync.


## License

MIT No Attribution


## Changelog

### 1.3.0

* Updated references to work with game version 0.215.2.
* Fixed MissingFieldException.


### 1.2.0

* Updated references to work with game version 211.11.
* Updated ServerSync to 1.13.
* Added management of discovery radius when not "attached" to a boat.
* Added management of discovery radius when sitting, holding fast, or using rudder on a boat.


### 1.1.0
If upgrading from 1.0.0, you need to delete your existing config file and set up a new config file.

* Added management of minimap zoom (if minimap enabled).
* Added management of biome name on minimap (if minimap enabled).
* Added management of wind direction on minimap (if minimap enabled).


### 1.0.0
Initial release.