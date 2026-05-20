window.InventoryIdEditor = (function(){
    function init(opts){
        if(!opts) return;
        var input = document.getElementById(opts.inputId);
        var btn = document.getElementById(opts.previewBtnId);
        var out = document.getElementById(opts.previewResultId);
        if(!input || !btn || !out) return;
        btn.addEventListener('click', function(){
            var fd = new FormData();
            // send both possible names to be tolerant
            fd.append('IdFormat', input.value || '');
            fd.append('idFormat', input.value || '');
            fetch('/Inventories/PreviewIdFormat', { method: 'POST', body: fd, credentials: 'same-origin' })
                .then(function(res){
                    if(!res.ok) return res.json().then(function(j){ throw j; });
                    return res.json();
                })
                .then(function(json){
                    out.textContent = json.example || '-';
                })
                .catch(function(err){
                    out.textContent = err && err.error ? err.error : 'Preview error';
                });
        });
    }
    return { init: init };
})();
