
document.body.addEventListener('htmx:configRequest', e => {
  const tokenEl = document.querySelector('meta[name="request-verification-token"]');
  if (tokenEl) {
    e.detail.headers['RequestVerificationToken'] = tokenEl.content;
  }
});

document.body.addEventListener('htmx:afterSwap', e => {
  const el = e.target;
  if (!el.id) return;

  const ids = ['search-predictions-desktop', 'search-predictions-mobile', 'search-predictions-tablet'];
  if (ids.includes(el.id)) {
    const empty = el.innerHTML.trim() === '';
    el.classList.toggle('hidden', empty);
  }
});

(function () {
    const ids = ['search-predictions-desktop', 'search-predictions-mobile', 'search-predictions-tablet'];
    const dd = () => ids.map(id => document.getElementById(id)).filter(Boolean);
    const hide = () => dd().forEach(el => el.classList.add('hidden'));
    const showIfNotEmpty = el => el.classList.toggle('hidden', el.innerHTML.trim() === '');

    // 1) Show after HTMX inject (auto-hide if empty)
    document.body.addEventListener('htmx:afterSwap', e => {
        if (e.target && ids.includes(e.target.id)) showIfNotEmpty(e.target);
    });

    // 2) Hide on overlay click
    const overlay = document.getElementById('overlay');
    if (overlay) overlay.addEventListener('click', hide);

    // 3) Hide on click outside navbar
    const navbars = document.querySelectorAll('nav');
    document.addEventListener('click', ev => {
        if (![...navbars].some(nav => nav.contains(ev.target))) hide();
    });

    // 4) Hide on ESC
    document.addEventListener('keydown', ev => { if (ev.key === 'Escape') hide(); });

    // 5) Hide shortly after input blur (lets click on items)
    document.querySelectorAll('input[type="search"]').forEach(inp => {
        inp.addEventListener('blur', () => setTimeout(hide, 150));
    });
})();


(function () {
    const ids = ['search-predictions-desktop', 'search-predictions-mobile', 'search-predictions-tablet'];
    const dd = () => ids.map(id => document.getElementById(id)).filter(Boolean);
    const hide = () => dd().forEach(el => el.classList.add('hidden'));
    const showIfNotEmpty = el => el.classList.toggle('hidden', el.innerHTML.trim() === '');

    // After HTMX inject: show if not empty and remember the query used
    document.body.addEventListener('htmx:afterSwap', e => {
        if (!e.target || !ids.includes(e.target.id)) return;
        const input = document.querySelector(`input[type="search"][hx-target="#${e.target.id}"]`);
        if (input) e.target.dataset.lastQ = (input.value || '').trim();
        showIfNotEmpty(e.target);
    });

    // Focus behavior: if there's text, show existing results or fetch again
    const inputs = document.querySelectorAll('input[type="search"][hx-get][hx-target]');
    inputs.forEach(inp => {
        inp.addEventListener('focus', () => {
            const val = (inp.value || '').trim();
            const targetSel = inp.getAttribute('hx-target');
            const target = document.querySelector(targetSel);
            if (!target || !val) { hide(); return; }

            // Reuse if content exists for same query; else re-fetch
            if (target.innerHTML.trim() !== '' && target.dataset.lastQ === val) {
                target.classList.remove('hidden');
            } else {
                htmx.ajax('GET', inp.getAttribute('hx-get'), {
                    target: target,
                    swap: 'innerHTML',
                    values: { q: val }
                });
            }
        });

        // Hide shortly after blur so clicks on items still register
        inp.addEventListener('blur', () => setTimeout(hide, 150));

        // Prevent request storms
        inp.setAttribute('hx-sync', 'this:queue first');
    });

    // Hide on overlay click / outside click / ESC
    const overlay = document.getElementById('overlay');
    if (overlay) overlay.addEventListener('click', hide);

    const navbars = document.querySelectorAll('nav');
    document.addEventListener('click', ev => {
        if (![...navbars].some(nav => nav.contains(ev.target))) hide();
    });
    document.addEventListener('keydown', ev => { if (ev.key === 'Escape') hide(); });
})();