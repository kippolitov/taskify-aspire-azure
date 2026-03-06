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

    function initWithNativeDnd(element, dotnetRef) {
        const root = element.closest(".kanban-board") ?? document;
        let pointerState = null;

        const makeCardsDraggable = () => {
            element.querySelectorAll("[data-task-id]").forEach((card) => {
                if (!card.hasAttribute("draggable")) {
                    card.setAttribute("draggable", "true");
                }
            });
        };

        const clearDragOver = () => {
            root
                .querySelectorAll(".kanban-column__cards--drag-over")
                .forEach((el) => el.classList.remove("kanban-column__cards--drag-over"));
        };

        const columnFromPoint = (x, y) => {
            const hit = document.elementFromPoint(x, y);
            return hit?.closest?.("[data-column]") ?? null;
        };

        const beginPointerDrag = (evt) => {
            if (evt.pointerType !== "touch" && evt.pointerType !== "pen") {
                return;
            }

            const card = evt.target?.closest?.("[data-task-id]");
            if (!card) return;

            const taskId = card.dataset.taskId;
            const fromColumn = element.dataset.column;
            if (!taskId || !fromColumn) return;

            pointerState = {
                pointerId: evt.pointerId,
                taskId,
                fromColumn,
                startX: evt.clientX,
                startY: evt.clientY,
                moved: false,
                card,
            };

            card.classList.add("task-card--dragging");
        };

        const updatePointerDrag = (evt) => {
            if (!pointerState || evt.pointerId !== pointerState.pointerId) return;

            const dx = evt.clientX - pointerState.startX;
            const dy = evt.clientY - pointerState.startY;
            if (!pointerState.moved && Math.hypot(dx, dy) > 8) {
                pointerState.moved = true;
            }

            clearDragOver();
            const targetColumn = columnFromPoint(evt.clientX, evt.clientY);
            if (targetColumn) {
                targetColumn.classList.add("kanban-column__cards--drag-over");
            }
        };

        const endPointerDrag = (evt) => {
            if (!pointerState || evt.pointerId !== pointerState.pointerId) return;

            const { taskId, fromColumn, moved, card } = pointerState;
            pointerState = null;

            card.classList.remove("task-card--dragging");

            const targetColumn = columnFromPoint(evt.clientX, evt.clientY);
            clearDragOver();

            const toColumn = targetColumn?.dataset?.column;
            if (!moved || !toColumn || toColumn === fromColumn) return;

            dotnetRef
                .invokeMethodAsync("OnTaskDropped", taskId, fromColumn, toColumn)
                .catch(console.error);
        };

        const cancelPointerDrag = (evt) => {
            if (!pointerState || evt.pointerId !== pointerState.pointerId) return;

            pointerState.card.classList.remove("task-card--dragging");
            pointerState = null;
            clearDragOver();
        };

        const onDragStart = (evt) => {
            const card = evt.target?.closest?.("[data-task-id]");
            if (!card) return;

            const taskId = card.dataset.taskId;
            const fromColumn = element.dataset.column;
            if (!taskId || !fromColumn) return;

            card.classList.add("task-card--dragging");

            if (evt.dataTransfer) {
                evt.dataTransfer.effectAllowed = "move";
                evt.dataTransfer.setData("text/plain", taskId);
                evt.dataTransfer.setData("application/x-taskify-from-column", fromColumn);
            }
        };

        const onDragEnd = (evt) => {
            const card = evt.target?.closest?.("[data-task-id]");
            if (card) {
                card.classList.remove("task-card--dragging");
            }
        };

        const onDragOver = (evt) => {
            evt.preventDefault();
            element.classList.add("kanban-column__cards--drag-over");
            if (evt.dataTransfer) {
                evt.dataTransfer.dropEffect = "move";
            }
        };

        const onDragLeave = (evt) => {
            if (!element.contains(evt.relatedTarget)) {
                element.classList.remove("kanban-column__cards--drag-over");
            }
        };

        const onDrop = (evt) => {
            evt.preventDefault();
            element.classList.remove("kanban-column__cards--drag-over");

            const toColumn = element.dataset.column;
            const taskId = evt.dataTransfer?.getData("text/plain");
            const fromColumn = evt.dataTransfer?.getData("application/x-taskify-from-column");

            if (!taskId || !fromColumn || !toColumn || fromColumn === toColumn) return;

            dotnetRef
                .invokeMethodAsync("OnTaskDropped", taskId, fromColumn, toColumn)
                .catch(console.error);
        };

        makeCardsDraggable();
        element.addEventListener("dragstart", onDragStart);
        element.addEventListener("dragend", onDragEnd);
        element.addEventListener("dragover", onDragOver);
        element.addEventListener("dragleave", onDragLeave);
        element.addEventListener("drop", onDrop);
        element.addEventListener("pointerdown", beginPointerDrag);
        element.addEventListener("pointermove", updatePointerDrag);
        element.addEventListener("pointerup", endPointerDrag);
        element.addEventListener("pointercancel", cancelPointerDrag);

        const observer = new MutationObserver(makeCardsDraggable);
        observer.observe(element, { childList: true, subtree: true });

        instances.set(element, {
            mode: "native",
            observer,
            handlers: {
                onDragStart,
                onDragEnd,
                onDragOver,
                onDragLeave,
                onDrop,
                beginPointerDrag,
                updatePointerDrag,
                endPointerDrag,
                cancelPointerDrag,
            },
        });
    }

    function init(element, dotnetRef, columnName) {
        if (instances.has(element)) return;

        if (typeof window.Sortable === "undefined" || !window.Sortable?.create) {
            initWithNativeDnd(element, dotnetRef);
            return;
        }

        const sortable = Sortable.create(element, {
            group: "kanban", // shared group allows cross-column moves
            animation: 120,
            direction: "vertical",
            swapThreshold: 0.65,
            invertSwap: true,
            invertedSwapThreshold: 0.8,
            delayOnTouchOnly: true,
            delay: 110,
            touchStartThreshold: 4,
            fallbackTolerance: 4,
            dragClass: "task-card--dragging",
            ghostClass: "task-card--ghost",
            chosenClass: "task-card--chosen",
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

        instances.set(element, { mode: "sortable", sortable });
    }

    function dispose(element) {
        const instance = instances.get(element);
        if (!instance) return;

        if (instance.mode === "sortable" && instance.sortable) {
            instance.sortable.destroy();
        }

        if (instance.mode === "native") {
            const { handlers, observer } = instance;
            observer?.disconnect();
            element.removeEventListener("dragstart", handlers.onDragStart);
            element.removeEventListener("dragend", handlers.onDragEnd);
            element.removeEventListener("dragover", handlers.onDragOver);
            element.removeEventListener("dragleave", handlers.onDragLeave);
            element.removeEventListener("drop", handlers.onDrop);
            element.removeEventListener("pointerdown", handlers.beginPointerDrag);
            element.removeEventListener("pointermove", handlers.updatePointerDrag);
            element.removeEventListener("pointerup", handlers.endPointerDrag);
            element.removeEventListener("pointercancel", handlers.cancelPointerDrag);
        }

        instances.delete(element);
    }

    return { init, dispose };
})();
