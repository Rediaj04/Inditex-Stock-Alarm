using System.Text.Json;

namespace InditexStockAlarm;

internal static class MonitorScripts
{
    public static string ReadyScript() =>
        "!!document.querySelector('button.size-button, button.size-selector-sizes-size__button, button.size-item, [data-qa-action=\"size-in\"], .size-selector-sizes__size, [data-qa-action=\"add-to-cart\"]')";

    public static string SizeScript(string[] sizes)
    {
        var sizesJson = JsonSerializer.Serialize(sizes);

        return $$"""
(async () => {
  const sizes = {{sizesJson}};
  const SIZE_SELECTOR = [
    'button.size-button',
    'button.size-selector-sizes-size__button',
    'button.size-item',
    '[data-qa-action="size-in"]',
    '.size-selector-sizes__size'
  ].join(',');

  function visible(el) {
    return !!(el && (el.getClientRects().length || el.offsetParent));
  }

  function matchesSize(text, size) {
    const t = (text || '').trim();
    if (t === size) return true;
    if (t.indexOf(size) === 0) {
      const rest = t.slice(size.length).trim();
      return /^(ver similares|similares|notify|av[ií]same|avisame)/i.test(rest);
    }
    return false;
  }

  function stateOf(el) {
    let node = el;
    let depth = 0;
    while (node && node.nodeType === 1 && depth < 5) {
      const cls = typeof node.className === 'string' ? node.className : '';
      if (node.disabled === true) return 'SOLD_OUT';
      if (node.getAttribute && node.getAttribute('aria-disabled') === 'true') return 'SOLD_OUT';
      if (/disabled|unavailable|out-?of-?stock|sold-?out|agotad|no-?stock|sin-?stock|size-no-stock/i.test(cls)) return 'SOLD_OUT';
      if (/low-?stock|last-?units|last-?items|pocas|ultimas|últimas/i.test(cls)) return 'RUNNING_OUT';
      node = node.parentElement;
      depth++;
    }
    return 'SHOW';
  }

  function collect() {
    const nodes = Array.from(document.querySelectorAll(SIZE_SELECTOR));
    const generic = Array.from(document.querySelectorAll('button, [role="button"], [role="radio"], li'))
      .filter(function (e) {
        const t = (e.textContent || '').trim();
        if (!sizes.some(function (s) { return matchesSize(t, s); })) return false;
        let p = e;
        let d = 0;
        while (p && d < 6) {
          const c = typeof p.className === 'string' ? p.className : '';
          if (/size/i.test(c) || /size/i.test(p.id || '')) return true;
          p = p.parentElement;
          d++;
        }
        return false;
      });
    return nodes.concat(generic).filter(visible);
  }

  function read() {
    const nodes = collect();
    if (!nodes.length) return null;
    const out = [];
    for (const sz of sizes) {
      const el = nodes.find(function (n) { return matchesSize(n.textContent, sz); });
      if (!el) {
        out.push({ size: sz, visibility: 'NO_EXISTE', price: null });
        continue;
      }
      const btn = el.tagName === 'BUTTON' ? el : (el.querySelector('button') || el);
      out.push({ size: sz, visibility: stateOf(btn), price: null });
    }
    return out;
  }

  // Aceptar cookies si hay banner
  const accept = Array.from(document.querySelectorAll('button, a, [role="button"]')).find(function (b) {
    if (!visible(b)) return false;
    const t = (b.textContent || '').trim();
    return /^(aceptar( todas)?( las)? cookies|aceptar todas|aceptar y continuar|aceptar|acepto|accept all( cookies)?|accept cookies|accept)$/i.test(t);
  });
  if (accept) {
    try { accept.click(); } catch (e) {}
    await new Promise(function (r) { setTimeout(r, 2500); });
  }

  let res = read();

  if (!res) {
    const add = document.querySelector('[data-qa-action="add-to-cart"]') ||
      Array.from(document.querySelectorAll('button')).find(function (b) {
        return /^(añadir|agregar|comprar|add to (bag|cart|basket))$/i.test((b.textContent || '').trim());
      });

    if (add) {
      try { add.click(); } catch (e) {}
      await new Promise(function (r) { setTimeout(r, 1600); });
      res = read();
    }
  }

  if (!res) {
    try { window.scrollTo(0, 900); } catch (e) {}
    await new Promise(function (r) { setTimeout(r, 900); });
    res = read();
  }

  if (!res) {
    const selects = Array.from(document.querySelectorAll('select')).filter(visible);
    for (const sel of selects) {
      const opts = Array.from(sel.options || []);
      let any = false;
      const tmp = [];
      for (const sz of sizes) {
        const opt = opts.find(function (o) { return (o.textContent || '').trim() === sz; });
        if (opt) any = true;
        tmp.push({ size: sz, visibility: opt ? (opt.disabled ? 'SOLD_OUT' : 'SHOW') : 'NO_EXISTE', price: null });
      }
      if (any) { res = tmp; break; }
    }
  }

  const h1 = document.querySelector('h1');
  let product = h1 ? h1.textContent.trim() : '';
  if (!product) product = (document.title || '').split(' | ')[0].trim();

  if (!res) return JSON.stringify({ ok: false, reason: 'sin-tallas' });
  return JSON.stringify({ ok: true, product: product, color: '', sizes: res });
})()
""";
    }
}
