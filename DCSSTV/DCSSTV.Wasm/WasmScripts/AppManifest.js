var UnoAppManifest = {

    splashScreenImage: "Assets/SplashScreen.png",
    splashScreenColor: "#0078D7",
    displayName: "DCSSTV"

}

function openFilePicker(htmlId) {
    console.log(htmlId);
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.ttyrec';
    input.onchange = e => {
        var file = e.target.files[0];
        document.evaluate("/html/body/div/div/div[1]/div/div/div[1]/div/div/div/div/div/p", document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue.textContent = "File Selected, Loading..."
        var reader = new FileReader();
        reader.readAsDataURL(file);
        reader.onload = readerEvent => {
            var content = readerEvent.target.result; // this is the content!
            //var imageDiv = document.getElementById(htmlId);
            //var image = imageDiv.getElementsByTagName('img')[0];
            //image.src = content;
            var selectFile = Module.mono_bind_static_method("[DCSSTV.Wasm] DCSSTV.MainPage:SelectFile");
            selectFile(content);
        };
    };
    input.click();
}