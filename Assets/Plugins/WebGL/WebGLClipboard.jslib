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

mergeInto(LibraryManager.library, {
    CopyToClipboardWebGL: function(textPtr) {
        var text = UTF8ToString(textPtr);
        
        // Modern browser clipboard API
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).catch(function(err) {
                console.error("Could not copy text to browser clipboard: ", err);
            });
        } else {
            // Fallback for older browsers
            var el = document.createElement('textarea');
            el.value = text;
            document.body.appendChild(el);
            el.select();
            document.execCommand('copy');
            document.body.removeChild(el);
        }
    }
});

mergeInto(LibraryManager.library, {
    // 1. Instantly push the highlighted selection string to a global browser cache
    UpdateWebGLSelectionCache: function(textPtr) {
        var text = UTF8ToString(textPtr);
        window.unitySelectionCache = text;
    },
    
    // 2. Register a native secure browser listener once on startup to handle Ctrl+C
    InitializeNativeCopyListener: function() {
        if (window.isCopyListenerInitialized) return;
        window.isCopyListenerInitialized = true;
        
        window.addEventListener('copy', function(e) {
            // If we have text pre-cached in our IDE, securely write it
            if (window.unitySelectionCache && window.unitySelectionCache !== "") {
                e.clipboardData.setData('text/plain', window.unitySelectionCache);
                e.preventDefault(); // Intercept browser's copy event
            }
        });
    }
});