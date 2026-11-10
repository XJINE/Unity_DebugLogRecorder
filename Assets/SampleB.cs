using UnityEngine;

namespace DebugLogRecorders {
public class SampleB : MonoBehaviour
{
    private void Update()
    {
        DebugLogRecorder.RecordWithoutLogging($"Frame Count: {Time.frameCount} / {Time.realtimeSinceStartup}", this);
    }
}}