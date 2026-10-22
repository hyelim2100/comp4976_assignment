// Client-side rule for [ImageFile]: checks the chosen file's extension and size before upload.
(function ($) {
    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    $.validator.addMethod("imagefile", function (value, element, params) {
        if (!element.files || element.files.length === 0) {
            return true;
        }
        var file = element.files[0];
        var match = file.name.toLowerCase().match(/\.[^.]+$/);
        var extension = match ? match[0] : "";
        return params.extensions.indexOf(extension) >= 0 && file.size <= params.maxbytes;
    });

    $.validator.unobtrusive.adapters.add("imagefile", ["extensions", "maxbytes"], function (options) {
        options.rules.imagefile = {
            extensions: options.params.extensions.split(","),
            maxbytes: parseInt(options.params.maxbytes, 10)
        };
        options.messages.imagefile = options.message;
    });
})(window.jQuery);
