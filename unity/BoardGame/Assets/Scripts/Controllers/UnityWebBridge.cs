using System.Runtime.InteropServices;
using UnityEngine;

public static class UnityWebBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void DispatchReactUnityEvent(string eventName, string payloadJson);
#endif

    public static void Emit(string eventName, string payloadJson = "{}")
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        string safePayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;

#if UNITY_WEBGL && !UNITY_EDITOR
        DispatchReactUnityEvent(eventName, safePayload);
#else
        Debug.Log($"[UnityWebBridge] {eventName}: {safePayload}");
#endif
    }
}
