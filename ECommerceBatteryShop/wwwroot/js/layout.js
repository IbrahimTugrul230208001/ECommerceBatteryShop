
const onReady = (callback) => {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', callback, { once: true });
  } else {
    callback();
  }
};

onReady(() => {
  const body = document.body;
  if (!body) {
    return;
  }

  body.addEventListener('htmx:configRequest', e => {
    const tokenEl = document.querySelector('meta[name="request-verification-token"]');
    if (tokenEl) {
      e.detail.headers['RequestVerificationToken'] = tokenEl.content;
    }
  });

  const searchIds = ['search-predictions-desktop', 'search-predictions-mobile', 'search-predictions-tablet'];
  const dropdowns = () => searchIds.map(id => document.getElementById(id)).filter(Boolean);
  const hideDropdowns = () => dropdowns().forEach(el => el.classList.add('hidden'));
  const showIfNotEmpty = (el) => el.classList.toggle('hidden', el.innerHTML.trim() === '');

  body.addEventListener('htmx:afterSwap', e => {
    const target = e.target;
    if (!target || !target.id || !searchIds.includes(target.id)) {
      return;
    }

    const input = document.querySelector(`input[type="search"][hx-target="#${target.id}"]`);
    if (input) {
      target.dataset.lastQ = (input.value || '').trim();
    }
    showIfNotEmpty(target);
  });

  const inputs = document.querySelectorAll('input[type="search"][hx-get][hx-target]');
  inputs.forEach(inp => {
    inp.addEventListener('focus', () => {
      const val = (inp.value || '').trim();
      const targetSel = inp.getAttribute('hx-target');
      const target = document.querySelector(targetSel);
      if (!target || !val) {
        hideDropdowns();
        return;
      }

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

    inp.addEventListener('blur', () => setTimeout(hideDropdowns, 150));
    inp.setAttribute('hx-sync', 'this:queue first');
  });

  const overlay = document.getElementById('overlay');
  if (overlay) {
    overlay.addEventListener('click', hideDropdowns);
  }

  const navbars = document.querySelectorAll('nav');
  document.addEventListener('click', ev => {
    if (![...navbars].some(nav => nav.contains(ev.target))) {
      hideDropdowns();
    }
  });

  document.addEventListener('keydown', ev => {
    if (ev.key === 'Escape') {
      hideDropdowns();
    }
  });

  body.addEventListener('cookie-consent-required', (event) => {
    const detail = typeof event.detail === 'string'
      ? event.detail
      : (event.detail && event.detail.message) || '';
    window.dispatchEvent(new CustomEvent('open-cookie-banner', { detail }));
  });
});

window.cookieBanner = function () {
  const cookieName = 'COOKIE_CONSENT';
  const acceptedValue = 'accepted';
  const rejectedValue = 'rejected';
  const defaultMessage = 'Çerezleri kabul ediyor musunuz?';

  const readConsent = () => {
    const match = document.cookie.match(new RegExp('(?:^|; )' + cookieName + '=([^;]*)'));
    return match ? decodeURIComponent(match[1]) : null;
  };

  const writeConsent = (value) => {
    const expires = new Date(Date.now() + 365 * 24 * 60 * 60 * 1000);
    document.cookie = `${cookieName}=${value}; expires=${expires.toUTCString()}; path=/; SameSite=Lax`;
  };

  const clearAnonId = () => {
    document.cookie = 'ANON_ID=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; SameSite=Lax';
  };

  return {
    visible: false,
    message: defaultMessage,
    defaultMessage,
    init() {
      const consent = readConsent();
      this.visible = !consent;
      this.message = this.defaultMessage;

      window.addEventListener('open-cookie-banner', (event) => {
        const detail = typeof event.detail === 'string' ? event.detail : this.defaultMessage;
        this.message = detail || this.defaultMessage;
        this.visible = true;
      });
    },
    accept() {
      writeConsent(acceptedValue);
      this.message = this.defaultMessage;
      this.visible = false;
      window.dispatchEvent(new CustomEvent('cookie-consent-updated', { detail: acceptedValue }));
    },
    reject() {
      writeConsent(rejectedValue);
      clearAnonId();
      this.message = this.defaultMessage;
      this.visible = false;
      window.dispatchEvent(new CustomEvent('cookie-consent-updated', { detail: rejectedValue }));
    },
    close() {
      this.visible = false;
      this.message = this.defaultMessage;
    }
  };
};

window.addEventListener('cookie-consent-updated', (event) => {
  if (event.detail === 'rejected') {
    ['cart-count-mobile', 'cart-count-tablet', 'cart-count-desktop'].forEach(id => {
      const el = document.getElementById(id);
      if (el) el.textContent = '0';
    });
  }
});
