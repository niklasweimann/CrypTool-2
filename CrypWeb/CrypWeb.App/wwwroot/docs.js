// Helpers of the documentation page (Components/DocsPage.razor).
window.ct2docs = {
    // back to the top of the article when another page is shown
    scrollTop() {
        const main = document.getElementById('dc-main');
        if (main) main.scrollTop = 0;
    },

    // typesets the formulas ($...$ inline, $$...$$ display) of the article; a formula KaTeX cannot read stays as text
    renderMath() {
        const main = document.getElementById('dc-main');
        if (!main || !window.renderMathInElement) return;
        renderMathInElement(main, {
            delimiters: [
                {left: '$$', right: '$$', display: true},
                {left: '$', right: '$', display: false}
            ],
            throwOnError: false
        });
    }
};
