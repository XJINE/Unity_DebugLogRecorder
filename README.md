# Unity_DebugLogRecorder

Replaces the default Debug.unityLogger.logHandler and records log history per class.

## Importing

You can use Package Manager or import it directly.

```
https://github.com/XJINE/Unity_DebugLogRecorder.git?path=Assets/Packages/DebugLogRecorders
```

## How To Use

Just call `Debug.Log()` as usual.
Then get the history with `DebugLogRecorder.GetHistory<T>()`.

### Recommends

- https://github.com/XJINE/Unity_DebugLogContextWeaver

DebugLogContextWeaver adds `context` to your `Debug.Log()` calls automatically,
so each entry also records the instance name and ID.