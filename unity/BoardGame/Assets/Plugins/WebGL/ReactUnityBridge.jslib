mergeInto(LibraryManager.library, {
  DispatchReactUnityEvent: function (eventNamePtr, payloadPtr) {
    var eventName = UTF8ToString(eventNamePtr);
    var payload = UTF8ToString(payloadPtr);

    if (
      typeof window !== "undefined" &&
      typeof window.dispatchReactUnityEvent === "function"
    ) {
      window.dispatchReactUnityEvent(eventName, payload);
      return;
    }

    if (typeof console !== "undefined" && typeof console.warn === "function") {
      console.warn(
        "[ReactUnityBridge] dispatchReactUnityEvent is not available",
        eventName,
        payload
      );
    }
  },
});
