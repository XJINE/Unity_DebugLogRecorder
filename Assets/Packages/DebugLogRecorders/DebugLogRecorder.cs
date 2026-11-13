using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DebugLogRecorders {
public class DebugLogRecorder : ILogHandler
{
    // CAUTION:
    // GetInstanceID() is obsolete since Unity 6.4 and a compile error since 6.5.
    // GetEntityId() is available from Unity 6.3 (6000.3).

    private const int DefaultMaxHistoryPerClass = 20;

    private static int _maxHistoryPerClass = DefaultMaxHistoryPerClass;

    public static int MaxHistoryPerClass
    {
        get => _maxHistoryPerClass;
        set
        {
            lock (HistoryByClass)
            {
                _maxHistoryPerClass = Math.Max(1, value);

                foreach (var queue in HistoryByClass.Values)
                {
                    while (queue.Count > _maxHistoryPerClass)
                    {
                        queue.Dequeue();
                    }
                }
            }
        }
    }

    public struct LogEntry
    {
        public DateTime  Timestamp;
        public LogType   LogType;
        #if UNITY_6000_3_OR_NEWER
        public EntityId? InstanceId;
        #else
        public int?      InstanceId;
        #endif
        public string    InstanceName;
        public string    Message;
    }

    // NOTE:
    // C# does not allow an indexer (this[...]) on a static class or as a static member.
    // So an API like DebugLogRecorder[key] is not possible.

    private static readonly Dictionary<string, Queue<LogEntry>> HistoryByClass = new();

    private static ILogHandler _defaultLogHandler;

    #if UNITY_EDITOR
    [InitializeOnLoadMethod]
    #endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Setup()
    {
        if (Debug.unityLogger.logHandler is not DebugLogRecorder)
        {
            _defaultLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = new DebugLogRecorder();
        }
    }

    // NOTE:
    // "HideInCallstack" is required so that double-clicking a log in the Editor Console
    // jumps to where the log was actually emitted.

    [HideInCallstack]
    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        var message = format == "{0}" && args is { Length: 1 } && args[0] is string s
                    ? s
                    : string.Format(format, args);

        Record(logType, context, message);

        _defaultLogHandler.LogFormat(logType, context, format, args); // Output to default editor console.
    }

    [HideInCallstack]
    public void LogException(Exception exception, UnityEngine.Object context)
    {
        Record(LogType.Exception, context, exception.ToString());
        _defaultLogHandler.LogException(exception, context);
    }

    [HideInCallstack]
    private static void Record(LogType logType, UnityEngine.Object context, string message)
    {
        string    className;
        #if UNITY_6000_3_OR_NEWER
        EntityId? instanceId = null;
        #else
        int?      instanceId = null;
        #endif
        string    instanceName = null;

        // NOTE:
        // When called like Debug.Log(message:msg, context:this)
        if (context != null)
        {
            className    = context.GetType().Name;
            #if UNITY_6000_3_OR_NEWER
            instanceId   = context.GetEntityId();
            #else
            instanceId   = context.GetInstanceID();
            #endif
            instanceName = context.name;
        }
        // NOTE:
        // When called like Debug.Log(message:msg)
        else
        {
            className = FindCallerClassName();
        }

        lock (HistoryByClass)
        {
            if (!HistoryByClass.TryGetValue(className, out var queue))
            {
                queue = new Queue<LogEntry>(_maxHistoryPerClass);
                HistoryByClass[className] = queue;
            }

            if (queue.Count >= _maxHistoryPerClass)
            {
                queue.Dequeue();
            }

            queue.Enqueue(new LogEntry
            {
                Timestamp    = DateTime.Now,
                LogType      = logType,
                InstanceId   = instanceId,
                InstanceName = instanceName,
                Message      = message
            });
        }
    }

    private static string FindCallerClassName()
    {
        // NOTE:
        // Walks up the stack and returns the first frame outside the logging pipeline.
        // The stack from here is:
        // Record -> LogFormat/LogException -> UnityEngine.Logger.* -> UnityEngine.Debug.* -> caller,
        // but a fixed skip count is fragile (overload depth, JIT inlining), so skip by type instead.

        const int maxFramesToInspect = 16;

        var loggerType = Debug.unityLogger?.GetType();

        for (var i = 1; i < maxFramesToInspect; i++)
        {
            var method = new StackFrame(skipFrames:i, fNeedFileInfo:false).GetMethod();
            if (method == null)
            {
                break; // Reached the end of the stack.
            }

            var type = method.DeclaringType;
            if (type == null
             || type == typeof(DebugLogRecorder)
             || type == typeof(Debug)
             || type == typeof(Logger)
             || type == loggerType)
            {
                continue;
            }

            return type.Name;
        }

        return "UnknownClass";
    }

    // NOTE:
    // A string-based method is required because some types are not accessible
    // and some logs are recorded under compiler-generated type names.

    public static IReadOnlyCollection<LogEntry> GetHistory<T>() => GetHistory(typeof(T).Name);

    public static IReadOnlyCollection<LogEntry> GetHistory(string className)
    {
        // CAUTION:
        // Returning an empty array is the correct behavior.
        // A class may have no logs yet, but it may log later.
        // A debug feature should throw as few exceptions as possible.

        lock (HistoryByClass)
        {
            return HistoryByClass.TryGetValue(className, out var queue)
                  ? queue.ToArray() // To prevent InvalidOperationException caused by Debug.Log in another thread.
                  : Array.Empty<LogEntry>();
        }
    }

    public static IReadOnlyCollection<string> GetClassNames()
    {
        lock (HistoryByClass)
        {
            return HistoryByClass.Count > 0 ? new List<string>(HistoryByClass.Keys)
                                            : Array.Empty<string>();
        }
    }

    public static IReadOnlyDictionary<string, IReadOnlyCollection<LogEntry>> GetAllHistory()
    {
        lock (HistoryByClass)
        {
            var snapshot = new Dictionary<string, IReadOnlyCollection<LogEntry>>(HistoryByClass.Count);

            foreach (var (className, queue) in HistoryByClass)
            {
                snapshot[className] = queue.ToArray();
            }

            return snapshot;
        }
    }

    [HideInCallstack]
    public static void LogWithoutRecording(string message, UnityEngine.Object context = null, LogType logType = LogType.Log)
    {
        // NOTE:
        // Writes to the original log handler without recording to history.

        var handler = _defaultLogHandler ?? Debug.unityLogger.logHandler;
            handler.LogFormat(logType, context, "{0}", message);
    }

    [HideInCallstack]
    public static void RecordWithoutLogging(string message, UnityEngine.Object context = null, LogType logType = LogType.Log)
    {
        // NOTE:
        // Records to history without writing to the console.
        // Counterpart of LogWithoutRecording.

        Record(logType, context, message);
    }
}}