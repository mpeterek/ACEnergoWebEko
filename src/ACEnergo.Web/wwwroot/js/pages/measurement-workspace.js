(function () {
    'use strict';
    var input = document.querySelector('[data-tree-search]');
    var tree = document.querySelector('[data-measurement-tree]');
    if (!input || !tree) { return; }
    var workspace = document.querySelector('.measurement-workspace');
    function fitTree() {
        var labels = tree.querySelectorAll('summary a');
        var width = 304;
        var i;
        for (i = 0; i < labels.length; i += 1) {
            width = Math.max(width, labels[i].scrollWidth + 64);
        }
        if (workspace) { workspace.style.setProperty('--measurement-tree-width', Math.min(width, 480) + 'px'); }
    }
    fitTree();
    window.addEventListener('resize', fitTree);
    input.addEventListener('input', function () {
        var query = input.value.toLocaleLowerCase();
        var items = tree.querySelectorAll('[data-tree-item]');
        var count = 0;
        var i;
        for (i = 0; i < items.length; i += 1) {
            var visible = items[i].getAttribute('data-search-text').toLocaleLowerCase().indexOf(query) >= 0;
            items[i].hidden = !visible;
            if (visible) { count += 1; }
        }
        var empty = document.querySelector('[data-tree-empty]');
        if (empty) { empty.hidden = count !== 0; }
        fitTree();
    });
}());
