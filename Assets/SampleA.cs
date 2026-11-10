using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DebugLogRecorders {
public class SampleA : MonoBehaviour
{
    private void Start()
    {
        // NOTE:
        // Debug.Assert(true, ...) never reaches the log handler,
        // so use LogAssertion to exercise the LogType.Assert path.

        Debug.Log("instance method");
        Debug.Log("explicit context", this);
        Debug.LogWarning(Time.frameCount >= 0 ? "ternary a" : "ternary b");
        Debug.LogFormat("format {0}", Time.frameCount);
        Debug.LogAssertion("assert message");

        void Lambda() => Debug.Log("lambda capturing only this: " + name);
             Lambda();

        StartCoroutine(Coroutine());
    }

    private IEnumerator Coroutine()
    {
        yield return null;
        Debug.Log("coroutine");
        Dump();
    }

    [ContextMenu(nameof(Dump))]
    private void Dump()
    {
        var builder = new StringBuilder();

        builder.AppendLine($"--- {nameof(SampleA)} history ---");
        BuildLogs(DebugLogRecorder.GetHistory<SampleA>(), builder);
        builder.AppendLine($"--- {nameof(SampleB)} history ---");
        BuildLogs(DebugLogRecorder.GetHistory<SampleB>(), builder);

        // NOTE:
        // the coroutine entry is recorded under the state-machine type name ("<Co>d__N"),
        // not under Sample, so it will not appear above.

        DebugLogRecorder.LogWithoutRecording(builder.ToString(), this);
    }

    private static void BuildLogs(IReadOnlyCollection<DebugLogRecorder.LogEntry> entries, StringBuilder builder)
    {
        foreach (var entry in entries)
        {
            builder.Append($"[{entry.LogType}] id:{entry.InstanceId?.ToString()??"NULL"}, ");
            builder.Append($"name:{entry.InstanceName??"NULL"}, message:{entry.Message}\n");
        }
    }
}}