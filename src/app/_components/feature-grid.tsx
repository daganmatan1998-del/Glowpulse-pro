import { Zap, ShieldCheck, MonitorSmartphone, Droplets, Palette, Coffee } from "lucide-react";

const features = [
  {
    icon: Zap,
    title: "3-Second Auto-Blend",
    desc: "The built-in magnetic stirrer fully blends your drink in just 3 seconds — no lumps, no manual stirring.",
  },
  {
    icon: ShieldCheck,
    title: "304 Food-Grade Steel",
    desc: "The inner cup is premium 304 stainless steel — safe, durable, and easy to wipe clean.",
  },
  {
    icon: MonitorSmartphone,
    title: "Touch-Screen Display",
    desc: "A built-in LCD touch display on the handle shows live temperature and a 20-second stir timer.",
  },
  {
    icon: Droplets,
    title: "Waterproof & Odor-Free",
    desc: "A sealed, waterproof housing made without harsh chemical smells — safe for everyday use.",
  },
  {
    icon: Palette,
    title: "7 Colors to Choose From",
    desc: "Pick the shade that matches your kitchen counter or your desk at work.",
  },
  {
    icon: Coffee,
    title: "Works With Any Drink",
    desc: "Coffee, cocoa, protein shakes, matcha — if it needs stirring, this mug handles it.",
  },
];

export function FeatureGrid() {
  return (
    <section id="features" className="mx-auto max-w-6xl px-4 py-20 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl text-center">
        <h2 className="font-heading text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl">
          Everything Your Morning Coffee Needs
        </h2>
        <p className="mt-4 text-lg text-muted-foreground">
          Designed to replace your spoon — and upgrade your routine.
        </p>
      </div>

      <div className="mt-12 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {features.map(({ icon: Icon, title, desc }) => (
          <div
            key={title}
            className="rounded-2xl border border-border bg-card p-6 shadow-sm transition-shadow hover:shadow-md"
          >
            <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Icon size={22} aria-hidden />
            </span>
            <h3 className="mt-4 font-heading text-lg font-bold text-foreground">
              {title}
            </h3>
            <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
              {desc}
            </p>
          </div>
        ))}
      </div>
    </section>
  );
}
