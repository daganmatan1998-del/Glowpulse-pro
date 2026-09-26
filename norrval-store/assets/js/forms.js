/* Contact + order tracking forms. */
(() => {
  'use strict';
  const D = JSON.parse(document.getElementById('store-data').textContent);
  const msg = (el, text, ok) => {
    el.hidden = false;
    el.textContent = text;
    el.className = `form-msg ${ok ? 'is-ok' : 'is-err'}`;
    el.focus?.();
  };
  const validate = (form) => {
    let ok = true;
    [...form.elements].forEach((f) => {
      if (!f.willValidate) return;
      const bad = !f.checkValidity() || (f.required && !String(f.value).trim());
      f.setAttribute('aria-invalid', String(bad));
      if (bad && ok) {
        f.focus();
        ok = false;
      }
    });
    return ok;
  };

  const contact = document.querySelector('[data-contact]');
  if (contact) {
    const out = document.querySelector('[data-contact-msg]');
    contact.addEventListener('submit', async (e) => {
      e.preventDefault();
      if (!validate(contact)) return msg(out, 'Please complete the highlighted fields.', false);
      const data = Object.fromEntries(new FormData(contact));
      if (!D.contactFormEndpoint) {
        // No form backend yet: hand off to the visitor's email app instead.
        const body = `Name: ${data.name}\nOrder: ${data.order || '-'}\n\n${data.message}`;
        location.href = `mailto:${encodeURIComponent(D.supportEmail)}?subject=${encodeURIComponent(data.topic)}&body=${encodeURIComponent(body)}`;
        return msg(out, `Opening your email app. If nothing happens, write to ${D.supportEmail}.`, true);
      }
      try {
        const r = await fetch(D.contactFormEndpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
          body: JSON.stringify(data),
        });
        if (!r.ok) throw new Error();
        contact.reset();
        msg(out, 'Thanks — your message has been sent. We will reply by email.', true);
      } catch {
        msg(out, `Your message could not be sent. Please email ${D.supportEmail}.`, false);
      }
    });
  }

  const track = document.querySelector('[data-track]');
  if (track) {
    const out = document.querySelector('[data-track-msg]');
    track.addEventListener('submit', (e) => {
      e.preventDefault();
      if (!validate(track)) return msg(out, 'Please enter your order number and email.', false);
      if (!D.trackingUrl) {
        return msg(out, `Online tracking is not connected yet. Use the tracking link in your shipping email, or contact ${D.supportEmail} with your order number.`, false);
      }
      location.href = D.trackingUrl
        .replace('{order}', encodeURIComponent(track.order.value.trim().replace(/^#/, '')))
        .replace('{email}', encodeURIComponent(track.email.value.trim()));
    });
  }
})();
