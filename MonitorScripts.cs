using System.Text.Json;

namespace InditexStockAlarm;

internal static class MonitorScripts
{
    public static string ReadyScript() =>
        "!!document.querySelector('button.size-button, button.size-selector-sizes-size__button, button.size-item, [data-qa-action=\"size-in\"], .size-selector-sizes__size, [data-qa-action=\"add-to-cart\"]')";

    public static string ListSizesScript() => """
(async () => {
  const SIZE_SELECTOR = [
    'button.size-button',
    'button.size-selector-sizes-size__button',
    'button.size-item',
    '[data-qa-action="size-in"]',
    '.size-selector-sizes__size'
  ].join(',');

  const KNOWN = /^(XXS|XS|S|M|L|XL|XXL|XXXL|XXXXL|\d{1,3}|[UÚ]nica?|[UÚ]nico|Talla [UÚ]nica)$/i;

  function visible(el) {
    return !!(el && (el.getClientRects().length || el.offsetParent));
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

  function clean(text) {
    return (text || '')
      .replace(/ver similares/ig, '')
      .replace(/av[ií]same/ig, '')
      .replace(/\s+/g, ' ')
      .trim();
  }

  function notCarousel(el) {
    let p = el;
    let d = 0;
    while (p && p.nodeType === 1 && d < 7) {
      const c = typeof p.className === 'string' ? p.className : '';
      if (/grid-product|product-carousel|recommend|similar-products|other-products|complete-the-look/i.test(c)) return false;
      p = p.parentElement;
      d++;
    }
    return true;
  }

  function scan() {
    const nodes = Array.from(document.querySelectorAll(SIZE_SELECTOR));
    const generic = Array.from(document.querySelectorAll('button, [role="button"], [role="radio"], li'))
      .filter(function (e) {
        const t = clean(e.textContent);
        if (t.length === 0 || t.length > 10 || !KNOWN.test(t)) return false;
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

    const all = nodes.concat(generic).filter(visible).filter(notCarousel);
    const out = [];
    const seen = {};

    for (const el of all) {
      const t = clean(el.textContent);
      if (!t || t.length > 10 || seen[t]) continue;
      const btn = el.tagName === 'BUTTON' ? el : (el.querySelector('button') || el);
      seen[t] = true;
      out.push({ size: t, state: stateOf(btn) });
    }
    return out;
  }

  function scanSelects() {
    const out = [];
    for (const sel of Array.from(document.querySelectorAll('select')).filter(visible)) {
      for (const opt of Array.from(sel.options || [])) {
        const t = clean(opt.textContent);
        if (t && KNOWN.test(t)) out.push({ size: t, state: opt.disabled ? 'SOLD_OUT' : 'SHOW' });
      }
    }
    return out;
  }

  const accept = Array.from(document.querySelectorAll('button, a, [role="button"]')).find(function (b) {
    if (!visible(b)) return false;
    const t = (b.textContent || '').trim();
    return /^(aceptar( todas)?( las)? cookies|aceptar todas|aceptar y continuar|aceptar|acepto|accept all( cookies)?|accept cookies|accept)$/i.test(t);
  });
  if (accept) {
    try { accept.click(); } catch (e) {}
    await new Promise(function (r) { setTimeout(r, 1800); });
  }

  let sizes = scan();

  if (!sizes.length) {
    const add = document.querySelector('[data-qa-action="add-to-cart"]') ||
      Array.from(document.querySelectorAll('button')).find(function (b) {
        return /^(añadir|agregar|comprar|add to (bag|cart|basket))$/i.test((b.textContent || '').trim());
      });
    if (add) {
      try { add.click(); } catch (e) {}
      await new Promise(function (r) { setTimeout(r, 1600); });
      sizes = scan();
    }
  }

  if (!sizes.length) {
    try { window.scrollTo(0, 900); } catch (e) {}
    await new Promise(function (r) { setTimeout(r, 900); });
    sizes = scan();
  }

  if (!sizes.length) sizes = scanSelects();

  const h1 = document.querySelector('h1');
  let product = h1 ? h1.textContent.trim() : '';
  if (!product) product = (document.title || '').split(' | ')[0].trim();

  return JSON.stringify({
    ok: sizes.length > 0,
    product: product,
    sizes: sizes.slice(0, 40),
    reason: sizes.length ? null : 'sin-tallas'
  });
})()
""";

