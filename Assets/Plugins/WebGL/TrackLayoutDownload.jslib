mergeInto(LibraryManager.library, {
  ReReVoltDownloadTrackLayout: function (fileNamePointer, jsonPointer) {
    var fileName = UTF8ToString(fileNamePointer);
    var json = UTF8ToString(jsonPointer);
    var blob = new Blob([json], { type: "application/json;charset=utf-8" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }
});
