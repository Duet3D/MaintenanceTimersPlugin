# Maintenance Timers Plugin

## Description

This plugin maintains custom timers every minute, which can be used to monitor machine usage. These timers are organized in `esys/timers.json` and each timer in the timer list may provide the following fields:

```
{
	"name": "TimerName",
	"title": "User-defined title",
	"initialValue": 0,
	"value": 0,
	"conditions": [
		"condition a",
		...
	],
	"thresholdValue": 0,
	"action": "M118 S\"Timer expired\"",
	"canReset": true
}
```

The fields mean the following:

- `name`: Short name of the timer used to identify it
- `title`: User-defined title for this timer
- `value`: Current value of this timer
- `conditions`: List of conditions (expressions) to be met in order for this timer to be updated
- `thresholdValue`: Threshold value (in mins) or -1 if not applicable
- `action`: Action to perform (G/M/T-code) when the timer reaches the threshold value
- `canReset`: Can this timer be reset

If the `timers.json` file could not be loaded, this plugin attempts to load `timers.json.bak` instead.
Before `timers.json` is overwritten, it is also moved to `timers.json.bak`.

## Building

Run `build.sh` in the `pkg` directory on a Linux machine with dotnet SDK and dpkg utilities installed to generate a Debian package.


## Bug reports

Please use the [forum](https://forum.duet3d.com) for support requests or the [DuetSoftwareFramework](https://github.com/Duet3D/DuetSoftwareFramework) GitHub repository for feature requests and bug reports.
