/**
 * sortable-interop.js
 * Bridges SortableJS drag-and-drop to Blazor server callbacks.
 *
 * Usage from Blazor:
 *   await JS.InvokeVoidAsync("sortableInterop.init", columnEl, dotnetRef, fromCol);
 *   await JS.InvokeVoidAsync("sortableInterop.dispose", columnEl);
 */
window.sortableInterop = (() => {
    const instances = new WeakMap();

    function init(element, dotnetRef, columnName) {
        if (instances.has(element)) return;

        const sortable = Sortable.create(element, {
            group: "kanban", // shared group allows cross-column moves
            animation: 150,
            ghostClass: "task-card--dragging",
            onEnd(evt) {
                const taskId = evt.item.dataset.taskId;
                const toColumn = evt.to.dataset.column;
                const fromColumn = evt.from.dataset.column;

                if (fromColumn !== toColumn && taskId && toColumn) {
                    // Use InvokeVoidAsync — positional args only (R-011)
                    dotnetRef
                        .invokeMethodAsync("OnTaskDropped", taskId, fromColumn, toColumn)
                        .catch(console.error);
                }
            },
        });

        instances.set(element, sortable);
    }

    function dispose(element) {
        const sortable = instances.get(element);
        if (sortable) {
            sortable.destroy();
            instances.delete(element);
        }
    }

    return { init, dispose };
})();