    public static string ResolveApiScript(string colorHint) => $$"""
(async () => {
  const colorHint = {{JsonSerializer.Serialize(colorHint ?? "")}};

  let storeId = null;
  for (const n of performance.getEntriesByType('resource').map(function (e) { return e.name; })) {
    const m = n.match(/\/store\/(\d{5,})/);
    if (m) { storeId = m[1]; break; }
  }
  if (!storeId) {
    const m = document.documentElement.innerHTML.match(/"storeId"\s*:\s*\[?\s*"?(\d{5,})/);
    if (m) storeId = m[1];
  }
  if (!storeId) return JSON.stringify({ ok: false, reason: 'sin-store' });

  const q = new URLSearchParams(location.search);
  let productId = q.get('pelement');
  if (!productId) {
    const m = location.pathname.match(/-c0p(\d+)\.html/);
    if (m) productId = m[1];
  }
  if (!productId) {
    const m = document.documentElement.innerHTML.match(/"productId"\s*:\s*(\d{5,})/);
    if (m) productId = m[1];
  }
  if (!productId) return JSON.stringify({ ok: false, reason: 'sin-producto' });

  const colorId = colorHint || q.get('colorId') || q.get('cS') || '';

  let catalogId = '0';

  async function rawDetail(pid, cat) {
    const url = '/itxrest/2/catalog/store/' + storeId + '/' + cat + '/category/0/product/' + pid + '/detail?languageId=-5&appId=1';
    try {
      const r = await fetch(url, { headers: { accept: 'application/json' } });
      if (!r.ok) return null;
      return await r.json();
    } catch (e) { return null; }
  }

  async function detail(pid) {
    let j = await rawDetail(pid, catalogId);
    if (j) return j;

    if (catalogId === '0') {
      try {
        const r = await fetch('/itxrest/2/catalog/store/' + storeId + '?languageId=-5&appId=1',
          { headers: { accept: 'application/json' } });
        if (r.ok) {
          const store = await r.json();
          const c = (store.catalogs || [])[0];
          if (c && c.id) {
            catalogId = String(c.id);
            j = await rawDetail(pid, catalogId);
          }
        }
      } catch (e) { /* seguimos */ }
    }

    return j;
  }

  function aggregate(colors) {
    const c = colors.find(function (x) { return String(x.id) === String(colorId); }) || colors[0];
    const map = {};
    const order = [];
    for (const s of (c.sizes || [])) {
      const name = (s.name || '').trim();
      if (!name) continue;
      if (!map[name]) {
        map[name] = { size: name, visibility: 'SOLD_OUT', price: s.price ? Number(s.price) : null };
        order.push(name);
      }
      const cur = map[name];
      if (s.visibilityValue === 'SHOW') cur.visibility = 'SHOW';
      else if (s.visibilityValue === 'RUNNING_OUT' && cur.visibility !== 'SHOW') cur.visibility = 'RUNNING_OUT';
      if (cur.price === null && s.price) cur.price = Number(s.price);
    }
    return { color: c, sizes: order.map(function (n) { return map[n]; }) };
  }

  let j = await detail(productId);
  if (!j) return JSON.stringify({ ok: false, reason: 'api-http' });

  let colors = (j.detail && j.detail.colors) || [];
  if (!colors.length) {
    const candidates = [];
    for (const s of (j.bundleProductSummaries || [])) if (s.id) candidates.push(s.id);
    if (j.alternativeProductId) candidates.push(j.alternativeProductId);

    for (const alt of candidates) {
      if (String(alt) === String(productId)) continue;

      const j2 = await detail(alt);
      if (!j2) continue;

      let c2 = (j2.detail && j2.detail.colors) || [];
      if (c2.length) { j = j2; colors = c2; break; }

      const nested = [];
      for (const s of (j2.bundleProductSummaries || [])) if (s.id) nested.push(s.id);
      if (j2.alternativeProductId) nested.push(j2.alternativeProductId);

      for (const n of nested) {
        const j3 = await detail(n);
        if (!j3) continue;
        const c3 = (j3.detail && j3.detail.colors) || [];
        if (c3.length) { j = j3; colors = c3; break; }
      }

      if (colors.length) break;
    }
  }
  if (!colors.length) return JSON.stringify({ ok: false, reason: 'sin-colores' });

  const agg = aggregate(colors);
  const og = document.querySelector('meta[property="og:image"]');
  const image = og && og.content ? new URL(og.content, location.href).href : '';

  return JSON.stringify({
    ok: true,
    storeId: String(storeId),
    productId: String(j.id || productId),
    colorId: String(agg.color.id),
    product: j.name,
    color: agg.color.name,
    image: image,
    sizes: agg.sizes
  });
})()
""";

