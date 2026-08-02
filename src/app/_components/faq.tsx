import { product } from "@/lib/product";

const faqs = [
  {
    q: "How fast does it actually stir my coffee?",
    a: "The internal magnetic stirrer blends a full cup in about 3 seconds. The touch display on the handle also runs a 20-second timer so you can watch it count down.",
  },
  {
    q: "What is the mug made of?",
    a: "The inner cup is 304 food-grade stainless steel. The outer body is a sealed, waterproof housing made without harsh chemical odors, so it's safe for daily use.",
  },
  {
    q: "How do I turn it on and control it?",
    a: "Everything is controlled from the touch-sensitive LCD display built into the handle — no app or remote needed.",
  },
  {
    q: "What colors does it come in?",
    a: "It's available in 7 colors — pick your favorite at checkout.",
  },
  {
    q: "Where do I buy it, and how much does it cost?",
    a: `It's $${product.priceUSD}, shipping included. Tap "Shop Now" anywhere on this page to place your order.`,
  },
];

export function FAQ() {
  return (
    <section id="faq" className="bg-muted/60 py-20">
      <div className="mx-auto max-w-3xl px-4 sm:px-6 lg:px-8">
        <h2 className="text-center font-heading text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl">
          Frequently Asked Questions
        </h2>

        <div className="mt-10 space-y-4">
          {faqs.map((f) => (
            <details
              key={f.q}
              className="group rounded-2xl border border-border bg-card p-5 open:shadow-sm"
            >
              <summary className="flex cursor-pointer list-none items-center justify-between gap-4 font-heading font-semibold text-foreground">
                {f.q}
                <span
                  aria-hidden
                  className="shrink-0 text-xl text-muted-foreground transition-transform duration-200 group-open:rotate-45"
                >
                  +
                </span>
              </summary>
              <p className="mt-3 text-muted-foreground">{f.a}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}
