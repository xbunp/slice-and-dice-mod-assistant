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

mergeInto(LibraryManager.library, {
    // Register a native secure browser listener once on startup to handle Ctrl+V
    InitializeNativePasteListener: function(objectNamePtr, methodNamePtr) {
        if (window.isPasteListenerInitialized) return;
        window.isPasteListenerInitialized = true;
        
        var objectName = UTF8ToString(objectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);
        
        window.addEventListener('paste', function(e) {
            var clipboardText = "";
            
            // Extract text from the native browser paste event
            if (e.clipboardData && e.clipboardData.getData) {
                clipboardText = e.clipboardData.getData('text/plain');
            }
            
            // Send the pasted text back to Unity
            if (clipboardText !== "") {
                SendMessage(objectName, methodName, clipboardText);
            }
        });
    }
});

mergeInto(LibraryManager.library, {
    // Detects if the game is embedded in a cross-origin iframe (like itch.io)
    IsInsideIframe: function() {
        try {
            return window.self !== window.top;
        } catch (e) {
            return true; // Safe fallback if access is restricted
        }
    },

    // Listens for the browser's native 'paste' event (Ctrl+V)
    InitializeNativePasteListener: function(objectNamePtr, methodNamePtr) {
        if (window.isPasteListenerInitialized) return;
        window.isPasteListenerInitialized = true;

        var objectName = UTF8ToString(objectNamePtr);
        var methodName = UTF8ToString(methodNamePtr);

        window.addEventListener('paste', function(e) {
            var clipboardText = "";
            if (e.clipboardData && e.clipboardData.getData) {
                clipboardText = e.clipboardData.getData('text/plain');
            }
            if (clipboardText !== "") {
                SendMessage(objectName, methodName, clipboardText);
            }
        });
    }
});