    public static string PollApiScript(string storeId, string productId, string colorId, string[] sizes)
    {
        var sizesJson = JsonSerializer.Serialize(sizes);

        return $$"""
(async () => {
  const storeId = {{JsonSerializer.Serialize(storeId)}};
  const productId = {{JsonSerializer.Serialize(productId)}};
  const colorId = {{JsonSerializer.Serialize(colorId ?? "")}};
  const wanted = {{sizesJson}};

  const url = '/itxrest/2/catalog/store/' + storeId + '/0/category/0/product/' + productId + '/detail?languageId=-5&appId=1';

  let j;
  try {
    const r = await fetch(url, { headers: { accept: 'application/json' } });
    if (!r.ok) return JSON.stringify({ ok: false, reason: 'api-http' });
    j = await r.json();
  } catch (e) {
    return JSON.stringify({ ok: false, reason: 'api-exc' });
  }

  const colors = (j.detail && j.detail.colors) || [];
  const c = colors.find(function (x) { return String(x.id) === String(colorId); }) || colors[0];
  if (!c) return JSON.stringify({ ok: false, reason: 'sin-colores' });

  const map = {};
  for (const s of (c.sizes || [])) {
    const name = (s.name || '').trim();
    if (!name) continue;
    if (!map[name]) map[name] = { size: name, visibility: 'SOLD_OUT', price: s.price ? Number(s.price) : null };
    const cur = map[name];
    if (s.visibilityValue === 'SHOW') cur.visibility = 'SHOW';
    else if (s.visibilityValue === 'RUNNING_OUT' && cur.visibility !== 'SHOW') cur.visibility = 'RUNNING_OUT';
    if (cur.price === null && s.price) cur.price = Number(s.price);
  }

  const out = [];
  for (const sz of wanted) out.push(map[sz] || { size: sz, visibility: 'NO_EXISTE', price: null });

  return JSON.stringify({
    ok: true,
    storeId: String(storeId),
    productId: String(productId),
    colorId: String(c.id),
    product: j.name,
    color: c.name,
    sizes: out
  });
})()
""";
    }

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

  function notCarousel(el) {
    let p = el;
    let d = 0;
    while (p && p.nodeType === 1 && d < 7) {
      const c = typeof p.className === 'string' ? p.className : '';
      if (/grid-product|product-carousel|recommend|similar-products|other-products|complete-the-look/i.test(c)) return false;
      p = p.parentElement;
      d++;
    }
    return true;
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
    return nodes.concat(generic).filter(visible).filter(notCarousel);
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
