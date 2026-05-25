mergeInto(LibraryManager.library, {
    TriggerFileOpen: function () {
        console.log("[JS] TriggerFileOpen called.");
        var fileInput = document.getElementById('unity-file-input');
        if (!fileInput) {
            console.log("[JS] Creating new hidden file input.");
            fileInput = document.createElement('input');
            fileInput.id = 'unity-file-input';
            fileInput.type = 'file';
            fileInput.accept = 'image/png, image/jpeg';
            fileInput.style.display = 'none';
            document.body.appendChild(fileInput);
        }

        fileInput.value = '';

        fileInput.onchange = function (event) {
            console.log("[JS] File selected by user.");
            var file = event.target.files[0];
            if (!file) {
                console.error("[JS] No file detected in event.");
                return;
            }

            var reader = new FileReader();
            reader.onload = function (e) {
                console.log("[JS] File loaded into browser memory.");
                var base64Data = e.target.result.split(',')[1];
                console.log("[JS] Base64 string length: " + base64Data.length);
                
                try {
                    console.log("[JS] Attempting to SendMessage to Unity GameObject 'ImageReceiver'...");
                    // Standard Emscripten SendMessage
                    SendMessage('ImageReceiver', 'OnImageLoaded', base64Data);
                    console.log("[JS] SendMessage successfully fired!");
                } catch (err) {
                    console.error("[JS] Native SendMessage failed: ", err);
                    
                    // Fallback for custom itch.io WebGL templates
                    if (typeof window.unityInstance !== 'undefined') {
                        console.log("[JS] Trying window.unityInstance.SendMessage...");
                        window.unityInstance.SendMessage('ImageReceiver', 'OnImageLoaded', base64Data);
                    } else {
                        console.error("[JS] CRITICAL: Could not locate Unity SendMessage API.");
                    }
                }
            };
            reader.readAsDataURL(file);
        };

        console.log("[JS] Simulating click on file browser...");
        fileInput.click();
    }
});