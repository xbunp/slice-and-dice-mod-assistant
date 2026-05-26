mergeInto(LibraryManager.library, {
    ReadOSClipboard: function(objectNamePtr, methodNamePtr) {
        var objectName = UTF8ToString(objectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);

        // Modern browser async clipboard API
        if (navigator.clipboard && navigator.clipboard.readText) {
            navigator.clipboard.readText().then(function(clipText) {
                // Send the text back to the Unity GameObject
                SendMessage(objectName, methodName, clipText);
            }).catch(function(err) {
                console.error("Clipboard access denied by browser.", err);
                SendMessage(objectName, methodName, "");
            });
        } else {
            console.warn("Async clipboard not available.");
            SendMessage(objectName, methodName, "");
        }
    }
});