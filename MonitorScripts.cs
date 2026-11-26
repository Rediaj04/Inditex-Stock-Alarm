using System.Text.Json;

namespace BershkaMonitor;

internal static class MonitorScripts
{
    public static string ReadyScript() =>
        "!!document.querySelector('button.size-button')";

    public static string SizeScript(string[] sizes)
    {
        var sizesJson = JsonSerializer.Serialize(sizes);

        return $$"""
(() => {
  const sizes = {{sizesJson}};
  const btns = Array.from(document.querySelectorAll('button.size-button'));
  const out = [];
  for (const sz of sizes) {
    const b = btns.find(function (x) { return (x.textContent || '').trim() === sz; });
    if (!b) { out.push({ size: sz, visibility: 'NO_EXISTE', price: null }); continue; }
    let v = 'SHOW';
    if (b.disabled || b.classList.contains('size-button--disabled')) v = 'SOLD_OUT';
    else if (b.classList.contains('size-button--low-stock')) v = 'RUNNING_OUT';
    out.push({ size: sz, visibility: v, price: null });
  }
  const h1 = document.querySelector('h1');
  let product = h1 ? h1.textContent.trim() : '';
  if (!product) product = (document.title || '').split(' - ')[0].trim();
  let color = '';
  const sel = document.querySelector('.color-selector__item--selected, .color-selector__item[aria-checked="true"], .color-selector__item[aria-selected="true"]');
  if (sel) color = (sel.getAttribute('title') || sel.getAttribute('aria-label') || (sel.textContent || '')).trim();
  return JSON.stringify({ ok: true, product: product, color: color, sizes: out });
})()
""";
    }
}
