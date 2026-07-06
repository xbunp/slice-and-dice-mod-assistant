using System;
using UnityEditor.PackageManager.UI;

mergeInto(LibraryManager.library, {
DownloadFileWebGL: function(fileNamePtr, base64DataPtr) {
        var fileName = UTF8ToString(fileNamePtr);
        var base64Data = UTF8ToString(base64DataPtr);

        var byteCharacters = atob(base64Data);
        var byteNumbers = new Array(byteCharacters.length);
        for (var i = 0; i < byteCharacters.length; i++)
        {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        var byteArray = new Uint8Array(byteNumbers);
        var blob = new Blob([byteArray], { type: "image/png" });

    var link = document.createElement('a');
    link.href = window.URL.createObjectURL(blob);
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}
});